using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "parents" command: returns breadcrumb path from target to root.
/// </summary>
public class ParentsCommandHandler : IRequestHandler<ParentsCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public ParentsCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(ParentsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Target))
            {
                return ElFinderErrorResponse.InvalidParams("Target is required");
            }

            var targetPath = _adapter.DecodePath(request.Target);
            if (targetPath == null)
            {
                return ElFinderErrorResponse.InvalidParams("Invalid target hash");
            }

            // Check accessibility
            if (!await _adapter.IsAccessibleAsync(targetPath, cancellationToken))
            {
                return ElFinderErrorResponse.Access("Access denied");
            }

            var response = new ParentsResponse { VolumeId = request.VolumeId };
            var seen = new HashSet<string>();

            // Get all ancestor paths from root down to the target's parent.
            var ancestors = await _adapter.GetAncestorsAsync(targetPath, cancellationToken);

            // For each ancestor, add all sibling directories at that level so the
            // tree panel can render the full folder hierarchy (matches legacy handler).
            foreach (var ancestor in ancestors)
            {
                var ancestorPath = ancestor.Path.TrimEnd('/');
                var lastSlash = ancestorPath.LastIndexOf('/');
                var parentPath = lastSlash >= 0 ? ancestorPath[..lastSlash] : string.Empty;

                if (string.IsNullOrEmpty(parentPath))
                {
                    // Root level — add the ancestor itself.
                    if (seen.Add(ancestorPath))
                    {
                        response.Tree.Add(ConvertToElFinderObject(ancestor, ancestorPath));
                    }
                }
                else
                {
                    // Add all directory siblings at this level.
                    var siblings = await _adapter.GetEntriesAsync(parentPath, cancellationToken);
                    foreach (var sibling in siblings.Where(e => e.IsDirectory))
                    {
                        var sibPath = sibling.Path.TrimEnd('/');
                        if (seen.Add(sibPath))
                        {
                            response.Tree.Add(ConvertToElFinderObject(sibling, sibPath));
                        }
                    }
                }
            }

            // Include the target itself.
            var targetEntry = await _adapter.GetEntryAsync(targetPath, cancellationToken);
            if (targetEntry != null && seen.Add(targetPath.TrimEnd('/')))
            {
                response.Tree.Add(ConvertToElFinderObject(targetEntry, targetPath));
            }

            // Include children of the target so the node can expand in the tree panel.
            var targetChildren = await _adapter.GetEntriesAsync(targetPath, cancellationToken);
            foreach (var child in targetChildren.Where(e => e.IsDirectory))
            {
                var childPath = child.Path.TrimEnd('/');
                if (seen.Add(childPath))
                {
                    response.Tree.Add(ConvertToElFinderObject(child, childPath));
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Parents failed: {ex.Message}");
        }
    }

    private ElFinderObject ConvertToElFinderObject(Cosmos.BlobService.FileManagerEntry entry, string path)
    {
        var hash = _adapter.EncodePath(path);
        var parentPath = path.TrimEnd('/');
        var lastSlash = parentPath.LastIndexOf('/');
        var phash = lastSlash >= 0 ? _adapter.EncodePath(parentPath.Substring(0, lastSlash + 1)) : _adapter.EncodePath("/");

        return new ElFinderObject
        {
            Hash = hash,
            PHash = phash,
            Name = entry.Name,
            Size = entry.Size,
            Mime = entry.IsDirectory ? "directory" : GetMimeType(entry.Name),
            Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
            Read = 1,
            Write = 1,
            Locked = 0,
            Dirs = entry.IsDirectory ? 1 : 0,
            Tmb = null,
            Url = null
        };
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
