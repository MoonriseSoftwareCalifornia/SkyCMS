// <copyright file="SearchSuggestionsQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Features.Search.Suggest;

using System.Diagnostics;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services.Configurations;
using Cosmos.DynamicConfig;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sky.Cms.Api.Shared.Models.Search;

/// <summary>
/// Handler for search suggestions queries.
/// </summary>
public class SearchSuggestionsQueryHandler : IQueryHandler<SearchSuggestionsQuery, SearchSuggestionsApiResponse>
{
    private readonly ApplicationDbContext dbContext;
    private readonly IDynamicConfigurationProvider configurationProvider;
    private readonly ILogger<SearchSuggestionsQueryHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchSuggestionsQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="configurationProvider">Configuration provider for tenant resolution.</param>
    /// <param name="logger">Logger.</param>
    public SearchSuggestionsQueryHandler(
        ApplicationDbContext dbContext,
        IDynamicConfigurationProvider configurationProvider,
        ILogger<SearchSuggestionsQueryHandler> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handle the search suggestions query.
    /// </summary>
    /// <param name="request">Search suggestions query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search suggestions.</returns>
    public async Task<SearchSuggestionsApiResponse> HandleAsync(SearchSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var tenantDomain = configurationProvider.GetTenantDomainNameFromRequest();
            
            logger.LogInformation(
                "Generating search suggestions for query '{Query}' for tenant '{TenantDomain}'",
                request.Query,
                tenantDomain);

            var suggestions = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.Query) && request.Query.Length >= 2)
            {
                var searchTerm = request.Query.ToLower();

                // Get title-based suggestions
                var titleSuggestions = await dbContext.Articles
                    .Where(a => a.StatusCode == 0)
                    .Where(a => a.Title.ToLower().Contains(searchTerm))
                    .Select(a => a.Title)
                    .Distinct()
                    .Take(request.MaxResults / 2)
                    .ToListAsync(cancellationToken);

                suggestions.AddRange(titleSuggestions);

                // Add some common search terms/keywords if we have space
                if (suggestions.Count < request.MaxResults)
                {
                    var keywordSuggestions = GenerateKeywordSuggestions(request.Query)
                        .Take(request.MaxResults - suggestions.Count);
                    suggestions.AddRange(keywordSuggestions);
                }

                // Remove duplicates and trim to max results
                suggestions = suggestions
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(request.MaxResults)
                    .ToList();
            }

            stopwatch.Stop();

            return new SearchSuggestionsApiResponse
            {
                Query = request.Query,
                Suggestions = suggestions.ToArray(),
                GenerationTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating search suggestions for query '{Query}'", request.Query);
            throw;
        }
    }

    private static IEnumerable<string> GenerateKeywordSuggestions(string query)
    {
        // Simple keyword expansion - in production this could be more sophisticated
        var commonKeywords = new Dictionary<string, string[]>
        {
            { "home", new[] { "homepage", "main page", "index" } },
            { "about", new[] { "about us", "company", "information" } },
            { "contact", new[] { "contact us", "get in touch", "support" } },
            { "news", new[] { "latest news", "articles", "updates" } },
            { "help", new[] { "support", "assistance", "faq" } },
            { "search", new[] { "find", "lookup", "locate" } }
        };

        var queryLower = query.ToLower();
        foreach (var kvp in commonKeywords)
        {
            if (kvp.Key.Contains(queryLower) || queryLower.Contains(kvp.Key))
            {
                foreach (var suggestion in kvp.Value)
                {
                    yield return suggestion;
                }
            }
        }
    }
}