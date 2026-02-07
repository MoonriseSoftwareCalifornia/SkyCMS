// <copyright file="DocsImportController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Editor.Controllers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.BlobService;
using Cosmos.BlobService.Models;
using Cosmos.Cms.Common;
using Cosmos.Common.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
[Route("_api/import/docs")]
public class DocsImportController : ControllerBase
{
    private const string ApiKeyConfigPath = "DocsImport:ApiKey";
    private const string ApiKeyFallbackConfigPath = "DocsImportApiKey";
    private const string UserIdConfigPath = "DocsImport:UserId";
    private const string UserIdFallbackConfigPath = "DocsImportUserId";

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
    [HttpPut("items/{sourceKey}")]
    public async Task<IActionResult> Upsert(string sourceKey, [FromBody] DocsUpsertRequest request, CancellationToken cancellationToken = default)
    {
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
                return BadRequest(result.Errors);
            }

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
            return BadRequest(saveResult.Errors);
        }

        return Ok(new { status = "updated", articleNumber = existing.ArticleNumber, sourceKey });
    }

    /// <summary>
    /// Uploads a docs asset to blob storage under /pub/docs.
    /// </summary>
    [HttpPost("assets")]
    [RequestSizeLimit(26_214_400)]
    public async Task<IActionResult> UploadAsset(
        [FromForm] IFormFile file,
        [FromForm] string? relativePath,
        CancellationToken cancellationToken = default)
    {
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

        return Ok(new { url = publicUrl, path = metaData.RelativePath });
    }

    /// <summary>
    /// Deletes a docs page by source key.
    /// </summary>
    [HttpDelete("items/{sourceKey}")]
    public async Task<IActionResult> Delete(string sourceKey, CancellationToken cancellationToken = default)
    {
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
        return Ok(new { status = "deleted", articleNumber = catalogEntry.ArticleNumber, sourceKey });
    }

    /// <summary>
    /// Renames a docs page by source paths.
    /// </summary>
    [HttpPost("rename")]
    public async Task<IActionResult> Rename([FromBody] DocsRenameRequest request, CancellationToken cancellationToken = default)
    {
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
            return BadRequest(saveResult.Errors);
        }

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

        if (!string.Equals(token, configuredKey, StringComparison.Ordinal))
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

    private static ArticleType ParseArticleType(string? articleType)
    {
        if (string.IsNullOrWhiteSpace(articleType))
        {
            return ArticleType.General;
        }

        return Enum.TryParse<ArticleType>(articleType, true, out var parsed)
            ? parsed
            : ArticleType.General;
    }

    private static DateTimeOffset? ResolvePublishedTimestamp(DocsUpsertRequest request)
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
            .FirstOrDefaultAsync(t => t.Title.ToLower() == templateKey.ToLower(), cancellationToken);

        return template?.Id;
    }

    private static string NormalizeSourceToUrlPath(string sourceKey)
    {
        var normalized = sourceKey.Replace('\\', '/').ToLowerInvariant();
        var trimmed = normalized.StartsWith("docs/") ? normalized[5..] : normalized;

        if (trimmed.EndsWith("/index.md", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^9];
        }
        else if (trimmed.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3];
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

    private static void RewriteAttribute(HtmlNode node, string attributeName, string pageDir, string sourceDir, string assetBase)
    {
        if (!node.Attributes.Contains(attributeName))
        {
            return;
        }

        var attribute = node.Attributes[attributeName];
        var value = attribute?.Value;
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

    private static bool IsExternalOrAbsolute(string value)
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

    private static bool IsMarkdownLink(string value) =>
        value.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        value.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    private static string TrimMarkdownExtension(string value)
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

    private static string GetUrlDirectory(string urlPath)
    {
        if (string.IsNullOrWhiteSpace(urlPath))
        {
            return "docs";
        }

        var normalized = urlPath.Trim('/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash >= 0 ? normalized[..lastSlash] : normalized;
    }

    private static string GetSourceDirectory(string? sourcePath)
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

    private static string CombineUrlPath(string basePath, string relativePath)
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

    private static string NormalizeAssetRelativePath(string? relativePath, string fallbackFileName)
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

    private static string UrlEncodePath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var encoded = parts.Select(UrlEncodePathSegment);
        return string.Join('/', encoded);
    }

    private static string UrlEncodePathSegment(string segment)
    {
        return Uri.EscapeDataString(segment.Replace(" ", "-"));
    }

    /// <summary>
    /// Upsert request payload.
    /// </summary>
    public sealed class DocsUpsertRequest
    {
        public string Title { get; init; } = string.Empty;
        public string UrlPath { get; init; } = string.Empty;
        public string Html { get; init; } = string.Empty;
        public string? TemplateKey { get; init; }
        public bool Published { get; init; }
        public DateTimeOffset? PublishedAt { get; init; }
        public string? Introduction { get; init; }
        public string? BannerImage { get; init; }
        public string? ArticleType { get; init; }
        public DocsSourceInfo? Source { get; init; }
        public bool RewriteLinks { get; init; } = true;
        public string? AssetBasePath { get; init; }
    }

    /// <summary>
    /// Rename request payload.
    /// </summary>
    public sealed class DocsRenameRequest
    {
        public string FromPath { get; init; } = string.Empty;
        public string ToPath { get; init; } = string.Empty;
        public string? FromTitle { get; init; }
        public string? ToTitle { get; init; }
    }

    /// <summary>
    /// Source tracking metadata.
    /// </summary>
    public sealed class DocsSourceInfo
    {
        public string? Path { get; init; }
        public string? Hash { get; init; }
    }
}
