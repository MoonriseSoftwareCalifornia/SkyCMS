// <copyright file="IPublishingService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Publishing;

using Cosmos.Common.Data;
using Sky.Editor.Services.CDN;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Defines operations for publishing articles to the database and static web pages,
/// managing redirects, and coordinating CDN cache operations.
/// </summary>
/// <remarks>
/// <para>
/// This service handles the core publishing lifecycle including:
/// </para>
/// <list type="bullet">
///   <item><description>Publishing articles as static HTML files to blob storage</description></item>
///   <item><description>Managing published page records in the database</description></item>
///   <item><description>Version control and unpublishing earlier versions</description></item>
///   <item><description>Coordinating CDN cache purge operations</description></item>
///   <item><description>Generating and maintaining table of contents (TOC) files</description></item>
/// </list>
/// <para>
/// This service does NOT handle catalog updates or hierarchical table of contents maintenance
/// beyond writing the TOC JSON file.
/// </para>
/// </remarks>
public interface IPublishingService
{
    /// <summary>
    /// Publishes an article to the database and generates associated static content.
    /// </summary>
    /// <param name="article">The article to publish. Must have valid <see cref="Article.ArticleNumber"/>, <see cref="Article.UrlPath"/>, and content properties.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// A task producing a list of <see cref="CdnResult"/> objects representing the outcome of CDN purge operations.
    /// Returns an empty list if no CDN service is configured or if the operation fails.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs a complete publish workflow:
    /// </para>
    /// <list type="number">
    ///   <item><description>Sets the publish timestamp if not already specified</description></item>
    ///   <item><description>Un-publishes earlier versions of the same article number</description></item>
    ///   <item><description>Removes prior published page records (excluding redirects)</description></item>
    ///   <item><description>Creates a new <see cref="PublishedPage"/> record with author information</description></item>
    ///   <item><description>Generates and uploads a static HTML file to blob storage (if static pages enabled)</description></item>
    ///   <item><description>Regenerates the table of contents JSON file</description></item>
    ///   <item><description>If is a blog post, updates the table of contents JSON file for the blog stream</description></item>
    ///   <item><description>Purges the CDN cache for the specific page URL</description></item>
    /// </list>
    /// <para>
    /// If <see cref="Article.Published"/> is null, it will be set to 1 second before the current UTC time
    /// to ensure immediate publication.
    /// </para>
    /// </remarks>
    Task<List<CdnResult>> PublishAsync(Article article, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates static HTML files for the specified published pages and purges the CDN cache.
    /// </summary>
    /// <param name="ids">Collection of page identifiers to generate static files for.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This method is used for batch static page generation, typically during republishing operations
    /// or site-wide regeneration events. It performs the following actions:
    /// </para>
    /// <list type="number">
    ///   <item><description>Retrieves all published pages matching the provided IDs from the database</description></item>
    ///   <item><description>Generates and uploads static HTML files for each page to blob storage</description></item>
    ///   <item><description>Regenerates the table of contents (TOC) JSON file</description></item>
    ///   <item><description>Triggers a full CDN cache purge if a CDN service is configured</description></item>
    /// </list>
    /// <para>
    /// Unlike <see cref="PublishAsync"/>, this method performs a full CDN purge rather than selective path purging.
    /// Only processes pages if static web page generation is enabled in editor settings.
    /// </para>
    /// </remarks>
    Task CreateStaticPages(IEnumerable<Guid> ids);

    /// <summary>
    /// Unpublishes all versions of an article and removes associated published content.
    /// </summary>
    /// <param name="article">The article to unpublish. Must have a valid <see cref="Article.ArticleNumber"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This method removes an article from public visibility by:
    /// </para>
    /// <list type="number">
    ///   <item><description>Locating all published versions matching the article number</description></item>
    ///   <item><description>Setting their <see cref="Article.Published"/> property to null</description></item>
    ///   <item><description>Removing corresponding <see cref="PublishedPage"/> records (excluding redirects)</description></item>
    ///   <item><description>Deleting associated static HTML files from blob storage</description></item>
    ///   <item><description>Purging CDN cache for each removed page</description></item>
    ///   <item><description>Regenerating the table of contents JSON file</description></item>
    /// </list>
    /// <para>
    /// If no published versions exist for the article number, the method returns immediately without changes.
    /// Redirect pages (those with <c>StatusCode == StatusCodeEnum.Redirect</c>) are preserved.
    /// </para>
    /// </remarks>
    Task UnpublishAsync(Article article);

    /// <summary>
    /// Generates and uploads a table of contents (TOC) JSON file to blob storage.
    /// </summary>
    /// <param name="prefix">The path prefix for the TOC file location. Defaults to "/".</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous upload operation.</returns>
    /// <remarks>
    /// <para>
    /// This method creates a JSON representation of the site's content hierarchy and uploads it
    /// to blob storage for consumption by clients (e.g., navigation widgets, sitemaps).
    /// </para>
    /// <para>
    /// The TOC is generated by querying all published pages and their hierarchical relationships,
    /// serialized to JSON, and uploaded to a path determined by the prefix:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Empty or null prefix → "/toc.json"</description></item>
    ///   <item><description>Non-empty prefix → "/{prefix}/toc.json"</description></item>
    /// </list>
    /// <para>
    /// Only executes if static web page generation is enabled in editor settings.
    /// Returns immediately if TOC data cannot be generated (e.g., no published content exists).
    /// </para>
    /// </remarks>
    Task WriteTocAsync(string prefix = "/");
}
