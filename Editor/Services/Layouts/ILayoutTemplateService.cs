// <copyright file="ILayoutTemplateService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layouts
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Sky.Editor.Services.Templates;

    /// <summary>
    /// Provides methods for retrieving layout page templates.
    /// </summary>
    public interface ILayoutTemplateService
    {
        /// <summary>
        /// Gets all available templates.
        /// </summary>
        /// <returns>A page template list.</returns>
        Task<List<PageTemplate>> GetAllTemplatesAsync();
    }
}
