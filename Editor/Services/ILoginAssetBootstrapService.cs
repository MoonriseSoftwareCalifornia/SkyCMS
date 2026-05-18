// <copyright file="ILoginAssetBootstrapService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to project.
// </copyright>

namespace Cosmos.Editor.Services
{
    using System.Threading.Tasks;

    /// <summary>
    /// Ensures required login-time assets exist in blob storage.
    /// </summary>
    public interface ILoginAssetBootstrapService
    {
        /// <summary>
        /// Ensures the required assets exist for the current website.
        /// </summary>
        /// <param name="website">Website domain name when available.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EnsureRequiredAssetsAsync(string website = "");
    }
}
