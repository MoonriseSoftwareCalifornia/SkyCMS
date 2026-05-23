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
    private readonly IElFinderNameResolver _nameResolver;

    public OpenCommandHandler(IElFinderStorageAdapter adapter, IElFinderNameResolver nameResolver)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
    }

    public async Task<IElFinderResponse> HandleAsync(OpenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var response = new OpenResponse();

            // Determine target path — default to volume root on init or empty target.
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

            // Check accessibility
            if (!await _adapter.IsAccessibleAsync(targetPath, cancellationToken))
            {
                return ElFinderErrorResponse.Access("Access denied");
            }

            // Get current directory info
            var cwdEntry = await _adapter.GetEntryAsync(targetPath, cancellationToken);
            if (cwdEntry == null)
            {
                return ElFinderErrorResponse.Open("Cannot open directory");
            }

            // Get directory contents — fetched once, reused for cwd.Dirs computation.
            var entries = await _adapter.GetEntriesAsync(targetPath, cancellationToken);

            // Build cwd object.
            bool isRoot = IsVolumeRoot(targetPath, request.RootPath);
            response.Cwd = await BuildElFinderObjectAsync(cwdEntry, targetPath, request.VolumeId, isRoot, cancellationToken, request.RootPath);

            // Set cwd.Dirs based on whether any children are directories (no extra round-trip).
            response.Cwd.Dirs = entries.Any(e => e.IsDirectory) ? 1 : 0;

            // The protocol requires cwd to appear in the files list as well.
            response.Files.Add(response.Cwd);

            // Add child entries.
            foreach (var entry in entries)
            {
                var entryPath = targetPath.TrimEnd('/') + "/" + entry.Name;
                response.Files.Add(await BuildElFinderObjectAsync(entry, entryPath, request.VolumeId, false, cancellationToken, request.RootPath));
            }

            // When tree=1 (panel navigation), include ancestor directories so the tree
            // panel can expand to the correct node without extra round-trips.
            if (request.Tree)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    targetPath.TrimEnd('/'),
                };

                // First, add siblings of the cwd so the tree shows all peers at this level.
                var targetLastSlash = targetPath.LastIndexOf('/');
                if (targetLastSlash > 0) // Skip if target is at root
                {
                    var targetParentPath = targetPath.Substring(0, targetLastSlash);
                    var cwdSiblings = await _adapter.GetEntriesAsync(targetParentPath, cancellationToken);
                    foreach (var sibling in cwdSiblings.Where(e => e.IsDirectory))
                    {
                        var siblingPath = targetParentPath.TrimEnd('/') + "/" + sibling.Name;
                        if (seen.Add(siblingPath))
                        {
                            response.Files.Add(await BuildElFinderObjectAsync(sibling, siblingPath, request.VolumeId, false, cancellationToken, request.RootPath));
                        }
                    }
                }

                var ancestors = await _adapter.GetAncestorsAsync(targetPath, cancellationToken);
                foreach (var ancestor in ancestors)
                {
                    var ancestorPath = ancestor.Path?.TrimEnd('/') ?? string.Empty;
                    if (!string.IsNullOrEmpty(ancestorPath))
                    {
                        // Ensure leading slash for consistent path comparison
                        if (!ancestorPath.StartsWith("/"))
                        {
                            ancestorPath = "/" + ancestorPath;
                        }

                        if (seen.Add(ancestorPath))
                        {
                            bool ancestorIsRoot = IsVolumeRoot(ancestorPath, request.RootPath);
                            // Insert ancestors after cwd (at index 1) but before any children.
                            // Safe insert: ensure we don't exceed the current list size.
                            int insertPosition = Math.Min(1, response.Files.Count);
                            response.Files.Insert(insertPosition, await BuildElFinderObjectAsync(ancestor, ancestorPath, request.VolumeId, ancestorIsRoot, cancellationToken, request.RootPath));

                            // Also include siblings of each ancestor so the tree can be fully
                            // expanded at each level without additional round-trips.
                            // To get siblings, we need to fetch entries from the ancestor's parent.
                            var lastSlash = ancestorPath.LastIndexOf('/');
                            if (lastSlash > 0) // Skip if ancestor is at root
                            {
                                var parentPath = ancestorPath.Substring(0, lastSlash);
                                var siblingsFromParent = await _adapter.GetEntriesAsync(parentPath, cancellationToken);
                                foreach (var sibling in siblingsFromParent.Where(e => e.IsDirectory))
                                {
                                    var siblingPath = parentPath.TrimEnd('/') + "/" + sibling.Name;
                                    if (seen.Add(siblingPath))
                                    {
                                        response.Files.Add(await BuildElFinderObjectAsync(sibling, siblingPath, request.VolumeId, false, cancellationToken, request.RootPath));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            response.VolumeId = request.VolumeId;

            // Init-specific fields required by the elFinder client bootstrap.
            if (request.Init)
            {
                response.Api = "2.1049";
                response.UplMaxSize = "2G";
                response.Options = await BuildOptionsAsync(targetPath, request.BlobPublicUrl, request.TmbUrl, cancellationToken);
                response.NetDrivers = new List<object>();
            }

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Open failed: {ex.Message}");
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private async Task<ElFinderObject> BuildElFinderObjectAsync(
        Cosmos.BlobService.FileManagerEntry entry,
        string path,
        string volumeId,
        bool isRoot,
        CancellationToken cancellationToken,
        string rootPath = null)
    {
        var hash = _adapter.EncodePath(path);
        var normalizedPath = "/" + path.Trim('/');
        var lastSlash = normalizedPath.LastIndexOf('/');
        var phash = lastSlash >= 0
            ? _adapter.EncodePath(normalizedPath.Substring(0, lastSlash + 1))
            : _adapter.EncodePath("/");

        var resolvedName = await _nameResolver.ResolveNameAsync(path, entry.Name ?? string.Empty, cancellationToken);
        var rawName = entry.Name ?? string.Empty;

        var obj = new ElFinderObject
        {
            Hash = hash,
            PHash = isRoot ? string.Empty : phash,
            Name = resolvedName,
            Size = entry.Size,
            Mime = entry.IsDirectory ? "directory" : ElFinderMimeHelper.GetMimeType(entry.Name),
            Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
            Read = 1,
            Write = 1,
            Locked = 0,
            Dirs = entry.IsDirectory ? 1 : 0,
            RealPath = normalizedPath,
            DisplayPath = await BuildDisplayPathAsync(path, cancellationToken),
        };

        // All directories (root and non-root) need volumeid so the elFinder tree can anchor them.
        if (entry.IsDirectory)
        {
            obj.VolumeId = volumeId;

            // Set the root hash on all directories so the client can resolve the volume.
            if (!string.IsNullOrEmpty(rootPath))
            {
                obj.Root = _adapter.EncodePath(NormalizeRootPath(rootPath));
            }
        }

        if (isRoot)
        {
            obj.IsRoot = 1;
        }

        // Set thumbnail suffix for image files.
        if (!entry.IsDirectory)
        {
            var ext = (entry.Extension ?? string.Empty).ToLowerInvariant();
            if (FileStorageConstants.ValidImageExtensions.Contains(ext))
            {
                // The tmb field must be a URL suffix (appended after tmbUrl), not a full URL.
                // elFinder builds the full URL as: tmbUrl + tmb.
                // tmbUrl = "/FileManager/GetImageThumbnail?target="
                // So tmb = "{encodedPath}&width=80&height=80"
                obj.Tmb = $"{Uri.EscapeDataString(normalizedPath)}&width=80&height=80";
            }
        }

        return obj;
    }

    private async Task<ElFinderOptions> BuildOptionsAsync(string targetPath, string blobPublicUrl, string tmbUrl, CancellationToken cancellationToken)
    {
        var blobBase = (blobPublicUrl ?? string.Empty).TrimEnd('/');
        var canonicalPath = targetPath.TrimStart('/').TrimEnd('/');
        var displayPath = (await BuildDisplayPathAsync(targetPath, cancellationToken)).TrimStart('/').TrimEnd('/');
        var volumeUrl = string.IsNullOrEmpty(blobBase)
            ? "/" + canonicalPath + "/"
            : blobBase + "/" + canonicalPath + "/";

        return new ElFinderOptions
        {
            Url = volumeUrl,
            TmbUrl = tmbUrl ?? "/FileManager/GetImageThumbnail?target=",
            Path = string.IsNullOrEmpty(displayPath) ? "Media" : displayPath,
        };
    }

    private async Task<string> BuildDisplayPathAsync(string canonicalPath, CancellationToken cancellationToken)
    {
        var normalizedPath = canonicalPath.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return "/";
        }

        var segments = normalizedPath
            .TrimStart('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (segments.Count >= 3)
        {
            var scope = segments[0];
            var kind = segments[1];
            var idSegment = segments[2];

            if (scope.Equals("pub", StringComparison.OrdinalIgnoreCase)
                && kind.Equals("articles", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(idSegment, out var articleNumber))
            {
                var friendly = await _nameResolver.ResolveNameAsync($"/{scope}/{kind}/{articleNumber}", idSegment, cancellationToken);
                if (!string.IsNullOrWhiteSpace(friendly))
                {
                    segments[2] = friendly;
                }
            }
            else if (scope.Equals("pub", StringComparison.OrdinalIgnoreCase)
                && kind.Equals("templates", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(idSegment, out _))
            {
                var friendly = await _nameResolver.ResolveNameAsync($"/{scope}/{kind}/{idSegment}", idSegment, cancellationToken);
                if (!string.IsNullOrWhiteSpace(friendly))
                {
                    segments[2] = friendly;
                }
            }
        }

        return "/" + string.Join('/', segments);
    }

    private static bool IsVolumeRoot(string targetPath, string rootPath)
    {
        var root = NormalizeRootPath(rootPath);
        return string.Equals(
            targetPath.TrimEnd('/'),
            root.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRootPath(string rootPath) =>
        string.IsNullOrEmpty(rootPath) ? "/" : rootPath;
}

