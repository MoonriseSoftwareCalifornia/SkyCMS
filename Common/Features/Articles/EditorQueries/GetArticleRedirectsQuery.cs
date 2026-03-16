// <copyright file="GetArticleRedirectsQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using System;
using System.Collections.Generic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Query to retrieve all article redirects (articles with redirect status).
/// </summary>
public class GetArticleRedirectsQuery : IQuery<IEnumerable<RedirectItemViewModel>>
{
    /// <summary>
    /// Gets or initializes the optional cache duration for the redirect list.
    /// </summary>
    /// <remarks>
    /// When set, the handler will cache the redirect list for the specified duration.
    /// Recommended: 5-10 minutes for production, null (no caching) during active redirect management.
    /// Cache is automatically invalidated when articles are published/unpublished.
    /// </remarks>
    public TimeSpan? CacheDuration { get; init; }
}
