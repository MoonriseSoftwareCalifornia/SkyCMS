// <copyright file="DeploymentController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Editor.Controllers;

using BCrypt.Net;
using Cosmos.BlobService;
using Cosmos.BlobService.Models;
using Cosmos.Cms.Common;
using Cosmos.Cms.Common.Models;
using Cosmos.Common.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sky.Editor.Services.CDN;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// API controller for deploying SPA applications from CI/CD pipelines.
/// </summary>
/// <remarks>
/// <para>
/// Accepts authenticated POST requests from GitHub Actions, Azure DevOps, or any CI/CD system
/// to deploy compiled SPA applications to blob storage.
/// </para>
/// <para>
/// Security is provided through:
/// <list type="bullet">
///   <item>32-character cryptographic deployment key (BCrypt hashed)</item>
///   <item>Rate limiting (10 requests per 5 minutes per IP)</item>
///   <item>Request size limits (100 MB maximum)</item>
///   <item>File type validation (allowlist of safe extensions)</item>
///   <item>Path traversal protection</item>
///   <item>Comprehensive audit logging</item>
/// </list>
/// </para>
/// <para>Endpoint: POST /api/spa/deploy</para>
/// </remarks>
[Route("api/spa/deploy")]
[ApiController]
[EnableRateLimiting("deployment")]
public class DeploymentController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly IStorageContext storageContext;
    private readonly ILogger<DeploymentController> logger;

    // Maximum allowed zip file size (100 MB)
    private const long MaxZipFileSize = 100_000_000;

    // Allowed file extensions in the SPA bundle
    private static readonly string[] AllowedExtensions =
    [
        ".html", ".htm", ".js", ".mjs", ".css", ".json", ".svg", ".png", ".jpg", ".jpeg",
        ".gif", ".webp", ".ico", ".woff", ".woff2", ".ttf", ".eot", ".map", ".txt", ".xml"
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentController"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="storageContext">Blob storage context.</param>
    /// <param name="logger">Logger instance.</param>
    public DeploymentController(
        ApplicationDbContext dbContext,
        IStorageContext storageContext,
        ILogger<DeploymentController> logger)
    {
        this.dbContext = dbContext;
        this.storageContext = storageContext;
        this.logger = logger;
    }

    /// <summary>
    /// Deploys a SPA application from a zip file.
    /// </summary>
    /// <param name="articleId">The GUID of the SPA article.</param>
    /// <param name="password">The deployment key (password).</param>
    /// <param name="zipFile">The zip file containing the compiled SPA.</param>
    /// <returns>Deployment result.</returns>
    /// <remarks>
    /// <para>
    /// Authentication is performed via the deployment key (password parameter).
    /// The deployment key is:
    /// <list type="bullet">
    ///   <item>32 characters of cryptographic randomness</item>
    ///   <item>BCrypt hashed with high work factor (cost 10-12)</item>
    ///   <item>Verified in constant-time to prevent timing attacks</item>
    ///   <item>Supports key rotation with 24-hour grace period</item>
    /// </list>
    /// </para>
    /// <para>
    /// Optional headers for audit trail:
    /// <list type="bullet">
    ///   <item>X-GitHub-SHA: Git commit SHA</item>
    ///   <item>X-GitHub-Repository: Repository name (owner/repo)</item>
    /// </list>
    /// </para>
    /// </remarks>
    [HttpPost]
    [RequestSizeLimit(MaxZipFileSize)]
    public async Task<IActionResult> Deploy(
    [FromForm] Guid articleId,
    [FromForm] string password,
    [FromForm] IFormFile zipFile)
    {
        try
        {
            // 1. Load the SPA article from PublishedPages
            var spaAppType = (int)ArticleType.SpaApp;
            var article = await dbContext.Pages
                .FirstOrDefaultAsync(p => p.Id == articleId && p.ArticleType == spaAppType);

            if (article == null)
            {
                logger.LogWarning("Deployment attempt for non-existent SPA article: {ArticleId}", articleId);
                return NotFound(new { success = false, error = "SPA article not found" });
            }

            // 2. Deserialize SPA metadata
            var metadata = article.Content != null
                ? System.Text.Json.JsonSerializer.Deserialize<SpaMetadata>(article.Content)
                : null;

            if (metadata == null)
            {
                logger.LogError("SPA article {ArticleId} has invalid or missing metadata", articleId);
                return BadRequest(new { success = false, error = "Invalid SPA metadata" });
            }

            // 3. Verify deployment password (check current + previous for rotation grace period)
            if (!VerifyPassword(password, metadata))
            {
                logger.LogWarning("Deployment attempt with invalid password. Article: {ArticleId}, IP: {IP}",
                    articleId, HttpContext.Connection.RemoteIpAddress);
                return Unauthorized(new { success = false, error = "Invalid deployment key" });
            }

            // 4. Validate zip file
            var validationError = ValidateZipFile(zipFile);
            if (validationError != null)
            {
                logger.LogWarning("Invalid zip file for article {ArticleId}: {Error}", articleId, validationError);
                return BadRequest(new { success = false, error = validationError });
            }

            // 5. Deploy to blob storage
            var deployedFiles = await DeployToStorageAsync(article.UrlPath, zipFile);

            // 6. Purge CDN cache for deployed SPA
            var cdnPurged = await PurgeCdnCacheAsync(article.UrlPath, deployedFiles);

            // 7. Update deployment metadata
            metadata.LastDeployedAt = DateTimeOffset.UtcNow;
            metadata.DeploymentCount++;

            // Extract Git info from headers if available
            if (Request.Headers.ContainsKey("X-GitHub-SHA"))
            {
                metadata.LastCommitSha = Request.Headers["X-GitHub-SHA"].ToString();
            }

            if (Request.Headers.ContainsKey("X-GitHub-Repository"))
            {
                metadata.LastDeployedFrom = Request.Headers["X-GitHub-Repository"].ToString();
            }

            article.Content = System.Text.Json.JsonSerializer.Serialize(metadata);
            article.Updated = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync();

            logger.LogInformation(
                "Successfully deployed SPA to {UrlPath}. Article: {ArticleId}, Deployment #{Count}, CDN Purged: {CdnPurged}, Files: {FileCount}, IP: {IP}",
                article.UrlPath,
                articleId,
                metadata.DeploymentCount,
                cdnPurged,
                deployedFiles.Count,
                HttpContext.Connection.RemoteIpAddress);

            return Ok(new
            {
                success = true,
                deployedAt = metadata.LastDeployedAt,
                deploymentCount = metadata.DeploymentCount,
                urlPath = article.UrlPath,
                filesDeployed = deployedFiles.Count,
                cdnPurged = cdnPurged
            });
        }
        catch (System.Text.Json.JsonException ex)
        {
            // Invalid JSON in SPA metadata
            logger.LogError(ex, "Invalid JSON in SPA metadata for article {ArticleId}", articleId);
            return BadRequest(new { success = false, error = "Invalid SPA metadata" });
        }
        catch (InvalidOperationException)
        {
            // Re-throw security validation exceptions (e.g., path traversal attempts)
            // These should propagate to indicate malicious input
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deployment failed for article {ArticleId}", articleId);
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    /// <summary>
    /// Verifies the deployment password against stored hashes.
    /// </summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="metadata">The SPA metadata containing password hashes.</param>
    /// <returns>True if password is valid, false otherwise.</returns>
    /// <remarks>
    /// <para>
    /// Verification process:
    /// <list type="number">
    ///   <item>Checks current deployment key hash (BCrypt.Verify)</item>
    ///   <item>If failed, checks previous key hash if within 24-hour grace period</item>
    ///   <item>Uses constant-time comparison to prevent timing attacks</item>
    /// </list>
    /// </para>
    /// <para>
    /// This allows for zero-downtime key rotation:
    /// <list type="bullet">
    ///   <item>Generate new deployment key in admin UI</item>
    ///   <item>Old key remains valid for 24 hours</item>
    ///   <item>Update GitHub Actions secret within grace period</item>
    ///   <item>Old key automatically expires after 24 hours</item>
    /// </list>
    /// </para>
    /// </remarks>
    private bool VerifyPassword(string password, SpaMetadata metadata)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(metadata.DeploymentKeyHash))
        {
            return false;
        }

        // Check current password
        bool isValidKey = BCrypt.Verify(password, metadata.DeploymentKeyHash);

        // Check previous password (grace period for rotation)
        if (!isValidKey && !string.IsNullOrEmpty(metadata.DeploymentKeyHashPrevious))
        {
            var rotationAge = DateTimeOffset.UtcNow - (metadata.DeploymentKeyRotatedAt ?? DateTimeOffset.MinValue);
            if (rotationAge < TimeSpan.FromHours(24))
            {
                isValidKey = BCrypt.Verify(password, metadata.DeploymentKeyHashPrevious);

                if (isValidKey)
                {
                    logger.LogInformation(
                        "Deployment used previous key within grace period. Rotation age: {Hours:F1} hours",
                        rotationAge.TotalHours);
                }
            }
        }

        return isValidKey;
    }

    /// <summary>
    /// Validates the uploaded zip file.
    /// </summary>
    /// <param name="zipFile">The zip file to validate.</param>
    /// <returns>Error message if invalid, null if valid.</returns>
    private string ValidateZipFile(IFormFile zipFile)
    {
        if (zipFile == null || zipFile.Length == 0)
        {
            return "No file uploaded";
        }

        if (zipFile.Length > MaxZipFileSize)
        {
            return $"File size exceeds maximum allowed size of {MaxZipFileSize / 1_000_000} MB";
        }

        if (!zipFile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return "File must be a .zip archive";
        }

        return null;
    }

    /// <summary>
    /// Purges CDN cache for the deployed SPA and its assets.
    /// </summary>
    /// <param name="urlPath">The article URL path (e.g., "/my-app").</param>
    /// <param name="deployedFiles">List of files that were deployed.</param>
    /// <returns>True if CDN was purged, false if no CDN configured or purge failed.</returns>
    private async Task<bool> PurgeCdnCacheAsync(string urlPath, List<string> deployedFiles)
    {
        try
        {
            var cdnService = await CdnService.GetCdnServiceAsync(dbContext, logger, HttpContext);

            if (!cdnService.IsConfigured())
            {
                logger.LogInformation("No CDN configured, skipping cache purge");
                return false;
            }

            // Build list of URLs to purge
            var purgeUrls = new List<string>
            {
                // Purge the SPA root path and its wildcard
                urlPath.TrimStart('/'),
                $"{urlPath.TrimStart('/')}/*"
            };

            logger.LogInformation("Purging CDN cache for SPA at {UrlPath} ({Count} paths)", urlPath, purgeUrls.Count);

            var results = await cdnService.PurgeCdn(purgeUrls);

            var allSuccessful = results.All(r => r.IsSuccessStatusCode);

            if (allSuccessful)
            {
                logger.LogInformation("Successfully purged CDN cache for {UrlPath}", urlPath);
            }
            else
            {
                var failures = results.Where(r => !r.IsSuccessStatusCode).ToList();
                logger.LogWarning("CDN purge completed with {FailureCount} failures for {UrlPath}", failures.Count, urlPath);
                foreach (var failure in failures)
                {
                    logger.LogWarning("CDN purge failed: {Message}", failure.Message);
                }
            }

            return allSuccessful;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to purge CDN cache for {UrlPath}", urlPath);
            // Don't fail the deployment if CDN purge fails
            return false;
        }
    }

    /// <summary>
    /// Deploys the SPA to blob storage by extracting and uploading zip contents.
    /// </summary>
    /// <param name="urlPath">The article URL path (e.g., "/my-app").</param>
    /// <param name="zipFile">The zip file containing the SPA build.</param>
    /// <returns>List of deployed file paths.</returns>
    private async Task<List<string>> DeployToStorageAsync(string urlPath, IFormFile zipFile)
    {
        var basePath = urlPath.TrimStart('/');
        var deployedFiles = new List<string>();

        using var zipStream = zipFile.OpenReadStream();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            // Skip directories
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            // Validate file path (prevent path traversal)
            if (entry.FullName.Contains(".."))
            {
                throw new InvalidOperationException($"Invalid file path detected: {entry.FullName}");
            }

            // Validate file extension
            var extension = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                logger.LogWarning("Skipping unsupported file type: {FileName}", entry.FullName);
                continue;
            }

            // Construct blob path
            var blobPath = $"{basePath}/{entry.FullName}";

            // Extract file to memory
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms);

            // Determine content type
            var contentType = GetContentType(extension);

            // Upload to blob storage
            var fileMetadata = new FileUploadMetaData
            {
                FileName = blobPath,
                RelativePath = blobPath,
                ContentType = contentType,
                TotalFileSize = ms.Length,
                ChunkIndex = 0,
                TotalChunks = 1,
                UploadUid = Guid.NewGuid().ToString()
            };

            ms.Position = 0;
            await storageContext.AppendBlob(ms, fileMetadata, "block");

            deployedFiles.Add(blobPath);

            logger.LogDebug("Uploaded {BlobPath} ({Size} bytes)", blobPath, ms.Length);
        }

        return deployedFiles;
    }

    /// <summary>
    /// Gets the MIME content type for a file extension.
    /// </summary>
    /// <param name="extension">The file extension (including dot).</param>
    /// <returns>The MIME type.</returns>
    private static string GetContentType(string extension)
    {
        return extension switch
        {
            ".html" or ".htm" => "text/html",
            ".js" or ".mjs" => "application/javascript",
            ".css" => "text/css",
            ".json" => "application/json",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".eot" => "application/vnd.ms-fontobject",
            ".map" => "application/json",
            ".txt" => "text/plain",
            ".xml" => "application/xml",
            _ => "application/octet-stream"
        };
    }
}