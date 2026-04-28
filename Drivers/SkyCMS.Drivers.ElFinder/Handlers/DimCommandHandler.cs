using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SixLabors.ImageSharp;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "dim" command: returns "WxH" pixel dimensions using ImageSharp.Identify.
/// See Docs/commands/dim.md.
/// </summary>
public class DimCommandHandler : IRequestHandler<DimCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public DimCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(DimCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
        {
            return ElFinderErrorResponse.InvalidParams("Target is required");
        }

        var path = _adapter.DecodePath(request.Target);
        if (path == null)
        {
            return ElFinderErrorResponse.InvalidParams("Invalid target hash");
        }

        if (!await _adapter.IsAccessibleAsync(path, cancellationToken))
        {
            return ElFinderErrorResponse.Access();
        }

        var stream = await _adapter.GetReadStreamAsync(path, cancellationToken);
        if (stream == null)
        {
            return ElFinderErrorResponse.NotFound();
        }

        try
        {
            using (stream)
            {
                var info = await Image.IdentifyAsync(stream, cancellationToken);
                if (info == null)
                {
                    return ElFinderErrorResponse.Generic("Cannot identify image dimensions");
                }

                return new DimResponse { Dim = $"{info.Width}x{info.Height}" };
            }
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Failed to read image dimensions: {ex.Message}");
        }
    }
}
