// <copyright file="CheckDefaultLayoutExistsQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Layouts.Queries;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Handler for checking if any default layout exists.
/// Useful for setup/initialization scenarios to determine if a default layout needs to be created.
/// </summary>
/// <param name="dbContext">Database context for layout queries.</param>
public class CheckDefaultLayoutExistsQueryHandler(IApplicationDbContext dbContext) : IQueryHandler<CheckDefaultLayoutExistsQuery, bool>
{
    private readonly IApplicationDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    public async Task<bool> HandleAsync(CheckDefaultLayoutExistsQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var now = DateTimeOffset.UtcNow;
        return await dbContext.Layouts
            .Where(l => l.IsDefault && l.Published <= now)
            .AnyAsync(cancellationToken);
    }
}
