// <copyright file="GetLayoutInventoryQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Sky.Editor.Models;

    /// <summary>
    /// Handles layout inventory retrieval for editor and VS Code APIs.
    /// </summary>
    public class GetLayoutInventoryQueryHandler : IQueryHandler<GetLayoutInventoryQuery, List<LayoutInventoryItem>>
    {
        private readonly ApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetLayoutInventoryQueryHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        public GetLayoutInventoryQueryHandler(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        /// <inheritdoc/>
        public async Task<List<LayoutInventoryItem>> HandleAsync(
            GetLayoutInventoryQuery query,
            CancellationToken cancellationToken = default)
        {
            query ??= new GetLayoutInventoryQuery();

            var layoutQuery = dbContext.Layouts
                .AsNoTracking()
                .Select(l => new
                {
                    l.LayoutNumber,
                    l.LayoutName,
                    l.Version,
                    l.IsDefault,
                    l.Published,
                    l.LastModified,
                });

            if (query.PublishedOnly)
            {
                layoutQuery = layoutQuery.Where(l => l.Published != null);
            }

            var rows = await layoutQuery.ToListAsync(cancellationToken);

            var lastPublishedByLayoutNumber = rows
                .Where(r => r.Published.HasValue)
                .GroupBy(r => r.LayoutNumber)
                .ToDictionary(g => g.Key, g => g.Max(r => r.Published));

            var latestRows = rows
                .GroupBy(l => l.LayoutNumber)
                .Select(g => g.OrderByDescending(l => l.Version ?? 0).First())
                .ToList();

            var items = latestRows
                .Select(l =>
                {
                    var hasPublishedDate = lastPublishedByLayoutNumber.TryGetValue(l.LayoutNumber, out var lastPublished)
                        && lastPublished.HasValue;

                    return new LayoutInventoryItem
                    {
                        LayoutNumber = l.LayoutNumber,
                        LayoutName = l.LayoutName,
                        Version = l.Version ?? 0,
                        IsDefault = l.IsDefault,
                        IsPublished = hasPublishedDate,
                        LastPublished = hasPublishedDate
                            ? lastPublished?.UtcDateTime.ToString("o")
                            : null,
                        LastModified = l.LastModified?.UtcDateTime.ToString("o") ?? string.Empty,
                    };
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(query.Term))
            {
                var searchTerm = query.Term.ToLowerInvariant();
                items = items
                    .Where(i => i.LayoutName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return items
                .OrderBy(i => i.IsDefault ? 0 : 1)
                .ThenBy(i => i.LayoutName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
