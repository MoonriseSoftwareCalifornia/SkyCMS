// <copyright file="StaticProxyController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Publisher.Controllers;

using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Cosmos.BlobService;
using Cosmos.Cms.Common;
using Cosmos.Common.Data;
using Cosmos.Publisher.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Static proxy controller with SPA fallback routing support.
/// </summary>
/// <remarks>
/// Serves static files from blob storage and provides fallback routing
/// for Single Page Applications (SPAs) to handle client-side routing.
/// </remarks>
public class StaticProxyController : Controller
{
    private readonly StorageContext storageContext;
    private readonly IMemoryCache memoryCache;
    private readonly ApplicationDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticProxyController"/> class.
    /// </summary>
    /// <param name="storageContext">The storage context used to manage and access data.</param>
    /// <param name="memoryCache">Memory cache.</param>
    /// <param name="dbContext">Database context for querying published pages.</param>
    public StaticProxyController(
        StorageContext storageContext,
        IMemoryCache memoryCache,
        ApplicationDbContext dbContext)
    {
        this.storageContext = storageContext;
        this.memoryCache = memoryCache;
        this.dbContext = dbContext;
    }

    /// <summary>
    ///  Retrieves and serves a static file based on the request path.
    ///  Supports SPA fallback routing for ArticleType.SpaApp.
    /// </summary>
    /// <returns>Returns a file or content result.</returns>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        string path = string.IsNullOrWhiteSpace(HttpContext.Request.Path) || HttpContext.Request.Path == "/"
            ? "index.html"
            : HttpContext.Request.Path;

        try
        {
            // Try to serve the exact requested file
            var fileResult = await TryServeFileAsync(path);
            if (fileResult != null)
            {
                return fileResult;
            }

            // File not found - check if this is a SPA route
            var spaIndexPath = await GetSpaFallbackPathAsync(path);
            if (spaIndexPath != null)
            {
                var spaResult = await TryServeFileAsync(spaIndexPath);
                if (spaResult != null)
                {
                    return spaResult;
                }
            }

            return NotFound();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return NotFound();
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
        if (memoryCache.TryGetValue(path, out FileCacheObject fileCacheObject))
        {
            return CreateFileResult(fileCacheObject);
        }

        // Check if file exists in blob storage
        var properties = await storageContext.GetFileAsync(path);
        if (properties == null)
        {
            return null;
        }

        // Load file from blob storage
        fileCacheObject = new FileCacheObject(properties);

        using var fileStream = await storageContext.GetStreamAsync(path);
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        fileCacheObject.FileData = ms.ToArray();

        // Cache the file (use shorter cache for index.html to allow updates)
        var cacheExpiration = path.EndsWith("index.html", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromSeconds(10)
            : TimeSpan.FromMinutes(5);

        memoryCache.Set(path, fileCacheObject, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(cacheExpiration));

        return CreateFileResult(fileCacheObject);
    }

    /// <summary>
    /// Creates an IActionResult from a cached file object.
    /// </summary>
    /// <param name="fileCacheObject">The cached file object.</param>
    /// <returns>An IActionResult containing the file content.</returns>
    private IActionResult CreateFileResult(FileCacheObject fileCacheObject)
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

        if (textTypes.Contains(fileCacheObject.ContentType))
        {
            // Convert byte[] to string for text content
            return Content(System.Text.Encoding.UTF8.GetString(fileCacheObject.FileData), fileCacheObject.ContentType);
        }

        var contentType = Utilities.GetContentType(fileCacheObject.Name);

        return File(
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

        // Check if this could be a SPA route by examining the first segment
        // We need to check multiple potential article URLs as the path could be nested
        for (int i = segments.Length; i > 0; i--)
        {
            var potentialArticleUrl = "/" + string.Join("/", segments.Take(i));

            // Check cache first
            var cacheKey = $"SPA_CHECK_{potentialArticleUrl}";
            if (memoryCache.TryGetValue(cacheKey, out bool isSpa))
            {
                if (isSpa)
                {
                    return potentialArticleUrl.TrimStart('/') + "/index.html";
                }

                continue;
            }

            // Query database to check if this URL is a SPA article
            var publishedPage = await dbContext.Pages
                .Where(p => p.UrlPath == potentialArticleUrl && p.ArticleType == (int)ArticleType.SpaApp)
                .Select(p => new { p.UrlPath, p.ArticleType })
                .FirstOrDefaultAsync();

            // Cache the result (cache both positive and negative results)
            var foundSpa = publishedPage != null;
            memoryCache.Set(cacheKey, foundSpa, TimeSpan.FromMinutes(5));

            if (foundSpa)
            {
                return potentialArticleUrl.TrimStart('/') + "/index.html";
            }
        }

        return null;
    }
}
