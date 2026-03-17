// <copyright file="GetArticleRedirectsQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Query to retrieve all article redirects (articles with redirect status).
/// </summary>
public class GetArticleRedirectsQuery : IQuery<IEnumerable<RedirectItemViewModel>>
{
    /// <summary>
    /// Gets or sets the optional cache duration. If null, no caching is applied.
    /// </summary>
    /// <remarks>
    /// Recommended duration: 5-10 minutes for redirects that change infrequently.
    /// Cache is automatically invalidated when articles are published/unpublished.
    /// </remarks>
    public TimeSpan? CacheDuration { get; set; }
}
