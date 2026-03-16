// <copyright file="IStaticFileService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.StaticFiles
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Models;

    /// <summary>
    /// Service for generating and managing static HTML files in blob storage.
    /// </summary>
    /// <remarks>
    /// This service creates pre-rendered HTML files from published pages and stores them in blob storage
    /// for direct serving without server-side rendering. Static files improve performance by eliminating
    /// the need to query the database and render views for every page request.
    /// </remarks>
    public interface IStaticFileService
    {
        /// <summary>
        /// Generates and uploads a static HTML file to blob storage for the specified published page.
        /// </summary>
        /// <param name="page">The published page to generate a static file for. Must have valid <see cref="PublishedPage.UrlPath"/>, <see cref="PublishedPage.Title"/>, and <see cref="PublishedPage.Content"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous upload operation.</returns>
        /// <remarks>
        /// <para>
        /// Constructs a complete, minimal HTML5 document from the page metadata and content.
        /// The generated HTML structure includes:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>HTML5 doctype and language attribute</description></item>
        ///   <item><description>UTF-8 character encoding declaration</description></item>
        ///   <item><description>HTML-encoded page title from <see cref="PublishedPage.Title"/></description></item>
        ///   <item><description>Optional header scripts from <see cref="PublishedPage.HeaderJavaScript"/></description></item>
        ///   <item><description>Page body content from <see cref="PublishedPage.Content"/></description></item>
        ///   <item><description>Optional footer scripts from <see cref="PublishedPage.FooterJavaScript"/></description></item>
        /// </list>
        /// <para>
        /// The file is uploaded to blob storage with MIME type "text/html" at a path determined by the page's URL:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>"root" → "/index.html"</description></item>
        ///   <item><description>Other paths → "/{urlPath}" (normalized with leading slash)</description></item>
        /// </list>
        /// <para>
        /// Only executes if <see cref="IEditorSettings.StaticWebPages"/> is enabled.
        /// </para>
        /// </remarks>
        Task CreateStaticFileAsync(PublishedPage page);

        /// <summary>
        /// Deletes static HTML files from blob storage for the specified published pages.
        /// </summary>
        /// <param name="pages">
        /// The published pages whose static files should be deleted.
        /// Each page's <see cref="PublishedPage.UrlPath"/> determines which file to remove.
        /// </param>
        /// <remarks>
        /// <para>
        /// Each page's <see cref="PublishedPage.UrlPath"/> is converted to a storage-relative path:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>"root" → "/index.html"</description></item>
        ///   <item><description>Any other path → "/{urlPath}" (normalized with leading slash)</description></item>
        /// </list>
        /// <para>
        /// Only executes if <see cref="IEditorSettings.StaticWebPages"/> is enabled.
        /// </para>
        /// <para>
        /// Failures are silently ignored (e.g., file not found) to ensure unpublish operations complete successfully.
        /// </para>
        /// </remarks>
        void DeleteStaticFiles(IEnumerable<PublishedPage> pages);
    }
}
