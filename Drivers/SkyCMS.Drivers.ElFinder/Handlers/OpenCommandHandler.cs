using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Helpers;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "open" command: retrieves root or target directory contents.
/// </summary>
public class OpenCommandHandler : IElFinderHandler<OpenCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public OpenCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    /// <summary>
    /// Handles the "open" command and builds the full elFinder payload for the requested folder.
    /// </summary>
    /// <param name="request">The incoming open request with target hash, root path, and UI flags.</param>
    /// <param name="cancellationToken">A token used to cancel long-running storage calls.</param>
    /// <returns>
    /// A successful open response containing current folder metadata, child entries, and options,
    /// or an error response when the request is invalid or cannot be completed.
    /// </returns>
    /// <remarks>
    /// Step-by-step flow:
    /// 1. Create an open response object. This is the container that will be sent back to the elFinder client.
    /// 2. Resolve the target path. If no target hash is provided, use the root path; if a hash is provided, decode it.
    /// 3. Validate access for the resolved path. Stop early with an access error if it cannot be opened.
    /// 4. Load the current working directory (cwd) entry. Stop early with an open error if it is missing.
    /// 5. Load direct child entries once and reuse them for multiple response fields.
    /// 6. Build the cwd object and set its directory flag based on whether it has child directories.
    /// 7. Add cwd to files, then add all child entries.
    /// 8. If tree mode is requested, include ancestor folders and their siblings so the left tree can render without extra requests.
    /// 9. Attach volume information and options required by the client on every open response.
    /// 10. If this is an init call, add bootstrap fields required by elFinder startup.
    /// 11. Return the completed response, or return a generic error if any unexpected exception occurs.
    /// </remarks>
    public async Task<IElFinderResponse> HandleAsync(OpenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Create an open response.
            // This object is the final payload sent back to the elFinder client.
            var response = new OpenResponse();

            // Step 2: Resolve the target path.
            // Start from the normalized root. If a target hash is provided, decode it.
            // If decoding fails, the request is invalid and we return immediately.
            string targetPath = NormalizeRootPath(request.RootPath);
            if (!string.IsNullOrEmpty(request.Target))
            {
                var decodedPath = _adapter.DecodePath(request.Target);
                if (decodedPath == null)
                {
                    return ElFinderErrorResponse.InvalidParams("Invalid target hash");
                }

                targetPath = decodedPath;
            }

            // Step 3: Verify the target path exists in storage.
            // This checks if the path format is valid and if the file or directory can be found.
            // It does not check user permissions; it only confirms the path exists.
            if (!await _adapter.IsAccessibleAsync(targetPath, cancellationToken))
            {
                return ElFinderErrorResponse.Access("Target path does not exist or is invalid");
            }

            // Step 4: Resolve metadata for the target path.
            // GetEntryAsync retrieves existing entries or synthesizes virtual directory entries
            // for folders that exist only through their children (blob storage pattern).
            // If the path cannot be resolved in any way, we cannot proceed.
            var cwdEntry = await _adapter.GetEntryAsync(targetPath, cancellationToken);
            if (cwdEntry == null)
            {
                return ElFinderErrorResponse.NotFound("Target path does not exist or cannot be resolved");
            }

            // Step 5: Load immediate child entries once.
            // This fetches all files and folders directly under the target path.
            // If the path validation fails or storage returns an error, an empty list is returned silently.
            // We reuse this data for both the child listing and the cwd.Dirs flag calculation.
            var entries = await _adapter.GetEntriesAsync(targetPath, cancellationToken);

            // Step 6: Build an elFinder-formatted object representing the current working directory (CWD).
            // This converts the storage entry into the protocol format expected by the elFinder client.
            // BuildElFinderObjectAsync does extensive transformation:
            //   - Encodes the path into a hash for safe client-side reference
            //   - Computes the parent hash (phash) for tree navigation
            //   - Resolves display names (e.g., friendly names for articles and templates)
            //   - Determines MIME type based on file extension or directory type
            //   - Converts modification time to Unix timestamp
            //   - Sets permissions (read=1, write=1, locked=0)
            //   - Flags directories and computes directory counts
            //   - For directories, attaches the volume ID and root hash so the client can rebuild the tree
            //   - For image files, computes a thumbnail URL suffix (e.g., for 80x80 previews)
            // The isRoot flag indicates whether this is the volume root (affects hash formatting and flags).
            bool isRoot = IsVolumeRoot(targetPath, request.RootPath);
            response.Cwd = await BuildElFinderObjectAsync(cwdEntry, targetPath, request.VolumeId, isRoot, cancellationToken, request.RootPath);

            // Step 7: Set the Dirs flag on the current working directory (CWD).
            // The Dirs field (0 or 1) tells the elFinder client whether to show an expand chevron in the tree UI.
            // If any direct child is a directory, Dirs = 1 (has expandable content).
            // If no children are directories or the folder is empty, Dirs = 0 (leaf node, no chevron).
            response.Cwd.Dirs = entries.Any(e => e.IsDirectory) ? 1 : 0;

            // Step 8: Add the current working directory (CWD) to the response files list.
            // The elFinder protocol requires that the CWD also appears in the files collection,
            // even though it is separately available as the cwd field. This is redundant but protocol-mandated.
            response.Files.Add(response.Cwd);

            // Step 9: Transform each direct child entry into elFinder format and add to files.
            // Each child goes through the same extensive transformation as the CWD:
            // encoding, name resolution, MIME detection, timestamp conversion, etc.
            // All children have isRoot=false (only the actual volume root is marked as root).
            foreach (var entry in entries)
            {
                var entryPath = GetEntryPath(targetPath, entry);
                response.Files.Add(await BuildElFinderObjectAsync(entry, entryPath, request.VolumeId, false, cancellationToken, request.RootPath));
            }

            // Step 10: If tree mode is enabled, enrich the payload for navigation tree rendering.
            // This lets the client expand to the correct node without extra round-trips.
            if (request.Tree)
            {
                // Tree mode means the client needs extra folder context for the left navigation tree.
                // Use a case-insensitive set so folders discovered from different paths are added only once.
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    targetPath.TrimEnd('/'),
                };

                // Step 10a: Add directory siblings of the current working directory (CWD).
                // This lets the current level show peer folders, not just the selected folder.
                var targetLastSlash = targetPath.LastIndexOf('/');
                if (targetLastSlash > 0) // Skip if target is at root
                {
                    var targetParentPath = targetPath.Substring(0, targetLastSlash);
                    var cwdSiblings = await _adapter.GetEntriesAsync(targetParentPath, cancellationToken);
                    foreach (var sibling in cwdSiblings.Where(e => e.IsDirectory))
                    {
                        var siblingPath = GetEntryPath(targetParentPath, sibling);
                        if (seen.Add(siblingPath))
                        {
                            response.Files.Add(await BuildElFinderObjectAsync(sibling, siblingPath, request.VolumeId, false, cancellationToken, request.RootPath));
                        }
                    }
                }

                // Step 10b: Add ancestor directories so the full root -> CWD path can be expanded in the tree.
                var ancestors = await _adapter.GetAncestorsAsync(targetPath, cancellationToken);
                foreach (var ancestor in ancestors)
                {
                    var ancestorPath = ancestor.Path?.TrimEnd('/') ?? string.Empty;
                    if (!string.IsNullOrEmpty(ancestorPath))
                    {
                        // Normalize to a leading-slash path before dedup checks.
                        if (!ancestorPath.StartsWith("/"))
                        {
                            ancestorPath = "/" + ancestorPath;
                        }

                        if (seen.Add(ancestorPath))
                        {
                            bool ancestorIsRoot = IsVolumeRoot(ancestorPath, request.RootPath);

                            // Insert after CWD so ancestors are available early in the files payload.
                            int insertPosition = Math.Min(1, response.Files.Count);
                            response.Files.Insert(insertPosition, await BuildElFinderObjectAsync(ancestor, ancestorPath, request.VolumeId, ancestorIsRoot, cancellationToken, request.RootPath));

                            // Step 10c: Add directory siblings for each ancestor level.
                            // This fills out peer branches so each expanded level is complete.
                            var lastSlash = ancestorPath.LastIndexOf('/');
                            if (lastSlash > 0) // Skip if ancestor is at root
                            {
                                // Get siblings of this ancestor to populate the tree level.
                                var parentPath = ancestorPath.Substring(0, lastSlash);

                                // Only query siblings if we haven't already seen this parent path (avoids redundant queries for shared ancestors).
                                var siblingsFromParent = await _adapter.GetEntriesAsync(parentPath, cancellationToken);
                                foreach (var sibling in siblingsFromParent.Where(e => e.IsDirectory))
                                {
                                    var siblingPath = GetEntryPath(parentPath, sibling);
                                    if (seen.Add(siblingPath))
                                    {
                                        // Insert siblings after the ancestor so they are available near their related ancestor in the files payload.
                                        response.Files.Add(await BuildElFinderObjectAsync(sibling, siblingPath, request.VolumeId, false, cancellationToken, request.RootPath));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Step 11: Set the response-level volume id.
            // This tells elFinder which logical storage volume this open result belongs to.
            response.VolumeId = request.VolumeId;

            // Step 12: Build connector options for this specific target path.
            // Options include URL/path data (and thumbnail endpoint) the client needs after navigation.
            // These are required on every open response, not only during init.
            response.Options = await BuildOptionsAsync(targetPath, request.BlobPublicUrl, request.TmbUrl, cancellationToken);

            // Step 13: Add init-only bootstrap fields for first-load handshake.
            // Api identifies protocol version, UplMaxSize advertises upload limit, and
            // NetDrivers is returned for compatibility with the expected init payload shape.
            if (request.Init)
            {
                response.Api = "2.1049";
                response.UplMaxSize = "2G";
                response.NetDrivers = new List<object>();
            }

            // Step 14: Return the fully built success response.
            return response;
        }
        catch (Exception ex)
        {
            // Step 15: Any unexpected error is returned as a generic open failure.
            return ElFinderErrorResponse.Generic($"Open failed: {ex.Message}");
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a storage entry into an <see cref="ElFinderObject"/> that follows the elFinder response format.
    /// </summary>
    /// <param name="entry">
    /// The storage entry (file or directory) that contains source metadata such as name, size,
    /// modified time, extension, and directory/file type.
    /// </param>
    /// <param name="path">
    /// The logical path for this entry. This is used to compute hashes, parent hashes,
    /// display paths, and thumbnail target values.
    /// </param>
    /// <param name="volumeId">
    /// The elFinder volume id (for example <c>l1_</c>) used to anchor directory nodes
    /// to the correct logical storage volume in the client tree.
    /// </param>
    /// <param name="isRoot">
    /// A flag indicating whether this entry represents the volume root.
    /// When <see langword="true"/>, parent hash is omitted and the root flag is set.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel asynchronous name/display resolution.</param>
    /// <param name="rootPath">
    /// Optional configured root path for the current volume.
    /// When provided and the entry is a directory, its encoded value is assigned to <c>root</c>
    /// so the client can resolve the volume consistently.
    /// </param>
    /// <returns>
    /// A fully populated <see cref="ElFinderObject"/> containing protocol fields needed by the client UI,
    /// including hash identifiers, display name, type metadata, permissions flags,
    /// navigation metadata, and optional thumbnail metadata.
    /// </returns>
    /// <remarks>
    /// Step-by-step behavior:
    /// 1. Encode the current path into <c>hash</c> so the client can refer to this item safely.
    /// 2. Normalize the path and compute <c>phash</c> (parent hash) for navigation hierarchy.
    /// 3. Resolve a friendly display name using the name resolver.
    /// 4. Build the core object fields (name, size, mime type, timestamp, read/write flags, and paths).
    /// 5. If the entry is a directory, attach <c>volumeid</c> and (when available) encoded <c>root</c>.
    /// 6. If this entry is the root, set <c>isroot=1</c> and omit parent hash.
    /// 7. If this is an image file, set <c>tmb</c> as a thumbnail URL suffix used by <c>tmbUrl</c>.
    ///
    /// Notes:
    /// - <c>Dirs</c> is a protocol flag (0/1), not a recursive directory count.
    /// - <c>Tmb</c> is not a full URL; the client appends it to the configured thumbnail endpoint.
    /// </remarks>
    private async Task<ElFinderObject> BuildElFinderObjectAsync(
        Cosmos.BlobService.FileManagerEntry entry,
        string path,
        string volumeId,
        bool isRoot,
        CancellationToken cancellationToken,
        string rootPath = null)
    {
        // Step 1: Build protocol-safe identifiers for this node.
        // hash = this item id. phash = parent item id used by elFinder tree navigation.
        var hash = _adapter.EncodePath(path);

        // Normalize path once so all downstream fields use consistent slash formatting.
        var normalizedPath = "/" + path.Trim('/');

        // Find parent segment and encode it as phash. Root fallback is '/'.
        var lastSlash = normalizedPath.LastIndexOf('/');
        var phash = lastSlash >= 0
            ? _adapter.EncodePath(normalizedPath.Substring(0, lastSlash + 1))
            : _adapter.EncodePath("/");

        // Step 2: Build the core elFinder object fields.
        // Most fields here are direct protocol fields consumed by the client.
        var sourceDisplayPath = string.IsNullOrWhiteSpace(entry.DisplayPath)
            ? normalizedPath
            : (entry.DisplayPath!.StartsWith("/", StringComparison.Ordinal) ? entry.DisplayPath : "/" + entry.DisplayPath.TrimStart('/'));

        var obj = new ElFinderObject
        {
            Hash = hash,
            // Root nodes must not have phash, so use empty string when isRoot is true.
            PHash = isRoot ? string.Empty : phash,
            Name = entry.Name,
            Size = entry.Size,
            Mime = entry.IsDirectory ? "directory" : ElFinderMimeHelper.GetMimeType(entry.Name),
            Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
            // Current implementation marks all entries as readable/writable and not locked.
            Read = 1,
            Write = 1,
            Locked = 0,
            // Dirs is a binary flag (0/1), not a recursive count.
            Dirs = entry.IsDirectory ? 1 : 0,
            // RealPath is canonical path used by connector-side logic.
            RealPath = normalizedPath,
            // DisplayPath is UI-oriented and should resolve friendly article/template segment names.
            DisplayPath = sourceDisplayPath,
        };

        // Step 4: Attach directory-only volume metadata.
        // elFinder uses volumeid/root on directory nodes to anchor and navigate the tree.
        if (entry.IsDirectory)
        {
            obj.VolumeId = volumeId;

            // If rootPath is configured, encode it so client can resolve the volume root hash.
            if (!string.IsNullOrEmpty(rootPath))
            {
                obj.Root = _adapter.EncodePath(NormalizeRootPath(rootPath));
            }
        }

        // Step 5: Mark the volume root explicitly.
        // Root marker is required by elFinder to recognize the root node.
        if (isRoot)
        {
            obj.IsRoot = 1;
        }

        // Step 6: Add thumbnail token for supported image files.
        // tmb must be a suffix appended by elFinder to options.tmbUrl, not a full URL.
        if (!entry.IsDirectory)
        {
            var ext = (entry.Extension ?? string.Empty).ToLowerInvariant();
            if (FileStorageConstants.ValidImageExtensions.Contains(ext))
            {
                // Example final client URL:
                //   /FileManager/GetImageThumbnail?target= + {escapedPath}&width=80&height=80
                obj.Tmb = $"{Uri.EscapeDataString(normalizedPath)}&width=80&height=80";
            }
        }

        // Step 7: Return a fully populated elFinder object ready for response payloads.
        return obj;
    }

    /// <summary>
    /// Builds the elFinder options object for the current open response.
    /// </summary>
    /// <param name="targetPath">The current folder path being opened.</param>
    /// <param name="blobPublicUrl">
    /// Optional public base URL for blob content. If missing, a root-relative URL is used.
    /// </param>
    /// <param name="tmbUrl">
    /// Optional thumbnail endpoint base. If missing, the default thumbnail endpoint is used.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel display path resolution.</param>
    /// <returns>
    /// An <see cref="ElFinderOptions"/> object containing navigation URL, thumbnail URL base,
    /// and user-friendly path text for the current folder.
    /// </returns>
    /// <remarks>
    /// Step-by-step behavior:
    /// 1. Normalize the optional blob base URL.
    /// 2. Normalize the target path into a canonical relative path segment.
    /// 3. Build a user-friendly display path (may resolve article/template names).
    /// 4. Compose the folder URL using blob base URL when available.
    /// 5. Return options with URL, thumbnail endpoint, and display path.
    /// </remarks>
    private async Task<ElFinderOptions> BuildOptionsAsync(string targetPath, string blobPublicUrl, string tmbUrl, CancellationToken cancellationToken)
    {
        // Step 1: Normalize the blob base URL so URL composition is predictable.
        var blobBase = (blobPublicUrl ?? string.Empty).TrimEnd('/');

        // Step 2: Normalize target path to a canonical folder segment (no leading/trailing slash).
        var canonicalPath = targetPath.TrimStart('/').TrimEnd('/');

        // Step 3: Build the navigable folder URL for this volume/path.
        // If no blob base URL is configured, use a root-relative URL.
        var volumeUrl = string.IsNullOrEmpty(blobBase)
            ? "/" + canonicalPath + "/"
            : blobBase + "/" + canonicalPath + "/";

        // Step 5: Return protocol options consumed by the elFinder client.
        return new ElFinderOptions
        {
            Url = volumeUrl,
            TmbUrl = tmbUrl ?? "/FileManager/GetImageThumbnail?target=",
            Path = targetPath,
        };
    }
       

    private static string GetEntryPath(string parentPath, Cosmos.BlobService.FileManagerEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Path))
        {
            return "/" + entry.Path.Trim('/');
        }

        return parentPath.TrimEnd('/') + "/" + entry.Name;
    }

    /// <summary>
    /// Determines whether a target path is the configured volume root path.
    /// </summary>
    /// <param name="targetPath">The path to evaluate.</param>
    /// <param name="rootPath">The configured root path for the current volume.</param>
    /// <returns>
    /// <see langword="true"/> when both paths refer to the same normalized root location;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Comparison trims trailing slashes and uses case-insensitive matching
    /// to avoid false negatives from minor path-format differences.
    /// </remarks>
    private static bool IsVolumeRoot(string targetPath, string rootPath)
    {
        // Normalize root once so comparisons are stable.
        var root = NormalizeRootPath(rootPath);

        // Compare canonicalized values (trim trailing slash + ignore case).
        return string.Equals(
            targetPath.TrimEnd('/'),
            root.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes the configured root path by returning <c>/</c> when input is empty.
    /// </summary>
    /// <param name="rootPath">The configured root path value.</param>
    /// <returns>The original root path, or <c>/</c> when no root path is provided.</returns>
    private static string NormalizeRootPath(string rootPath) =>
        string.IsNullOrEmpty(rootPath) ? "/" : rootPath;
}
