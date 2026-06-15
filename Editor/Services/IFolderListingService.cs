// <copyright file="IFolderListingService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cosmos.BlobService;

    /// <summary>
    /// Shared folder-listing service that produces a uniform <see cref="FileManagerEntry"/> list
    /// for any requested path, regardless of whether the path represents a catalog-backed
    /// virtual folder (/pub/articles, /pub/templates) or a real blob-storage folder.
    /// </summary>
    /// <remarks>
    /// Both the File Manager UI (<c>FileManagerController</c>) and the SkyCMS VS Code Explorer
    /// (<c>VsCodeController</c>) must return identical results for the same path.
    /// Centralising the logic here ensures the two surfaces cannot diverge.
    /// </remarks>
    public interface IFolderListingService
    {
        /// <summary>
        /// Returns the entries for <paramref name="path"/>, applying catalog look-ups and
        /// deleted-article filtering as appropriate for the path type.
        /// </summary>
        /// <param name="path">
        /// Normalised absolute path (e.g. <c>/pub</c>, <c>/pub/articles</c>,
        /// <c>/pub/articles/42</c>, <c>/pub/templates</c>).
        /// </param>
        /// <param name="tenantDomain">
        /// Tenant domain associated with the current request.
        /// Obtain from <c>IDynamicConfigurationProvider.GetTenantDomainNameFromRequest()</c>.
        /// </param>
        /// <returns>Flat list of entries for the requested path.</returns>
        Task<List<FileManagerEntry>> GetEntriesAsync(string path, string tenantDomain);
    }
}
