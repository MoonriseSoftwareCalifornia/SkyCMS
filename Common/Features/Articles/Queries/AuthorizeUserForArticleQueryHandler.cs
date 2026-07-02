// <copyright file="AuthorizeUserForArticleQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Handler for authorizing user access to an article based on article permissions.
/// Checks anonymous access, authenticated access, user-specific permissions, and role-based permissions.
/// </summary>
/// <param name="dbContext">Database context for querying article permissions.</param>
public class AuthorizeUserForArticleQueryHandler(IApplicationDbContext dbContext): IQueryHandler<AuthorizeUserForArticleQuery, bool>
{
    private readonly IApplicationDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    public async Task<bool> HandleAsync(AuthorizeUserForArticleQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        List<ArticlePermission> permissions = null;
        try
        {
            permissions = dbContext.ArticleCatalog.AsNoTracking()
                .FirstOrDefault(l => l.ArticleNumber == query.articleNumber)
                ?.ArticlePermissions;

            if (permissions == null || permissions.Count == 0)
            {
                return false; // No one can access this page.
            }
        }
        catch (Exception)
        {
            return false;
        }

        var roleIds = permissions.Where(w => w.IsRoleObject).Select(s => s.IdentityObjectId).ToArray();

        // Check for anonymous user access.
        if (await dbContext.Roles.AsNoTracking()
            .Where(w => roleIds.Contains(w.Id) && w.NormalizedName == "ANONYMOUS")
            .AnyAsync(cancellationToken))
        {
            return true; // Anonymous users can view, so that means everyone.
        }

        if (query.user.Identity.IsAuthenticated &&
            await dbContext.Roles.AsNoTracking()
                .Where(w => roleIds.Contains(w.Id) && w.NormalizedName == "AUTHENTICATED")
                .AnyAsync(cancellationToken))
        {
            return true;
        }

        // Get the current user ID and see if this person has user-specific access.
        var userId = query.user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (permissions.Exists(a => a.IdentityObjectId.Equals(userId, StringComparison.OrdinalIgnoreCase)))
        {
            return true; // Current user has access.
        }

        // Finally, if a user has role permissions, grant access here.
        return (await dbContext.UserRoles.AsNoTracking()
            .CountAsync(a => a.UserId == userId && roleIds.Contains(a.RoleId), cancellationToken)) > 0;
    }
}
