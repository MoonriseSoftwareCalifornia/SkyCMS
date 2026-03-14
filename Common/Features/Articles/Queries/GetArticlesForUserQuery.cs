// <copyright file="GetArticlesForUserQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System.Collections.Generic;
using System.Security.Claims;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Query to get all articles accessible to a given user based on their roles and permissions.
/// Replaces CosmosUtilities.GetArticlesForUser() method.
/// </summary>
/// <param name="User">The claims principal representing the current user.</param>
public record GetArticlesForUserQuery(ClaimsPrincipal User) : IQuery<List<TableOfContentsItem>>;
