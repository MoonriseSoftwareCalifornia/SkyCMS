// <copyright file="PubControllerBase.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Publisher.Controllers
{
    using Cosmos.BlobService;
    using Cosmos.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Services.Caching;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using System;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Secure file access controller and proxy.
    /// </summary>
    public class PubControllerBase : Controller
    {
        private readonly IMediator mediator;
        private readonly ApplicationDbContext dbContext;
        private readonly IStorageContext storageContext;
        private readonly bool requiresAuthentication;
        private readonly ILogger<PubControllerBase> logger;
        private readonly ICacheService<CachedFile> cacheService;
        private readonly ICacheKeyProvider cacheKeyProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PubControllerBase"/> class.
        /// Constructor.
        /// </summary>
        /// <param name="mediator">Mediator for CQRS queries.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="requiresAuthentication">Indicates if authentication is required for the publisher.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="cacheService">Cache service for file caching.</param>
        /// <param name="cacheKeyProvider">Cache key provider.</param>
        public PubControllerBase(
            IMediator mediator,
            ApplicationDbContext dbContext,
            IStorageContext storageContext,
            bool requiresAuthentication,
            ILogger<PubControllerBase> logger,
            ICacheService<CachedFile> cacheService,
            ICacheKeyProvider cacheKeyProvider)
        {
            this.mediator = mediator;
            this.requiresAuthentication = requiresAuthentication;
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));
            this.logger = logger;
            this.cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            this.cacheKeyProvider = cacheKeyProvider ?? throw new ArgumentNullException(nameof(cacheKeyProvider));
        }

        /// <summary>
        /// Gets a file and validates user authentication.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public virtual async Task<IActionResult> Index()
        {
            var path = this.HttpContext.Request.Path.ToString();

            // Handle authentication and set cache headers
            this.SetCacheHeaders(path);

            if (this.requiresAuthentication)
            {
                var authResult = await this.AuthorizeRequestAsync(path);
                if (authResult != null)
                {
                    return authResult;
                }
            }

            try
            {
                return await this.ServeFileAsync(path);
            }
            catch (FileNotFoundException ex)
            {
                this.logger?.LogWarning(ex, "File not found: {Path}", path);
                return this.NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                this.logger?.LogWarning(ex, "Unauthorized access to file: {Path}", path);
                return this.Unauthorized();
            }
            catch (Exception ex)
            {
                this.logger?.LogError(ex, "Unexpected error serving file {Path}", path);
                return this.NotFound();
            }
        }

        /// <summary>
        /// Sets appropriate cache headers based on authentication status.
        /// </summary>
        /// <param name="path">The request path.</param>
        private void SetCacheHeaders(string path)
        {
            if (this.requiresAuthentication)
            {
                this.Response.Headers.CacheControl = "private, no-cache, no-store, must-revalidate";
                this.Response.Headers.Expires = DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'");
            }
            else
            {
                // Public files could be cached
                this.Response.Headers.CacheControl = "public, max-age=3600";
            }
        }

        /// <summary>
        /// Authorizes the request asynchronously if authentication is required.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <returns>An unauthorized result if authorization fails; otherwise null.</returns>
        private async Task<IActionResult> AuthorizeRequestAsync(string path)
        {
            // If the user is not logged in, have them login first.
            if (this.User?.Identity?.IsAuthenticated != true)
            {
                this.logger?.LogWarning("Unauthorized access attempt to {Path} - User not authenticated", path);
                return this.Unauthorized();
            }

            // See if the article is in protected storage.
            if (path.StartsWith("/pub/articles/", StringComparison.OrdinalIgnoreCase))
            {
                var pathParts = path.TrimStart('/').Split('/');
                if (pathParts.Length > 2 && int.TryParse(pathParts[2], out var articleNumber))
                {
                    if (!await this.mediator.QueryAsync(new AuthorizeUserForArticleQuery(this.User, articleNumber)))
                    {
                        this.logger?.LogWarning(
                            "Unauthorized access attempt to {Path} - User {UserName} not authorized for article {ArticleNumber}",
                            path,
                            this.User?.Identity?.Name,
                            articleNumber);
                        return this.Unauthorized();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Serves a file from blob storage with caching.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>A file action result.</returns>
        private async Task<IActionResult> ServeFileAsync(string path)
        {
            var cacheKey = this.cacheKeyProvider.GenerateFileKey(this.HttpContext.Request.Host.Host, path);

            // Try cache first
            if (this.cacheService.TryGet(cacheKey, out var cachedFile))
            {
                return this.CreateFileResult(cachedFile);
            }

            // Get file from storage
            var properties = await this.storageContext.GetFileAsync(path);
            if (properties == null)
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            var fileStream = await this.storageContext.GetStreamAsync(path);
            var contentType = properties.ContentType ?? Utilities.GetContentType(properties.Name);

            // Read file to byte array
            byte[] fileData;
            using (var memoryStream = new MemoryStream())
            {
                await fileStream.CopyToAsync(memoryStream);
                fileData = memoryStream.ToArray();
            }

            // Create cache entry
            var cachedFileEntry = new CachedFile
            {
                Data = fileData,
                Metadata = properties,
                ETag = PubControllerBase.CreateETag(properties.ETag)
            };

            // Cache with sliding expiration
            this.cacheService.Set(cacheKey, cachedFileEntry, null, TimeSpan.FromMinutes(4));

            return this.CreateFileResult(cachedFileEntry);
        }

        /// <summary>
        /// Creates a file action result from a cached file entry.
        /// </summary>
        /// <param name="cachedFile">The cached file entry.</param>
        /// <returns>A file action result.</returns>
        private IActionResult CreateFileResult(CachedFile cachedFile)
        {
            return this.File(
                fileContents: cachedFile.Data,
                contentType: cachedFile.Metadata.ContentType,
                lastModified: cachedFile.Metadata.ModifiedUtc,
                entityTag: cachedFile.ETag);
        }

        /// <summary>
        /// Creates an ETag header value from the storage ETag.
        /// </summary>
        /// <param name="etag">The storage ETag string.</param>
        /// <returns>An EntityTagHeaderValue for HTTP responses.</returns>
        private static Microsoft.Net.Http.Headers.EntityTagHeaderValue CreateETag(string etag)
        {
            if (string.IsNullOrWhiteSpace(etag))
            {
                // Generate a weak ETag if none provided
                return new Microsoft.Net.Http.Headers.EntityTagHeaderValue("\"default\"", isWeak: true);
            }

            // Ensure the ETag is properly quoted
            var quotedETag = etag.Trim();
            if (!quotedETag.StartsWith("\""))
            {
                quotedETag = $"\"{quotedETag}\"";
            }

            try
            {
                return new Microsoft.Net.Http.Headers.EntityTagHeaderValue(quotedETag);
            }
            catch (FormatException ex)
            {
                // If the ETag is still invalid, create a weak ETag from hash
                var validETag = $"\"{Math.Abs(etag.GetHashCode())}\"";
                return new Microsoft.Net.Http.Headers.EntityTagHeaderValue(validETag, isWeak: true);
            }
        }

        /// <summary>
        /// Represents a cached file with metadata.
        /// </summary>
        public class CachedFile
        {
            /// <summary>
            /// Gets or sets the file data.
            /// </summary>
            public byte[] Data { get; set; }

            /// <summary>
            /// Gets or sets the file metadata.
            /// </summary>
            public FileManagerEntry Metadata { get; set; }

            /// <summary>
            /// Gets or sets the ETag for cache validation.
            /// </summary>
            public Microsoft.Net.Http.Headers.EntityTagHeaderValue ETag { get; set; }
        }
    }
}
