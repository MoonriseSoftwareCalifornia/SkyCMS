using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "url" command: returns a public URL for a file hash.
/// See Docs/commands/url.md.
/// </summary>
public class UrlCommandHandler : IRequestHandler<UrlCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public UrlCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public Task<IElFinderResponse> Handle(UrlCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
        {
            return Task.FromResult<IElFinderResponse>(ElFinderErrorResponse.InvalidParams("Target is required"));
        }

        var path = _adapter.DecodePath(request.Target);
        if (path == null)
        {
            return Task.FromResult<IElFinderResponse>(ElFinderErrorResponse.InvalidParams("Invalid target hash"));
        }

        var baseUrl = (request.BlobPublicUrl ?? string.Empty).TrimEnd('/');
        var filePath = path.TrimStart('/');
        var url = string.IsNullOrEmpty(baseUrl) ? "/" + filePath : $"{baseUrl}/{filePath}";

        return Task.FromResult<IElFinderResponse>(new UrlResponse { Url = url });
    }
}
