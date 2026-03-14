// <copyright file="GetLayoutByIdQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Layouts.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Handler for retrieving a layout by its unique identifier.
/// </summary>
/// <param name="dbContext">Database context for layout queries.</param>
public class GetLayoutByIdQueryHandler(IApplicationDbContext dbContext) : IQueryHandler<GetLayoutByIdQuery, Layout?>
{
    private readonly IApplicationDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    public async Task<Layout?> HandleAsync(GetLayoutByIdQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (query.LayoutId == Guid.Empty)
        {
            return null;
        }

        return await dbContext.Layouts.FirstOrDefaultAsync(l => l.Id == query.LayoutId, cancellationToken);
    }
}
