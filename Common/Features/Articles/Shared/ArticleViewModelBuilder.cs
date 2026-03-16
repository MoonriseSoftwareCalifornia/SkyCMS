// <copyright file="ArticleViewModelBuilder.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Shared;

using Cosmos.Cms.Common;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Layouts.Queries;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

/// <summary>
/// Service for building ArticleViewModel instances from Article and PublishedPage entities.
/// Handles author info resolution, layout resolution, and Open Graph metadata generation.
/// </summary>
public class ArticleViewModelBuilder : IArticleViewModelBuilder
{
    private readonly IMediator mediator;
    private readonly ApplicationDbContext dbContext;
    private readonly IMemoryCache memoryCache;
    private readonly string publisherUrl;
    private readonly bool isEditor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleViewModelBuilder"/> class.
    /// </summary>
    /// <param name="mediator">Mediator for CQRS queries.</param>
    /// <param name="dbContext">Database context for author and layout resolution.</param>
    /// <param name="memoryCache">Memory cache for layout caching (optional).</param>
    /// <param name="publisherUrl">Base publisher URL for Open Graph URL generation.</param>
    /// <param name="isEditor">Whether building for editor context (affects ReadWriteMode flag).</param>
    public ArticleViewModelBuilder(
        IMediator mediator,
        ApplicationDbContext dbContext,
        IMemoryCache memoryCache,
        string publisherUrl,
        bool isEditor = false)
    {
        this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.memoryCache = memoryCache;
        this.publisherUrl = publisherUrl ?? string.Empty;
        this.isEditor = isEditor;
    }

    /// <inheritdoc />
    public async Task<ArticleViewModel> BuildFromArticleAsync(Article article, string lang, bool includeLayout = true)
    {
        if (article == null)
        {
            throw new ArgumentNullException(nameof(article));
        }

        var author = string.Empty;
        if (!string.IsNullOrEmpty(article.UserId))
        {
            var authorInfo = await dbContext.AuthorInfos
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == article.UserId);
            if (authorInfo != null)
            {
                author = JsonConvert.SerializeObject(authorInfo).Replace("\"", "'");
            }
        }

        var layout = includeLayout ? await GetDefaultLayoutAsync() : null;
        return new ArticleViewModel(article, layout, isEditor, author, lang);
    }

    /// <inheritdoc />
    public async Task<ArticleViewModel> BuildFromPublishedPageAsync(
        PublishedPage publishedPage,
        string lang,
        TimeSpan? layoutCacheDuration = null,
        bool includeLayout = true)
    {
        if (publishedPage == null)
        {
            throw new ArgumentNullException(nameof(publishedPage));
        }

        return new ArticleViewModel
        {
            ArticleNumber = publishedPage.ArticleNumber,
            BannerImage = publishedPage.BannerImage,
            LanguageCode = lang,
            LanguageName = string.Empty,
            CacheDuration = 10,
            Content = publishedPage.Content,
            StatusCode = (StatusCodeEnum)publishedPage.StatusCode,
            Id = publishedPage.Id,
            Published = publishedPage.Published ?? null,
            Title = publishedPage.Title,
            UrlPath = publishedPage.UrlPath,
            Updated = publishedPage.Updated,
            VersionNumber = publishedPage.VersionNumber,
            HeadJavaScript = publishedPage.HeaderJavaScript,
            FooterJavaScript = publishedPage.FooterJavaScript,
            Layout = includeLayout ? await GetDefaultLayoutAsync(layoutCacheDuration) : null,
            ReadWriteMode = isEditor,
            Expires = publishedPage.Expires ?? null,
            AuthorInfo = publishedPage.AuthorInfo,
            OGDescription = string.Empty,
            OGImage = string.IsNullOrEmpty(publishedPage.BannerImage)
                ? string.Empty
                : publishedPage.BannerImage.StartsWith("http")
                    ? publishedPage.BannerImage
                    : publisherUrl.TrimEnd('/') + "/" + publishedPage.BannerImage.TrimStart('/'),
            OGUrl = GetOGUrl(publishedPage.UrlPath),
            ArticleType = (ArticleType)publishedPage.ArticleType,
            Category = publishedPage.Category,
            Introduction = publishedPage.Introduction
        };
    }

    /// <summary>
    /// Returns the default layout (optionally cached) including navigation markup placeholders.
    /// </summary>
    private async Task<LayoutViewModel> GetDefaultLayoutAsync(TimeSpan? layoutCache = null)
    {
        if (memoryCache == null || layoutCache == null)
        {
            return await mediator.QueryAsync(new GetDefaultLayoutQuery());
        }

        if (!memoryCache.TryGetValue("defLayout", out LayoutViewModel model))
        {
            model = await mediator.QueryAsync(new GetDefaultLayoutQuery());
            memoryCache.Set("defLayout", model, layoutCache.Value);
        }

        return model;
    }

    /// <summary>
    /// Compose an absolute Open Graph URL for a page based on publisher base URL.
    /// </summary>
    private string GetOGUrl(string urlPath)
    {
        if (string.IsNullOrWhiteSpace(publisherUrl))
        {
            return urlPath;
        }

        return publisherUrl.TrimEnd('/') + "/" + urlPath.TrimStart('/');
    }
}
