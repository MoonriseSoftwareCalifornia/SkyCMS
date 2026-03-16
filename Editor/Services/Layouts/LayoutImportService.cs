// <copyright file="LayoutImportService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layouts
{
    using Cosmos.Cms.Data.Logic;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using HtmlAgilityPack;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Service for importing layouts and templates from external sources.
    /// </summary>
    public class LayoutImportService : ILayoutImportService
    {
        private const string COSMOSLAYOUTSREPO = "https://cwalabs.github.io/Cosmos.Starter.Designs";
        private const string CATALOG_CACHE_KEY = "LayoutCatalog";
        private static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromHours(1);

        private readonly IHttpClientFactory httpClientFactory;
        private readonly IMemoryCache cache;
        private readonly ILogger<LayoutImportService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutImportService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        /// <param name="cache">Memory cache.</param>
        /// <param name="logger">Logger.</param>
        public LayoutImportService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            ILogger<LayoutImportService> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.cache = cache;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task<Root> GetCommunityCatalogAsync()
        {
            if (cache.TryGetValue(CATALOG_CACHE_KEY, out Root cachedCatalog))
            {
                return cachedCatalog;
            }

            try
            {
                var client = httpClientFactory.CreateClient();
                var url = $"{COSMOSLAYOUTSREPO}/catalog.json";

                var data = await client.GetStringAsync(url);
                var catalog = JsonConvert.DeserializeObject<Root>(data);

                cache.Set(CATALOG_CACHE_KEY, catalog, CatalogCacheDuration);
                return catalog;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load community layout catalog");
                return new Root { LayoutCatalog = new List<LayoutCatalogItem>() };
            }
        }

        /// <inheritdoc/>
        public async Task<Layout> GetCommunityLayoutAsync(string layoutId, bool isDefault)
        {
            var catalog = await GetCommunityCatalogAsync();
            var item = catalog.LayoutCatalog.FirstOrDefault(f => f.Id == layoutId);

            if (item == null)
            {
                throw new InvalidOperationException($"Layout with ID '{layoutId}' not found.");
            }

            var client = httpClientFactory.CreateClient();
            var url = $"{COSMOSLAYOUTSREPO}/Layouts/{item.Id}/layout.html";
            var html = await client.GetStringAsync(url);

            var layout = ParseHtml(html);
            layout.CommunityLayoutId = layoutId;
            layout.IsDefault = isDefault;
            layout.LayoutName = item.Name;
            layout.Notes = item.Description;

            return layout;
        }

        /// <inheritdoc/>
        public async Task<List<Page>> GetPageTemplatesAsync(string layoutId)
        {
            var catalog = await GetCommunityCatalogAsync();
            var layout = catalog.LayoutCatalog.FirstOrDefault(f => f.Id == layoutId);

            if (layout == null)
            {
                return new List<Page>();
            }

            try
            {
                var client = httpClientFactory.CreateClient();
                var url = $"{COSMOSLAYOUTSREPO}/Layouts/{layout.Id}/catalog.json";
                var data = await client.GetStringAsync(url);

                var root = JsonConvert.DeserializeObject<PageRoot>(data);
                return root.Pages.OrderBy(o => o.Title).ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load page templates for layout {LayoutId}", layoutId);
                return new List<Page>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<Template>> GetCommunityTemplatePagesAsync(string communityLayoutId = "")
        {
            if (string.IsNullOrEmpty(communityLayoutId))
            {
                communityLayoutId = "bs5-strt"; // Default layout ID
            }

            var catalog = await GetCommunityCatalogAsync();
            var layout = catalog.LayoutCatalog.FirstOrDefault(f => f.Id == communityLayoutId);

            if (layout == null)
            {
                return new List<Template>();
            }

            var templates = new List<Template>();
            var pages = await GetPageTemplatesAsync(layout.Id);
            var client = httpClientFactory.CreateClient();

            foreach (var page in pages)
            {
                try
                {
                    var url = $"{COSMOSLAYOUTSREPO}/Layouts/{layout.Id}/{page.Path}";
                    var html = await client.GetStringAsync(url);

                    var template = ParseHtml<Template>(html);
                    template.PageType = page.Type;
                    template.Description = page.Description;
                    template.Title = page.Type == "home" ? "Home Page" : page.Title;
                    template.CommunityLayoutId = layout.Id;
                    templates.Add(template);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load template page {PagePath} for layout {LayoutId}", page.Path, layout.Id);
                }
            }

            return templates.Distinct().ToList();
        }

        /// <inheritdoc/>
        public Layout ParseHtml(string html)
        {
            var contentHtmlDocument = new HtmlDocument();
            contentHtmlDocument.LoadHtml(html);

            var head = contentHtmlDocument.DocumentNode.SelectSingleNode("//head");
            var body = contentHtmlDocument.DocumentNode.SelectSingleNode("//body");
            var bodyHeader = contentHtmlDocument.DocumentNode.SelectSingleNode("//body/cosmos-layout-header");
            var bodyFooter = contentHtmlDocument.DocumentNode.SelectSingleNode("//body/cosmos-layout-footer");

            var layout = new Layout
            {
                IsDefault = false,
                CommunityLayoutId = string.Empty,
                LayoutName = string.Empty,
                Notes = string.Empty,
                Head = head?.InnerHtml,
                BodyHtmlAttributes = ParseAttributes(body?.Attributes),
                HtmlHeader = bodyHeader?.InnerHtml,
                FooterHtmlContent = bodyFooter?.InnerHtml
            };

            return layout;
        }

        /// <inheritdoc/>
        public T ParseHtml<T>(string html)
        {
            var contentHtmlDocument = new HtmlDocument();
            contentHtmlDocument.LoadHtml(html);

            // Remove layout elements
            var bodyHeader = contentHtmlDocument.DocumentNode.SelectSingleNode("//body/cosmos-layout-header");
            var bodyFooter = contentHtmlDocument.DocumentNode.SelectSingleNode("//body/cosmos-layout-footer");
            bodyHeader?.Remove();
            bodyFooter?.Remove();

            // Save what remains in the body
            var body = contentHtmlDocument.DocumentNode.SelectSingleNode("//body");

            object model = null;
            if (typeof(T) == typeof(Template))
            {
                model = new Template
                {
                    Content = body.InnerHtml,
                    Description = string.Empty,
                    Title = string.Empty
                };
            }
            else if (typeof(T) == typeof(Article))
            {
                model = new Article
                {
                    Content = body.InnerHtml,
                    Title = string.Empty,
                    StatusCode = (int)StatusCodeEnum.Active
                };
            }
            else
            {
                throw new NotSupportedException($"Type {typeof(T)} is not supported for this operation.");
            }

            return (T)model;
        }

        private string ParseAttributes(HtmlAttributeCollection collection)
        {
            if (collection == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var attribute in collection)
            {
                builder.Append($"{attribute.Name}=\"{attribute.Value}\" ");
            }

            return builder.ToString().Trim();
        }
    }
}
