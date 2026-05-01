// <copyright file="PathUtilities.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService;

/// <summary>
/// Provides utility methods for working with blob storage paths.
/// </summary>
internal static class PathUtilities
{
    /// <summary>
    /// Singleton instance of <see cref="IPathNormalizer"/> used for all path normalization operations.
    /// </summary>
    private static readonly IPathNormalizer Normalizer = new PathNormalizer();

    /// <summary>
    /// Normalizes a blob storage path to canonical form suitable for storage operations.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>
    /// The normalized path with leading/trailing slashes removed, separators unified to forward slashes,
    /// and consecutive separators collapsed. Returns an empty string if the input is null or empty.
    /// </returns>
    /// <remarks>
    /// Blob storage providers typically don't expect leading slashes in paths.
    /// This method ensures consistent path formatting across all storage operations and storage providers
    /// (Azure Blob Storage, Amazon S3, Azure Files, etc.).
    /// </remarks>
    public static string NormalizePath(string path)
    {
        return Normalizer.Normalize(path);
    }
}
