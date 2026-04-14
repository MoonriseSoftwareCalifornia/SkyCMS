// <copyright file="UrlChange.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Titles
{
    /// <summary>
    /// Represents a URL change that occurred during a title change operation,
    /// tracking the old URL, new URL, and whether the article is published.
    /// </summary>
    /// <remarks>
    /// This class is used to track URL changes for redirect creation. Redirects should
    /// only be created for published articles to avoid creating unnecessary redirect entries
    /// for draft or unpublished content.
    /// </remarks>
    internal sealed class UrlChange
    {
        /// <summary>
        /// Gets or sets the old URL path before the title change.
        /// </summary>
        required public string OldUrl { get; set; }

        /// <summary>
        /// Gets or sets the new URL path after the title change.
        /// </summary>
        required public string NewUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the article is currently published.
        /// </summary>
        /// <remarks>
        /// An article is considered published if it has a Published timestamp that is
        /// less than or equal to the current time. Only published articles should have
        /// redirects created for their URL changes.
        /// </remarks>
        required public bool IsPublished { get; set; }

        /// <summary>
        /// Gets or sets the article number for diagnostic and logging purposes.
        /// </summary>
        required public int ArticleNumber { get; set; }
    }
}
