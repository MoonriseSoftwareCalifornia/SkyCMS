// <copyright file="TokenVerificationResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services;

/// <summary>
/// Indicates the verification result for a one-time token.
/// </summary>
public enum TokenVerificationResult
{
    /// <summary>
    /// Token is valid.
    /// </summary>
    Valid,

    /// <summary>
    /// Token is invalid.
    /// </summary>
    Invalid,

    /// <summary>
    /// Token is expired.
    /// </summary>
    Expired
}
