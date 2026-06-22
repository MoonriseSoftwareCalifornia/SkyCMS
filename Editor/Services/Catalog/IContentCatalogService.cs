// <copyright file="IContentCatalogService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Service for querying article and template catalog metadata across the application.
    /// Provides a consistent interface for listing and resolving content titles.
    /// </summary>
    public interface IContentCatalogService
    {
        /// <summary>
        /// Gets a summary list of all articles from the catalog.
        /// </summary>
        /// <returns>List of article summaries with number, title, and last updated timestamp.</returns>
        Task<List<ArticleCatalogSummary>> GetArticlesAsync();

        /// <summary>
        /// Gets a summary list of all templates for the current layout.
        /// </summary>
        /// <param name="layoutNumber">Layout number to filter templates.</param>
        /// <returns>List of template summaries with ID and title.</returns>
        Task<List<TemplateCatalogSummary>> GetTemplatesAsync(int layoutNumber);

        /// <summary>
        /// Gets a list of blog stream articles (parent blogs).
        /// </summary>
        /// <returns>List of blog stream summaries with article number, title, and blog key.</returns>
        Task<List<BlogStreamSummary>> GetBlogStreamsAsync();

        /// <summary>
        /// Gets a list of blog posts for a specific blog key.
        /// </summary>
        /// <param name="blogKey">The blog key identifying the parent blog stream.</param>
        /// <returns>List of blog post summaries sorted newest-first by publish date.</returns>
        Task<List<BlogPostSummary>> GetBlogPostsAsync(string blogKey);

        /// <summary>
        /// Resolves the title for a given article number from the catalog.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Article title or null if not found.</returns>
        Task<string?> ResolveArticleTitleAsync(int articleNumber);

        /// <summary>
        /// Resolves the title for a given template ID.
        /// </summary>
        /// <param name="templateId">Template ID.</param>
        /// <returns>Template title or null if not found.</returns>
        Task<string?> ResolveTemplateTitleAsync(Guid templateId);
    }

    /// <summary>
    /// Article catalog summary DTO.
    /// </summary>
    public class ArticleCatalogSummary
    {
        /// <summary>
        /// Gets or sets the article number.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the article title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last updated timestamp.
        /// </summary>
        public DateTimeOffset Updated { get; set; }
    }

    /// <summary>
    /// Template catalog summary DTO.
    /// </summary>
    public class TemplateCatalogSummary
    {
        /// <summary>
        /// Gets or sets the template ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the template title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the layout number this template belongs to.
        /// </summary>
        public int LayoutNumber { get; set; }
    }

    /// <summary>
    /// Blog stream summary DTO.
    /// </summary>
    public class BlogStreamSummary
    {
        /// <summary>
        /// Gets or sets the article number of the blog stream.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the blog stream title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog key.
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// Blog post summary DTO.
    /// </summary>
    public class BlogPostSummary
    {
        /// <summary>
        /// Gets or sets the article ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the article number.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the post title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the post is currently published.
        /// </summary>
        public bool IsPublished { get; set; }

        /// <summary>
        /// Gets or sets the publish date.
        /// </summary>
        public DateTimeOffset? Published { get; set; }
    }
}
