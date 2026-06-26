// <copyright file="AiSourceCodeIndexService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Local repository source code index for help queries.
/// </summary>
public sealed class AiSourceCodeIndexService : IAiSourceCodeIndexService
{
    private const int MaxResults = 3;
    private const int EmbeddingCandidateLimit = 8;
    private const int MaxSnippetLength = 700;
    private static readonly object HealthGate = new();

    private static DateTimeOffset? lastSuccessfulRefreshUtc;
    private static DateTimeOffset? lastAttemptUtc;
    private static DateTimeOffset? lastFetchErrorUtc;
    private static DateTimeOffset? lastParseErrorUtc;
    private static int lastIndexedEntryCount;
    private static string? lastFetchError;
    private static string? lastParseError;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "it", "in", "of", "to", "and", "or", "for",
        "on", "at", "by", "with", "do", "be", "as", "up", "my", "we", "i",
        "can", "how", "what", "when", "where", "why", "who", "this", "that",
    };

    private readonly IHostEnvironment hostEnvironment;
    private readonly IAiEmbeddingSemanticRanker embeddingSemanticRanker;
    private readonly ILogger<AiSourceCodeIndexService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiSourceCodeIndexService"/> class.
    /// </summary>
    /// <param name="hostEnvironment">Host environment used to locate the repository root.</param>
    /// <param name="logger">Logger.</param>
    public AiSourceCodeIndexService(IHostEnvironment hostEnvironment, IAiEmbeddingSemanticRanker embeddingSemanticRanker, ILogger<AiSourceCodeIndexService> logger)
    {
        this.hostEnvironment = hostEnvironment;
        this.embeddingSemanticRanker = embeddingSemanticRanker;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiSourceCodeSearchResult>> SearchSourceCodeAsync(string query, CancellationToken cancellationToken = default)
    {
        RecordAttempt();

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var repositoryRoot = ResolveRepositoryRoot(this.hostEnvironment.ContentRootPath);
        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot))
        {
            RecordFetchError("Repository root could not be resolved or does not exist.");
            this.logger.LogWarning("Source-code index fetch error: repository root was not found. ContentRootPath={ContentRootPath}", this.hostEnvironment.ContentRootPath);
            return [];
        }

        var keywords = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => Regex.Replace(word, "[^a-zA-Z0-9]", string.Empty))
            .Where(word => word.Length > 2 && !StopWords.Contains(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywords.Count == 0)
        {
            return [];
        }

        var matches = new List<AiSourceCodeSearchResult>();
        IEnumerable<string> filePaths;
        try
        {
            filePaths = Directory.EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            RecordFetchError(ex.Message);
            this.logger.LogWarning(ex, "Source-code index fetch error while enumerating files under {RepositoryRoot}.", repositoryRoot);
            return [];
        }

        var processedFiles = 0;
        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(repositoryRoot, filePath);
            if (IsIgnoredPath(relativePath))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                RecordParseError(ex.Message);
                this.logger.LogWarning(ex, "Source-code index parse error while reading {FilePath}.", relativePath);
                continue;
            }

            processedFiles++;
            var keywordScore = keywords.Sum(keyword => CountOccurrences(text, keyword))
                + keywords.Sum(keyword => CountOccurrences(relativePath, keyword));

            var semanticSearchable = $"{relativePath}\n{ExtractSymbolName(text)}\n{ExtractSignature(text)}\n{ExtractSnippet(text, keywords)}";
            var semanticScore = ComputeSemanticSimilarity(query, semanticSearchable);
            var score = keywordScore + (int)Math.Round(semanticScore * 8, MidpointRounding.AwayFromZero);

            if (score <= 0)
            {
                continue;
            }

            var symbolName = ExtractSymbolName(text);
            var signature = ExtractSignature(text);
            var snippet = ExtractSnippet(text, keywords);

            matches.Add(new AiSourceCodeSearchResult
            {
                FilePath = relativePath,
                SymbolName = symbolName,
                Signature = signature,
                Snippet = snippet,
                GitHubUrl = BuildGitHubUrl(relativePath),
                RelevanceScore = score,
            });
        }

        var topCandidates = matches
            .OrderByDescending(match => match.RelevanceScore)
            .ThenBy(match => match.FilePath, StringComparer.OrdinalIgnoreCase)
            .Take(EmbeddingCandidateLimit)
            .ToList();

        if (topCandidates.Count > 0)
        {
            var embeddingScores = await this.embeddingSemanticRanker
                .ScoreAsync(query, topCandidates.Select(c => $"{c.FilePath}\n{c.SymbolName}\n{c.Signature}\n{c.Snippet}").ToList(), cancellationToken)
                .ConfigureAwait(false);

            if (embeddingScores.Count > 0)
            {
                var scoreByIndex = embeddingScores.ToDictionary(x => x.CandidateIndex, x => x.Score);
                for (var index = 0; index < topCandidates.Count; index++)
                {
                    if (!scoreByIndex.TryGetValue(index, out var embeddingScore))
                    {
                        continue;
                    }

                    topCandidates[index].RelevanceScore += (int)Math.Round(embeddingScore * 12, MidpointRounding.AwayFromZero);
                }
            }
        }

        var topResults = topCandidates
            .OrderByDescending(match => match.RelevanceScore)
            .ThenBy(match => match.FilePath, StringComparer.OrdinalIgnoreCase)
            .Take(MaxResults)
            .ToList();

        if (topResults.Count == 0)
        {
            this.logger.LogInformation("No source-code matches found for query {Query}.", query);
        }

        RecordSuccess(processedFiles);

        return topResults;
    }

    private static double ComputeSemanticSimilarity(string query, string content)
    {
        try
        {
            var queryVector = BuildTermVector(query);
            var contentVector = BuildTermVector(content);
            if (queryVector.Count == 0 || contentVector.Count == 0)
            {
                return 0;
            }

            var dot = 0d;
            foreach (var term in queryVector)
            {
                if (contentVector.TryGetValue(term.Key, out var contentWeight))
                {
                    dot += term.Value * contentWeight;
                }
            }

            if (dot <= 0)
            {
                return 0;
            }

            var queryNorm = Math.Sqrt(queryVector.Values.Sum(v => v * v));
            var contentNorm = Math.Sqrt(contentVector.Values.Sum(v => v * v));
            if (queryNorm <= 0 || contentNorm <= 0)
            {
                return 0;
            }

            return dot / (queryNorm * contentNorm);
        }
        catch
        {
            // Fallback to keyword-only ranking if semantic scoring fails.
            return 0;
        }
    }

    private static Dictionary<string, double> BuildTermVector(string text)
    {
        var tokens = Regex.Matches(text ?? string.Empty, "[A-Za-z0-9]+")
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Length > 2 && !StopWords.Contains(token))
            .ToList();

        var total = tokens.Count;
        if (total == 0)
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in tokens.GroupBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            vector[group.Key] = (double)group.Count() / total;
        }

        return vector;
    }

    /// <summary>
    /// Returns source-code index freshness and health metadata.
    /// </summary>
    /// <returns>Index health snapshot.</returns>
    public static AiIndexHealthSnapshot GetHealthSnapshot()
    {
        lock (HealthGate)
        {
            return new AiIndexHealthSnapshot
            {
                IndexName = "source-code",
                LastSuccessfulRefreshUtc = lastSuccessfulRefreshUtc,
                LastAttemptUtc = lastAttemptUtc,
                LastIndexedEntryCount = lastIndexedEntryCount,
                LastFetchError = lastFetchError,
                LastFetchErrorUtc = lastFetchErrorUtc,
                LastParseError = lastParseError,
                LastParseErrorUtc = lastParseErrorUtc,
            };
        }
    }

    private static void RecordAttempt()
    {
        lock (HealthGate)
        {
            lastAttemptUtc = DateTimeOffset.UtcNow;
        }
    }

    private static void RecordSuccess(int indexedEntryCount)
    {
        lock (HealthGate)
        {
            lastSuccessfulRefreshUtc = DateTimeOffset.UtcNow;
            lastIndexedEntryCount = indexedEntryCount;
            lastFetchError = null;
            lastFetchErrorUtc = null;
            lastParseError = null;
            lastParseErrorUtc = null;
        }
    }

    private static void RecordFetchError(string message)
    {
        lock (HealthGate)
        {
            lastFetchError = message;
            lastFetchErrorUtc = DateTimeOffset.UtcNow;
        }
    }

    private static void RecordParseError(string message)
    {
        lock (HealthGate)
        {
            lastParseError = message;
            lastParseErrorUtc = DateTimeOffset.UtcNow;
        }
    }

    private static string ResolveRepositoryRoot(string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            return string.Empty;
        }

        var parent = Directory.GetParent(contentRootPath);
        return parent?.FullName ?? contentRootPath;
    }

    private static bool IsIgnoredPath(string relativePath)
    {
        return relativePath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("bin\\", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("obj\\", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("\\.git\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractSymbolName(string text)
    {
        var classMatch = Regex.Match(text, @"\b(class|interface|record|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)");
        return classMatch.Success ? classMatch.Groups["name"].Value : string.Empty;
    }

    private static string ExtractSignature(string text)
    {
        var signatureMatch = Regex.Match(text, @"(?:public|internal|protected|private)?\s*(?:static\s+)?(?:async\s+)?[A-Za-z0-9_<>,\[\]?\s]+\s+[A-Za-z_][A-Za-z0-9_]*\s*\([^\)]*\)");
        return signatureMatch.Success ? signatureMatch.Value.Trim() : string.Empty;
    }

    private static string ExtractSnippet(string text, IEnumerable<string> keywords)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var matchLineIndex = Array.FindIndex(lines, line => keywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        if (matchLineIndex < 0)
        {
            return text.Length > MaxSnippetLength ? text[..MaxSnippetLength] : text;
        }

        var start = Math.Max(0, matchLineIndex - 2);
        var endInclusive = Math.Min(lines.Length - 1, matchLineIndex + 4);
        var endExclusive = endInclusive + 1;
        var snippet = string.Join(Environment.NewLine, lines[start..endExclusive]);
        return snippet.Length > MaxSnippetLength ? snippet[..MaxSnippetLength] : snippet;
    }

    private static int CountOccurrences(string text, string keyword)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += keyword.Length;
        }

        return count;
    }

    private static string BuildGitHubUrl(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return $"https://github.com/CWALabs/SkyCMS/blob/main/{normalized}";
    }
}