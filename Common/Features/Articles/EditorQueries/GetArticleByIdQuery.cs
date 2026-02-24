// <copyright file="GetArticleByIdQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using System;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Query to retrieve an article by row ID for editor usage.
/// </summary>
public class GetArticleByIdQuery : IQuery<ArticleViewModel?>
{
    /// <summary>
    /// Gets or sets the article row ID.
    /// </summary>
    public Guid Id { get; set; }
}
