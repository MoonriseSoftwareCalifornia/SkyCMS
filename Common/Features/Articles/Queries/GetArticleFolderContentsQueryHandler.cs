// <copyright file="GetArticleFolderContentsQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.BlobService;
using Cosmos.Common.Features.Shared;

/// <summary>
/// Handler for retrieving folder contents for an article from storage.
/// </summary>
/// <param name="storageContext">File storage context for accessing article files.</param>
public class GetArticleFolderContentsQueryHandler(IStorageContext storageContext): IQueryHandler<GetArticleFolderContentsQuery, List<FileManagerEntry>>
{
    private readonly IStorageContext storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));

    /// <inheritdoc />
    public async Task<List<FileManagerEntry>> HandleAsync(GetArticleFolderContentsQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var path = $"/pub/articles/{query.articleNumber}/{query.path.TrimStart('/')}";

        var contents = await storageContext.GetFilesAndDirectories(path);

        return contents;
    }
}
