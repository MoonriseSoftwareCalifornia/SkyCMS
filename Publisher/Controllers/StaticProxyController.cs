// <copyright file="StaticProxyController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Publisher.Controllers;

using Cosmos.BlobService;
using Cosmos.Cms.Common;
using Cosmos.Common.Data;
using Cosmos.Common.Services.Caching;
using Cosmos.Publisher.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;

/// <summary>
/// Static proxy controller with SPA fallback routing support.
/// </summary>
/// <remarks>
/// Serves static files from blob storage and provides fallback routing
/// for Single Page Applications (SPAs) to handle client-side routing.
/// </remarks>
public class StaticProxyController : Controller
{
    private readonly IStorageContext storageContext;
    private readonly ICacheService<FileCacheObject> cacheService;
    private readonly ICacheService<bool> spaCacheService;
    private readonly ICacheKeyProvider cacheKeyProvider;
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<StaticProxyController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticProxyController"/> class.
    /// </summary>
    /// <param name="storageContext">The storage context used to manage and access data.</param>
    /// <param name="cacheService">Cache service for file caching.</param>
    /// <param name="spaCacheService">Cache service for SPA detection results.</param>
    /// <param name="cacheKeyProvider">Cache key provider.</param>
    /// <param name="dbContext">Database context for querying published pages.</param>
    /// <param name="logger">Logger instance.</param>
    public StaticProxyController(
        IStorageContext storageContext,
        ICacheService<FileCacheObject> cacheService,
        ICacheService<bool> spaCacheService,
        ICacheKeyProvider cacheKeyProvider,
        ApplicationDbContext dbContext,
        ILogger<StaticProxyController> logger)
    {
        this.storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));
        this.cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        this.spaCacheService = spaCacheService ?? throw new ArgumentNullException(nameof(spaCacheService));
        this.cacheKeyProvider = cacheKeyProvider ?? throw new ArgumentNullException(nameof(cacheKeyProvider));
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves and serves a static file based on the request path.
    /// Supports SPA fallback routing for ArticleType.SpaApp.
    /// </summary>
    /// <returns>Returns a file or content result.</returns>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        string path = string.IsNullOrWhiteSpace(this.HttpContext.Request.Path) || this.HttpContext.Request.Path == "/"
            ? "index.html"
            : this.HttpContext.Request.Path.ToString();

        try
        {
            // Try to serve the exact requested file
            var fileResult = await this.TryServeFileAsync(path);
            if (fileResult != null)
            {
                return fileResult;
            }

            // File not found - check if this is a SPA route
            var spaIndexPath = await this.GetSpaFallbackPathAsync(path);
            if (spaIndexPath != null)
            {
                var spaResult = await this.TryServeFileAsync(spaIndexPath);
                if (spaResult != null)
                {
                    return spaResult;
                }
            }

            this.logger.LogWarning("File not found: {Path}", path);
            return this.NotFound();
        }
        catch (FileNotFoundException ex)
        {
            this.logger.LogWarning(ex, "File not found: {Path}", path);
            return this.NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            this.logger.LogWarning(ex, "Unauthorized access to file: {Path}", path);
            return this.StatusCode((int)HttpStatusCode.Forbidden);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Unexpected error serving file {Path}", path);
            return this.StatusCode((int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Attempts to serve a file from blob storage at the specified path.
    /// </summary>
    /// <param name="path">The file path to retrieve.</param>
    /// <returns>An IActionResult if the file exists, null otherwise.</returns>
    private async Task<IActionResult> TryServeFileAsync(string path)
    {
        // Check cache first
        if (this.cacheService.TryGet(path, out var fileCacheObject))
        {
            return this.CreateFileResult(fileCacheObject);
        }

        try
        {
            // Check if file exists in blob storage
            var properties = await this.storageContext.GetFileAsync(path);
            if (properties == null)
            {
                return null;
            }

            // Load file from blob storage
            fileCacheObject = new FileCacheObject(properties);

            using (var fileStream = await this.storageContext.GetStreamAsync(path))
            {
                using (var ms = new MemoryStream())
                {
                    await fileStream.CopyToAsync(ms);
                    fileCacheObject.FileData = ms.ToArray();
                }
            }

            // Cache the file (use shorter cache for index.html to allow updates)
            var cacheExpiration = path.EndsWith("index.html", StringComparison.OrdinalIgnoreCase)
                ? TimeSpan.FromSeconds(10)
                : TimeSpan.FromMinutes(5);

            this.cacheService.Set(path, fileCacheObject, cacheExpiration);

            return this.CreateFileResult(fileCacheObject);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error loading file from storage: {Path}", path);
            throw;
        }
    }

    /// <summary>
    /// Creates an IActionResult from a cached file object.
    /// </summary>
    /// <param name="fileCacheObject">The cached file object.</param>
    /// <returns>An IActionResult containing the file content.</returns>
    private IActionResult CreateFileResult(FileCacheObject fileCacheObject)
    {
        if (StaticProxyController.IsTextContent(fileCacheObject.ContentType))
        {
            // Convert byte[] to string for text content
            return this.Content(System.Text.Encoding.UTF8.GetString(fileCacheObject.FileData), fileCacheObject.ContentType);
        }

        var contentType = Utilities.GetContentType(fileCacheObject.Name);

        return this.File(
            fileStream: new MemoryStream(fileCacheObject.FileData),
            contentType: contentType,
            lastModified: fileCacheObject.ModifiedUtc,
            entityTag: null);
    }

    /// <summary>
    /// Determines if the requested path belongs to a SPA and returns the index.html path.
    /// </summary>
    /// <param name="requestedPath">The requested path.</param>
    /// <returns>The path to index.html if this is a SPA route, null otherwise.</returns>
    private async Task<string> GetSpaFallbackPathAsync(string requestedPath)
    {
        // Normalize the path
        requestedPath = requestedPath.TrimStart('/');

        // Extract the potential article URL (first segment of the path)
        var segments = requestedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        // Check if this could be a SPA route by examining segments
        // We need to check multiple potential article URLs as the path could be nested
        for (int i = segments.Length; i > 0; i--)
        {
            var potentialArticleUrl = "/" + string.Join("/", segments.Take(i));

            // Check cache first
            var cacheKey = this.cacheKeyProvider.GenerateSpaCheckKey(potentialArticleUrl);
            if (this.spaCacheService.TryGet(cacheKey, out bool isSpa))
            {
                if (isSpa)
                {
                    return potentialArticleUrl.TrimStart('/') + "/index.html";
                }

                continue;
            }

            try
            {
                // Query database to check if this URL is a SPA article
                var publishedPage = await this.dbContext.Pages
                    .Where(p => p.UrlPath == potentialArticleUrl && p.ArticleType == (int)ArticleType.SpaApp)
                    .Select(p => new { p.UrlPath, p.ArticleType })
                    .FirstOrDefaultAsync();

                // Cache the result (cache both positive and negative results)
                var foundSpa = publishedPage != null;
                this.spaCacheService.Set(cacheKey, foundSpa, TimeSpan.FromMinutes(5));

                if (foundSpa)
                {
                    return potentialArticleUrl.TrimStart('/') + "/index.html";
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error checking SPA status for article URL: {ArticleUrl}", potentialArticleUrl);

                // Continue to next iteration instead of failing
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines if a content type is text-based.
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns>True if the content type is text-based; otherwise false.</returns>
    private static bool IsTextContent(string contentType)
    {
        var textTypes = new[]
        {
            MediaTypeNames.Text.Plain,
            MediaTypeNames.Text.Html,
            MediaTypeNames.Text.Xml,
            MediaTypeNames.Application.Json,
            "application/javascript",
            "application/xml",
            "text/css",
            "image/svg+xml",
        };

        return textTypes.Contains(contentType);
    }
}
