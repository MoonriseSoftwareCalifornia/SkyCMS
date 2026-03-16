// <copyright file="ITocService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.TableOfContents
{
    using System.Threading.Tasks;

    /// <summary>
    /// Service for generating and managing Table of Contents (TOC) JSON files.
    /// </summary>
    /// <remarks>
    /// This service creates denormalized TOC JSON files in blob storage for fast client-side access.
    /// TOC files are generated when articles are published/unpublished to keep navigation menus current.
    /// </remarks>
    public interface ITocService
    {
        /// <summary>
        /// Writes a Table of Contents (TOC) JSON file to blob storage for the specified prefix.
        /// </summary>
        /// <param name="prefix">
        /// The URL prefix to generate TOC for. Defaults to "/" for the root TOC.
        /// Blog-specific TOCs use the blog key (e.g., "/tech-blog").
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// Generates a JSON representation of the site's navigation structure and stores it in blob storage
        /// for client-side consumption. The TOC includes published articles within the specified prefix scope.
        /// </para>
        /// <para>
        /// TOC file locations:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Root TOC → "/toc.json"</description></item>
        ///   <item><description>Blog TOC → "/pub/---toc/{prefix}/toc.json"</description></item>
        /// </list>
        /// <para>
        /// This operation is thread-safe using a semaphore to prevent concurrent writes
        /// that could cause DbContext concurrency exceptions in multi-tenant scenarios.
        /// </para>
        /// <para>
        /// Only executes when <see cref="IEditorSettings.StaticWebPages"/> is enabled.
        /// </para>
        /// </remarks>
        Task WriteTocAsync(string prefix = "/");
    }
}
