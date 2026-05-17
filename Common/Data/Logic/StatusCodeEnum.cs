// <copyright file="StatusCodeEnum.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data.Logic
{
    /// <summary>
    /// Represents the administrative lifecycle state of an article record.
    /// </summary>
    /// <remarks>
    /// <c>StatusCode</c> and <c>Published</c> are independent axes.
    /// <c>StatusCode</c> governs the administrative/lifecycle state; <c>Published</c>
    /// (a nullable <c>DateTimeOffset</c>) governs whether the article is live on the
    /// public website. An article can be <see cref="Active"/> and unpublished, or
    /// <see cref="Active"/> and published — the two fields do not imply each other.
    /// <para>
    /// For the complete lifecycle narrative, state-transition diagram, blob-retention
    /// rules, and guidance on <see cref="Inactive"/>, see
    /// <c>docs/adr/0037-article-lifecycle-and-status-code-semantics.md</c>.
    /// </para>
    /// </remarks>
    public enum StatusCodeEnum
    {
        /// <summary>
        /// Default editable state. Set on article creation and preserved through all
        /// save, publish, unpublish, and scheduling operations.
        /// A value of <c>Published</c> determines live visibility; this status does not.
        /// </summary>
        Active = 0,

        /// <summary>
        /// Vestigial / reserved. Not set by any current production code path.
        /// Treat the same as <see cref="Active"/> until a feature formally adopts this
        /// value via an ADR. See ADR 0037 for details.
        /// </summary>
        Inactive = 1,

        /// <summary>
        /// Soft-deleted ("Send to Trash"). Set on all versions by <c>DeleteArticleHandler</c>.
        /// The blob storage folder <c>/pub/articles/{ArticleNumber}</c> is retained until
        /// permanently trashed. Must be hidden from all file-listing surfaces — use
        /// <c>PublicFileEntryTitleResolver.FilterDeletedArticleEntriesAsync</c>.
        /// </summary>
        Deleted = 2,

        /// <summary>
        /// URL redirect stub for articles whose canonical path has changed.
        /// Not editable content; treat the same as <see cref="Deleted"/> in file listings.
        /// </summary>
        Redirect = 3
    }
}
