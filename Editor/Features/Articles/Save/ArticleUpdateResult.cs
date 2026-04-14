// <copyright file="ArticleUpdateResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Save
{
    using System.Collections.Generic;
    using Cosmos.Common.Models;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Result of an article update operation including CDN purge results.
    /// </summary>
    public class ArticleUpdateResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the server-side update succeeded.
        /// </summary>
        public bool ServerSideSuccess { get; set; }

        /// <summary>
        /// Gets or sets the updated article view model.
        /// </summary>
        public ArticleViewModel Model { get; set; }

        /// <summary>
        /// Gets or sets the CDN purge results (empty if not published or no CDN configured).
        /// </summary>
        public List<CdnResult> CdnResults { get; set; } = new List<CdnResult>();
    }
}
