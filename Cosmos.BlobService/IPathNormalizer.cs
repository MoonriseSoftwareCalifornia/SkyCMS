// <copyright file="IPathNormalizer.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService
{
    /// <summary>
    /// Defines an abstraction for normalizing storage paths to a canonical form.
    /// </summary>
    /// <remarks>
    /// Path normalization ensures that paths are consistently formatted across all storage operations,
    /// regardless of how they arrive at the storage layer (from HTTP requests, internal APIs, or file explorer).
    /// This contract is crucial for:
    /// - Hash generation consistency in file explorer protocols (elFinder).
    /// - Cross-provider compatibility (Azure Blob, Amazon S3, Azure Files).
    /// - Preventing path traversal vulnerabilities.
    /// </remarks>
    public interface IPathNormalizer
    {
        /// <summary>
        /// Normalizes a path to a canonical form suitable for storage operations.
        /// </summary>
        /// <param name="path">The path to normalize. May be null, empty, or contain leading/trailing slashes.</param>
        /// <returns>
        /// The normalized path following these rules:
        /// - Null or whitespace inputs return an empty string.
        /// - Leading and trailing slashes are removed.
        /// - All separators are forward slashes (/).
        /// - Consecutive slashes are collapsed to a single slash.
        /// - Paths like "." or ".." are returned without modification (caller responsible for validation).
        /// </returns>
        /// <example>
        /// <code>
        /// normalizer.Normalize("/folder/file.txt") → "folder/file.txt"
        /// normalizer.Normalize("folder/") → "folder"
        /// normalizer.Normalize("/") → ""
        /// normalizer.Normalize("") → ""
        /// normalizer.Normalize(null) → ""
        /// </code>
        /// </example>
        string Normalize(string path);

        /// <summary>
        /// Normalizes a path and ensures it has a leading slash (suitable for HTTP responses or API outputs).
        /// </summary>
        /// <param name="path">The path to normalize with leading slash.</param>
        /// <returns>
        /// The normalized path with a leading slash prepended. Returns "/" if the input is empty after normalization.
        /// </returns>
        /// <example>
        /// <code>
        /// normalizer.NormalizeWithLeadingSlash("folder/file.txt") → "/folder/file.txt"
        /// normalizer.NormalizeWithLeadingSlash("/") → "/"
        /// normalizer.NormalizeWithLeadingSlash("") → "/"
        /// </code>
        /// </example>
        string NormalizeWithLeadingSlash(string path);
    }
}
