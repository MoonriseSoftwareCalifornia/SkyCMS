// <copyright file="GetEditorInventoryQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Sky.Editor.Models;

    /// <summary>
    /// Handles article inventory retrieval for editor and VS Code APIs.
    /// </summary>
    public class GetEditorInventoryQueryHandler : IQueryHandler<GetEditorInventoryQuery, List<EditorInventoryItem>>
    {
        private readonly ApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetEditorInventoryQueryHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        public GetEditorInventoryQueryHandler(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        /// <inheritdoc/>
        public async Task<List<EditorInventoryItem>> HandleAsync(
            GetEditorInventoryQuery query,
            CancellationToken cancellationToken = default)
        {
            query ??= new GetEditorInventoryQuery();

            var activeStatusCode = (int)StatusCodeEnum.Active;

            var articleQuery = dbContext.Articles
                .AsNoTracking()
                .Where(a => a.StatusCode == activeStatusCode)
                .Select(a => new
                {
                    a.ArticleNumber,
                    a.ArticleType,
                    a.Title,
                    a.UrlPath,
                    a.BlogKey,
                    a.Published,
                    a.Updated,
                    a.VersionNumber,
                    a.Content,
                });

            if (query.PublishedOnly)
            {
                articleQuery = articleQuery.Where(a => a.Published != null);
            }

            if (query.ArticleType > 0)
            {
                articleQuery = articleQuery.Where(a => a.ArticleType == query.ArticleType);
            }

            var rows = await articleQuery.ToListAsync(cancellationToken);

            var lastPublishedByArticleNumber = rows
                .Where(r => r.Published.HasValue)
                .GroupBy(r => r.ArticleNumber)
                .ToDictionary(g => g.Key, g => g.Max(r => r.Published));

            var latestRows = rows
                .GroupBy(a => a.ArticleNumber)
                .Select(g => g.OrderByDescending(a => a.VersionNumber).First())
                .ToList();

            var model = BuildEditorInventory(latestRows.Select(s =>
            {
                var hasPublishedDate = lastPublishedByArticleNumber.TryGetValue(s.ArticleNumber, out var lastPublished)
                    && lastPublished.HasValue;

                return new EditorInventoryItem
                {
                    ArticleNumber = s.ArticleNumber,
                    ArticleType = s.ArticleType,
                    Title = s.Title,
                    BlogKey = s.BlogKey,
                    IsDefault = string.Equals(s.UrlPath, "root", StringComparison.OrdinalIgnoreCase),
                    LastPublished = hasPublishedDate
                        ? lastPublished?.UtcDateTime.ToString("o")
                        : null,
                    IsPublished = hasPublishedDate,
                    UrlPath = s.UrlPath,
                    Updated = s.Updated.UtcDateTime.ToString("o"),
                    HtmlEditorEnabled = HasEditableRegions(s.Content),
                    UsesHtmlEditor = HasEditableRegions(s.Content),
                };
            }));

            return FilterEditorInventoryByTerm(model, query.Term);
        }

        private static List<EditorInventoryItem> BuildEditorInventory(IEnumerable<EditorInventoryItem> rows)
        {
            var blogPostArticleType = (int)ArticleType.BlogPost;
            var blogStreamArticleType = (int)ArticleType.BlogStream;

            var normalizedRows = rows
                .Select(CloneAndNormalizeRow)
                .ToList();

            var blogStreams = normalizedRows
                .Where(r => r.ArticleType == blogStreamArticleType)
                .ToDictionary(r => r.BlogKey ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            var blogPostsByKey = normalizedRows
                .Where(r => r.ArticleType == blogPostArticleType && !string.IsNullOrWhiteSpace(r.BlogKey))
                .GroupBy(r => r.BlogKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(r => r.LastPublished ?? r.Updated)
                        .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var blogStream in blogStreams.Values)
            {
                blogStream.RowType = EditorInventoryRowType.Blog;

                if (blogPostsByKey.TryGetValue(blogStream.BlogKey, out var blogPosts))
                {
                    foreach (var blogPost in blogPosts)
                    {
                        blogPost.RowType = EditorInventoryRowType.BlogPost;
                        blogPost.PreviewUrlPath = CombineUrlPath(blogStream.UrlPath, blogPost.UrlPath);
                    }

                    blogStream.Children = blogPosts;
                    blogStream.ChildCount = blogPosts.Count;
                }
            }

            var topLevelRows = normalizedRows
                .Where(r => r.ArticleType != blogPostArticleType)
                .ToList();

            var orphanPosts = normalizedRows
                .Where(r => r.ArticleType == blogPostArticleType && !blogStreams.ContainsKey(r.BlogKey ?? string.Empty))
                .ToList();

            foreach (var orphanPost in orphanPosts)
            {
                orphanPost.RowType = EditorInventoryRowType.BlogPost;
            }

            topLevelRows.AddRange(orphanPosts);

            return topLevelRows
                .OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<EditorInventoryItem> FilterEditorInventoryByTerm(List<EditorInventoryItem> items, string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return items;
            }

            var filtered = new List<EditorInventoryItem>();

            foreach (var item in items)
            {
                var parentMatches = MatchesSearchTerm(item, term);
                var matchingChildren = item.Children
                    .Where(c => MatchesSearchTerm(c, term))
                    .Select(CloneAndNormalizeRow)
                    .ToList();

                if (!parentMatches && matchingChildren.Count == 0)
                {
                    continue;
                }

                var filteredItem = CloneAndNormalizeRow(item);
                filteredItem.RowType = item.RowType;

                if (item.Children.Count > 0)
                {
                    filteredItem.Children = parentMatches
                        ? item.Children.Select(CloneAndNormalizeRow).ToList()
                        : matchingChildren;

                    filteredItem.ChildCount = filteredItem.Children.Count;
                }

                filtered.Add(filteredItem);
            }

            return filtered;
        }

        private static bool MatchesSearchTerm(EditorInventoryItem item, string term)
        {
            return ContainsIgnoreCase(item.Title, term)
                || ContainsIgnoreCase(item.BlogKey, term)
                || ContainsIgnoreCase(item.UrlPath, term)
                || ContainsIgnoreCase(item.PreviewUrlPath, term);
        }

        private static bool ContainsIgnoreCase(string value, string term)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static EditorInventoryItem CloneAndNormalizeRow(EditorInventoryItem row)
        {
            var normalizedPath = NormalizeUrlPath(row.UrlPath);

            return new EditorInventoryItem
            {
                ArticleNumber = row.ArticleNumber,
                ArticleType = row.ArticleType,
                RowType = row.RowType,
                Title = row.Title,
                UrlPath = normalizedPath,
                PreviewUrlPath = normalizedPath,
                BlogKey = row.BlogKey ?? string.Empty,
                IsDefault = row.IsDefault,
                LastPublished = row.LastPublished,
                IsPublished = row.IsPublished,
                Updated = row.Updated,
                HtmlEditorEnabled = row.HtmlEditorEnabled,
                UsesHtmlEditor = row.UsesHtmlEditor || row.HtmlEditorEnabled,
                ChildCount = row.ChildCount,
                Children = new List<EditorInventoryItem>(),
            };
        }

        private static string NormalizeUrlPath(string urlPath)
        {
            if (string.IsNullOrWhiteSpace(urlPath))
            {
                return string.Empty;
            }

            return HttpUtility.UrlEncode(urlPath).Replace("%2f", "/");
        }

        private static string CombineUrlPath(string parentPath, string childPath)
        {
            var normalizedParent = NormalizeUrlPath(parentPath).Trim('/');
            var normalizedChild = NormalizeUrlPath(childPath).Trim('/');

            if (string.IsNullOrEmpty(normalizedParent))
            {
                return normalizedChild;
            }

            if (string.IsNullOrEmpty(normalizedChild))
            {
                return normalizedParent;
            }

            return $"{normalizedParent}/{normalizedChild}";
        }

        private static bool HasEditableRegions(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            var loweredContent = content.ToLowerInvariant();
            return loweredContent.Contains(" contenteditable=") || loweredContent.Contains(" data-ccms-ceid=");
        }
    }
}
