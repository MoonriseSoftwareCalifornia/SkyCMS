// <copyright file="GetArticleByArticleNumberQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Query to retrieve an article by article number and optional version.
/// </summary>
public class GetArticleByArticleNumberQuery : IQuery<ArticleViewModel?>
{
    /// <summary>
    /// Gets or sets the article number.
    /// </summary>
    public int ArticleNumber { get; set; }

    /// <summary>
    /// Gets or sets the optional version number.
    /// </summary>
    public int? VersionNumber { get; set; }
}
