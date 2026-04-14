// <copyright file="ITitleChangeService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Titles
{
    using System.Threading.Tasks;
    using Cosmos.Common.Data;

    /// <summary>
    /// Handles side effects that must occur when an article title changes (e.g., slug updates, redirects, events).
    /// </summary>
    public interface ITitleChangeService
    {
        /// <summary>
        /// Constructs a URL for the specified article based on its properties.
        /// </summary>
        /// <param name="article">The article for which to build the URL. Must not be <see langword="null"/>.</param>
        /// <remarks>The URL is constructed using the article's title and if a blog post, uses the blog's URL as a prefix path.</remarks>
        /// <returns>A string representing the URL of the article.</returns>
        string BuildArticleUrl(Article article);

        /// <summary>
        /// Processes a title change for the supplied <paramref name="article"/>, given the previous title.
        /// </summary>
        /// <param name="article">The article entity whose title has just been modified (unsaved or saved, per calling convention).</param>
        /// <param name="oldTitle">The previous title of the article before the change.</param>
        /// <param name="oldUrlPath">The prior URL path used to detect changes and generate redirects if required.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task HandleTitleChangeAsync(Article article, string oldTitle, string oldUrlPath);

        /// <summary>
        /// Validates whether a proposed title is usable (not reserved and not used by a different article).
        /// </summary>
        /// <param name="title">Proposed title.</param>
        /// <param name="articleNumber">Current article number (null when creating new).</param>
        /// <returns>True if available; false if conflict.</returns>
        Task<bool> ValidateTitle(string title, int? articleNumber);
    }
}
