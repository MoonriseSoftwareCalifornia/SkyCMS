// <copyright file="GetTableOfContentsQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Query to retrieve a table of contents listing.
/// </summary>
public class GetTableOfContentsQuery : IQuery<TableOfContents>
{
    /// <summary>
    /// Gets or sets the page prefix.
    /// </summary>
    public string Page { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    public int PageNo { get; set; } = 0;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether to order by published date.
    /// </summary>
    public bool OrderByPublishedDate { get; set; }
}
