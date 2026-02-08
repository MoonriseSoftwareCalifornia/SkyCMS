// <copyright file="DocsImportController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

#nullable enable

namespace Cosmos.Cms.Editor.Controllers;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.BlobService;
using Cosmos.BlobService.Models;
using Cosmos.Cms.Common;
using Cosmos.Common.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeTypes;
using HtmlAgilityPack;
using Sky.Cms.Services;
using Sky.Cms.Controllers;
using Sky.Editor.Data.Logic;
using Sky.Editor.Features.Articles.Create;
using Sky.Editor.Features.Articles.Save;
using Sky.Editor.Features.Shared;
using Sky.Editor.Services.EditorSettings;

/// <summary>
/// API controller for importing documentation content from CI pipelines.
/// </summary>
[ApiController]
[EnableRateLimiting("docs-import")]
[Route("_api/import/docs")]
public class DocsImportController : ControllerBase
{
    private const string ApiKeyConfigPath = "DocsImport:ApiKey";
    private const string ApiKeyFallbackConfigPath = "DocsImportApiKey";
    private const string UserIdConfigPath = "DocsImport:UserId";
    private const string UserIdFallbackConfigPath = "DocsImportUserId";
    private const int MaxHtmlBytes = 1_048_576;

    private readonly ApplicationDbContext dbContext;
    private readonly IMediator mediator;
    private readonly ArticleEditLogic articleLogic;
    private readonly IConfiguration configuration;
    private readonly ILogger<DocsImportController> logger;
    private readonly IStorageContext storageContext;
    private readonly IEditorSettings editorSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocsImportController"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for content persistence.</param>
    /// <param name="mediator">Mediator for create/save commands.</param>
    /// <param name="articleLogic">Article editor logic for deletes.</param>
    /// <param name="configuration">Configuration source for import settings.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="storageContext">Storage context for asset uploads.</param>
    /// <param name="editorSettings">Editor settings accessor.</param>
    public DocsImportController(
        ApplicationDbContext dbContext,
        IMediator mediator,
        ArticleEditLogic articleLogic,
        IConfiguration configuration,
        ILogger<DocsImportController> logger,
        IStorageContext storageContext,
        IEditorSettings editorSettings)
    {
        this.dbContext = dbContext;
        this.mediator = mediator;
        this.articleLogic = articleLogic;
        this.configuration = configuration;
        this.logger = logger;
        this.storageContext = storageContext;
        this.editorSettings = editorSettings;
    }

    /// <summary>
    /// Creates or updates a docs page by source key.
    /// </summary>
    /// <param name="sourceKey">Source key for the document.</param>
    /// <param name="request">Upsert request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upsert result payload.</returns>
    [HttpPut("items/{sourceKey}")]
    [RequestSizeLimit(MaxHtmlBytes)]
    public async Task<IActionResult> Upsert(string sourceKey, [FromBody] DocsUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!TryAuthorize(out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.UrlPath) || string.IsNullOrWhiteSpace(request.Html))
        {
            return BadRequest("Title, UrlPath, and Html are required.");
        }

        var htmlBytes = Encoding.UTF8.GetByteCount(request.Html);

        if (htmlBytes > MaxHtmlBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "Html payload exceeds the 1 MB limit.");
        }

        logger.LogInformation(
            "Docs import upsert started: sourceKey={SourceKey}, urlPath={UrlPath}, title={Title}, templateKey={TemplateKey}, articleType={ArticleType}, htmlBytes={HtmlBytes}",
            sourceKey,
            request.UrlPath,
            request.Title,
            request.TemplateKey ?? string.Empty,
            request.ArticleType ?? string.Empty,
            htmlBytes);

        var userId = GetImporterUserId();
        if (userId == Guid.Empty)
        {
            return StatusCode(500, "Docs import user ID is not configured.");
        }

        var templateId = await ResolveTemplateIdAsync(request.TemplateKey, cancellationToken);
        var articleType = ParseArticleType(request.ArticleType);
        var published = ResolvePublishedTimestamp(request);
        var html = request.RewriteLinks ? RewriteLinks(request) : request.Html;

        var existing = await dbContext.Articles
            .Where(a => a.UrlPath == request.UrlPath)
            .OrderByDescending(a => a.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing == null)
        {
            var createCommand = new CreateArticleCommand
            {
                Title = request.Title,
                UserId = userId,
                TemplateId = templateId,
                ArticleType = articleType,
                ContentOverride = html,
                UrlPathOverride = request.UrlPath,
                Published = published,
                Introduction = request.Introduction,
                BannerImage = request.BannerImage
            };

            var result = await mediator.SendAsync(createCommand, cancellationToken);
            if (!result.IsSuccess)
            {
                logger.LogWarning("Docs import upsert failed: sourceKey={SourceKey}, errors={Errors}", sourceKey, result.Errors);
                return BadRequest(result.Errors);
            }

            stopwatch.Stop();
            logger.LogInformation(
                "Docs import upsert created: sourceKey={SourceKey}, articleNumber={ArticleNumber}, durationMs={DurationMs}",
                sourceKey,
                result.Data?.ArticleNumber,
                stopwatch.ElapsedMilliseconds);

            return Ok(new { status = "created", articleNumber = result.Data?.ArticleNumber, sourceKey });
        }

        var saveCommand = new SaveArticleCommand
        {
            ArticleNumber = existing.ArticleNumber,
            Title = request.Title,
            Content = html,
            UrlPath = request.UrlPath,
            Published = published,
            ArticleType = articleType,
            Introduction = request.Introduction,
            BannerImage = request.BannerImage,
            UserId = userId
        };

        var saveResult = await mediator.SendAsync(saveCommand, cancellationToken);
        if (!saveResult.IsSuccess)
        {
            logger.LogWarning("Docs import upsert failed: sourceKey={SourceKey}, errors={Errors}", sourceKey, saveResult.Errors);
            return BadRequest(saveResult.Errors);
        }

        stopwatch.Stop();
        logger.LogInformation(
            "Docs import upsert updated: sourceKey={SourceKey}, articleNumber={ArticleNumber}, durationMs={DurationMs}",
            sourceKey,
            existing.ArticleNumber,
            stopwatch.ElapsedMilliseconds);

        return Ok(new { status = "updated", articleNumber = existing.ArticleNumber, sourceKey });
    }

    /// <summary>
    /// Uploads a docs asset to blob storage under /pub/docs.
    /// </summary>
    /// <param name="file">Uploaded file.</param>
    /// <param name="relativePath">Relative path under the docs root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upload result payload.</returns>
    [HttpPost("assets")]
    [RequestSizeLimit(26_214_400)]
    public async Task<IActionResult> UploadAsset(
        [FromForm] IFormFile file,
        [FromForm] string? relativePath,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!TryAuthorize(out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        var fileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (FileManagerController.DangerousFileExtensions.Contains(extension))
        {
            return BadRequest("File type is not allowed.");
        }

        var normalizedRelative = NormalizeAssetRelativePath(relativePath, fileName);
        if (!normalizedRelative.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized("Assets must be uploaded under /pub/docs.");
        }

        var targetPath = "/pub/" + normalizedRelative.TrimStart('/');
        await EnsureFolderPathExists(targetPath, cancellationToken);

        var metaData = new FileUploadMetaData
        {
            ChunkIndex = 0,
            TotalChunks = 1,
            TotalFileSize = file.Length,
            UploadUid = Guid.NewGuid().ToString(),
            FileName = UrlEncodePathSegment(fileName),
            RelativePath = UrlEncodePath(targetPath),
            ContentType = MimeTypeMap.GetMimeType(extension)
        };

        await using (var stream = file.OpenReadStream())
        await using (var memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream, cancellationToken);
            await storageContext.AppendBlob(memoryStream, metaData);
        }

        var baseUrl = editorSettings.BlobPublicUrl.TrimEnd('/');
        var publicUrl = baseUrl + "/" + metaData.RelativePath.TrimStart('/');

        stopwatch.Stop();
        logger.LogInformation(
            "Docs import asset uploaded: path={Path}, size={Size}, durationMs={DurationMs}",
            metaData.RelativePath,
            file.Length,
            stopwatch.ElapsedMilliseconds);

        return Ok(new { url = publicUrl, path = metaData.RelativePath });
    }

    /// <summary>
    /// Deletes a docs page by source key.
    /// </summary>
    /// <param name="sourceKey">Source key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Delete result payload.</returns>
    [HttpDelete("items/{sourceKey}")]
    public async Task<IActionResult> Delete(string sourceKey, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!TryAuthorize(out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var urlPath = NormalizeSourceToUrlPath(sourceKey);
        var catalogEntry = await dbContext.ArticleCatalog
            .FirstOrDefaultAsync(c => c.UrlPath == urlPath, cancellationToken);

        if (catalogEntry == null)
        {
            return NotFound();
        }

        await articleLogic.DeleteArticle(catalogEntry.ArticleNumber);

        stopwatch.Stop();
        logger.LogInformation(
            "Docs import delete: sourceKey={SourceKey}, articleNumber={ArticleNumber}, durationMs={DurationMs}",
            sourceKey,
            catalogEntry.ArticleNumber,
            stopwatch.ElapsedMilliseconds);

        return Ok(new { status = "deleted", articleNumber = catalogEntry.ArticleNumber, sourceKey });
    }

    /// <summary>
    /// Renames a docs page by source paths.
    /// </summary>
    /// <param name="request">Rename request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rename result payload.</returns>
    [HttpPost("rename")]
    public async Task<IActionResult> Rename([FromBody] DocsRenameRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!TryAuthorize(out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.FromPath) || string.IsNullOrWhiteSpace(request.ToPath))
        {
            return BadRequest("FromPath and ToPath are required.");
        }

        if (string.IsNullOrWhiteSpace(request.ToTitle))
        {
            return BadRequest("ToTitle is required for rename operations.");
        }

        var userId = GetImporterUserId();
        if (userId == Guid.Empty)
        {
            return StatusCode(500, "Docs import user ID is not configured.");
        }

        var fromUrlPath = NormalizeSourceToUrlPath(request.FromPath);
        var toUrlPath = NormalizeSourceToUrlPath(request.ToPath);

        var existing = await dbContext.Articles
            .Where(a => a.UrlPath == fromUrlPath)
            .OrderByDescending(a => a.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing == null)
        {
            return NotFound();
        }

        var saveCommand = new SaveArticleCommand
        {
            ArticleNumber = existing.ArticleNumber,
            Title = request.ToTitle,
            Content = existing.Content,
            UrlPath = toUrlPath,
            Published = existing.Published,
            ArticleType = existing.ArticleType.HasValue
                ? (ArticleType)existing.ArticleType.Value
                : ArticleType.General,
            Introduction = existing.Introduction,
            BannerImage = existing.BannerImage,
            UserId = userId
        };

        var saveResult = await mediator.SendAsync(saveCommand, cancellationToken);
        if (!saveResult.IsSuccess)
        {
            logger.LogWarning("Docs import rename failed: fromPath={FromPath}, toPath={ToPath}, errors={Errors}", request.FromPath, request.ToPath, saveResult.Errors);
            return BadRequest(saveResult.Errors);
        }

        stopwatch.Stop();
        logger.LogInformation(
            "Docs import rename: fromPath={FromPath}, toPath={ToPath}, articleNumber={ArticleNumber}, durationMs={DurationMs}",
            request.FromPath,
            request.ToPath,
            existing.ArticleNumber,
            stopwatch.ElapsedMilliseconds);

        return Ok(new { status = "renamed", articleNumber = existing.ArticleNumber });
    }

    private bool TryAuthorize(out IActionResult result)
    {
        result = Unauthorized();

        var configuredKey = configuration[ApiKeyConfigPath] ?? configuration[ApiKeyFallbackConfigPath];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            logger.LogWarning("Docs import API key is not configured.");
            result = StatusCode(500, "Docs import API key is not configured.");
            return false;
        }

        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header[7..].Trim()
            : header.Trim();

        var keys = configuredKey
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!keys.Any(key => string.Equals(token, key, StringComparison.Ordinal)))
        {
            return false;
        }

        result = Ok();
        return true;
    }

    private Guid GetImporterUserId()
    {
        var configuredUserId = configuration[UserIdConfigPath] ?? configuration[UserIdFallbackConfigPath];
        return Guid.TryParse(configuredUserId, out var parsed) ? parsed : Guid.Empty;
    }

    private ArticleType ParseArticleType(string? articleType)
    {
        if (string.IsNullOrWhiteSpace(articleType))
        {
            return ArticleType.General;
        }

        return Enum.TryParse<ArticleType>(articleType, true, out var parsed)
            ? parsed
            : ArticleType.General;
    }

    private DateTimeOffset? ResolvePublishedTimestamp(DocsUpsertRequest request)
    {
        if (request.PublishedAt.HasValue)
        {
            return request.PublishedAt;
        }

        return request.Published ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
    }

    private async Task<Guid?> ResolveTemplateIdAsync(string? templateKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            return null;
        }

        var template = await dbContext.Templates
            .FirstOrDefaultAsync(t => string.Equals(t.Title, templateKey, StringComparison.OrdinalIgnoreCase), cancellationToken);

        return template?.Id;
    }

    private string NormalizeSourceToUrlPath(string sourceKey)
    {
        var normalized = sourceKey.Replace('\\', '/').ToLowerInvariant();
        var trimmed = normalized.StartsWith("docs/") ? normalized[5..] : normalized;

        if (trimmed.EndsWith("/index.md", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^9];
        }
        else if (trimmed.EndsWith("/index.markdown", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^13];
        }
        else if (trimmed.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3];
        }
        else if (trimmed.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^9];
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "docs" : $"docs/{trimmed}";
    }

    private string RewriteLinks(DocsUpsertRequest request)
    {
        var html = request.Html;
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var pageDir = GetUrlDirectory(request.UrlPath);
        var sourceDir = GetSourceDirectory(request.Source?.Path);
        var assetBase = (request.AssetBasePath ?? "/pub/docs").TrimEnd('/');

        foreach (var node in doc.DocumentNode.Descendants())
        {
            RewriteAttribute(node, "href", pageDir, sourceDir, assetBase);
            RewriteAttribute(node, "src", pageDir, sourceDir, assetBase);
        }

        return doc.DocumentNode.OuterHtml;
    }

    private void RewriteAttribute(HtmlNode node, string attributeName, string pageDir, string sourceDir, string assetBase)
    {
        if (!node.Attributes.Contains(attributeName))
        {
            return;
        }

        var attribute = node.Attributes[attributeName];
        if (attribute == null)
        {
            return;
        }

        var value = attribute.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (IsExternalOrAbsolute(value))
        {
            return;
        }

        var normalized = value.Replace("\\", "/");
        var combinedBase = string.IsNullOrWhiteSpace(sourceDir) ? pageDir : sourceDir;

        if (IsMarkdownLink(normalized))
        {
            var target = CombineUrlPath(pageDir, normalized);
            target = TrimMarkdownExtension(target);
            attribute.Value = "/" + target.TrimStart('/');
            return;
        }

        var assetTarget = CombineUrlPath(combinedBase, normalized);
        attribute.Value = assetBase + "/" + assetTarget.TrimStart('/');
    }

    private bool IsExternalOrAbsolute(string value)
    {
        if (value.StartsWith("#", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var htmlUtilities = new HtmlUtilities();
        return htmlUtilities.IsAbsoluteUri(value);
    }

    private bool IsMarkdownLink(string value) =>
        value.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        value.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    private string TrimMarkdownExtension(string value)
    {
        if (value.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            return value[..^9];
        }

        if (value.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return value[..^3];
        }

        return value;
    }

    private string GetUrlDirectory(string urlPath)
    {
        if (string.IsNullOrWhiteSpace(urlPath))
        {
            return "docs";
        }

        var normalized = urlPath.Trim('/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash >= 0 ? normalized[..lastSlash] : normalized;
    }

    private string GetSourceDirectory(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }

        var normalized = sourcePath.Replace("\\", "/");
        if (normalized.StartsWith("Docs/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[5..];
        }

        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash >= 0 ? normalized[..lastSlash] : string.Empty;
    }

    private string CombineUrlPath(string basePath, string relativePath)
    {
        var combined = string.IsNullOrWhiteSpace(basePath)
            ? relativePath
            : basePath.TrimEnd('/') + "/" + relativePath;

        var parts = combined.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();

        foreach (var part in parts)
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (stack.Count > 0)
                {
                    stack.RemoveAt(stack.Count - 1);
                }

                continue;
            }

            stack.Add(part);
        }

        return string.Join('/', stack);
    }

    private string NormalizeAssetRelativePath(string? relativePath, string fallbackFileName)
    {
        var normalized = (relativePath ?? string.Empty).Replace("\\", "/").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "docs/" + fallbackFileName;
        }

        if (normalized.StartsWith("/"))
        {
            normalized = normalized.TrimStart('/');
        }

        if (normalized.EndsWith("/"))
        {
            normalized += fallbackFileName;
        }

        if (!normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "docs/" + normalized.TrimStart('/');
        }

        return normalized;
    }

    private async Task EnsureFolderPathExists(string targetPath, CancellationToken cancellationToken)
    {
        var path = targetPath.Trim('/');
        var parts = path.Split('/');
        var current = string.Empty;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            current = string.IsNullOrEmpty(current) ? parts[i] : current + "/" + parts[i];
            if (!string.Equals(current, "pub", StringComparison.OrdinalIgnoreCase))
            {
                await storageContext.CreateFolder(current);
            }
        }
    }

    private string UrlEncodePath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var encoded = parts.Select(UrlEncodePathSegment);
        return string.Join('/', encoded);
    }

    private string UrlEncodePathSegment(string segment)
    {
        return Uri.EscapeDataString(segment.Replace(" ", "-"));
    }

    /// <summary>
    /// Upsert request payload.
    /// </summary>
    public sealed class DocsUpsertRequest
    {
        /// <summary>
        /// Gets the document title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the URL path for the page.
        /// </summary>
        public string UrlPath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the HTML payload to store.
        /// </summary>
        public string Html { get; init; } = string.Empty;

        /// <summary>
        /// Gets the template key used to resolve a template ID.
        /// </summary>
        public string? TemplateKey { get; init; }

        /// <summary>
        /// Gets a value indicating whether the page should be published.
        /// </summary>
        public bool Published { get; init; }

        /// <summary>
        /// Gets the explicit publish timestamp.
        /// </summary>
        public DateTimeOffset? PublishedAt { get; init; }

        /// <summary>
        /// Gets the summary/intro text.
        /// </summary>
        public string? Introduction { get; init; }

        /// <summary>
        /// Gets the banner image URL.
        /// </summary>
        public string? BannerImage { get; init; }

        /// <summary>
        /// Gets the article type name.
        /// </summary>
        public string? ArticleType { get; init; }

        /// <summary>
        /// Gets the source tracking metadata.
        /// </summary>
        public DocsSourceInfo? Source { get; init; }

        /// <summary>
        /// Gets a value indicating whether relative links should be rewritten.
        /// </summary>
        public bool RewriteLinks { get; init; } = true;

        /// <summary>
        /// Gets the asset base path for rewritten links.
        /// </summary>
        public string? AssetBasePath { get; init; }
    }

    /// <summary>
    /// Rename request payload.
    /// </summary>
    public sealed class DocsRenameRequest
    {
        /// <summary>
        /// Gets the source path to rename from.
        /// </summary>
        public string FromPath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the destination path to rename to.
        /// </summary>
        public string ToPath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the original title (optional).
        /// </summary>
        public string? FromTitle { get; init; }

        /// <summary>
        /// Gets the new title for the page.
        /// </summary>
        public string? ToTitle { get; init; }
    }

    /// <summary>
    /// Source tracking metadata.
    /// </summary>
    public sealed class DocsSourceInfo
    {
        /// <summary>
        /// Gets the source file path.
        /// </summary>
        public string? Path { get; init; }

        /// <summary>
        /// Gets the source content hash.
        /// </summary>
        public string? Hash { get; init; }
    }
}
