// <copyright file="ILayoutVersioningService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layouts
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;

    /// <summary>
    /// Provides shared operations for layout versioning and template import.
    /// </summary>
    public interface ILayoutVersioningService
    {
        /// <summary>
        /// Creates a new draft version from an existing layout.
        /// </summary>
        /// <param name="layout">Source layout.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>New layout version.</returns>
        Task<Layout> CreateNewVersionAsync(Layout layout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports community templates into the target layout.
        /// </summary>
        /// <param name="communityPages">Community templates to import.</param>
        /// <param name="layoutId">Target layout ID.</param>
        /// <param name="layoutNumber">Target layout number.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ImportCommunityTemplatesAsync(
            IEnumerable<Template> communityPages,
            Guid layoutId,
            int layoutNumber,
            CancellationToken cancellationToken = default);
    }
}
