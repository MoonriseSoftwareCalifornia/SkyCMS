// <copyright file="GetArticleFolderContentsQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System.Collections.Generic;
using Cosmos.BlobService;
using Cosmos.Common.Features.Shared;

/// <summary>
/// Query to get folder contents for an article.
/// Replaces CosmosUtilities.GetArticleFolderContents() method.
/// </summary>
/// <param name="articleNumber">Article number (not ID).</param>
/// <param name="path">Path to article folder (default is root).</param>
/// <remarks>Does NOT authenticate the user. Authorization must be performed separately.</remarks>
public record GetArticleFolderContentsQuery(
    int articleNumber,
    string path = ""): IQuery<List<FileManagerEntry>>;
