// <copyright file="ArticleEditMenuItem.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Cosmos.Common.Data;

    /// <summary>
    /// Article Edit Menu Item.
    /// </summary>
    public class ArticleEditMenuItem
    {
        /// <inheritdoc cref="Article.Id" />
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets article Number.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <inheritdoc cref="Article.VersionNumber" />
        public int VersionNumber { get; set; }

        /// <inheritdoc cref="Article.Published" />
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether can use Live editor.
        /// </summary>
        public bool UsesHtmlEditor { get; set; } = false;
    }
}
