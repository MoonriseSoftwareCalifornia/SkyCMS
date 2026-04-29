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
            var notFound = new List<string>();
            var notFoundDetails = new List<RmDiagnosticEntry>();
            var notRemoved = new List<string>();
            var notRemovedDetails = new List<RmDiagnosticEntry>();

            // Handle single or multiple targets (comma-separated)
            var targets = request.Target.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var target in targets)
            {
                var trimmedTarget = target.Trim();
                var path = _adapter.DecodePath(trimmedTarget);
                if (path == null)
                {
                    notFound.Add(trimmedTarget);
                    notFoundDetails.Add(new RmDiagnosticEntry
                    {
                        Hash = trimmedTarget,
                        Reason = "Unable to decode target hash",
                        ReasonCode = "hash_decode_failed",
                    });
                    continue;
                }

                // Resolve the entry once; this also acts as the accessibility + existence check.
                var entry = await _adapter.GetEntryAsync(path, cancellationToken);
                if (entry == null)
                {
                    notFound.Add(trimmedTarget);
                    notFoundDetails.Add(new RmDiagnosticEntry
                    {
                        Hash = trimmedTarget,
                        Path = path,
                        Reason = "Target is not accessible or does not exist",
                        ReasonCode = "not_accessible",
                    });
                    continue;
                }

                try
                {
                    await _adapter.DeleteAsync(entry, cancellationToken);

                    // Only report success when the target no longer resolves as accessible.
                    if (!await _adapter.IsAccessibleAsync(path, cancellationToken))
                    {
                        response.Removed.Add(trimmedTarget);
                    }
                    else
                    {
                        notRemoved.Add(trimmedTarget);
                        notRemovedDetails.Add(new RmDiagnosticEntry
                        {
                            Hash = trimmedTarget,
                            Path = path,
                            Reason = "Delete call completed but target is still accessible",
                            ReasonCode = "delete_no_effect",
                        });
                    }
                }
                catch
                {
                    notRemoved.Add(trimmedTarget);
                    notRemovedDetails.Add(new RmDiagnosticEntry
                    {
                        Hash = trimmedTarget,
                        Path = path,
                        Reason = "Delete operation threw an exception",
                        ReasonCode = "delete_exception",
                    });
                    continue;
                }
            }

            if (notFound.Count > 0)
            {
                response.NotFound = notFound;
                response.NotFoundDetails = notFoundDetails;
            }

            if (notRemoved.Count > 0)
            {
                response.NotRemoved = notRemoved;
                response.NotRemovedDetails = notRemovedDetails;
            }

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Rm failed: {ex.Message}");
        }
    }
}
