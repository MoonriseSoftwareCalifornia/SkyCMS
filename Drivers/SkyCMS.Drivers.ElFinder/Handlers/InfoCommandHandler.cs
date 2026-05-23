using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "info" command.
/// </summary>
public class InfoCommandHandler : IElFinderHandler<InfoCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public InfoCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> HandleAsync(InfoCommand request, CancellationToken cancellationToken)
    {
        var response = new InfoResponse();
        if (string.IsNullOrWhiteSpace(request.Targets))
        {
            return response;
        }

        var targets = request.Targets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var target in targets)
        {
            var path = _adapter.DecodePath(target);
            if (path == null)
            {
                continue;
            }

            if (!await _adapter.IsAccessibleAsync(path, cancellationToken))
            {
                continue;
            }

            var entry = await _adapter.GetEntryAsync(path, cancellationToken);
            if (entry == null)
            {
                continue;
            }

            var parentPath = path.Contains('/') ? path[..path.LastIndexOf('/')] : "/";
            if (string.IsNullOrEmpty(parentPath))
            {
                parentPath = "/";
            }

            response.Files.Add(new ElFinderObject
            {
                Hash = target,
                PHash = _adapter.EncodePath(parentPath),
                Name = entry.Name,
                Size = entry.IsDirectory ? 0 : entry.Size,
                Mime = entry.IsDirectory ? "directory" : string.IsNullOrWhiteSpace(entry.ContentType) ? "application/octet-stream" : entry.ContentType,
                Ts = new DateTimeOffset(entry.Modified == default ? DateTime.UtcNow : entry.Modified).ToUnixTimeSeconds(),
                Read = 1,
                Write = 1,
                Locked = 0,
                Dirs = entry.IsDirectory && entry.HasDirectories ? 1 : 0,
                Url = entry.IsDirectory ? null : entry.Path
            });
        }

        return response;
    }
}
