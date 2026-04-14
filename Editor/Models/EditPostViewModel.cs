// <copyright file="EditPostViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Models
{
    using System;
    using Cosmos.Cms.Common;

    /// <summary>
    /// Unified editor post view model for all editor types (Live, Code, Designer).
    /// </summary>
    public class EditPostViewModel
    {
        /// <summary>
        /// Gets or sets article record ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets id of the Article entity being worked on.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets edit ID as defined by the data-ccms-ceid attribute (Live Editor).
        /// </summary>
        public string EditorId { get; set; }

        /// <summary>
        /// Gets or sets user Id (Email address).
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets user position in document.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Gets or sets command (SaveBody, SaveRegion, SavePageProperties, SaveCode, SaveDesigner).
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is Focused.
        /// </summary>
        public bool IsFocused { get; set; }

        /// <summary>
        /// Gets or sets page version number.
        /// </summary>
        public int VersionNumber { get; set; }

        /// <summary>
        /// Gets or sets encrypted primary content payload for Live, Code, and Designer editors.
        /// </summary>
        /// <remarks>Empty content is valid. The developer may want to wipe content from a page and start over.</remarks>
        public string Payload { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets encrypted HEAD JavaScript (Code Editor).
        /// </summary>
        /// <remarks>Empty content is valid. The developer may want to wipe content from a page and start over.</remarks>
        public string HeadJavaScript { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets encrypted footer JavaScript (Code Editor).
        /// </summary>
        /// <remarks>Empty content is valid. The developer may want to wipe content from a page and start over.</remarks>
        public string FooterJavaScript { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets encrypted CSS content (Designer).
        /// </summary>
        /// <remarks>Empty content is valid. The developer may want to wipe content from a page and start over.</remarks>
        public string CssContent { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets which code field was edited (Code Editor).
        /// </summary>
        public string EditingField { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets editor type hint (html, css, javascript, etc.).
        /// </summary>
        public string EditorType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to update existing content vs replace (Code Editor).
        /// </summary>
        public bool UpdateExisting { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to save as new version.
        /// </summary>
        public bool SaveAsNewVersion { get; set; }

        /// <summary>
        /// Gets or sets date/time published.
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets date/time updated.
        /// </summary>
        public DateTimeOffset? Updated { get; set; }

        /// <summary>
        /// Gets or sets encrypted payload context token used to resolve per-session encryption key.
        /// </summary>
        public string CryptoContextToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets article title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets URL path.
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets banner image URL.
        /// </summary>
        /// <remarks>Empty content is valid. The developer may want to wipe content from a page and start over.</remarks>
        public string BannerImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets role access list.
        /// </summary>
        public string RoleList { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets article type (General, BlogPost, etc.).
        /// </summary>
        public ArticleType ArticleType { get; set; } = ArticleType.General;

        /// <summary>
        /// Gets or sets blog category.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets blog introduction.
        /// </summary>
        public string Introduction { get; set; } = string.Empty;
    }
}
