// <copyright file="AiHelpQueryContextService.cs" company="Moonrise Software, LLC">
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Assembles docs and source-code context for help chat prompts.
/// </summary>
public sealed class AiHelpQueryContextService : IAiHelpQueryContextService
{
    private const int MaxContextChars = 12000;

    /// <summary>
    /// Marker appended when context text exceeds the configured budget.
    /// </summary>
    private const string TruncationMarker = "\n\n... (help query context truncated to token budget)";

    private readonly IAiDocumentationContextService documentationContextService;
    private readonly IAiSourceCodeIndexService sourceCodeIndexService;
    private readonly IAiFaqIndexService faqIndexService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiHelpQueryContextService"/> class.
    /// </summary>
    /// <param name="documentationContextService">Documentation context service.</param>
    /// <param name="sourceCodeIndexService">Source code index service.</param>
    /// <param name="faqIndexService">FAQ index service.</param>
    public AiHelpQueryContextService(
        IAiDocumentationContextService documentationContextService,
        IAiSourceCodeIndexService sourceCodeIndexService,
        IAiFaqIndexService faqIndexService)
    {
        this.documentationContextService = documentationContextService;
        this.sourceCodeIndexService = sourceCodeIndexService;
        this.faqIndexService = faqIndexService;
    }

    /// <inheritdoc />
    public async Task<AiHelpQueryContextResult> BuildContextAsync(AiHelpQueryContextRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new AiHelpQueryContextResult();
        }

        var enrichmentRequest = new AiContextEnrichmentRequest
        {
            DocumentKind = request.DocumentKind,
            SectionKind = request.SectionKind,
            Message = request.Query,
            ArticleNumber = request.ArticleNumber,
            TemplateId = request.TemplateId,
            LayoutId = request.LayoutId,
            UrlPath = request.UrlPath,
        };

        var docsTask = this.documentationContextService.GetDocumentationContextAsync(enrichmentRequest, cancellationToken);
        var sourceTask = this.sourceCodeIndexService.SearchSourceCodeAsync(request.Query, cancellationToken);
        var faqTask = this.faqIndexService.SearchFaqAsync(request.Query, cancellationToken);

        await Task.WhenAll(docsTask, sourceTask, faqTask);

        var docsContext = docsTask.Result.ContextText;
        var sourceResults = sourceTask.Result;
        var faqResults = faqTask.Result;

        var sources = BuildSourceAttributions(docsContext, sourceResults, faqResults);
        var contextText = BuildContextText(docsContext, sourceResults, faqResults);

        if (contextText.Length > MaxContextChars)
        {
            contextText = TruncateContext(contextText);
        }

        return new AiHelpQueryContextResult
        {
            ContextText = contextText,
            Sources = sources,
        };
    }

    private static string BuildContextText(string docsContext, IReadOnlyList<AiSourceCodeSearchResult> sourceResults, IReadOnlyList<AiFaqMatch> faqResults)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(docsContext))
        {
            sb.AppendLine(docsContext.Trim());
        }

        if (sourceResults.Count > 0)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine("Source code context from SkyCMS repository:");
            foreach (var result in sourceResults)
            {
                sb.AppendLine($"- {result.SymbolName ?? result.FilePath} (Relevance: {result.RelevanceScore})");
                if (!string.IsNullOrWhiteSpace(result.Signature))
                {
                    sb.AppendLine($"  Signature: {TrimTail(result.Signature, 800)}");
                }

                if (!string.IsNullOrWhiteSpace(result.Snippet))
                {
                    sb.AppendLine($"  Snippet: {TrimTail(result.Snippet, 1200)}");
                }

                if (!string.IsNullOrWhiteSpace(result.GitHubUrl))
                {
                    sb.AppendLine($"  Source: {result.GitHubUrl}");
                }
            }
        }

        if (faqResults.Count > 0)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine("FAQ context from SkyCMS documentation:");
            foreach (var faq in faqResults)
            {
                sb.AppendLine($"Q: {faq.Question}");
                sb.AppendLine($"A: {TrimTail(faq.Answer, 600)}");
                if (!string.IsNullOrWhiteSpace(faq.SourceUrl))
                {
                    sb.AppendLine($"Source: {faq.SourceUrl}");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString().Trim();
    }

    private static List<AiHelpSourceAttribution> BuildSourceAttributions(string docsContext, IReadOnlyList<AiSourceCodeSearchResult> sourceResults, IReadOnlyList<AiFaqMatch> faqResults)
    {
        var docsSources = ExtractUrls(docsContext)
            .Select(url => new AiHelpSourceAttribution
            {
                SourceType = "docs",
                Title = url,
                Url = url,
            });

        var codeSources = sourceResults
            .Where(result => !string.IsNullOrWhiteSpace(result.GitHubUrl))
            .Select(result => new AiHelpSourceAttribution
            {
                SourceType = "code",
                Title = string.IsNullOrWhiteSpace(result.SymbolName) ? result.FilePath : result.SymbolName,
                Url = result.GitHubUrl,
                RelevanceScore = result.RelevanceScore,
            });

        var faqSources = faqResults
            .Where(faq => !string.IsNullOrWhiteSpace(faq.SourceUrl))
            .Select(faq => new AiHelpSourceAttribution
            {
                SourceType = "faq",
                Title = faq.Question,
                Url = faq.SourceUrl,
                RelevanceScore = faq.RelevanceScore,
            });

        return docsSources
            .Concat(codeSources)
            .Concat(faqSources)
            .GroupBy(source => source.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(10)
            .ToList();
    }

    private static List<string> ExtractUrls(string docsContext)
    {
        if (string.IsNullOrWhiteSpace(docsContext))
        {
            return [];
        }

        var matches = Regex.Matches(docsContext, @"https?://[^\s\)]+", RegexOptions.IgnoreCase);
        return matches
            .Select(match => match.Value.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string TrimTail(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength];
    }

    private static string TruncateContext(string text)
    {
        var contentBudget = MaxContextChars - TruncationMarker.Length;
        if (contentBudget <= 0)
        {
            return TruncationMarker.TrimStart('\n');
        }

        var cutIndex = text.LastIndexOf('\n', contentBudget);
        if (cutIndex < contentBudget / 2)
        {
            cutIndex = contentBudget;
        }

        return text[..cutIndex].TrimEnd() + TruncationMarker;
    }
}
