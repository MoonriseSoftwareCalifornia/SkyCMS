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
    using System.Text;
    using System.Web;
    using Cosmos.BlobService;
    using MimeTypes;
    using SkyCMS.Drivers.ElFinder;

    /// <summary>
    /// Provides helper methods for file path manipulation, validation, and display name resolution
    /// in the SkyCMS public file management system.
    /// </summary>
    public static class PublicFileEntryHelper
    {
        /// <summary>
        /// Normalizes a file path by converting backslashes to forward slashes, removing duplicate slashes,
        /// ensuring the path starts with a forward slash, and removing trailing slashes (except for root).
        /// </summary>
        /// <param name="path">The path to normalize.</param>
        /// <returns>
        /// A normalized path string that starts with '/', contains no duplicate slashes,
        /// and has no trailing slash (unless it is the root path "/").
        /// Returns "/" if the input is null, empty, or whitespace.
        /// </returns>
        /// <example>
        /// <code>
        /// NormalizePath("foo\\bar//baz/") returns "/foo/bar/baz"
        /// NormalizePath("") returns "/"
        /// NormalizePath("pub/articles") returns "/pub/articles"
        /// </code>
        /// </example>
        public static string NormalizePath(string path)
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

        /// <summary>
        /// Determines whether the specified path is within the given root path,
        /// preventing path traversal attacks by rejecting paths containing "..".
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <param name="rootPath">The root path that should contain the path.</param>
        /// <returns>
        /// <see langword="true"/> if the path is within the root path and does not contain "..";
        /// otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This method normalizes both paths before comparison and uses case-insensitive matching.
        /// </remarks>
        public static bool IsPathWithinRoot(string path, string rootPath)
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

        /// <summary>
        /// Retrieves the display name for a file manager entry, including the file extension if present.
        /// </summary>
        /// <param name="entry">The file manager entry.</param>
        /// <returns>
        /// For directories, returns the name. For files, returns the name with extension appended
        /// (if not already present). Returns an empty string if the entry name is null.
        /// </returns>
        public static string GetDisplayName(FileManagerEntry entry)
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

        /// <summary>
        /// Determines the MIME type for a file manager entry.
        /// </summary>
        /// <param name="entry">The file manager entry.</param>
        /// <returns>
        /// Returns "directory" for directories, or the MIME type based on the file extension
        /// for files (e.g., "image/png", "text/html").
        /// </returns>
        public static string GetEntryMimeType(FileManagerEntry entry)
        {
            if (entry.IsDirectory)
            {
                return "directory";
            }

            var extension = Path.GetExtension(GetDisplayName(entry));
            return MimeTypeMap.GetMimeType(extension);
        }

        /// <summary>
        /// Resolves the full path for a file manager entry, using either its explicit path
        /// or combining the parent path with the entry's display name.
        /// </summary>
        /// <param name="parentPath">The parent directory path.</param>
        /// <param name="entry">The file manager entry.</param>
        /// <returns>
        /// A normalized absolute path. If the entry has an explicit path, that path is used.
        /// Otherwise, the display name is appended to the parent path.
        /// If the entry has no name, "untitled" is used as the filename.
        /// </returns>
        public static string ResolveEntryPath(string parentPath, FileManagerEntry entry)
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

        /// <summary>
        /// Attempts to extract an article number from a file manager entry's path.
        /// </summary>
        /// <param name="entry">The file manager entry.</param>
        /// <param name="articleNumber">When this method returns, contains the article number if successful; otherwise, zero.</param>
        /// <returns>
        /// <see langword="true"/> if the article number was successfully parsed; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Validates the path format <c>/pub/articles/{number}</c> before extracting the third segment.
        /// </remarks>
        public static bool TryGetArticleNumber(FileManagerEntry entry, out int articleNumber)
        {
            articleNumber = 0;
            if (entry?.Path == null)
            {
                return false;
            }

            return TryGetArticleNumberFromPath(entry.Path, out articleNumber);
        }

        /// <summary>
        /// Attempts to extract a template GUID from a directory entry's path.
        /// </summary>
        /// <param name="entry">The file manager entry (must be a directory).</param>
        /// <param name="templateId">When this method returns, contains the template GUID if successful; otherwise, <see cref="Guid.Empty"/>.</param>
        /// <returns>
        /// <see langword="true"/> if the entry is a directory and the template GUID was successfully parsed; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool TryGetTemplateId(FileManagerEntry entry, out Guid templateId)
        {
            templateId = Guid.Empty;
            if (!entry.IsDirectory)
            {
                return false;
            }

            var segment = GetLastPathSegment(entry.Path, entry.Name);
            return Guid.TryParse(segment, out templateId);
        }

        /// <summary>
        /// Attempts to extract an article number from the last segment of a file path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="articleNumber">When this method returns, contains the article number if successful; otherwise, zero.</param>
        /// <returns>
        /// <see langword="true"/> if the article number was successfully parsed from the last path segment; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool TryGetArticleNumber(string path, out int articleNumber)
        {
            articleNumber = 0;
            var segment = GetLastPathSegment(path, string.Empty);
            return int.TryParse(segment, out articleNumber);
        }

        /// <summary>
        /// Attempts to extract a template GUID from the last segment of a file path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="templateId">When this method returns, contains the template GUID if successful; otherwise, <see cref="Guid.Empty"/>.</param>
        /// <returns>
        /// <see langword="true"/> if the template GUID was successfully parsed from the last path segment; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool TryGetTemplateId(string path, out Guid templateId)
        {
            templateId = Guid.Empty;
            var segment = GetLastPathSegment(path, string.Empty);
            return Guid.TryParse(segment, out templateId);
        }

        /// <summary>
        /// Resolves a user-friendly display name for a file manager entry, replacing numeric article IDs
        /// or template GUIDs with their corresponding titles when applicable.
        /// </summary>
        /// <param name="parentPath">The parent directory path.</param>
        /// <param name="entry">The file manager entry.</param>
        /// <param name="articleTitlesByNumber">A dictionary mapping article numbers to article titles.</param>
        /// <param name="templateTitlesById">A dictionary mapping template GUIDs to template titles.</param>
        /// <returns>
        /// For directories under <c>/pub/articles</c>, returns the article title if found; for directories under
        /// <c>/pub/templates</c>, returns the template title if found. Otherwise, returns the standard display name.
        /// </returns>
        public static string ResolveFriendlyDisplayName(
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

        /// <summary>
        /// Attempts to extract an article number from a path in the format <c>/pub/articles/{number}/...</c>.
        /// </summary>
        /// <param name="path">The file path to parse.</param>
        /// <param name="articleNumber">When this method returns, contains the article number if successful; otherwise, zero.</param>
        /// <returns>
        /// <see langword="true"/> if the path starts with <c>/pub/articles</c> and contains a valid numeric third segment;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public static bool TryGetArticleNumberFromPath(string path, out int articleNumber)
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
        public static List<int> ExtractArticleNumbersFromEntries(IEnumerable<FileManagerEntry>? entries)
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

        /// <summary>
        /// Resolves a user-friendly display path for a file manager entry by replacing the numeric
        /// article ID segment with the article title.
        /// </summary>
        /// <param name="canonicalPath">The canonical file path.</param>
        /// <param name="articleNumber">The article number to replace.</param>
        /// <param name="articleTitle">The article title to use as a replacement.</param>
        /// <returns>
        /// A path string with the article number replaced by the article title.
        /// Returns the original normalized path if the path structure is invalid.
        /// </returns>
        public static string ResolveFriendlyDisplayPath(
            string canonicalPath,
            int articleNumber,
            string articleTitle)
        {
            var normalizedPath = NormalizePath(canonicalPath);
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

            // Ensure we have enough segments to replace the article number (at index 2)
            if (segments.Count < 3)
            {
                return normalizedPath;
            }

            segments[2] = articleTitle;
            return "/" + string.Join('/', segments);
        }

        /// <summary>
        /// Resolves a user-friendly display path by replacing the numeric article ID segment
        /// with the corresponding article title from the provided dictionary.
        /// </summary>
        /// <param name="canonicalPath">The canonical file path.</param>
        /// <param name="articleTitlesByNumber">A dictionary mapping article numbers to article titles.</param>
        /// <returns>
        /// A normalized path with the article number replaced by the article title if found.
        /// Returns the original normalized path if no article number is detected or no matching title exists.
        /// </returns>
        public static string ResolveFriendlyDisplayPath(
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

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="path"/> is a valid upload destination:
        /// non-empty, free of path-traversal sequences (<c>..</c>), and rooted under <c>/pub</c>.
        /// </summary>
        /// <param name="path">Candidate upload path.</param>
        /// <returns><see langword="true"/> if the path is safe; otherwise <see langword="false"/>.</returns>
        public static bool IsUploadPathSafe(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Trim('/') == string.Empty)
            {
                return false;
            }

            if (path.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            var normalized = NormalizePath(path);
            return normalized.Equals("/pub", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("/pub/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns <see langword="true"/> when the file extension of <paramref name="fileName"/>
        /// appears in the blocked-extensions list defined by <see cref="FileStorageConstants.DangerousFileExtensions"/>.
        /// </summary>
        /// <param name="fileName">File name (or extension) to test.</param>
        /// <returns><see langword="true"/> if the extension is dangerous; otherwise <see langword="false"/>.</returns>
        public static bool IsDangerousExtension(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return FileStorageConstants.DangerousFileExtensions.Contains(ext);
        }

        /// <summary>
        /// Extracts the last segment from a normalized path.
        /// </summary>
        /// <param name="path">The path to parse.</param>
        /// <param name="fallback">The fallback value to return if no segment is found.</param>
        /// <returns>
        /// The last non-empty segment of the path, or the fallback value (or empty string if fallback is null).
        /// </returns>
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

        /// <summary>
        /// Trims leading and trailing slashes and white space from a path segment.
        /// </summary>
        /// <param name="part">URL path part to trim.</param>
        /// <returns>Trimmed path part, or an empty string if <paramref name="part"/> is null or empty.</returns>
        public static string TrimPathPart(string part)
        {
            if (string.IsNullOrEmpty(part))
            {
                return string.Empty;
            }

            return part.Trim('/').Trim('\\').Trim();
        }

        /// <summary>
        /// Parses one or more path strings into an ordered array of non-empty, trimmed segments.
        /// </summary>
        /// <param name="pathParts">Path components to parse.</param>
        /// <returns>Array of non-empty, trimmed path segments.</returns>
        public static string[] ParsePath(params string?[] pathParts)
        {
            if (pathParts == null)
            {
                return Array.Empty<string>();
            }

            var paths = new List<string>();
            foreach (var part in pathParts)
            {
                if (!string.IsNullOrEmpty(part))
                {
                    foreach (var segment in part.Split('/'))
                    {
                        var trimmed = TrimPathPart(segment);
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            paths.Add(trimmed);
                        }
                    }
                }
            }

            return paths.ToArray();
        }

        /// <summary>
        /// URL-encodes each segment of <paramref name="path"/>, joining with '/',
        /// and replaces spaces with hyphens before encoding.
        /// </summary>
        /// <param name="path">Raw path string to encode.</param>
        /// <returns>URL-encoded path string, or an empty string if <paramref name="path"/> is empty.</returns>
        public static string UrlEncodePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var parts = ParsePath(path);
            var encoded = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                encoded.Add(HttpUtility.UrlEncode(part.Replace(" ", "-")).Replace("%40", "@"));
            }

            return TrimPathPart(string.Join('/', encoded));
        }

        /// <summary>
        /// Encodes a file path to a URL-safe base64 hash for transmission in URI path segments.
        /// </summary>
        /// <param name="path">File path to encode.</param>
        /// <returns>URL-safe base64-encoded path (no padding, '+' replaced with '-', '/' with '_').</returns>
        public static string EncodePathHash(string path)
        {
            var bytes = Encoding.UTF8.GetBytes(path);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        /// <summary>
        /// Decodes a URL-safe base64 hash back to a file path.
        /// </summary>
        /// <param name="hash">URL-safe base64-encoded path hash.</param>
        /// <returns>Decoded file path.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="hash"/> is invalid or empty.</exception>
        public static string DecodePathHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
            {
                throw new ArgumentException("Path hash cannot be empty.", nameof(hash));
            }

            // Restore standard base64 characters and padding.
            var padded = hash.Replace('-', '+').Replace('_', '/');
            var padding = 4 - (padded.Length % 4);
            if (padding < 4)
            {
                padded += new string('=', padding);
            }

            try
            {
                var bytes = Convert.FromBase64String(padded);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid base64-encoded path hash.", nameof(hash), ex);
            }
        }
    }
}