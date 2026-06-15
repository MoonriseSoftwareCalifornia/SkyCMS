using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MimeTypes;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "search" command.
/// Performs a recursive substring match on item names within the target directory.
/// See Docs/commands/search.md.
/// </summary>
public class SearchCommandHandler : IElFinderHandler<SearchCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public SearchCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> HandleAsync(SearchCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return ElFinderErrorResponse.InvalidParams("Query is required");
        }

        // Resolve search root — default to volume root when no target provided.
        string rootPath = "pub/";
        if (!string.IsNullOrWhiteSpace(request.Target))
        {
            var decoded = _adapter.DecodePath(request.Target);
            if (decoded == null)
            {
                return ElFinderErrorResponse.InvalidParams("Invalid target hash");
            }

            if (!await _adapter.IsAccessibleAsync(decoded, cancellationToken))
            {
                return ElFinderErrorResponse.Access();
            }

            rootPath = decoded;
        }

        var matches = await _adapter.SearchAsync(request.Query, rootPath, cancellationToken);

        var response = new SearchResponse();

        var mimeFilter = request.Mimes;
        foreach (var (entry, fullPath) in matches)
        {
            var mime = entry.IsDirectory ? "directory" : GetMimeType(entry.Name ?? string.Empty);

            // Apply optional MIME filter (prefix match: "image/" matches any image type).
            if (mimeFilter != null && mimeFilter.Count > 0)
            {
                if (!mimeFilter.Any(m => mime.StartsWith(m, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            var hash = _adapter.EncodePath(fullPath);
            var parentPath = fullPath.TrimEnd('/');
            var lastSlash = parentPath.LastIndexOf('/');
            var phash = lastSlash >= 0 ? _adapter.EncodePath(parentPath[..(lastSlash + 1)]) : _adapter.EncodePath("/");

            response.Files.Add(new ElFinderObject
            {
                Hash = hash,
                PHash = phash,
                Name = entry.Name ?? string.Empty,
                Size = entry.Size,
                Mime = mime,
                Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
                Read = 1,
                Write = 1,
                Locked = 0,
                Dirs = entry.IsDirectory ? 1 : 0,
            });
        }

        return response;
    }

    private static string GetMimeType(string fileName)
    {
        try
        {
            return MimeTypeMap.GetMimeType(Path.GetExtension(fileName));
        }
        catch
        {
            return "application/octet-stream";
        }
    }
}
