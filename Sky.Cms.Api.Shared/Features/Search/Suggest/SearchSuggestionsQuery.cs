// <copyright file="SearchSuggestionsQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Features.Search.Suggest;

using Cosmos.Common.Features.Shared;
using Sky.Cms.Api.Shared.Models.Search;

/// <summary>
/// Query for retrieving search suggestions.
/// </summary>
public class SearchSuggestionsQuery : IQuery<SearchSuggestionsApiResponse>
{
    /// <summary>
    /// Gets or sets the partial query text.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of suggestions to return.
    /// </summary>
    public int MaxResults { get; set; } = 10;
}