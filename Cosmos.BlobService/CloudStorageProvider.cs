// <copyright file="CloudStorageProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService;

/// <summary>
/// Represents the type of cloud storage provider.
/// </summary>
public enum CloudStorageProvider
{
    /// <summary>
    /// Unknown or unsupported provider.
    /// </summary>
    Unknown,

    /// <summary>
    /// Microsoft Azure Blob Storage.
    /// </summary>
    Azure,

    /// <summary>
    /// Amazon S3.
    /// </summary>
    AmazonS3,

    /// <summary>
    /// Cloudflare R2 (S3-compatible).
    /// </summary>
    CloudflareR2
}
