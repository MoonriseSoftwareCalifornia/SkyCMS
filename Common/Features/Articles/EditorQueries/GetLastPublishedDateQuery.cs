// <copyright file="GetLastPublishedDateQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using Cosmos.Common.Features.Shared;
using System;

/// <summary>
/// Query to retrieve the last published date for an article.
/// </summary>
public class GetLastPublishedDateQuery : IQuery<DateTimeOffset?>
{
    /// <summary>
    /// Gets or sets the article number.
    /// </summary>
    public int ArticleNumber { get; set; }
}
