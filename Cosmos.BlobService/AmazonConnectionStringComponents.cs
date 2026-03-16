// <copyright file="AmazonConnectionStringComponents.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService;

/// <summary>
/// Contains parsed components of an Amazon S3/Cloudflare R2 connection string.
/// </summary>
public class AmazonConnectionStringComponents
{
    /// <summary>
    /// Gets or sets the bucket name.
    /// </summary>
    required public string BucketName { get; set; }

    /// <summary>
    /// Gets or sets the AWS region (optional for Cloudflare R2).
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets the access key ID.
    /// </summary>
    required public string KeyId { get; set; }

    /// <summary>
    /// Gets or sets the secret access key.
    /// </summary>
    required public string Key { get; set; }

    /// <summary>
    /// Gets or sets the Cloudflare account ID (for R2 only).
    /// </summary>
    public string? AccountId { get; set; }
}
