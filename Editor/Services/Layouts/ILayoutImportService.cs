// <copyright file="ILayoutImportService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layouts
{
    using Cosmos.Cms.Data.Logic;
    using Cosmos.Common.Data;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Service for importing layouts and templates from external sources.
    /// </summary>
    public interface ILayoutImportService
    {
        /// <summary>
        /// Gets the community layout catalog.
        /// </summary>
        /// <returns>Catalog of available layouts.</returns>
        Task<Root> GetCommunityCatalogAsync();

        /// <summary>
        /// Gets a specified layout from the community catalog.
        /// </summary>
        /// <param name="layoutId">Layout ID.</param>
        /// <param name="isDefault">Is default layout.</param>
        /// <returns>Layout object.</returns>
        Task<Layout> GetCommunityLayoutAsync(string layoutId, bool isDefault);

        /// <summary>
        /// Gets template pages for a layout.
        /// </summary>
        /// <param name="layoutId">Layout ID.</param>
        /// <returns>List of page templates.</returns>
        Task<List<Page>> GetPageTemplatesAsync(string layoutId);

        /// <summary>
        /// Gets community template pages for a layout.
        /// </summary>
        /// <param name="communityLayoutId">Community layout ID.</param>
        /// <returns>List of templates.</returns>
        Task<List<Template>> GetCommunityTemplatePagesAsync(string communityLayoutId = "");

        /// <summary>
        /// Creates a layout from HTML.
        /// </summary>
        /// <param name="html">HTML content.</param>
        /// <returns>Layout object.</returns>
        Layout ParseHtml(string html);

        /// <summary>
        /// Parses an HTML page and loads it as either a Template or an Article.
        /// </summary>
        /// <typeparam name="T">Type to parse (Template or Article).</typeparam>
        /// <param name="html">HTML content.</param>
        /// <returns>Parsed content.</returns>
        T ParseHtml<T>(string html);
    }
}
