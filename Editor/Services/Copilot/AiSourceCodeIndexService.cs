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
    private const int MaxSnippetLength = 700;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "it", "in", "of", "to", "and", "or", "for",
        "on", "at", "by", "with", "do", "be", "as", "up", "my", "we", "i",
        "can", "how", "what", "when", "where", "why", "who", "this", "that",
    };

    private readonly IHostEnvironment hostEnvironment;
    private readonly ILogger<AiSourceCodeIndexService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiSourceCodeIndexService"/> class.
    /// </summary>
    /// <param name="hostEnvironment">Host environment used to locate the repository root.</param>
    /// <param name="logger">Logger.</param>
    public AiSourceCodeIndexService(IHostEnvironment hostEnvironment, ILogger<AiSourceCodeIndexService> logger)
    {
        this.hostEnvironment = hostEnvironment;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AiSourceCodeSearchResult>> SearchSourceCodeAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<AiSourceCodeSearchResult>>([]);
        }

        var repositoryRoot = ResolveRepositoryRoot(this.hostEnvironment.ContentRootPath);
        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot))
        {
            return Task.FromResult<IReadOnlyList<AiSourceCodeSearchResult>>([]);
        }

        var keywords = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => Regex.Replace(word, "[^a-zA-Z0-9]", string.Empty))
            .Where(word => word.Length > 2 && !StopWords.Contains(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywords.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<AiSourceCodeSearchResult>>([]);
        }

        var matches = new List<AiSourceCodeSearchResult>();
        foreach (var filePath in Directory.EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(repositoryRoot, filePath);
            if (IsIgnoredPath(relativePath))
            {
                continue;
            }

            var text = File.ReadAllText(filePath);
            var score = keywords.Sum(keyword => CountOccurrences(text, keyword))
                + keywords.Sum(keyword => CountOccurrences(relativePath, keyword));

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

        var topResults = matches
            .OrderByDescending(match => match.RelevanceScore)
            .ThenBy(match => match.FilePath, StringComparer.OrdinalIgnoreCase)
            .Take(MaxResults)
            .ToList();

        if (topResults.Count == 0)
        {
            this.logger.LogInformation("No source-code matches found for query {Query}.", query);
        }

        return Task.FromResult<IReadOnlyList<AiSourceCodeSearchResult>>(topResults);
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
        var end = Math.Min(lines.Length - 1, matchLineIndex + 4);
        var snippet = string.Join(Environment.NewLine, lines[start..(end + 1)]);
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