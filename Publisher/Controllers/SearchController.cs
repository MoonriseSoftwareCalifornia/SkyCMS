// <copyright file="SearchController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Publisher.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services.Configurations;
using Cosmos.DynamicConfig;
using Sky.Cms.Api.Shared.Features.Search.Query;
using Sky.Cms.Api.Shared.Features.Search.Suggest;
using Sky.Cms.Api.Shared.Models.Search;

/// <summary>
/// Public search controller for website visitors.
/// </summary>
[Route("Search")]
[EnableRateLimiting("search-policy")]
public class SearchController : Controller
{
    private readonly IMediator mediator;
    private readonly ApplicationDbContext dbContext;
    private readonly IDynamicConfigurationProvider configurationProvider;
    private readonly ILogger<SearchController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchController"/> class.
    /// </summary>
    /// <param name="mediator">Mediator for CQRS commands and queries.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="configurationProvider">Configuration provider for tenant resolution.</param>
    /// <param name="logger">Logger instance.</param>
    public SearchController(
        IMediator mediator,
        ApplicationDbContext dbContext,
        IDynamicConfigurationProvider configurationProvider,
        ILogger<SearchController> logger)
    {
        this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Display the public search page.
    /// </summary>
    /// <param name="q">Search query parameter.</param>
    /// <param name="page">Page number.</param>
    /// <param name="size">Page size.</param>
    /// <returns>Search results view.</returns>
    [HttpGet]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "q", "page", "size" })]
    public async Task<IActionResult> Index(string? q = null, int page = 1, int size = 10)
    {
        try
        {
            var model = new PublicSearchViewModel
            {
                Query = q ?? string.Empty,
                Page = Math.Max(1, page),
                PageSize = Math.Min(Math.Max(5, size), 50) // Between 5 and 50
            };

            // Only perform search if there's a query
            if (!string.IsNullOrWhiteSpace(q))
            {
                var searchQuery = new SearchQuery
                {
                    Query = q,
                    PageNumber = model.Page,
                    PageSize = model.PageSize,
                    IncludeContent = true,
                    IncludeHighlights = true,
                    SortBy = "relevance"
                };

                model.Results = await mediator.QueryAsync(searchQuery);

                // Track search analytics (simplified)
                logger.LogInformation(
                    "Public search performed: Query='{Query}', Results={ResultCount}, Page={Page}",
                    q,
                    model.Results.TotalResults,
                    page);
            }

            return View(model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing public search for query: {Query}", q);
            TempData["ErrorMessage"] = "An error occurred while searching. Please try again.";
            return View(new PublicSearchViewModel { Query = q ?? string.Empty });
        }
    }

    /// <summary>
    /// API endpoint for search suggestions (for AJAX autocomplete).
    /// </summary>
    /// <param name="term">Partial search term.</param>
    /// <returns>JSON array of suggestions.</returns>
    [HttpGet("api/suggest")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "term" })] // Cache for 5 minutes
    public async Task<IActionResult> GetSuggestions(string term)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return Json(new { suggestions = Array.Empty<string>() });
            }

            var query = new SearchSuggestionsQuery
            {
                Query = term.Trim(),
                MaxResults = 8 // Fewer suggestions for public site
            };

            var result = await mediator.QueryAsync(query);
            return Json(new { suggestions = result.Suggestions });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting search suggestions for term: {Term}", term);
            return Json(new { suggestions = Array.Empty<string>() });
        }
    }

    /// <summary>
    /// API endpoint for live search results (AJAX).
    /// </summary>
    /// <param name="request">Search request.</param>
    /// <returns>JSON search results.</returns>
    [HttpPost("api/search")]
    [EnableRateLimiting("search-api-policy")]
    public async Task<IActionResult> SearchApi([FromBody] PublicSearchApiRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Query))
            {
                return BadRequest(new { error = "Search query is required" });
            }

            var query = new SearchQuery
            {
                Query = request.Query.Trim(),
                PageNumber = Math.Max(1, request.Page ?? 1),
                PageSize = Math.Min(Math.Max(5, request.PageSize ?? 10), 25), // Limit API results
                SortBy = request.SortBy ?? "relevance",
                IncludeContent = true,
                IncludeHighlights = true
            };

            var result = await mediator.QueryAsync(query);

            // Return simplified response for public API
            return Json(new
            {
                query = result.Query,
                totalResults = result.TotalResults,
                page = result.PageNumber,
                pageSize = result.PageSize,
                totalPages = result.TotalPages,
                results = result.Results.Select(r => new
                {
                    id = r.Id,
                    title = r.Title,
                    content = r.Content,
                    highlightedContent = r.HighlightedContent,
                    url = r.Url,
                    publishDate = r.PublishDate,
                    score = r.Score
                }),
                searchTimeMs = result.SearchTimeMs
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing API search for query: {Query}", request?.Query);
            return StatusCode(500, new { error = "An error occurred while searching" });
        }
    }

    /// <summary>
    /// Simple health check for public search.
    /// </summary>
    /// <returns>Health status.</returns>
    [HttpGet("health")]
    [ResponseCache(Duration = 60)]
    public IActionResult Health()
    {
        try
        {
            // Simple health check - just verify the service is responding
            return Json(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in search health check");
            return StatusCode(503, new { status = "unhealthy", timestamp = DateTime.UtcNow });
        }
    }
}

/// <summary>
/// View model for public search page.
/// </summary>
public class PublicSearchViewModel
{
    /// <summary>
    /// Gets or sets the search query.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the search results.
    /// </summary>
    public SearchApiResponse? Results { get; set; }

    /// <summary>
    /// Gets a value indicating whether there are search results to display.
    /// </summary>
    public bool HasResults => Results?.Results?.Any() == true;

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => Results?.TotalPages ?? 0;

    /// <summary>
    /// Gets the search summary text.
    /// </summary>
    public string SearchSummary
    {
        get
        {
            if (Results == null || string.IsNullOrWhiteSpace(Query))
                return string.Empty;

            return Results.TotalResults switch
            {
                0 => $"No results found for \"{Query}\"",
                1 => $"1 result found for \"{Query}\"",
                _ => $"{Results.TotalResults:N0} results found for \"{Query}\" ({Results.SearchTimeMs}ms)"
            };
        }
    }
}

/// <summary>
/// Request model for public search API.
/// </summary>
public class PublicSearchApiRequest
{
    /// <summary>
    /// Gets or sets the search query.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// Gets or sets the sort order.
    /// </summary>
    public string? SortBy { get; set; }
}