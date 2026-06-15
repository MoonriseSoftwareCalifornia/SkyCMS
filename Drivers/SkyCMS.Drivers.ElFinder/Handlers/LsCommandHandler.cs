using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.BlobService;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "ls" command.
/// Returns a plain array of item names, optionally filtered to an intersect list
/// for conflict detection before rename/paste operations.
/// See Docs/commands/ls.md.
/// </summary>
public class LsCommandHandler : IElFinderHandler<LsCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public LsCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> HandleAsync(LsCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
        {
            return ElFinderErrorResponse.InvalidParams();
        }

        var path = _adapter.DecodePath(request.Target);
        if (path == null)
        {
            return ElFinderErrorResponse.InvalidParams();
        }

        if (!await _adapter.IsAccessibleAsync(path, cancellationToken))
        {
            return ElFinderErrorResponse.Access();
        }

        var entries = await _adapter.GetEntriesAsync(path, cancellationToken);

        var names = entries
            .Select(GetDisplayName)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        // Apply intersect filter when provided (conflict-check mode).
        var intersect = request.Intersect;
        if (intersect != null && intersect.Count > 0)
        {
            var intersectSet = new HashSet<string>(intersect, StringComparer.OrdinalIgnoreCase);
            names = names.Where(n => intersectSet.Contains(n)).ToList();
        }

        return new LsResponse { List = names };
    }

    private static string GetDisplayName(FileManagerEntry entry)
    {
        if (entry.IsDirectory)
        {
            return entry.Name ?? string.Empty;
        }

        var name = entry.Name ?? string.Empty;
        var ext = entry.Extension ?? string.Empty;

        if (!string.IsNullOrEmpty(ext) && !ext.StartsWith('.'))
        {
            ext = "." + ext;
        }

        if (string.IsNullOrEmpty(ext))
        {
            return name;
        }

        // Avoid doubling the extension when Name already includes it.
        return name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
            ? name
            : name + ext;
    }
}
