using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "tmb" command.
/// </summary>
public class TmbCommandHandler : IElFinderHandler<TmbCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public TmbCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> HandleAsync(TmbCommand request, CancellationToken cancellationToken)
    {
        var response = new TmbResponse();
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

            var entry = await _adapter.GetEntryAsync(path, cancellationToken);
            if (entry == null || entry.IsDirectory)
            {
                continue;
            }

            var ext = (entry.Extension ?? string.Empty).ToLowerInvariant();
            if (ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp")
            {
                response.Images[target] = $"/FileManager/GetImageThumbnail?target={Uri.EscapeDataString(path)}&width=80&height=80";
            }
        }

        return response;
    }
}
