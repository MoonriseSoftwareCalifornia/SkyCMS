// <copyright file="FacetItem.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Search.Models;

/// <summary>
/// Represents a facet value and its count for filtering.
/// </summary>
public class FacetItem
{
    /// <summary>
    /// Gets or sets the facet value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the count of items with this facet value.
    /// </summary>
    public int Count { get; set; }
}