// <copyright file="SearchPublishedArticlesQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using System.Collections.Generic;

/// <summary>
/// Query to search published articles by text.
/// </summary>
public class SearchPublishedArticlesQuery : IQuery<List<TableOfContentsItem>>
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
