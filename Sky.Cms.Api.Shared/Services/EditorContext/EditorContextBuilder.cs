// <copyright file="EditorContextBuilder.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Services.EditorContext;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Implementation of <see cref="IEditorContextBuilder"/> for assembling AI context payloads.
/// 
/// Implements ADR 0044: AI Editor Context Schema with Layered Delivery and Entity Awareness.
/// 
/// This service is client-agnostic and designed for reuse by both web editors (Monaco/CKEditor)
/// and VS Code extension endpoints.
/// </summary>
public class EditorContextBuilder : IEditorContextBuilder
{
    /// <summary>
    /// Maximum length for field values before truncation.
    /// </summary>
    private const int MaxFieldLength = 50_000;

    /// <summary>
    /// Field value truncation suffix.
    /// </summary>
    private const string TruncationSuffix = "\n\n... (truncated)";

    private readonly IApplicationDbContext _dbContext;
    private readonly IKnowledgeContextProvider _knowledgeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorContextBuilder"/> class.
    /// </summary>
    /// <param name="dbContext">Application database context for entity queries.</param>
    /// <param name="knowledgeProvider">Knowledge context provider for documentation and rules.</param>
    public EditorContextBuilder(
        IApplicationDbContext dbContext,
        IKnowledgeContextProvider knowledgeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _knowledgeProvider = knowledgeProvider ?? throw new ArgumentNullException(nameof(knowledgeProvider));
    }

    /// <inheritdoc />
    public async Task<ArticleEntityContext> BuildArticleContextAsync(
        int articleNumber,
        CancellationToken cancellationToken = default)
    {
        // Load all versions of the article, sorted by version descending
        var articles = await _dbContext.Articles
            .Where(a => a.ArticleNumber == articleNumber)
            .OrderByDescending(a => a.VersionNumber)
            .ToListAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Article with number {articleNumber} not found.");

        if (articles.Count == 0)
        {
            throw new KeyNotFoundException($"Article with number {articleNumber} not found.");
        }

        // Get the latest version (first after ordering by version desc)
        var article = articles[0];

        var status = ConvertArticleStatusToString(article.StatusCode);

        return new ArticleEntityContext
        {
            ArticleNumber = article.ArticleNumber,
            Title = article.Title ?? string.Empty,
            UrlPath = article.UrlPath ?? string.Empty,
            Content = TruncateIfNeeded(article.Content),
            HeaderJavaScript = TruncateIfNeeded(article.HeaderJavaScript),
            FooterJavaScript = TruncateIfNeeded(article.FooterJavaScript),
            BannerImage = string.IsNullOrEmpty(article.BannerImage)
                ? null
                : new BannerImage
                {
                    Url = article.BannerImage,
                    AltText = null,
                    Title = null,
                },
            TemplateId = article.TemplateId.HasValue ? article.TemplateId.Value.ToString() : null,
            LayoutId = "default-layout", // TODO: Get actual layout ID from article or configuration
            Category = article.Category,
            ArticleType = GetArticleTypeString(article.ArticleType),
            Status = status,
            Version = article.VersionNumber,
            LastModified = article.Updated.ToString("O"),
            IsDirty = false, // Set based on form state at request time
            PublishedDate = article.Published?.ToString("O"),
        };
    }

    /// <inheritdoc />
    public async Task<LayoutEntityContext> BuildLayoutContextAsync(
        string layoutId,
        CancellationToken cancellationToken = default)
    {
        // Parse layout ID as Guid
        if (!Guid.TryParse(layoutId, out var layoutGuid))
        {
            throw new ArgumentException($"Invalid layout ID format: {layoutId}", nameof(layoutId));
        }

        // Load the specific layout version
        var layout = await _dbContext.Layouts
            .FirstOrDefaultAsync(l => l.Id == layoutGuid, cancellationToken)
            ?? throw new KeyNotFoundException($"Layout with ID {layoutId} not found.");

        // Extract regions from HTML comments (<!--CCMS--START--REGIONNAME-->...<!--CCMS--END--REGIONNAME-->)
        var regions = ExtractLayoutRegions(layout);

        var status = GetLayoutStatus(layout.Published);

        return new LayoutEntityContext
        {
            LayoutId = layout.Id.ToString(),
            Name = layout.LayoutName ?? "Untitled Layout",
            Description = layout.Notes,
            Regions = regions,
            BodyInsertion = new BodyInsertion
            {
                Type = "direct",
                Location = "body-end",
            },
            Stylesheets = ExtractStylesheets(layout),
            Scripts = ExtractScripts(layout),
            LayoutMarkup = TruncateIfNeeded(layout.Head + "\n" + layout.HtmlHeader + "\n" + layout.FooterHtmlContent),
            ArticlesUsingThisLayout = 0, // TODO: Query from database if needed
            IsDefault = layout.IsDefault,
            Version = layout.Version ?? 1,
        };
    }

    /// <inheritdoc />
    public async Task<TemplateEntityContext> BuildTemplateContextAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        // Parse template ID as Guid
        if (!Guid.TryParse(templateId, out var templateGuid))
        {
            throw new ArgumentException($"Invalid template ID format: {templateId}", nameof(templateId));
        }

        // Load the template
        var template = await _dbContext.Templates
            .FirstOrDefaultAsync(t => t.Id == templateGuid, cancellationToken)
            ?? throw new KeyNotFoundException($"Template with ID {templateId} not found.");

        // Load the latest page design version (draft if exists, else latest published)
        var versions = await _dbContext.PageDesignVersions
            .Where(pdv => pdv.TemplateId == templateGuid)
            .OrderByDescending(pdv => pdv.Version)
            .ToListAsync(cancellationToken);

        var currentVersion = versions.FirstOrDefault() 
            ?? throw new KeyNotFoundException($"No design version found for template {templateId}");

        // Extract fields from the HTML content
        var fields = ExtractTemplateFields(currentVersion.Content ?? string.Empty);
        if (fields.Count == 0)
        {
            fields.Add(new TemplateField
            {
                FieldName = "content",
                DataType = "html",
                Required = true,
                Description = "Main content area",
            });
        }

        var status = GetTemplateStatus(currentVersion.Published);
        var compositionType = GetCompositionType(currentVersion.PageType ?? string.Empty);

        return new TemplateEntityContext
        {
            TemplateId = template.Id.ToString(),
            Name = currentVersion.Title ?? template.Title ?? "Untitled Template",
            Description = currentVersion.Description ?? template.Description,
            ExpectedFields = fields,
            CompositionType = compositionType,
            RenderingRules = new RenderingRules
            {
                PreserveArticleContent = true, // Default safe behavior
                AllowCustomScripts = false,    // Default restrictive
                AllowedHtmlElements = new List<string> { "div", "p", "span", "a", "strong", "em", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "table", "tr", "td", "th" },
            },
            TemplateMarkup = TruncateIfNeeded(currentVersion.Content ?? string.Empty),
            TemplateReference = template.Title,
            ArticlesUsingThisTemplate = 0, // TODO: Query from database if needed
            Version = currentVersion.Version,
        };
    }

    /// <inheritdoc />
    public async Task<RenderingContext> BuildRenderingContextAsync(
        int articleNumber,
        CancellationToken cancellationToken = default)
    {
        // Load the article (latest version)
        var articles = await _dbContext.Articles
            .Where(a => a.ArticleNumber == articleNumber)
            .OrderByDescending(a => a.VersionNumber)
            .ToListAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Article with number {articleNumber} not found.");

        if (articles.Count == 0)
        {
            throw new KeyNotFoundException($"Article with number {articleNumber} not found.");
        }

        var article = articles[0];

        // Build placeholder mappings showing content flow
        var placeholders = new List<PlaceholderMapping>();
        var notes = new List<string>();
        var scripts = new List<ScriptLoadingOrder>();
        string renderingFlow;

        // Check if article uses a template
        if (article.TemplateId.HasValue)
        {
            // Load template
            var template = await _dbContext.Templates
                .FirstOrDefaultAsync(t => t.Id == article.TemplateId, cancellationToken);

            if (template != null)
            {
                // Load latest version of template
                var templateVersions = await _dbContext.PageDesignVersions
                    .Where(pdv => pdv.TemplateId == article.TemplateId)
                    .OrderByDescending(pdv => pdv.Version)
                    .FirstOrDefaultAsync(cancellationToken);

                if (templateVersions != null)
                {
                    // Extract template fields and map them to article content
                    var fields = ExtractTemplateFields(templateVersions.Content ?? string.Empty);

                    foreach (var field in fields)
                    {
                        placeholders.Add(new PlaceholderMapping
                        {
                            Placeholder = $"data-ccms-ceid=\"{field.FieldName}\"",
                            Source = "template",
                            Field = field.FieldName,
                            Required = field.Required,
                        });
                    }

                    renderingFlow = $"Article #{articleNumber} → Template '{template.Title}' → Layout";
                    notes.Add($"Article uses template '{template.Title}' (v{templateVersions.Version})");
                }
                else
                {
                    renderingFlow = $"Article #{articleNumber} → Template (no version) → Layout";
                    notes.Add("Template reference found but no version published");
                }
            }
            else
            {
                renderingFlow = $"Article #{articleNumber} → Missing Template → Layout";
                notes.Add("Article references non-existent template");
            }
        }
        else
        {
            // Article renders directly to layout without template
            renderingFlow = $"Article #{articleNumber} → Layout (direct)";
            notes.Add("Article renders directly to layout without template");
        }

        // Try to get the layout (from template or default)
        Layout? layout = null;

        // If article uses template, try to get layout from template version
        if (article.TemplateId.HasValue)
        {
            var templateVersions = await _dbContext.PageDesignVersions
                .Where(pdv => pdv.TemplateId == article.TemplateId)
                .OrderByDescending(pdv => pdv.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (templateVersions != null && templateVersions.LayoutId.HasValue)
            {
                layout = await _dbContext.Layouts
                    .FirstOrDefaultAsync(l => l.Id == templateVersions.LayoutId, cancellationToken);
            }
        }

        // If still no layout, try to get default layout
        if (layout == null)
        {
            layout = await _dbContext.Layouts
                .Where(l => l.IsDefault)
                .OrderByDescending(l => l.Version)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Final fallback: use the latest available layout if no default is configured.
        if (layout == null)
        {
            layout = await _dbContext.Layouts
                .OrderByDescending(l => l.Version)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (layout != null)
        {
            // Extract layout regions and add to placeholders
            var regions = ExtractLayoutRegions(layout);

            foreach (var region in regions)
            {
                placeholders.Add(new PlaceholderMapping
                {
                    Placeholder = region.Placeholder,
                    Source = "layout",
                    Field = region.Name,
                    Required = region.Required,
                });
            }

            // Extract scripts from layout
            var layoutScripts = ExtractScripts(layout);
            foreach (var script in layoutScripts)
            {
                scripts.Add(new ScriptLoadingOrder
                {
                    Source = script.Url,
                    Location = script.Location,
                    Timing = "immediate",
                });
            }

            notes.Add($"Layout: {layout.LayoutName}");
        }

        // Add article's own scripts
        if (!string.IsNullOrWhiteSpace(article.HeaderJavaScript))
        {
            scripts.Add(new ScriptLoadingOrder
            {
                Source = "article-header",
                Location = "head",
                Timing = "immediate",
            });
        }

        if (!string.IsNullOrWhiteSpace(article.FooterJavaScript))
        {
            scripts.Add(new ScriptLoadingOrder
            {
                Source = "article-footer",
                Location = "body-end",
                Timing = "deferred",
            });
        }

        var contentInsertion = new ContentInsertion
        {
            Field = "content",
            Destination = article.TemplateId.HasValue ? "template-region" : "layout-region",
            Transformation = "html-sanitize", // Default safety transformation
        };

        return new RenderingContext
        {
            RenderingFlow = renderingFlow,
            ContentInsertion = contentInsertion,
            Placeholders = placeholders,
            ScriptLoadingOrder = scripts.Count > 0 ? scripts : null,
            Notes = notes.Count > 0 ? notes : null,
        };
    }

    /// <inheritdoc />
    public async Task<EditorContextBase> BuildEditorContextBaseAsync(
        EditorSurface surface,
        EditorKind kind,
        DocumentKind documentKind,
        string currentField,
        LanguageKind language,
        CancellationToken cancellationToken = default)
    {
        // Load article metadata for context (if editing an article)
        int? articleNumber = null;
        string? title = null;
        string? urlPath = null;

        if (kind == EditorKind.Article)
        {
            // Extract article number from context or request
            // This would be passed by the controller
            // For now, return a basic context
        }

        return new EditorContextBase
        {
            EditorSurface = surface,
            EditorKind = kind,
            DocumentKind = documentKind,
            ArticleNumber = articleNumber,
            CurrentField = currentField,
            Title = title,
            UrlPath = urlPath,
            Language = language,
            ReadOnly = false,
            AiEnabled = true,
        };
    }

    /// <inheritdoc />
    public async Task<KnowledgeContext> BuildKnowledgeContextAsync(
        DocumentKind documentKind,
        EditorKind editorKind,
        CancellationToken cancellationToken = default)
    {
        return await _knowledgeProvider.GetKnowledgeContextAsync(documentKind, editorKind, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ValidationContext> BuildValidationContextAsync(
        int? articleNumber = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement validation context builder
        // This would check for errors in the document (syntax, structure, etc.)
        return new ValidationContext
        {
            HasErrors = false,
            Errors = null,
            Warnings = null,
            ValidationStatus = new List<FieldValidationStatus>(),
        };
    }

    /// <summary>
    /// Truncates a string value if it exceeds the maximum field length.
    /// </summary>
    /// <param name="value">The value to potentially truncate.</param>
    /// <returns>The original value or truncated value with suffix if it exceeds max length.</returns>
    private string? TruncateIfNeeded(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= MaxFieldLength)
        {
            return value;
        }

        return value.Substring(0, MaxFieldLength) + TruncationSuffix;
    }

    /// <summary>
    /// Converts article status code to a displayable string.
    /// </summary>
    /// <param name="statusCode">The article status code (typically 1=Draft, 2=Published, etc.).</param>
    /// <returns>The status as a string ('draft', 'published', 'archived').</returns>
    private string ConvertArticleStatusToString(int statusCode)
    {
        // Status codes typically: 1 = Draft, 2 = Review, 3 = Published, 4 = Archived, 5 = Redirect
        return statusCode switch
        {
            1 => "draft",
            2 => "review",
            3 => "published",
            4 => "archived",
            5 => "redirect",
            _ => "unknown",
        };
    }

    /// <summary>
    /// Gets the article type as a displayable string.
    /// </summary>
    /// <param name="articleType">The article type code (nullable).</param>
    /// <returns>The article type as a string ('article', 'blog', etc.) or null.</returns>
    private string? GetArticleTypeString(int? articleType)
    {
        if (!articleType.HasValue)
        {
            return null;
        }

        // Article type codes typically: 1 = Article, 2 = Blog Post, etc.
        return articleType.Value switch
        {
            1 => "article",
            2 => "blog",
            3 => "page",
            _ => "unknown",
        };
    }

    /// <summary>
    /// Gets the template status as a displayable string.
    /// </summary>
    /// <param name="publishedDate">The published date (nullable).</param>
    /// <returns>The status as a string ('draft', 'published').</returns>
    private string GetTemplateStatus(DateTimeOffset? publishedDate)
    {
        return publishedDate.HasValue ? "published" : "draft";
    }

    /// <summary>
    /// Gets the composition type for a template based on PageType.
    /// </summary>
    /// <param name="pageType">The page type string (e.g., 'home', 'content').</param>
    /// <returns>The composition type ('wrapper', 'partial', 'composite', 'custom').</returns>
    private string GetCompositionType(string pageType)
    {
        return pageType.ToLowerInvariant() switch
        {
            "home" => "wrapper",
            "content" => "wrapper",
            "sidebar" => "partial",
            "widget" => "partial",
            "card" => "partial",
            _ => "custom",
        };
    }

    /// <summary>
    /// Extracts field definitions from template HTML using data-ccms-ceid markers.
    /// Markers format: &lt;div data-ccms-ceid="region-id"&gt;...&lt;/div&gt;
    /// </summary>
    /// <param name="templateContent">The template HTML content.</param>
    /// <returns>A list of TemplateField definitions.</returns>
    private List<TemplateField> ExtractTemplateFields(string templateContent)
    {
        var fields = new List<TemplateField>();

        // Look for data-ccms-ceid attributes: data-ccms-ceid="region-id"
        var pattern = "data-ccms-ceid=['\\\"]([^'\\\"]+)['\\\"]";
        var matches = Regex.Matches(templateContent, pattern, RegexOptions.IgnoreCase);

        var seenRegions = new HashSet<string>();

        foreach (Match match in matches)
        {
            var regionId = match.Groups[1].Value;

            // Avoid duplicates
            if (seenRegions.Contains(regionId))
            {
                continue;
            }

            seenRegions.Add(regionId);

            fields.Add(new TemplateField
            {
                FieldName = regionId,
                DataType = "html", // Template regions contain HTML
                Required = true,   // CCMS regions are required
                Description = $"Template region: {regionId}",
            });
        }

        // If no explicit regions found, create a default content field
        if (fields.Count == 0)
        {
            fields.Add(new TemplateField
            {
                FieldName = "content",
                DataType = "html",
                Required = true,
                Description = "Main content area",
            });
        }

        return fields;
    }

    /// <summary>
    /// Gets the layout status as a displayable string.
    /// </summary>
    /// <param name="publishedDate">The published date (nullable).</param>
    /// <returns>The status as a string ('draft', 'published').</returns>
    private string GetLayoutStatus(DateTimeOffset? publishedDate)
    {
        return publishedDate.HasValue ? "published" : "draft";
    }

    /// <summary>
    /// Extracts region definitions from layout HTML using CCMS markers.
    /// Markers format: <!--CCMS--START--REGIONNAME-->...<!--CCMS--END--REGIONNAME-->
    /// </summary>
    /// <param name="layout">The layout entity.</param>
    /// <returns>A list of LayoutRegion definitions.</returns>
    private List<LayoutRegion> ExtractLayoutRegions(Layout layout)
    {
        var regions = new List<LayoutRegion>();
        
        // Combine all layout HTML parts
        var allContent = (layout.Head ?? string.Empty) + "\n" 
                        + (layout.HtmlHeader ?? string.Empty) + "\n" 
                        + (layout.FooterHtmlContent ?? string.Empty);

        // Look for CCMS region markers: <!--CCMS--START--REGIONNAME-->
        var startPattern = @"<!--CCMS--START--([A-Za-z0-9_-]+)-->";
        var endPattern = @"<!--CCMS--END--([A-Za-z0-9_-]+)-->";

        var startMatches = Regex.Matches(allContent, startPattern, RegexOptions.IgnoreCase);
        var endMatches = Regex.Matches(allContent, endPattern, RegexOptions.IgnoreCase);

        // Build regions from start markers found
        foreach (Match match in startMatches)
        {
            var regionName = match.Groups[1].Value;
            var placeholder = $"<!--CCMS--START--{regionName}-->";

            // Check if corresponding end marker exists
            var hasEnd = endMatches.Cast<Match>()
                .Any(m => m.Groups[1].Value == regionName);

            regions.Add(new LayoutRegion
            {
                Name = regionName,
                Placeholder = placeholder,
                Description = $"Layout region: {regionName}",
                Required = true, // CCMS regions are required unless otherwise specified
            });
        }

        // If no explicit regions found, add default content region
        if (regions.Count == 0)
        {
            regions.Add(new LayoutRegion
            {
                Name = "Content",
                Placeholder = "<!-- [CONTENT] -->",
                Description = "Main content area",
                Required = true,
            });
        }

        return regions;
    }

    /// <summary>
    /// Extracts stylesheet information from layout HTML (naive implementation).
    /// </summary>
    /// <param name="layout">The layout entity.</param>
    /// <returns>A list of stylesheet references.</returns>
    private List<LayoutStylesheet> ExtractStylesheets(Layout layout)
    {
        var stylesheets = new List<LayoutStylesheet>();

        // Look for <link> tags in Head section
        var content = layout.Head ?? string.Empty;
        var linkPattern = @"<link\s+[^>]*href=['""]([^'""]+)['""][^>]*>";
        var matches = Regex.Matches(content, linkPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var url = match.Groups[1].Value;
            stylesheets.Add(new LayoutStylesheet
            {
                Url = url,
                Inline = false,
            });
        }

        return stylesheets;
    }

    /// <summary>
    /// Extracts script information from layout HTML (naive implementation).
    /// </summary>
    /// <param name="layout">The layout entity.</param>
    /// <returns>A list of script references.</returns>
    private List<LayoutScript> ExtractScripts(Layout layout)
    {
        var scripts = new List<LayoutScript>();

        // Check Head section for scripts
        var headContent = layout.Head ?? string.Empty;
        var headScriptPattern = @"<script\s+[^>]*src=['""]([^'""]+)['""][^>]*>";
        var headMatches = Regex.Matches(headContent, headScriptPattern, RegexOptions.IgnoreCase);

        foreach (Match match in headMatches)
        {
            var url = match.Groups[1].Value;
            scripts.Add(new LayoutScript
            {
                Url = url,
                Inline = false,
                Location = "head",
            });
        }

        // Check Footer section for scripts
        var footerContent = layout.FooterHtmlContent ?? string.Empty;
        var footerScriptPattern = @"<script\s+[^>]*src=['""]([^'""]+)['""][^>]*>";
        var footerMatches = Regex.Matches(footerContent, footerScriptPattern, RegexOptions.IgnoreCase);

        foreach (Match match in footerMatches)
        {
            var url = match.Groups[1].Value;
            scripts.Add(new LayoutScript
            {
                Url = url,
                Inline = false,
                Location = "body-end",
            });
        }

        return scripts;
    }
}
