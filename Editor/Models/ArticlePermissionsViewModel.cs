// <copyright file="ArticlePermissionsViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models
{
    using System;
    using Cosmos.Common.Data;

    /// <summary>
    /// Article permissions view model.
    /// </summary>
    public class ArticlePermissionsViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArticlePermissionsViewModel"/> class.
        /// </summary>
        public ArticlePermissionsViewModel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArticlePermissionsViewModel"/> class.
        /// </summary>
        /// <param name="entry">Catalog entry.</param>
        /// <param name="forRoles">Updating roles?.</param>
        public ArticlePermissionsViewModel(CatalogEntry entry, bool forRoles = true)
        {
            Title = entry.Title;
            Published = entry.Published;
            ShowingRoles = forRoles;
        }

        /// <summary>
        /// Gets or sets article number.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets article title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets date and time published.
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether permission set is for roles, otherwise is for users.
        /// </summary>
        public bool ShowingRoles { get; set; }
    }
}
