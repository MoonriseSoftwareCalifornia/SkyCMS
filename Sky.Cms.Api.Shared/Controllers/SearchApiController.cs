// <copyright file="SearchApiController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Cosmos.Common.Features.Shared;
using Sky.Cms.Api.Shared.Features.Search.Query;
using Sky.Cms.Api.Shared.Features.Search.Suggest;
using Sky.Cms.Api.Shared.Models.Search;

/// <summary>
/// API controller for handling search operations.
/// </summary>
[ApiController]
[Route("_api/search")]
[EnableRateLimiting("search-policy")]
public class SearchApiController : ControllerBase
{
    private readonly IMediator mediator;
    private readonly ILogger<SearchApiController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchApiController"/> class.
    /// </summary>
    /// <param name="mediator">Mediator for CQRS commands and queries.</param>
    /// <param name="logger">Logger instance.</param>
    public SearchApiController(
        IMediator mediator,
        ILogger<SearchApiController> logger)
    {
        this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Performs a search across the content for the current tenant.
    /// </summary>
    /// <param name="request">Search request containing query and filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results.</returns>
    [HttpGet]
    [ProducesResponseType<SearchApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchApiResponse>> SearchAsync(
        [FromQuery] SearchApiRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            this.logger.LogInformation(
                "Processing search request for query: {Query}, tenant: {TenantDomain}",
                request.Query,
                Request.Headers["x-origin-hostname"].FirstOrDefault() ?? Request.Host.Host);

            var query = new SearchQuery
            {
                Query = request.Query ?? string.Empty,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                ContentTypes = request.ContentTypes,
                DateFrom = request.DateFrom,
                DateTo = request.DateTo,
                SortBy = request.SortBy ?? "relevance",
                IncludeContent = request.IncludeContent,
                IncludeHighlights = request.IncludeHighlights
            };

            var result = await mediator.QueryAsync(query, cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid search request parameters");
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing search request");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Search Error",
                Detail = "An error occurred while processing your search request",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Gets search suggestions for autocomplete functionality.
    /// </summary>
    /// <param name="query">Partial query text.</param>
    /// <param name="maxResults">Maximum number of suggestions to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search suggestions.</returns>
    [HttpGet("suggest")]
    [ProducesResponseType<SearchSuggestionsApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SearchSuggestionsApiResponse>> GetSuggestionsAsync(
        [FromQuery] string query,
        [FromQuery] int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Ok(new SearchSuggestionsApiResponse { Suggestions = Array.Empty<string>() });
            }

            var suggestionQuery = new SearchSuggestionsQuery
            {
                Query = query.Trim(),
                MaxResults = Math.Min(maxResults, 50) // Cap at 50 suggestions
            };

            var result = await mediator.QueryAsync(suggestionQuery, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing search suggestions request");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Suggestions Error",
                Detail = "An error occurred while getting search suggestions",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Gets search health check status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check status.</returns>
    [HttpGet("health")]
    [ProducesResponseType<SearchHealthApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SearchHealthApiResponse>> GetHealthAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthQuery = new SearchHealthQuery();
            var result = await mediator.QueryAsync(healthQuery, cancellationToken);

            if (result.IsHealthy)
            {
                return Ok(result);
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking search service health");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new SearchHealthApiResponse
            {
                IsHealthy = false,
                StatusMessage = "Search service health check failed"
            });
        }
    }
}