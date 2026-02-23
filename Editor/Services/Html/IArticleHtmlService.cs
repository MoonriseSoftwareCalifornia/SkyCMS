// <copyright file="IArticleHtmlService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Html
{
    /// <summary>
    /// Provides operations for manipulating and analyzing article HTML fragments.
    /// Responsibilities include:
    /// - Ensuring editor (contenteditable) markers and stable identifiers exist.
    /// - Normalizing Angular application href values based on a URL path.
    /// - Extracting an introductory text snippet from article body HTML.
    /// </summary>
    public interface IArticleHtmlService
    {
        /// <summary>
        /// Ensures that all editable regions (contenteditable='true') contain a stable
        /// unique identifier and ordering metadata. If no editable region exists,
        /// the entire document is wrapped in a new editable container.
        /// </summary>
        /// <param name="html">Raw HTML fragment representing an article body.</param>
        /// <returns>HTML with required editing markers injected.</returns>
        string EnsureEditableMarkers(string html);

        /// <summary>
        /// For Angular-based articles (detected via meta name='ccms:framework' value='angular'),
        /// ensures a element exists (or is updated) with an href derived from the provided URL path.
        /// If the fragment is not Angular-marked, the original fragment is returned unchanged.
        /// </summary>
        /// <param name="headerFragment">HTML header fragment (head-level markup or similar).</param>
        /// <param name="urlPath">Logical URL path used to construct the base href.</param>
        /// <returns>Header fragment with a normalized element when applicable.</returns>
        string EnsureAngularBase(string headerFragment, string urlPath);

        /// <summary>
        /// Extracts the first non-empty paragraph's text content as an introduction,
        /// truncated to a maximum of 512 characters. Returns an empty string if none found or on parse failure.
        /// </summary>
        /// <param name="html">HTML fragment containing article content.</param>
        /// <returns>Introduction text snippet or empty string.</returns>
        string ExtractIntroduction(string html);

        /// <summary>
        /// Determines if there is an unmarked editable region.
        /// </summary>
        /// <param name="html">HTML to check.</param>
        /// <returns>Indicates if the editable area is missing.</returns>
        bool HasUnMarkedEditableRegions(string html);
    }
}
