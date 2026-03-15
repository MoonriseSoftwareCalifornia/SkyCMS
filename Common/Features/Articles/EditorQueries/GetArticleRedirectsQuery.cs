// <copyright file="GetArticleRedirectsQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using System.Collections.Generic;

/// <summary>
/// Query to retrieve all article redirects (articles with redirect status).
/// </summary>
public class GetArticleRedirectsQuery : IQuery<IEnumerable<RedirectItemViewModel>>
{
}
