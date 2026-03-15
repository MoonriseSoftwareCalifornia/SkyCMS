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
    /// Normalizes a blob storage path by removing leading slashes.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path without leading slashes, or an empty string if the input is null or empty.</returns>
    /// <remarks>
    /// Blob storage providers typically don't expect leading slashes in paths.
    /// This method ensures consistent path formatting across all storage operations.
    /// </remarks>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return path.TrimStart('/');
    }
}
