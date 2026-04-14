// <copyright file="ITenantArticleLogicFactory.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Scheduling
{
    using System.Threading.Tasks;
    using Sky.Editor.Data.Logic;

    /// <summary>
    /// Interface for a factory that creates ArticleEditLogic instances for specific tenants.
    /// </summary>
    public interface ITenantArticleLogicFactory
    {
        /// <summary>
        /// Creates an ArticleEditLogic instance for the specified tenant domain name.
        /// </summary>
        /// <param name="domainName">Domain name.</param>
        /// <returns>ArticleEditLogic.</returns>
        Task<ArticleEditLogic> CreateForTenantAsync(string domainName);
    }
}
