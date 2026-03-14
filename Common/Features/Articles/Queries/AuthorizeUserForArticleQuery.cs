// <copyright file="AuthorizeUserForArticleQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System.Security.Claims;
using Cosmos.Common.Features.Shared;

/// <summary>
/// Query to check if a user is authorized to access an article based on article permissions.
/// Replaces CosmosUtilities.AuthUser() method.
/// </summary>
/// <param name="User">The claims principal representing the current user.</param>
/// <param name="ArticleNumber">The article number to check authorization for.</param>
public record AuthorizeUserForArticleQuery(
    ClaimsPrincipal User,
    int ArticleNumber) : IQuery<bool>;
