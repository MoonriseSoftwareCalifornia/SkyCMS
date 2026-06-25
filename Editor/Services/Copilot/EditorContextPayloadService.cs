// <copyright file="EditorContextPayloadService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sky.Cms.Api.Shared.Services.EditorContext;

/// <summary>
/// Orchestrates typed editor context builders into prompt-ready payload blocks.
/// </summary>
public sealed class EditorContextPayloadService : IEditorContextPayloadService
{
    // Rough 4k-token budget approximation (4 chars/token).
    private const int MaxPayloadChars = 16_000;
    private const string TruncationMarker = "\n\n... (context payload truncated to token budget)";

    private readonly IEditorContextBuilder editorContextBuilder;
    private readonly ILogger<EditorContextPayloadService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorContextPayloadService"/> class.
    /// </summary>
    /// <param name="editorContextBuilder">Editor context builder.</param>
    /// <param name="logger">Logger.</param>
    public EditorContextPayloadService(
        IEditorContextBuilder editorContextBuilder,
        ILogger<EditorContextPayloadService> logger)
    {
        this.editorContextBuilder = editorContextBuilder;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> BuildPayloadAsync(EditorContextPayloadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var editorKind = ResolveEditorKind(request.EditorSurface, request.DocumentKind, request.SectionKind);
        var documentKind = ResolveDocumentKind(request.DocumentKind, request.SectionKind, request.Language);
        var language = ResolveLanguageKind(request.Language, documentKind);
        var surface = ResolveEditorSurface(request.EditorSurface, request.SectionKind);
        var currentField = string.IsNullOrWhiteSpace(request.CurrentField) ? "Content" : request.CurrentField;
        var defaultKnowledgeRuleLimit = request.Lightweight ? 3 : 6;
        var defaultKnowledgeDocLimit = request.Lightweight ? 2 : 4;
        var compactKnowledgeRuleLimit = request.Lightweight ? 2 : 3;
        var compactKnowledgeDocLimit = 1;

        string baseSection = string.Empty;
        string entitySection = string.Empty;
        string knowledgeSection = string.Empty;
        string compactKnowledgeSection = string.Empty;
        string validationSection = string.Empty;
        var usedCompactEntityFallback = false;

        try
        {
            var baseContext = await this.editorContextBuilder.BuildEditorContextBaseAsync(
                surface,
                editorKind,
                documentKind,
                currentField,
                language,
                cancellationToken);

            baseContext.ArticleNumber = ParseNullableInt(request.ArticleNumber);
            baseContext.LayoutId = request.LayoutId;
            baseContext.TemplateId = request.TemplateId;
            baseContext.Title = request.Title;
            baseContext.UrlPath = request.UrlPath;
            baseContext.CurrentFieldValue = request.CurrentFieldValue;
            if (!string.IsNullOrWhiteSpace(request.Selection))
            {
                baseContext.CurrentSelection = new SelectionRange
                {
                    Start = 0,
                    End = request.Selection.Length,
                    Text = request.Selection,
                };
            }

            baseSection = BuildBaseContextSection(baseContext);
        }
        catch (Exception ex)
        {
            this.logger.LogDebug(ex, "Failed to build editor base context payload.");
        }

        if (!request.Lightweight)
        {
            entitySection = await BuildEntityContextSectionAsync(request, editorKind, cancellationToken);
        }
        else
        {
            entitySection = BuildLightweightEntitySummarySection(request, editorKind);
        }

        try
        {
            var knowledge = await this.editorContextBuilder.BuildKnowledgeContextAsync(documentKind, editorKind, cancellationToken);
            knowledgeSection = BuildKnowledgeContextSection(knowledge, defaultKnowledgeRuleLimit, defaultKnowledgeDocLimit);
            compactKnowledgeSection = BuildKnowledgeContextSection(knowledge, compactKnowledgeRuleLimit, compactKnowledgeDocLimit);
        }
        catch (Exception ex)
        {
            this.logger.LogDebug(ex, "Failed to build knowledge context payload.");
        }

        try
        {
            var validation = await this.editorContextBuilder.BuildValidationContextAsync(ParseNullableInt(request.ArticleNumber), cancellationToken);
            validationSection = BuildValidationContextSection(validation);
        }
        catch (Exception ex)
        {
            this.logger.LogDebug(ex, "Failed to build validation context payload.");
        }

        var payload = ComposeSections(baseSection, entitySection, knowledgeSection, validationSection);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        if (payload.Length > MaxPayloadChars && !string.IsNullOrWhiteSpace(validationSection))
        {
            payload = ComposeSections(baseSection, entitySection, knowledgeSection, string.Empty);
        }

        if (payload.Length > MaxPayloadChars && !string.IsNullOrWhiteSpace(compactKnowledgeSection))
        {
            payload = ComposeSections(baseSection, entitySection, compactKnowledgeSection, string.Empty);
        }

        if (payload.Length > MaxPayloadChars && !request.Lightweight)
        {
            entitySection = BuildLightweightEntitySummarySection(request, editorKind);
            payload = ComposeSections(baseSection, entitySection, compactKnowledgeSection, string.Empty);
            usedCompactEntityFallback = true;
        }

        if (payload.Length > MaxPayloadChars)
        {
            payload = TruncateToBudget(payload);
        }

        if (usedCompactEntityFallback && !payload.Contains("Entity summary (lightweight):", StringComparison.Ordinal))
        {
            payload = ComposeSections(baseSection, BuildLightweightEntitySummarySection(request, editorKind), compactKnowledgeSection, string.Empty);
            if (payload.Length > MaxPayloadChars)
            {
                payload = TruncateToBudget(payload);
            }
        }

        return payload;
    }

    private static string ComposeSections(params string[] sections)
    {
        var sb = new StringBuilder();

        foreach (var section in sections)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine(section.Trim());
        }

        return sb.ToString().Trim();
    }

    private static string TruncateToBudget(string payload)
    {
        if (payload.Length <= MaxPayloadChars)
        {
            return payload;
        }

        var contentBudget = MaxPayloadChars - TruncationMarker.Length;
        if (contentBudget <= 0)
        {
            return TruncationMarker.TrimStart('\n');
        }

        var cutIndex = payload.LastIndexOf('\n', contentBudget);
        if (cutIndex < contentBudget / 2)
        {
            cutIndex = contentBudget;
        }

        return payload[..cutIndex].TrimEnd() + TruncationMarker;
    }

    private static string BuildBaseContextSection(EditorContextBase baseContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Editor context payload:");
        sb.AppendLine($"- Surface: {baseContext.EditorSurface}");
        sb.AppendLine($"- EditorKind: {baseContext.EditorKind}");
        sb.AppendLine($"- DocumentKind: {baseContext.DocumentKind}");
        sb.AppendLine($"- CurrentField: {baseContext.CurrentField}");
        sb.AppendLine($"- Language: {baseContext.Language}");

        if (baseContext.ArticleNumber.HasValue)
        {
            sb.AppendLine($"- ArticleNumber: {baseContext.ArticleNumber.Value}");
        }

        if (!string.IsNullOrWhiteSpace(baseContext.LayoutId))
        {
            sb.AppendLine($"- LayoutId: {baseContext.LayoutId}");
        }

        if (!string.IsNullOrWhiteSpace(baseContext.TemplateId))
        {
            sb.AppendLine($"- TemplateId: {baseContext.TemplateId}");
        }

        if (!string.IsNullOrWhiteSpace(baseContext.Title))
        {
            sb.AppendLine($"- Title: {baseContext.Title}");
        }

        if (!string.IsNullOrWhiteSpace(baseContext.UrlPath))
        {
            sb.AppendLine($"- UrlPath: {baseContext.UrlPath}");
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<string> BuildEntityContextSectionAsync(EditorContextPayloadRequest request, EditorKind editorKind, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        switch (editorKind)
        {
            case EditorKind.Article:
                if (int.TryParse(request.ArticleNumber, out var articleNumber))
                {
                    var article = await this.editorContextBuilder.BuildArticleContextAsync(articleNumber, cancellationToken);
                    sb.AppendLine();
                    sb.AppendLine("Article entity context:");
                    sb.AppendLine($"- Title: {article.Title}");
                    sb.AppendLine($"- UrlPath: {article.UrlPath}");
                    sb.AppendLine($"- Status: {article.Status}");
                    sb.AppendLine($"- Version: {article.Version}");
                    sb.AppendLine($"- TemplateId: {article.TemplateId ?? "(none)"}");
                }

                break;
            case EditorKind.Layout:
                if (!string.IsNullOrWhiteSpace(request.LayoutId))
                {
                    var layout = await this.editorContextBuilder.BuildLayoutContextAsync(request.LayoutId, cancellationToken);
                    sb.AppendLine();
                    sb.AppendLine("Layout entity context:");
                    sb.AppendLine($"- Name: {layout.Name}");
                    sb.AppendLine($"- Regions: {layout.Regions.Count}");
                    sb.AppendLine($"- Version: {layout.Version}");
                }

                break;
            case EditorKind.Template:
                if (!string.IsNullOrWhiteSpace(request.TemplateId))
                {
                    var template = await this.editorContextBuilder.BuildTemplateContextAsync(request.TemplateId, cancellationToken);
                    sb.AppendLine();
                    sb.AppendLine("Template entity context:");
                    sb.AppendLine($"- Name: {template.Name}");
                    sb.AppendLine($"- Fields: {template.ExpectedFields.Count}");
                    sb.AppendLine($"- CompositionType: {template.CompositionType}");
                }

                break;
            default:
                break;
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildLightweightEntitySummarySection(EditorContextPayloadRequest request, EditorKind editorKind)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("Entity summary (lightweight):");
        sb.AppendLine($"- EditorKind: {editorKind}");

        if (!string.IsNullOrWhiteSpace(request.ArticleNumber))
        {
            sb.AppendLine($"- ArticleNumber: {request.ArticleNumber}");
        }

        if (!string.IsNullOrWhiteSpace(request.LayoutId))
        {
            sb.AppendLine($"- LayoutId: {request.LayoutId}");
        }

        if (!string.IsNullOrWhiteSpace(request.TemplateId))
        {
            sb.AppendLine($"- TemplateId: {request.TemplateId}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildKnowledgeContextSection(KnowledgeContext knowledge, int maxItems, int maxDocs)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("Knowledge constraints:");

        foreach (var rule in (knowledge.PreservationRules ?? []).Take(maxItems))
        {
            sb.AppendLine($"- Preserve: {rule}");
        }

        foreach (var antiPattern in (knowledge.AntiPatterns ?? []).Take(maxItems))
        {
            sb.AppendLine($"- Avoid: {antiPattern}");
        }

        if (knowledge.RelevantDocumentation?.Count > 0)
        {
            sb.AppendLine("Documentation references:");
            foreach (var doc in knowledge.RelevantDocumentation.Take(maxDocs))
            {
                sb.AppendLine($"- {doc.Title}: {doc.Url}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildValidationContextSection(ValidationContext validation)
    {
        if ((validation.Errors?.Count ?? 0) == 0 && (validation.Warnings?.Count ?? 0) == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("Validation context:");
        foreach (var error in validation.Errors ?? [])
        {
            sb.AppendLine($"- Error ({error.Field}): {error.Message}");
        }

        foreach (var warning in validation.Warnings ?? [])
        {
            sb.AppendLine($"- Warning ({warning.Field}): {warning.Message}");
        }

        return sb.ToString().TrimEnd();
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static EditorSurface ResolveEditorSurface(string? editorSurface, string? sectionKind)
    {
        if (string.Equals(editorSurface, "help", StringComparison.OrdinalIgnoreCase))
        {
            return EditorSurface.Help;
        }

        if (string.Equals(editorSurface, "ckeditor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionKind, "article-content", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionKind, "blog-content", StringComparison.OrdinalIgnoreCase))
        {
            return EditorSurface.CKEditor;
        }

        return EditorSurface.Monaco;
    }

    private static EditorKind ResolveEditorKind(string? editorSurface, string? documentKind, string? sectionKind)
    {
        if (string.Equals(editorSurface, "help", StringComparison.OrdinalIgnoreCase))
        {
            return EditorKind.Settings;
        }

        if (string.Equals(documentKind, "layout", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(sectionKind) && sectionKind.StartsWith("layout-", StringComparison.OrdinalIgnoreCase)))
        {
            return EditorKind.Layout;
        }

        if (string.Equals(documentKind, "template", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(sectionKind) && sectionKind.StartsWith("template-", StringComparison.OrdinalIgnoreCase)))
        {
            return EditorKind.Template;
        }

        if (string.Equals(documentKind, "blog", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionKind, "blog-content", StringComparison.OrdinalIgnoreCase))
        {
            return EditorKind.Blog;
        }

        return EditorKind.Article;
    }

    private static DocumentKind ResolveDocumentKind(string? documentKind, string? sectionKind, string? language)
    {
        if (Enum.TryParse<DocumentKind>(documentKind ?? string.Empty, true, out var parsedDocKind))
        {
            return parsedDocKind;
        }

        if (!string.IsNullOrWhiteSpace(sectionKind) && sectionKind.StartsWith("layout-", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentKind.Layout;
        }

        if (!string.IsNullOrWhiteSpace(sectionKind) && sectionKind.StartsWith("template-", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentKind.Template;
        }

        return language?.ToLowerInvariant() switch
        {
            "html" => DocumentKind.Html,
            "css" => DocumentKind.Css,
            "javascript" or "js" => DocumentKind.JavaScript,
            "typescript" or "ts" => DocumentKind.TypeScript,
            "razor" => DocumentKind.Razor,
            "json" => DocumentKind.Json,
            "xml" => DocumentKind.Xml,
            "markdown" or "md" => DocumentKind.Markdown,
            _ => DocumentKind.Article,
        };
    }

    private static LanguageKind ResolveLanguageKind(string? language, DocumentKind documentKind)
    {
        if (Enum.TryParse<LanguageKind>(language ?? string.Empty, true, out var parsedLanguageKind))
        {
            return parsedLanguageKind;
        }

        return documentKind switch
        {
            DocumentKind.Html => LanguageKind.Html,
            DocumentKind.Css => LanguageKind.Css,
            DocumentKind.JavaScript => LanguageKind.JavaScript,
            DocumentKind.TypeScript => LanguageKind.TypeScript,
            DocumentKind.Razor => LanguageKind.Razor,
            DocumentKind.Json => LanguageKind.Json,
            DocumentKind.Xml => LanguageKind.Xml,
            DocumentKind.Markdown => LanguageKind.Markdown,
            _ => LanguageKind.Markdown,
        };
    }
}