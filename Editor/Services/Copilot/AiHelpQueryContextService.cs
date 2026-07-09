// <copyright file="AiHelpQueryContextService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Assembles docs and source-code context for help chat prompts.
/// </summary>
public sealed class AiHelpQueryContextService : IAiHelpQueryContextService
{
    private const int MaxContextChars = 12000;
    private const int ContextAssemblyTargetMs = 200;

    /// <summary>
    /// Marker appended when context text exceeds the configured budget.
    /// </summary>
    private const string TruncationMarker = "\n\n... (help query context truncated to token budget)";

    private readonly IAiDocumentationContextService documentationContextService;
    private readonly IAiSourceCodeIndexService sourceCodeIndexService;
    private readonly IAiFaqIndexService faqIndexService;
    private readonly ILogger<AiHelpQueryContextService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiHelpQueryContextService"/> class.
    /// </summary>
    /// <param name="documentationContextService">Documentation context service.</param>
    /// <param name="sourceCodeIndexService">Source code index service.</param>
    /// <param name="faqIndexService">FAQ index service.</param>
    /// <param name="logger">Logger.</param>
    public AiHelpQueryContextService(
        IAiDocumentationContextService documentationContextService,
        IAiSourceCodeIndexService sourceCodeIndexService,
        IAiFaqIndexService faqIndexService,
        ILogger<AiHelpQueryContextService> logger)
    {
        this.documentationContextService = documentationContextService;
        this.sourceCodeIndexService = sourceCodeIndexService;
        this.faqIndexService = faqIndexService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<AiHelpQueryContextResult> BuildContextAsync(AiHelpQueryContextRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var overallStopwatch = Stopwatch.StartNew();

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

        var docsTask = MeasureAsync(() => this.documentationContextService.GetDocumentationContextAsync(enrichmentRequest, cancellationToken));
        var sourceTask = MeasureAsync(() => this.sourceCodeIndexService.SearchSourceCodeAsync(request.Query, cancellationToken));
        var faqTask = MeasureAsync(() => this.faqIndexService.SearchFaqAsync(request.Query, cancellationToken));

        await Task.WhenAll(docsTask, sourceTask, faqTask);

        var docsMeasured = docsTask.Result;
        var sourceMeasured = sourceTask.Result;
        var faqMeasured = faqTask.Result;

        var docsContext = docsMeasured.result.ContextText;
        var sourceResults = sourceMeasured.result;
        var faqResults = faqMeasured.result;

        var sources = BuildSourceAttributions(docsContext, sourceResults, faqResults);
        var tokenEstimateStopwatch = Stopwatch.StartNew();
        var contextText = BuildContextText(docsContext, sourceResults, faqResults);
        var estimatedTokens = Math.Max(1, (int)Math.Ceiling(contextText.Length / 4.0d));
        tokenEstimateStopwatch.Stop();

        if (contextText.Length > MaxContextChars)
        {
            contextText = TruncateContext(contextText);
        }

        overallStopwatch.Stop();
        this.logger.LogInformation(
            "Help context assembly profile: totalMs={TotalMs}; docsMs={DocsMs}; sourceMs={SourceMs}; faqMs={FaqMs}; tokenEstimationMs={TokenEstimationMs}; contextChars={ContextChars}; estimatedTokens={EstimatedTokens}; sourceCount={SourceCount}",
            Math.Round(overallStopwatch.Elapsed.TotalMilliseconds, 2),
            Math.Round(docsMeasured.elapsed.TotalMilliseconds, 2),
            Math.Round(sourceMeasured.elapsed.TotalMilliseconds, 2),
            Math.Round(faqMeasured.elapsed.TotalMilliseconds, 2),
            Math.Round(tokenEstimateStopwatch.Elapsed.TotalMilliseconds, 2),
            contextText.Length,
            estimatedTokens,
            sources.Count);

        if (overallStopwatch.Elapsed.TotalMilliseconds > ContextAssemblyTargetMs)
        {
            this.logger.LogWarning(
                "Help context assembly exceeded target: targetMs={TargetMs}; actualMs={ActualMs}; docsMs={DocsMs}; sourceMs={SourceMs}; faqMs={FaqMs}",
                ContextAssemblyTargetMs,
                Math.Round(overallStopwatch.Elapsed.TotalMilliseconds, 2),
                Math.Round(docsMeasured.elapsed.TotalMilliseconds, 2),
                Math.Round(sourceMeasured.elapsed.TotalMilliseconds, 2),
                Math.Round(faqMeasured.elapsed.TotalMilliseconds, 2));
        }

        return new AiHelpQueryContextResult
        {
            ContextText = contextText,
            Sources = sources,
        };
    }

    private static async Task<MeasuredResult<T>> MeasureAsync<T>(Func<Task<T>> func)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await func();
        stopwatch.Stop();
        return new MeasuredResult<T>(result, stopwatch.Elapsed);
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

    private sealed record MeasuredResult<T>(T result, TimeSpan elapsed);
}
