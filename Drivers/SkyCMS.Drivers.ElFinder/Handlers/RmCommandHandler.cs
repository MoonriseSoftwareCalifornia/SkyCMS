using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "rm" (remove/delete) command: deletes files and folders.
/// </summary>
public class RmCommandHandler : IRequestHandler<RmCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public RmCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(RmCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Target))
            {
                return ElFinderErrorResponse.InvalidParams("Target is required");
            }

            var response = new RmResponse { VolumeId = request.VolumeId };

            // Handle single or multiple targets (comma-separated)
            var targets = request.Target.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var target in targets)
            {
                var path = _adapter.DecodePath(target.Trim());
                if (path == null)
                {
                    continue; // Skip invalid hashes
                }

                // Check accessibility
                if (!await _adapter.IsAccessibleAsync(path, cancellationToken))
                {
                    continue; // Skip inaccessible items
                }

                try
                {
                    await _adapter.DeleteAsync(path, cancellationToken);
                    response.Removed.Add(target.Trim());
                }
                catch
                {
                    // Continue deleting other items even if one fails
                    continue;
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Rm failed: {ex.Message}");
        }
    }
}
