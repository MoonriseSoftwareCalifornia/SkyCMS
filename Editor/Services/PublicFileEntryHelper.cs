// <copyright file="PublicFileEntryHelper.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Cosmos.BlobService;
    using MimeTypes;

    internal static class PublicFileEntryHelper
    {
        internal static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            var clean = path.Replace('\\', '/').Trim();
            while (clean.Contains("//", StringComparison.Ordinal))
            {
                clean = clean.Replace("//", "/", StringComparison.Ordinal);
            }

            if (!clean.StartsWith("/", StringComparison.Ordinal))
            {
                clean = "/" + clean;
            }

            if (clean.Length > 1 && clean.EndsWith("/", StringComparison.Ordinal))
            {
                clean = clean.TrimEnd('/');
            }

            return clean;
        }

        internal static bool IsPathWithinRoot(string path, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(rootPath))
            {
                return false;
            }

            var normalizedPath = NormalizePath(path);
            var normalizedRoot = NormalizePath(rootPath);

            if (normalizedPath.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetDisplayName(FileManagerEntry entry)
        {
            if (entry.IsDirectory)
            {
                return entry.Name ?? string.Empty;
            }

            var name = entry.Name ?? string.Empty;
            var ext = entry.Extension ?? string.Empty;

            if (!string.IsNullOrEmpty(ext) && !ext.StartsWith(".", StringComparison.Ordinal))
            {
                ext = "." + ext;
            }

            if (string.IsNullOrEmpty(ext))
            {
                return name;
            }

            return name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ext;
        }

        internal static string GetEntryMimeType(FileManagerEntry entry)
        {
            if (entry.IsDirectory)
            {
                return "directory";
            }

            var extension = Path.GetExtension(GetDisplayName(entry));
            return MimeTypeMap.GetMimeType(extension);
        }

        internal static string ResolveEntryPath(string parentPath, FileManagerEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.Path))
            {
                var rootPath = entry.Path.StartsWith("/", StringComparison.Ordinal) ? entry.Path : "/" + entry.Path;
                return NormalizePath(rootPath);
            }

            var fileName = GetDisplayName(entry);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "untitled";
            }

            var prefix = NormalizePath(parentPath);
            var combined = prefix.EndsWith("/", StringComparison.Ordinal)
                ? prefix + fileName
                : prefix + "/" + fileName;

            return NormalizePath(combined);
        }

        internal static bool TryGetArticleNumber(FileManagerEntry entry, out int articleNumber)
        {
            articleNumber = 0;
            if (!entry.IsDirectory)
            {
                return false;
            }

            var segment = GetLastPathSegment(entry.Path, entry.Name);
            return int.TryParse(segment, out articleNumber);
        }

        internal static bool TryGetTemplateId(FileManagerEntry entry, out Guid templateId)
        {
            templateId = Guid.Empty;
            if (!entry.IsDirectory)
            {
                return false;
            }

            var segment = GetLastPathSegment(entry.Path, entry.Name);
            return Guid.TryParse(segment, out templateId);
        }

        internal static bool TryGetArticleNumber(string path, out int articleNumber)
        {
            articleNumber = 0;
            var segment = GetLastPathSegment(path, string.Empty);
            return int.TryParse(segment, out articleNumber);
        }

        internal static bool TryGetTemplateId(string path, out Guid templateId)
        {
            templateId = Guid.Empty;
            var segment = GetLastPathSegment(path, string.Empty);
            return Guid.TryParse(segment, out templateId);
        }

        internal static string ResolveFriendlyDisplayName(
            string parentPath,
            FileManagerEntry entry,
            IReadOnlyDictionary<int, string> articleTitlesByNumber,
            IReadOnlyDictionary<Guid, string> templateTitlesById)
        {
            if (entry.IsDirectory)
            {
                if (string.Equals(parentPath, "/pub/articles", StringComparison.OrdinalIgnoreCase)
                    && TryGetArticleNumber(entry, out var articleNumber)
                    && articleTitlesByNumber.TryGetValue(articleNumber, out var articleTitle)
                    && !string.IsNullOrWhiteSpace(articleTitle))
                {
                    return articleTitle;
                }

                if (string.Equals(parentPath, "/pub/templates", StringComparison.OrdinalIgnoreCase)
                    && TryGetTemplateId(entry, out var templateId)
                    && templateTitlesById.TryGetValue(templateId, out var templateTitle)
                    && !string.IsNullOrWhiteSpace(templateTitle))
                {
                    return templateTitle;
                }
            }

            return GetDisplayName(entry);
        }

        internal static bool TryGetArticleNumberFromPath(string path, out int articleNumber)
        {
            articleNumber = 0;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalizedPath = NormalizePath(path);
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
            {
                return false;
            }

            if (!segments[0].Equals("pub", StringComparison.OrdinalIgnoreCase)
                || !segments[1].Equals("articles", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return int.TryParse(segments[2], out articleNumber);
        }

        /// <summary>
        /// Extracts the distinct set of article numbers embedded in a collection of file entries.
        /// Only entries whose path contains a numeric third segment under <c>/pub/articles/</c>
        /// contribute to the result. Entries at shallower depths (e.g. the <c>/pub/articles</c>
        /// folder itself) are ignored.
        /// </summary>
        /// <param name="entries">File/folder entries to inspect. May be null or empty.</param>
        /// <returns>
        /// A distinct, sorted list of article numbers. Returns an empty list when
        /// <paramref name="entries"/> is null, empty, or contains no qualifying paths.
        /// </returns>
        /// <example>
        /// Entries with paths <c>/pub/articles/100</c>, <c>/pub/articles/200/logo.png</c>,
        /// and <c>/pub/articles/200/sub</c> return <c>[100, 200]</c>.
        /// </example>
        internal static List<int> ExtractArticleNumbersFromEntries(IEnumerable<FileManagerEntry>? entries)
        {
            if (entries == null)
            {
                return new List<int>();
            }

            var numbers = new HashSet<int>();
            foreach (var entry in entries)
            {
                if (TryGetArticleNumberFromPath(entry.Path, out var articleNumber))
                {
                    numbers.Add(articleNumber);
                }
            }

            return numbers.OrderBy(n => n).ToList();
        }

        internal static string ResolveFriendlyDisplayPath(
            string canonicalPath,
            IReadOnlyDictionary<int, string> articleTitlesByNumber)
        {
            if (string.IsNullOrWhiteSpace(canonicalPath) || articleTitlesByNumber == null || articleTitlesByNumber.Count == 0)
            {
                return NormalizePath(canonicalPath);
            }

            var normalizedPath = NormalizePath(canonicalPath);
            if (!TryGetArticleNumberFromPath(normalizedPath, out var articleNumber))
            {
                return normalizedPath;
            }

            if (!articleTitlesByNumber.TryGetValue(articleNumber, out var articleTitle) || string.IsNullOrWhiteSpace(articleTitle))
            {
                return normalizedPath;
            }

            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            segments[2] = articleTitle;
            return "/" + string.Join('/', segments);
        }

        private static string GetLastPathSegment(string? path, string? fallback)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var normalizedPath = NormalizePath(path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path);
                var segment = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    return segment;
                }
            }

            return fallback ?? string.Empty;
        }
    }
}