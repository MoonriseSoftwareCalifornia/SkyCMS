// <copyright file="BlogRenderingService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.BlogPublishing
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    ///  Blog publishing service implementation.
    /// </summary>
    public class BlogRenderingService : IBlogRenderingService
    {
        private readonly ApplicationDbContext dbContext;

        private readonly int deletedStatus = (int)StatusCodeEnum.Deleted;
        private readonly int redirectStatus = (int)StatusCodeEnum.Redirect;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlogRenderingService"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        public BlogRenderingService(ApplicationDbContext dbContext)
        {
            if (dbContext == null)
            {
                throw new ArgumentNullException(nameof(dbContext));
            }

            this.dbContext = dbContext;
        }

        /// <inheritdoc/>
        public async Task<string> GenerateBlogStreamHtml(Article article)
        {
            var template = await dbContext.Templates.FirstOrDefaultAsync(t => t.PageType == "blog-stream");
            if (template == null)
            {
                throw new InvalidOperationException("Blog stream template not found");
            }

            var htmlAgilityPack = new HtmlAgilityPack.HtmlDocument();
            htmlAgilityPack.LoadHtml(template.Content);

            var bannerImageNode = htmlAgilityPack.DocumentNode.SelectSingleNode("//img[@class='stream-banner']");
            if (bannerImageNode != null)
            {
                if (!string.IsNullOrEmpty(article.BannerImage))
                {
                    bannerImageNode.SetAttributeValue("src", article.BannerImage);
                }
                else
                {
                    bannerImageNode.SetAttributeValue("style", "display:none;");
                }
            }

            // Find the blog stream title node and set its inner text.
            var titleNode = htmlAgilityPack.DocumentNode.SelectSingleNode("//h1[@class='stream-title']");
            titleNode.InnerHtml = article.Title;

            // Find the blog stream description node and set its inner text.
            var descriptionNode = htmlAgilityPack.DocumentNode.SelectSingleNode("//p[@class='stream-description']");
            descriptionNode.InnerHtml = article.Introduction;

            // Find the blog entries container node.
            var entriesContainerNode = htmlAgilityPack.DocumentNode.SelectSingleNode("//div[@class='blog-items']");
            entriesContainerNode.RemoveAllChildren();

            // Query for blog entries belonging to this blog stream.
            var blogKey = article.BlogKey;
            var blogArticleNumber = article.ArticleNumber; // Exclude blog stream article from the list of blog entries.
            var publishedNow = DateTimeOffset.UtcNow;
            var entries = await dbContext.Articles
                .Where(a => a.BlogKey == blogKey
                    && a.ArticleNumber != blogArticleNumber
                    && a.Published <= publishedNow
                    && a.StatusCode != deletedStatus
                    && a.StatusCode != redirectStatus)
                .ToListAsync();

            // Take top 10 most recent entries, and remove duplicates by taking the last version published.
            var takeTop = entries
                .GroupBy(a => a.ArticleNumber)
                .Select(g => g.OrderByDescending(a => a.Published).First())
                .OrderByDescending(a => a.Published)
                .Take(10)
                .ToList();

            // Loop through each entry and create the HTML structure.
            foreach (var entry in takeTop)
            {
                // Create article node for each blog entry.
                var articleNode = htmlAgilityPack.CreateElement("article");
                articleNode.SetAttributeValue("class", "blog-item");

                // Add banner image if it exists.
                if (!string.IsNullOrEmpty(entry.BannerImage))
                {
                    var entryImageNode = htmlAgilityPack.CreateElement("img");
                    entryImageNode.SetAttributeValue("class", "blog-banner");
                    entryImageNode.SetAttributeValue("src", entry.BannerImage);
                    articleNode.AppendChild(entryImageNode);
                }

                // Add title node.
                var entryTitleNode = htmlAgilityPack.CreateElement("h2");
                entryTitleNode.SetAttributeValue("class", "blog-title");
                entryTitleNode.InnerHtml = entry.Title;
                articleNode.AppendChild(entryTitleNode);

                // Get first paragraph as introduction if Introduction is empty
                var introductionNode = htmlAgilityPack.CreateElement("p");
                introductionNode.SetAttributeValue("class", "blog-intro");

                // If Introduction is empty, extract first paragraph from content.
                if (string.IsNullOrEmpty(entry.Introduction))
                {
                    var content = await dbContext.Articles.Where(w => w.Id == entry.Id).Select(s => s.Content).FirstOrDefaultAsync();
                    var entryDoc = new HtmlAgilityPack.HtmlDocument();
                    entryDoc.LoadHtml(content);
                    var firstParagraph = entryDoc.DocumentNode.SelectSingleNode("//p");
                    introductionNode.InnerHtml = firstParagraph != null ? firstParagraph.InnerText : string.Empty;
                    articleNode.AppendChild(introductionNode);
                }
                else
                {
                    // Set introduction from the entry.
                    introductionNode.InnerHtml = entry.Introduction;
                    articleNode.AppendChild(introductionNode);
                }

                // Append entries to the container.
                entriesContainerNode.AppendChild(articleNode);
            }

            // Return content.
            return htmlAgilityPack.DocumentNode.OuterHtml;
        }

        /// <inheritdoc/>
        public async Task<string> GenerateBlogEntryHtml(Article article)
        {
            var template = await dbContext.Templates.FirstOrDefaultAsync(t => t.PageType == "blog-post");

            var htmlAgilityPack = new HtmlAgilityPack.HtmlDocument();
            htmlAgilityPack.LoadHtml(template.Content);

            // Find image div and add an image if there is a banner image.
            var imageDiv = htmlAgilityPack.DocumentNode.SelectSingleNode("//div[@class='ccms-blog-title-image']");
            imageDiv.ChildNodes.Clear();

            if (!string.IsNullOrEmpty(article.BannerImage))
            {
                var img = htmlAgilityPack.CreateElement("img");
                img.SetAttributeValue("src", article.BannerImage);
                img.AddClass("ccms-img-widget-img");
                imageDiv.AppendChild(img);
            }

            // Find the title node and set its inner text.
            var titleNode = htmlAgilityPack.DocumentNode.SelectSingleNode("//h1[@class='ccms-blog-item-title']");
            titleNode.InnerHtml = article.Title;

            // Find the content node and set its inner HTML.
            var contentNode = htmlAgilityPack.DocumentNode.SelectSingleNode("//div[@class='ccms-blog-item-content']");
            contentNode.InnerHtml = article.Content;

            return htmlAgilityPack.DocumentNode.OuterHtml;
        }
    }
}
