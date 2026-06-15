using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "size" command: calculates total size of files/folders.
/// </summary>
public class SizeCommandHandler : IElFinderHandler<SizeCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public SizeCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> HandleAsync(SizeCommand request, CancellationToken cancellationToken)
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

            // Calculate size
            var totalSize = await _adapter.GetSizeAsync(targetPath, cancellationToken);

            var response = new SizeResponse
            {
                Size = totalSize,
                VolumeId = request.VolumeId
            };

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Size failed: {ex.Message}");
        }
    }
}
