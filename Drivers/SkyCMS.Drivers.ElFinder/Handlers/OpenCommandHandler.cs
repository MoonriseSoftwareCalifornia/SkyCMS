using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Helpers;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "open" command: retrieves root or target directory contents.
/// </summary>
public class OpenCommandHandler : IRequestHandler<OpenCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public OpenCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(OpenCommand request, CancellationToken cancellationToken)
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
            response.Cwd = BuildElFinderObject(cwdEntry, targetPath, request.VolumeId, isRoot);

            // Set cwd.Dirs based on whether any children are directories (no extra round-trip).
            response.Cwd.Dirs = entries.Any(e => e.IsDirectory) ? 1 : 0;

            // The protocol requires cwd to appear in the files list as well.
            response.Files.Add(response.Cwd);

            // Add child entries.
            foreach (var entry in entries)
            {
                var entryPath = targetPath.TrimEnd('/') + "/" + entry.Name;
                response.Files.Add(BuildElFinderObject(entry, entryPath, request.VolumeId, false));
            }

            // When tree=1 (panel navigation), include ancestor directories so the tree
            // panel can expand to the correct node without extra round-trips.
            if (request.Tree)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    targetPath.TrimEnd('/'),
                };

                var ancestors = await _adapter.GetAncestorsAsync(targetPath, cancellationToken);
                foreach (var ancestor in ancestors)
                {
                    var ancestorPath = ancestor.Path?.TrimEnd('/') ?? string.Empty;
                    if (!string.IsNullOrEmpty(ancestorPath) && seen.Add(ancestorPath))
                    {
                        bool ancestorIsRoot = IsVolumeRoot(ancestorPath, request.RootPath);
                        // Insert ancestors before the children so the tree renders top-down.
                        response.Files.Insert(1, BuildElFinderObject(ancestor, ancestorPath, request.VolumeId, ancestorIsRoot));
                    }
                }
            }

            response.UplMaxSize = "2G";
            response.VolumeId = request.VolumeId;

            // Init-specific fields required by the elFinder client bootstrap.
            if (request.Init)
            {
                response.Options = BuildOptions(targetPath, request.BlobPublicUrl, request.TmbUrl);
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

    private ElFinderObject BuildElFinderObject(
        Cosmos.BlobService.FileManagerEntry entry,
        string path,
        string volumeId,
        bool isRoot)
    {
        var hash = _adapter.EncodePath(path);
        var normalizedPath = path.TrimEnd('/');
        var lastSlash = normalizedPath.LastIndexOf('/');
        var phash = lastSlash >= 0
            ? _adapter.EncodePath(normalizedPath.Substring(0, lastSlash + 1))
            : _adapter.EncodePath("/");

        var obj = new ElFinderObject
        {
            Hash = hash,
            PHash = isRoot ? null : phash,
            Name = entry.Name,
            Size = entry.Size,
            Mime = entry.IsDirectory ? "directory" : ElFinderMimeHelper.GetMimeType(entry.Name),
            Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
            Read = 1,
            Write = 1,
            Locked = 0,
            Dirs = entry.IsDirectory ? 1 : 0,
        };

        if (isRoot)
        {
            obj.IsRoot = 1;
            obj.VolumeId = volumeId;
        }

        return obj;
    }

    private static ElFinderOptions BuildOptions(string targetPath, string blobPublicUrl, string tmbUrl)
    {
        var blobBase = (blobPublicUrl ?? string.Empty).TrimEnd('/');
        var humanPath = targetPath.TrimStart('/').TrimEnd('/');
        var volumeUrl = string.IsNullOrEmpty(blobBase)
            ? "/" + humanPath + "/"
            : blobBase + "/" + humanPath + "/";

        return new ElFinderOptions
        {
            Url = volumeUrl,
            TmbUrl = tmbUrl ?? "/FileManager/GetImageThumbnail?target=",
            Path = string.IsNullOrEmpty(humanPath) ? "Media" : humanPath,
        };
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

