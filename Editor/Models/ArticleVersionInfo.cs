// <copyright file="ArticleVersionInfo.cs" company="Moonrise Software, LLC">
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
    ///     Article version list info item.
    /// </summary>
    [Serializable]
    public class ArticleVersionInfo
    {
        /// <inheritdoc cref="Article.Id" />
        [Key]
        public Guid Id { get; set; }

        /// <inheritdoc cref="Article.VersionNumber" />
        public int VersionNumber { get; set; }

        /// <inheritdoc cref="Article.Title" />
        public string Title { get; set; }

        /// <inheritdoc cref="Article.Updated" />
        public DateTimeOffset Updated { get; set; }

        /// <inheritdoc cref="Article.Published" />
        public DateTimeOffset? Published { get; set; }

        /// <inheritdoc cref="Article.Expires" />
        public DateTimeOffset? Expires { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether can use Live editor.
        /// </summary>
        public bool UsesHtmlEditor { get; set; }
    }
}
