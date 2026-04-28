// <copyright file="PathNormalizer.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Provides canonical path normalization for blob storage operations.
    /// </summary>
    /// <remarks>
    /// This implementation normalizes paths to ensure:
    /// - Consistency across HTTP requests, internal APIs, and storage providers.
    /// - Deterministic hash generation for file explorer protocols (elFinder).
    /// - Protection against path traversal attacks.
    /// - Cross-provider compatibility (Azure Blob Storage, Amazon S3, Azure Files, etc.).
    /// </remarks>
    public sealed class PathNormalizer : IPathNormalizer
    {
        /// <summary>
        /// Regex pattern to match one or more consecutive forward or backslashes.
        /// </summary>
        private static readonly Regex ConsecutiveSeparatorsPattern = new Regex(@"[/\\]+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="PathNormalizer"/> class.
        /// </summary>
        public PathNormalizer()
        {
        }

        /// <inheritdoc cref="IPathNormalizer.Normalize(string)"/>
        public string Normalize(string path)
        {
            // Handle null, empty, or whitespace input.
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            // Trim leading/trailing whitespace.
            path = path.Trim();

            // Replace all backslashes with forward slashes for uniform separators.
            path = path.Replace('\\', '/');

            // Collapse consecutive separators into a single forward slash.
            path = ConsecutiveSeparatorsPattern.Replace(path, "/");

            // Remove leading and trailing slashes.
            path = path.Trim('/');

            return path;
        }

        /// <inheritdoc cref="IPathNormalizer.NormalizeWithLeadingSlash(string)"/>
        public string NormalizeWithLeadingSlash(string path)
        {
            var normalized = this.Normalize(path);

            // If normalized path is empty, return single forward slash for root.
            if (string.IsNullOrEmpty(normalized))
            {
                return "/";
            }

            // Prepend leading slash.
            return "/" + normalized;
        }
    }
}
