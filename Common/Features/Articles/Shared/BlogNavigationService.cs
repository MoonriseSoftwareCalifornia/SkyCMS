// <copyright file="BlogNavigationService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Shared;

using System;
using System.Linq;
using System.Threading.Tasks;
using Cosmos.Cms.Common;
using Cosmos.Common.Data;
using Cosmos.Common.Models;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Implementation of IBlogNavigationService for blog post navigation.
/// Provides previous/next post links and enriches blog post view models with navigation metadata.
/// </summary>
public class BlogNavigationService : IBlogNavigationService
{
    private readonly ApplicationDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlogNavigationService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for querying blog post relationships.</param>
    public BlogNavigationService(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<(TableOfContentsItem? previous, TableOfContentsItem? next)> GetAdjacentBlogPostsAsync(
        DateTimeOffset published)
    {
        var prev = await dbContext.ArticleCatalog
            .Where(a => a.Published < published && a.Published != null)
            .OrderByDescending(a => a.Published)
            .Select(a => new TableOfContentsItem { Title = a.Title, UrlPath = a.UrlPath, Published = a.Published.Value })
            .FirstOrDefaultAsync();

        var next = await dbContext.ArticleCatalog
            .Where(a => a.Published > published && a.Published != null)
            .OrderBy(a => a.Published)
            .Select(a => new TableOfContentsItem { Title = a.Title, UrlPath = a.UrlPath, Published = a.Published.Value })
            .FirstOrDefaultAsync();

        return (prev, next);
    }

    /// <inheritdoc />
    public async Task EnrichBlogNavigationAsync(ArticleViewModel? model)
    {
        if (model == null || model.ArticleType != ArticleType.BlogPost || !model.Published.HasValue)
        {
            return;
        }

        var (previous, next) = await GetAdjacentBlogPostsAsync(model.Published.Value);

        if (previous != null)
        {
            model.PreviousTitle = previous.Title;
            model.PreviousUrl = previous.UrlPath == "root" ? "/" : "/" + previous.UrlPath.TrimStart('/');
        }

        if (next != null)
        {
            model.NextTitle = next.Title;
            model.NextUrl = next.UrlPath == "root" ? "/" : "/" + next.UrlPath.TrimStart('/');
        }
    }
}
