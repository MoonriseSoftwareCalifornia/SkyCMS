// <copyright file="ElFinderHashEncoder.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder;

using System;
using System.Text;

/// <summary>
/// Encodes and decodes elFinder path hashes using URL-safe Base64.
/// The format is: <c>&lt;volumeId&gt;&lt;base64url(path)&gt;</c>.
/// </summary>
public static class ElFinderHashEncoder
{
    /// <summary>
    /// The volume identifier prefix used for all hashes in this driver.
    /// </summary>
    public const string VolumeId = "l1_";

    /// <summary>
    /// Encodes a storage path into an elFinder hash.
    /// Leading slashes are stripped before encoding; trailing slashes are also stripped
    /// so that directory and file paths produce a stable, canonical hash.
    /// </summary>
    /// <param name="path">The storage path to encode (e.g. "pub/images/").</param>
    /// <returns>An elFinder hash string (e.g. "l1_cHViL2ltYWdlcw").</returns>
    public static string Encode(string path)
    {
        var normalized = (path ?? string.Empty).TrimStart('/').TrimEnd('/');
        var bytes = Encoding.UTF8.GetBytes(normalized);
        return VolumeId + Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Decodes an elFinder hash back to a storage path.
    /// Returns <see langword="null"/> when the hash is invalid or does not start with <see cref="VolumeId"/>.
    /// The returned path has <b>no leading slash</b> — it is the symmetric counterpart of <see cref="Encode"/>.
    /// Callers that require a leading slash (e.g. for HTTP responses) should pass the result through
    /// <see cref="Cosmos.BlobService.IPathNormalizer.NormalizeWithLeadingSlash"/>.
    /// </summary>
    /// <param name="hash">The elFinder hash to decode (e.g. "l1_cHViL2ltYWdlcw").</param>
    /// <returns>The decoded path without a leading slash (e.g. "pub/images"), or <see langword="null"/>.</returns>
    public static string? Decode(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || !hash.StartsWith(VolumeId, StringComparison.Ordinal))
        {
            return null;
        }

        var encoded = hash[VolumeId.Length..]
            .Replace('-', '+')
            .Replace('_', '/');

        var padding = encoded.Length % 4;
        if (padding > 0)
        {
            encoded += new string('=', 4 - padding);
        }

        try
        {
            var bytes = Convert.FromBase64String(encoded);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }
}
