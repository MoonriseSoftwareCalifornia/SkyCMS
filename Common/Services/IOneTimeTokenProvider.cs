// <copyright file="IOneTimeTokenProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Interface for one-time token generation and validation.
/// </summary>
/// <typeparam name="TUser">Identity user type.</typeparam>
public interface IOneTimeTokenProvider<TUser>
    where TUser : IdentityUser
{
    /// <summary>
    /// Generates a one-time token for a given user.
    /// </summary>
    /// <param name="user">Identity user.</param>
    /// <returns>Token value.</returns>
    Task<string> GenerateAsync(TUser user);

    /// <summary>
    /// Validates a login token for a given user.
    /// </summary>
    /// <param name="token">Token value to validate.</param>
    /// <param name="user">IdentityUser.</param>
    /// <param name="removeToken">Remove token if present.</param>
    /// <returns>Validation result.</returns>
    Task<TokenVerificationResult> ValidateAsync(string token, TUser user, bool removeToken = true);
}
