// <copyright file="GetSitemapQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Sitemap.Queries;

using System;
using Cosmos.Common.Features.Shared;
using X.Web.Sitemap;

/// <summary>
/// Query to generate the sitemap for the website.
/// </summary>
/// <remarks>
/// Builds a sitemap consisting of root and published content entries.
/// Uses basic priority heuristics (root=1.0, others=0.5).
/// Banner images are attached when present.
/// </remarks>
public record GetSitemapQuery : IQuery<Sitemap>
{
    /// <summary>
    /// Gets or sets the optional cache duration for the sitemap.
    /// </summary>
    /// <remarks>
    /// When set, the sitemap will be cached for the specified duration.
    /// Recommended: 30-60 minutes since sitemaps rarely change.
    /// Cache is automatically invalidated when articles are published/unpublished.
    /// </remarks>
    public TimeSpan? CacheDuration { get; init; }
}
