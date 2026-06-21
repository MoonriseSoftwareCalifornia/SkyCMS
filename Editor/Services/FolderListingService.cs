// <copyright file="FolderListingService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Sky.Editor.Models;

    /// <summary>
    /// Default implementation of <see cref="IFolderListingService"/>.
    /// Resolves entries for catalog-backed paths (/pub/articles, /pub/templates)
    /// and falls back to real blob-storage listings for all other paths.
    /// Deleted-article entries are filtered out from article sub-folders.
    /// </summary>
    public class FolderListingService : IFolderListingService
    {
        private const string ArticlesRoot = "pub/articles";
        private const string TemplatesRoot = "pub/templates";

        private readonly IApplicationDbContext dbContext;
        private readonly IStorageContext storageContext;
        private readonly IFileEntryTitleService titleResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="FolderListingService"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="storageContext">Blob storage context.</param>
        /// <param name="titleResolver">Article / template title resolver.</param>
        public FolderListingService(
            IApplicationDbContext dbContext,
            IStorageContext storageContext,
            IFileEntryTitleService titleResolver)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));
            this.titleResolver = titleResolver ?? throw new ArgumentNullException(nameof(titleResolver));
        }

        /// <inheritdoc/>
        public async Task<List<FileManagerEntry>> GetEntriesAsync(
            string path,
            string tenantDomain)
        {
            var normalised = FileEntryPathHelper.NormalizePath(path ?? "/");
            var trimmed = normalised.Trim('/');

            // ── /pub/articles root: virtual list from ArticleCatalog ──────────────
            if (string.Equals(trimmed, ArticlesRoot, StringComparison.OrdinalIgnoreCase))
            {
                // Filter: only include articles that are NOT deleted or redirected.
                // StatusCode 2 = Deleted, StatusCode 3 = Redirect (deprecated entry).
                // Keep Active (0) and Inactive (1).
                var deletedStatusCode = (int)Cosmos.Common.Data.Logic.StatusCodeEnum.Deleted;
                var redirectStatusCode = (int)Cosmos.Common.Data.Logic.StatusCodeEnum.Redirect;

                var raw = await this.dbContext.ArticleCatalog
                    .Select(s => new { s.ArticleNumber, s.Title, s.Updated, s.StatusCode, s.Status })
                    .ToListAsync();

                return raw
                    .Where(s => !IsDeletedOrRedirected(s.StatusCode ?? 0, s.Status, deletedStatusCode, redirectStatusCode))
                    .Select(s => new FileManagerEntry
                {
                    Created = s.Updated.DateTime,
                    CreatedUtc = s.Updated.UtcDateTime,
                    Extension = string.Empty,
                    HasDirectories = true,
                    IsDirectory = true,
                    Modified = s.Updated.DateTime,
                    ModifiedUtc = s.Updated.UtcDateTime,
                    Name = s.Title,
                    Path = "/pub/articles/" + s.ArticleNumber,
                    DisplayPath = "/pub/articles/" + s.Title,
                    Size = 0,
                }).ToList();
            }

            // ── /pub/templates root: virtual list from Templates ──────────────────
            if (string.Equals(trimmed, TemplatesRoot, StringComparison.OrdinalIgnoreCase))
            {
                var raw = await this.dbContext.Templates
                    .Select(s => new { s.Id, s.Title })
                    .ToListAsync();

                var now = DateTimeOffset.UtcNow.DateTime;
                return raw.Select(s => new FileManagerEntry
                {
                    Created = now,
                    CreatedUtc = now,
                    Extension = string.Empty,
                    HasDirectories = true,
                    IsDirectory = true,
                    Modified = now,
                    ModifiedUtc = now,
                    Name = s.Title,
                    Path = "/pub/templates/" + s.Id,
                    DisplayPath = "/pub/templates/" + s.Title,
                    Size = 0,
                }).ToList();
            }

            // ── All other paths: real blob-storage listing ────────────────────────
            var entries = await this.storageContext.GetFilesAndDirectories(normalised);

            // Filter soft-deleted article entries from any sub-folder of /pub/articles.
            if (trimmed.StartsWith(ArticlesRoot, StringComparison.OrdinalIgnoreCase))
            {
                await this.titleResolver.FilterDeletedArticleEntriesAsync(entries, tenantDomain);
            }

            return entries;
        }

        private static bool IsDeletedOrRedirected(int statusCode, string? status, int deletedStatusCode, int redirectStatusCode)
        {
            if (statusCode == deletedStatusCode || statusCode == redirectStatusCode)
            {
                return true;
            }

            return string.Equals(status, "Deleted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Redirect", StringComparison.OrdinalIgnoreCase);
        }
    }
}
