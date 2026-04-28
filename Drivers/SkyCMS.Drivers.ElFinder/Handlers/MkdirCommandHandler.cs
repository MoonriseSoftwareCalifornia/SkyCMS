using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "mkdir" command: creates a new directory.
/// </summary>
public class MkdirCommandHandler : IRequestHandler<MkdirCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public MkdirCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(MkdirCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Target))
            {
                return ElFinderErrorResponse.InvalidParams("Target is required");
            }

            if (string.IsNullOrEmpty(request.Name))
            {
                return ElFinderErrorResponse.InvalidParams("Name is required");
            }

            var parentPath = _adapter.DecodePath(request.Target);
            if (parentPath == null)
            {
                return ElFinderErrorResponse.InvalidParams("Invalid target hash");
            }

            // Check accessibility
            if (!await _adapter.IsAccessibleAsync(parentPath, cancellationToken))
            {
                return ElFinderErrorResponse.Access("Access denied");
            }

            // Create folder
            var newFolderPath = parentPath.TrimEnd('/') + "/" + request.Name;
            var createdEntry = await _adapter.CreateFolderAsync(newFolderPath, cancellationToken);

            var response = new MkdirResponse
            {
                VolumeId = request.VolumeId
            };

            if (createdEntry != null)
            {
                response.Added.Add(ConvertToElFinderObject(createdEntry, newFolderPath));
            }

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Mkdir failed: {ex.Message}");
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
            Mime = "directory",
            Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
            Read = 1,
            Write = 1,
            Locked = 0,
            Dirs = 1,
            Tmb = null,
            Url = null
        };
    }
}
