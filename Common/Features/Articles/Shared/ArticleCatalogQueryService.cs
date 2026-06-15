// <copyright file="ArticleCatalogQueryService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Shared;

using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

/// <summary>
/// Implementation of IArticleCatalogQueryService for querying article catalog.
/// Provides hierarchical table of contents and full-text search capabilities.
/// </summary>
public class ArticleCatalogQueryService : IArticleCatalogQueryService
{
    private readonly ApplicationDbContext dbContext;
    private readonly string publisherUrl;
    private readonly string blobPublicUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleCatalogQueryService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for querying article catalog.</param>
    /// <param name="publisherUrl">Base publisher URL for absolute URL construction.</param>
    /// <param name="blobPublicUrl">Public blob storage URL for asset resolution.</param>
    public ArticleCatalogQueryService(
        ApplicationDbContext dbContext,
        string publisherUrl,
        string blobPublicUrl)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.publisherUrl = publisherUrl ?? string.Empty;
        this.blobPublicUrl = blobPublicUrl ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<TableOfContents> GetTableOfContentsAsync(
        string prefix,
        int pageNo = 0,
        int pageSize = 10,
        bool orderByPublishedDate = false)
    {
        // Normalize prefix
        if (string.IsNullOrEmpty(prefix) || string.IsNullOrWhiteSpace(prefix) || prefix.Equals("/"))
        {
            prefix = string.Empty;
        }
        else
        {
            prefix = (HttpUtility.UrlDecode(prefix.ToLower()
                    .Replace("%20", "_")
                    .Replace(" ", "_")) + "/")
                .Trim('/');
        }

        var skip = pageNo * pageSize;

        // Exclude deleted and redirect articles from public surfaces
        var deletedStatusCode = (int)StatusCodeEnum.Deleted;
        var redirectStatusCode = (int)StatusCodeEnum.Redirect;

        IQueryable<TableOfContentsItem> query;

        if (string.IsNullOrEmpty(prefix))
        {
            // Root level articles
            query = from t in dbContext.ArticleCatalog
                    where t.Published.HasValue
                          && t.StatusCode != deletedStatusCode
                          && t.StatusCode != redirectStatusCode
                    select new TableOfContentsItem
                    {
                        UrlPath = t.UrlPath,
                        Title = t.Title,
                        Published = t.Published.Value,
                        Updated = t.Updated,
                        BannerImage = t.BannerImage,
                        AuthorInfo = t.AuthorInfo,
                        Introduction = t.Introduction
                    };
        }
        else
        {
            // One level deep from prefix
            var count = prefix.Count(c => c == '/');
            var dcount = "{" + count + "}";
            var escapedPrefix = Regex.Escape(prefix.TrimStart('/'));
            var pattern = $"(?i)(^{escapedPrefix})(\\/[^\\/]*){dcount}$";

            query = from t in dbContext.ArticleCatalog
                    where t.Published.HasValue
                          && t.StatusCode != deletedStatusCode
                          && t.StatusCode != redirectStatusCode
                          && t.UrlPath != prefix
                          && t.UrlPath.StartsWith(prefix)
                          && Regex.IsMatch(t.UrlPath, pattern)
                    select new TableOfContentsItem
                    {
                        UrlPath = t.UrlPath,
                        Title = t.Title,
                        Published = t.Published.Value,
                        Updated = t.Updated,
                        BannerImage = t.BannerImage,
                        AuthorInfo = t.AuthorInfo,
                        Introduction = t.Introduction
                    };
        }

        // Fetch all matching items and apply client-side filtering for time and sorting
        var data = await query.ToListAsync();
        var sort = data.AsQueryable();
        sort = orderByPublishedDate
            ? sort.OrderByDescending(o => o.Published)
            : sort.OrderBy(o => o.UrlPath);

        var now = DateTimeOffset.UtcNow;
        var items = sort
            .Where(w => w.Published.UtcDateTime <= now)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return new TableOfContents
        {
            TotalCount = items.Count,
            PageNo = pageNo,
            PageSize = pageSize,
            Items = items,
            PublisherUrl = publisherUrl,
            BlobPublicUrl = blobPublicUrl
        };
    }

    /// <inheritdoc />
    public async Task<List<TableOfContentsItem>> SearchAsync(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            return new List<TableOfContentsItem>();
        }

        searchText = searchText.ToLower();

        // Exclude deleted and redirect articles from public search results
        var deletedStatusCode = (int)StatusCodeEnum.Deleted;
        var redirectStatusCode = (int)StatusCodeEnum.Redirect;

        var dt = DateTimeOffset.UtcNow;
        var query = dbContext.ArticleCatalog
            .Where(a => a.Published.HasValue
                        && a.Published <= dt
                        && a.StatusCode != deletedStatusCode
                        && a.StatusCode != redirectStatusCode
                        && (a.Introduction.ToLower().Contains(searchText) || a.Title.ToLower().Contains(searchText)))
            .AsQueryable();

        // AND-combine multi-term searches
        var terms = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length > 1)
        {
            foreach (var term in terms)
            {
                query = query.Where(a => a.Introduction.ToLower().Contains(term) || a.Title.ToLower().Contains(term));
            }
        }

        query = query.OrderByDescending(o => o.Title);

        var results = await query.Select(s => new TableOfContentsItem
        {
            UrlPath = s.UrlPath,
            Title = s.Title,
            Published = s.Published.Value,
            Updated = s.Updated,
            BannerImage = s.BannerImage,
            AuthorInfo = s.AuthorInfo
        }).ToListAsync();

        return results;
    }
}
