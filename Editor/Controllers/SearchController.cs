// <copyright file="SearchController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Controllers;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services.Search.Models;
using Cosmos.DynamicConfig;
using Sky.Cms.Api.Shared.Controllers;
using Sky.Cms.Api.Shared.Models.Search;
using Sky.Cms.Api.Shared.Features.Search.Query;
using Sky.Cms.Api.Shared.Features.Search.Suggest;

/// <summary>
/// Editor search controller for content management with admin-specific functionality.
/// </summary>
[Authorize]
public class SearchController : Controller
{
    private readonly Cosmos.Common.Features.Shared.IMediator mediator;
    private readonly ILogger<SearchController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchController"/> class.
    /// </summary>
    /// <param name="mediator">The mediator for dispatching queries.</param>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public SearchController(
        Cosmos.Common.Features.Shared.IMediator mediator,
        ILogger<SearchController> logger)
    {
        this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Display the search page with admin-specific features.
    /// </summary>
    /// <param name="query">Optional search query.</param>
    /// <param name="page">Page number.</param>
    /// <returns>Search view with admin interface.</returns>
    [HttpGet]
    public async Task<IActionResult> Index(string? query = null, int page = 1)
    {
        try
        {
            var model = new SearchPageViewModel
            {
                Query = query ?? string.Empty,
                Page = Math.Max(1, page),
                PageSize = 20,
                ShowDrafts = true, // Admin can see drafts
                ShowAdminOptions = true
            };

            if (!string.IsNullOrWhiteSpace(query))
            {
                // Create search query and execute via mediator
                var searchQuery = new Sky.Cms.Api.Shared.Features.Search.Query.SearchQuery
                {
                    Query = query,
                    PageNumber = page,
                    PageSize = 20,
                    SortBy = "relevance",
                    IncludeContent = true,
                    IncludeHighlights = true
                };

                var searchResult = await mediator.QueryAsync(searchQuery);
                model.Results = searchResult;
                model.HasResults = searchResult.Results?.Any() == true;
            }

            return View(model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing admin search for query: {Query}", query);
            return View(new SearchPageViewModel 
            { 
                Query = query ?? string.Empty, 
                ErrorMessage = "Search temporarily unavailable" 
            });
        }
    }

    /// <summary>
    /// Get search suggestions for the admin interface.
    /// </summary>
    /// <param name="term">The search term to get suggestions for.</param>
    /// <returns>JSON result with suggestions.</returns>
    [HttpGet]
    public async Task<IActionResult> Suggestions(string term)
    {
        try
        {
            // Create suggestions query and execute via mediator
            var suggestionsQuery = new Sky.Cms.Api.Shared.Features.Search.Suggest.SearchSuggestionsQuery
            {
                Query = term,
                MaxResults = 10
            };

            var result = await mediator.QueryAsync(suggestionsQuery);
            return Json(new { suggestions = result.Suggestions });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting admin search suggestions for term: {Term}", term);
            return Json(new { suggestions = Array.Empty<string>() });
        }
    }

    /// <summary>
    /// Perform an advanced search with admin-specific options.
    /// </summary>
    /// <param name="request">The advanced search request.</param>
    /// <returns>JSON result with search results.</returns>
    [HttpPost]
    public async Task<IActionResult> AdvancedSearch([FromBody] SearchRequest request)
    {
        try
        {
            // Create search query and execute via mediator
            var searchQuery = new Sky.Cms.Api.Shared.Features.Search.Query.SearchQuery
            {
                Query = request.Query,
                PageNumber = request.Page,
                PageSize = request.PageSize,
                SortBy = request.SortBy,
                IncludeContent = true,
                IncludeHighlights = true
            };

            var result = await mediator.QueryAsync(searchQuery);
            return Json(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing advanced admin search");
            return StatusCode(500, new { error = "Search failed" });
        }
    }

    /// <summary>
    /// Get search health status for admin dashboard.
    /// </summary>
    /// <returns>JSON result with health status.</returns>
    [HttpGet]
    public async Task<IActionResult> Health()
    {
        try
        {
            // Create health query and execute via mediator
            var healthQuery = new Sky.Cms.Api.Shared.Features.Search.Query.SearchHealthQuery();
            var result = await mediator.QueryAsync(healthQuery);
            return Json(new 
            { 
                status = result.IsHealthy ? "healthy" : "unhealthy",
                message = result.StatusMessage,
                version = result.Version,
                lastChecked = result.LastChecked,
                metrics = result.Metrics
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking search health");
            return Json(new { status = "unhealthy", message = "Health check failed" });
        }
    }
}

/// <summary>
/// View model for the admin search page.
/// </summary>
public class SearchPageViewModel
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
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets the search results.
    /// </summary>
    public SearchApiResponse? Results { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there are search results.
    /// </summary>
    public bool HasResults { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show drafts.
    /// </summary>
    public bool ShowDrafts { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to show admin options.
    /// </summary>
    public bool ShowAdminOptions { get; set; } = true;

    /// <summary>
    /// Gets or sets an error message if search fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => Results?.TotalPages ?? 1;

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;
}