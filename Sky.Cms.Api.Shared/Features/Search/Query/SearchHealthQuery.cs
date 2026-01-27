// <copyright file="SearchHealthQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Features.Search.Query;

using Cosmos.Common.Features.Shared;
using Sky.Cms.Api.Shared.Models.Search;

/// <summary>
/// Query for checking search service health.
/// </summary>
public class SearchHealthQuery : IQuery<SearchHealthApiResponse>
{
    /// <summary>
    /// Gets or sets the expected result type.
    /// </summary>
    public SearchHealthApiResponse Result { get; set; }
}