// <copyright file="SearchQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Features.Search.Query;

using System.Diagnostics;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services.Configurations;
using Cosmos.DynamicConfig;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sky.Cms.Api.Shared.Models.Search;

/// <summary>
/// Handler for search queries.
/// </summary>
public class SearchQueryHandler : IQueryHandler<SearchQuery, SearchApiResponse>
{
    private readonly ApplicationDbContext dbContext;
    private readonly IDynamicConfigurationProvider configurationProvider;
    private readonly ArticleLogic articleLogic;
    private readonly ILogger<SearchQueryHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="configurationProvider">Configuration provider for tenant resolution.</param>
    /// <param name="articleLogic">Article logic for content operations.</param>
    /// <param name="logger">Logger.</param>
    public SearchQueryHandler(
        ApplicationDbContext dbContext,
        IDynamicConfigurationProvider configurationProvider,
        ArticleLogic articleLogic,
        ILogger<SearchQueryHandler> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        this.articleLogic = articleLogic ?? throw new ArgumentNullException(nameof(articleLogic));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handle the search query.
    /// </summary>
    /// <param name="request">Search query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results.</returns>
    public async Task<SearchApiResponse> HandleAsync(SearchQuery request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var tenantDomain = configurationProvider.GetTenantDomainNameFromRequest();
            
            logger.LogInformation(
                "Executing search query '{Query}' for tenant '{TenantDomain}'",
                request.Query,
                tenantDomain);

            // Build the base query
            // Note: Tenant isolation is handled at the database context level in SkyCMS
            var query = dbContext.Articles
                .Where(a => a.StatusCode == 0) // Published articles only
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                var searchTerm = request.Query.ToLower();
                query = query.Where(a => 
                    a.Title.ToLower().Contains(searchTerm) ||
                    a.Content.ToLower().Contains(searchTerm) ||
                    a.UrlPath.ToLower().Contains(searchTerm));
            }

            // Apply content type filter
            if (request.ContentTypes?.Length > 0)
            {
                query = query.Where(a => request.ContentTypes.Contains(a.ArticleNumber.ToString()) ||
                                        request.ContentTypes.Any(ct => a.Title.ToLower().Contains(ct.ToLower())));
            }

            // Apply date filters
            if (request.DateFrom.HasValue)
            {
                query = query.Where(a => a.Published >= request.DateFrom.Value);
            }

            if (request.DateTo.HasValue)
            {
                query = query.Where(a => a.Published <= request.DateTo.Value);
            }

            // Apply sorting
            query = request.SortBy?.ToLower() switch
            {
                "date" => query.OrderByDescending(a => a.Published),
                "title" => query.OrderBy(a => a.Title),
                "popularity" => query.OrderByDescending(a => a.VersionNumber), // Using version as proxy for popularity
                _ => query.OrderByDescending(a => a.Updated) // Default relevance by update time
            };

            // Get total count for pagination
            var totalResults = await query.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalResults / request.PageSize);

            // Apply pagination
            var pagedQuery = query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize);

            // Execute query
            var articles = await pagedQuery.ToListAsync(cancellationToken);

            // Map to response format
            var results = articles.Select(article => new SearchResultItem
            {
                Id = article.Id.ToString(),
                Title = article.Title,
                Content = request.IncludeContent ? TruncateContent(article.Content, 200) : string.Empty,
                HighlightedContent = request.IncludeHighlights ? 
                    HighlightSearchTerms(TruncateContent(article.Content, 200), request.Query) : 
                    string.Empty,
                Url = $"/{article.UrlPath}",
                ContentType = "Article",
                PublishDate = article.Published?.DateTime,
                LastModified = article.Updated.DateTime,
                Author = "System", // Could be enhanced to track actual authors
                Score = CalculateRelevanceScore(article, request.Query),
                Metadata = new Dictionary<string, string>
                {
                    ["ArticleNumber"] = article.ArticleNumber.ToString(),
                    ["VersionNumber"] = article.VersionNumber.ToString(),
                    ["StatusCode"] = article.StatusCode.ToString()
                }
            }).ToList();

            stopwatch.Stop();

            return new SearchApiResponse
            {
                Query = request.Query,
                TotalResults = totalResults,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalPages = totalPages,
                Results = results,
                SearchTimeMs = stopwatch.ElapsedMilliseconds,
                Facets = await BuildFacetsAsync(cancellationToken)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing search query '{Query}'", request.Query);
            throw;
        }
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            return content;

        var truncated = content.Substring(0, maxLength);
        var lastSpace = truncated.LastIndexOf(' ');
        
        return lastSpace > 0 ? truncated.Substring(0, lastSpace) + "..." : truncated + "...";
    }

    private static string HighlightSearchTerms(string content, string searchQuery)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(searchQuery))
            return content;

        var terms = searchQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = content;

        foreach (var term in terms)
        {
            if (term.Length > 2) // Only highlight terms longer than 2 characters
            {
                result = System.Text.RegularExpressions.Regex.Replace(
                    result,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(term)}\b",
                    $"<mark>{term}</mark>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
        }

        return result;
    }

    private static float CalculateRelevanceScore(Cosmos.Common.Data.Article article, string searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return 1.0f;

        var score = 0.0f;
        var query = searchQuery.ToLower();

        // Title matches get higher score
        if (article.Title.ToLower().Contains(query))
            score += 3.0f;

        // Content matches
        if (article.Content.ToLower().Contains(query))
            score += 1.0f;

        // URL path matches
        if (article.UrlPath.ToLower().Contains(query))
            score += 2.0f;

        // Boost recently updated articles
        var daysSinceUpdate = (DateTime.UtcNow - article.Updated).TotalDays;
        if (daysSinceUpdate < 30)
            score += 0.5f;

        return Math.Max(score, 0.1f); // Ensure minimum score
    }

    private async Task<Dictionary<string, List<FacetItem>>> BuildFacetsAsync(
        CancellationToken cancellationToken)
    {
        var facets = new Dictionary<string, List<FacetItem>>();

        try
        {
            // Content type facets (simplified)
            facets["ContentType"] = new List<FacetItem>
            {
                new() { Value = "Article", Count = await dbContext.Articles
                    .Where(a => a.StatusCode == 0)
                    .CountAsync(cancellationToken) }
            };

            // Date facets
            var now = DateTime.UtcNow;
            var articlesQuery = dbContext.Articles
                .Where(a => a.StatusCode == 0);

            facets["DateRange"] = new List<FacetItem>
            {
                new() { 
                    Value = "Last Week", 
                    Count = await articlesQuery.Where(a => a.Published >= now.AddDays(-7)).CountAsync(cancellationToken) 
                },
                new() { 
                    Value = "Last Month", 
                    Count = await articlesQuery.Where(a => a.Published >= now.AddDays(-30)).CountAsync(cancellationToken) 
                },
                new() { 
                    Value = "Last Year", 
                    Count = await articlesQuery.Where(a => a.Published >= now.AddDays(-365)).CountAsync(cancellationToken) 
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error building search facets");
            // Return empty facets on error
        }

        return facets;
    }
}