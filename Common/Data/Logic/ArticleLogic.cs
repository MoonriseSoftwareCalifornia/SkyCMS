// <copyright file="ArticleLogic.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data.Logic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Newtonsoft.Json;
    using X.Web.Sitemap;

    /// <summary>
    /// Core query and projection logic for retrieving, shaping, and enriching article/page
    /// data for public rendering and editorial experiences.
    /// </summary>
    /// <remarks>
    /// Responsibilities:
    /// <list type="bullet">
    ///   <item>Sitemap generation.</item>
    ///   <item>Hierarchical TOC derivation.</item>
    ///   <item>Published page resolution with optional in‑memory caching.</item>
    ///   <item>Layout resolution and lightweight layout caching.</item>
    ///   <item>Full text (LIKE-based) search (expensive; unindexed).</item>
    ///   <item>View model construction from persistence models.</item>
    ///   <item>Adjacent blog navigation (previous/next) enrichment.</item>
    /// </list>
    /// Thread-safety: Instance is not thread-safe; scope per request or operation pipeline.
    /// </remarks>
    public class ArticleLogic
    {
        /// <summary>
        /// Indicates whether current context is editor/authoring mode (enables extra data surface).
        /// </summary>
        private readonly bool isEditor;

        /// <summary>
        /// Optional in-memory cache (publisher tier). Null disables caching pathways.
        /// </summary>
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Base publisher URL used for absolute Open Graph URL/image construction.
        /// </summary>
        private readonly string publisherUrl;

        /// <summary>
        /// Public blob/content base URL (e.g., CDN root) used for image or asset resolution.
        /// </summary>
        private readonly string blobPublicUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleLogic"/> class.
        /// </summary>
        /// <param name="dbContext">EF Core context (content + identity).</param>
        /// <param name="memoryCache">Optional memory cache for short‑term view model/layout caching.</param>
        /// <param name="publisherUrl">Absolute publisher base URL (used for OG links).</param>
        /// <param name="blobPublicUrl">Public blob root (for assets).</param>
        /// <param name="isEditor">Flag indicating editor mode (affects view model flags/output).</param>
        public ArticleLogic(
            ApplicationDbContext dbContext,
            IMemoryCache memoryCache,
            string publisherUrl,
            string blobPublicUrl,
            bool isEditor = false)
        {
            this.memoryCache = memoryCache;
            DbContext = dbContext;
            this.isEditor = isEditor;
            this.publisherUrl = publisherUrl;
            this.blobPublicUrl = blobPublicUrl;
        }

        /// <summary>
        /// Gets provides simple diagnostics describing cache hit/miss sequence (optional usage).
        /// </summary>
        public string[] CacheResult { get; internal set; }

        /// <summary>
        /// Gets the backing database context for queries.
        /// </summary>
        protected ApplicationDbContext DbContext { get; }

        /// <summary>
        /// Health probe: returns true when publisher logic layer is available.
        /// </summary>
        /// <returns>Gets the health status of the publisher logic layer.</returns>
        public static bool GetPublisherHealth() => true;

        /// <summary>
        /// Deserialize a UTF-32 encoded JSON payload into a <typeparamref name="T"/> instance.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="bytes">UTF-32 encoded JSON byte array.</param>
        /// <returns>A deserialized instance of <typeparamref name="T"/>.</returns>
        public static T Deserialize<T>(byte[] bytes)
        {
            var data = Encoding.UTF32.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(data);
        }

        /// <summary>
        /// Serialize an object as JSON and return UTF-32 encoded bytes.
        /// </summary>
        /// <param name="obj">Object to serialize.</param>
        /// <returns>UTF-32 encoded JSON byte array.</returns>
        public static byte[] Serialize(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            return Encoding.UTF32.GetBytes(JsonConvert.SerializeObject(obj));
        }

        /// <summary>
        /// Builds a sitemap consisting of root and published content entries.
        /// </summary>
        /// <remarks>
        /// Uses basic priority heuristics (root=1.0, others=0.5). Banner image (if present) is attached.
        /// Not cached; caller should wrap with caching if frequently invoked.
        /// </remarks>
        /// <returns>Sitemap instance.</returns>
        public async Task<Sitemap> GetSiteMap()
        {
            var publicUrl = "/";
            var dt = DateTimeOffset.UtcNow.AddMinutes(10); // slight future window to allow near-future scheduled pages
            var query = from t in DbContext.ArticleCatalog
                        where t.Published <= dt
                        select new { t.UrlPath, t.Title, t.Published, t.Updated, t.BannerImage };
            var items = await query.ToListAsync();
            var home = items.FirstOrDefault(f => f.UrlPath == "root");
            var others = items.Where(w => w.UrlPath != "root").ToList();

            var sitemap = new Sitemap();

            var url = new Url
            {
                Location = publicUrl,
                LastMod = (home == null || home.Updated == null)
                    ? DateTimeOffset.UtcNow.ToString("u")
                    : home.Updated.ToString("u"),
                Priority = 1.0,
                Images = new List<Image>()
            };

            if (home != null && !string.IsNullOrWhiteSpace(home.BannerImage))
            {
                url.Images.Add(new Image { Location = $"{publicUrl}/images/logo.png" });
            }

            sitemap.Add(url);

            foreach (var item in others)
            {
                var images = new List<Image>();
                if (!string.IsNullOrEmpty(item.BannerImage))
                {
                    images.Add(new Image
                    {
                        Location = item.UrlPath.ToLower().StartsWith("http")
                            ? item.UrlPath
                            : $"{publicUrl}/{item.UrlPath.TrimStart('/')}"
                    });
                }

                sitemap.Add(new Url
                {
                    Location = $"{publicUrl}/{item.UrlPath}",
                    LastMod = item.Updated.ToString("u"),
                    Priority = 0.5,
                    Images = images
                });
            }

            return sitemap;
        }

        /// <summary>
        /// Returns a paged list of immediate child pages (or root-level pages if no prefix provided).
        /// </summary>
        /// <param name="prefix">Parent path fragment (e.g., "blog"). Slash/whitespace normalized.</param>
        /// <param name="pageNo">Zero-based page index.</param>
        /// <param name="pageSize">Page size.</param>
        /// <param name="orderByPublishedDate">When true sorts by newest publish date, else by Title.</param>
        /// <remarks>
        /// Filtering logic uses a regex pattern to approximate one-level deep children.
        /// Future optimization: replace regex with hierarchical index or persisted depth metadata.
        /// </remarks>
        /// <returns>Paged table of contents.</returns>
        [Obsolete("Use IArticleCatalogQueryService.GetTableOfContentsAsync instead", false)]
        public async Task<TableOfContents> GetTableOfContents(string prefix, int pageNo = 0, int pageSize = 10, bool orderByPublishedDate = false)
        {
            if (string.IsNullOrEmpty(prefix) || string.IsNullOrWhiteSpace(prefix) || prefix.Equals("/"))
            {
                prefix = string.Empty;
            }
            else
            {
                prefix = (System.Web.HttpUtility.UrlDecode(prefix.ToLower()
                        .Replace("%20", "_")
                        .Replace(" ", "_")) + "/")
                    .Trim('/');
            }

            var skip = pageNo * pageSize;

            IQueryable<TableOfContentsItem> query;

            if (string.IsNullOrEmpty(prefix))
            {
                query = from t in DbContext.ArticleCatalog
                        where t.Published.HasValue
                        select new TableOfContentsItem
                        {
                            UrlPath = t.UrlPath,
                            Title = t.Title,
                            Published = t.Published.Value,
                            Updated = t.Updated,
                            BannerImage = t.BannerImage,
                            AuthorInfo = t.AuthorInfo,
                            Introduction = t.Introduction
                        };
            }
            else
            {
                var count = prefix.Count(c => c == '/');
                var dcount = "{" + count + "}";
                var epath = prefix.TrimStart('/').Replace("/", "\\/");
                var pattern = $"(?i)(^[{epath}]*)(\\/[^\\/]*){dcount}$";

                query = from t in DbContext.ArticleCatalog
                        where t.Published.HasValue
                              && t.UrlPath != prefix
                              && t.UrlPath.StartsWith(prefix)
                              && Regex.IsMatch(t.UrlPath, pattern)
                        select new TableOfContentsItem
                        {
                            UrlPath = t.UrlPath,
                            Title = t.Title,
                            Published = t.Published.Value,
                            Updated = t.Updated,
                            BannerImage = t.BannerImage,
                            AuthorInfo = t.AuthorInfo,
                            Introduction = t.Introduction
                        };
            }

            var data = await query.ToListAsync();
            var sort = data.AsQueryable();
            sort = orderByPublishedDate
                ? sort.OrderByDescending(o => o.Published)
                : sort.OrderBy(o => o.Title);

            var now = DateTimeOffset.UtcNow;
            var items = sort
                .Where(w => w.Published.UtcDateTime <= now)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            return new TableOfContents
            {
                TotalCount = items.Count,
                PageNo = pageNo,
                PageSize = pageSize,
                Items = items,
                PublisherUrl = publisherUrl,
                BlobPublicUrl = blobPublicUrl
            };
        }

        /// <summary>
        /// Resolve the current published version of a page/article by URL path with optional caching.
        /// </summary>
        /// <param name="urlPath">URL path (e.g., "blog/my-article"). Case-insensitive. Root page is "root".</param>
        /// <param name="lang">Language code.</param>
        /// <param name="cacheSpan">Cache duration.</param>
        /// <param name="layoutCache">Layout cache duration.</param>
        /// <param name="includeLayout">Whether to include layout information.</param>
        /// <remarks>
        /// Cache key: {url}-{lang}-{includeLayout}. Layout caching duration is separate.
        /// SQLite nuance: DateTimeOffset comparison adjustments addressed by explicit HasValue checks.
        /// </remarks>
        /// <returns>Article view model.</returns>
        [Obsolete("Use IPublishedPageQueryService.GetPublishedPageByUrlAsync instead", false)]
        public virtual async Task<ArticleViewModel> GetPublishedPageByUrl(string urlPath, string lang = "", TimeSpan? cacheSpan = null, TimeSpan? layoutCache = null, bool includeLayout = true)
        {
            urlPath = urlPath?.ToLower().Trim(new char[] { ' ', '/' });
            if (string.IsNullOrEmpty(urlPath) || urlPath.Trim() == "/")
            {
                urlPath = "root";
            }

            if (memoryCache == null || cacheSpan == null)
            {
                var dt = DateTimeOffset.UtcNow;
                var entity = await DbContext.Pages
                    .Where(a => a.UrlPath == urlPath && a.Published <= dt)
                    .OrderByDescending(o => o.VersionNumber)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (entity == null)
                {
                    return null;
                }

                return await BuildArticleViewModel(entity, lang, includeLayout: includeLayout);
            }

            memoryCache.TryGetValue($"{urlPath}-{lang}-{includeLayout}", out ArticleViewModel model);

            if (model == null)
            {
                var dt = DateTimeOffset.UtcNow;
                var data = await DbContext.Pages
                    .Where(a => a.UrlPath == urlPath && a.Published.HasValue && a.Published <= dt)
                    .OrderByDescending(o => o.VersionNumber)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (data == null)
                {
                    return null;
                }

                model = await BuildArticleViewModel(data, lang, layoutCache, includeLayout);
                memoryCache.Set($"{urlPath}-{lang}-{includeLayout}", model, cacheSpan.Value);
            }

            return model;
        }

        /// <summary>
        /// Lightweight header-only fetch (omits large text fields) for a published page used in partial render or dependency checks.
        /// </summary>
        /// <param name="urlPath">URL path (e.g., "blog/my-article"). Case-insensitive. Root page is "root".</param>
        /// <returns>Article view model.</returns>
        [Obsolete("Use IPublishedPageQueryService.GetPublishedPageHeaderByUrlAsync instead", false)]
        public virtual async Task<ArticleViewModel> GetPublishedPageHeaderByUrl(string urlPath)
        {
            urlPath = urlPath?.ToLower().Trim(new char[] { ' ', '/' });
            if (string.IsNullOrEmpty(urlPath) || urlPath.Trim() == "/")
            {
                urlPath = "root";
            }

            var dt = DateTimeOffset.UtcNow;
            return await DbContext.Pages
                .Where(a => a.UrlPath == urlPath && a.Published.HasValue && a.Published <= dt)
                .Select(s => new ArticleViewModel
                {
                    ArticleNumber = s.ArticleNumber,
                    Id = s.Id,
                    Expires = s.Expires,
                    Updated = s.Updated,
                    VersionNumber = s.VersionNumber
                })
                .OrderByDescending(o => o.VersionNumber)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Returns the default layout (optionally cached) including navigation markup placeholders.
        /// </summary>
        /// <param name="layoutCache">Optional layout cache duration.</param>
        /// <returns>The default layout view model.</returns>
        public async Task<LayoutViewModel> GetDefaultLayout(TimeSpan? layoutCache = null)
        {
            if (memoryCache == null || layoutCache == null)
            {
                var entity = await LayoutHelper.GetCurrentDefaultLayoutAsync(DbContext);
                return new LayoutViewModel(entity);
            }

            memoryCache.TryGetValue("defLayout", out LayoutViewModel model);

            if (model == null)
            {
                var entity = await LayoutHelper.GetCurrentDefaultLayoutAsync(DbContext);

                DbContext.Entry(entity).State = EntityState.Detached;
                model = new LayoutViewModel(entity);
                memoryCache.Set("defLayout", model, layoutCache.Value);
            }

            return model;
        }

        /// <summary>
        /// Naive full-text (Contains/LIKE based) search across published pages (Title + Content).
        /// </summary>
        /// <param name="text">Search text.</param>
        /// <returns>A list of matching table of contents items.</returns>
        /// <remarks>
        /// Expensive for large datasets. Consider external indexing (e.g., Azure Search, Elastic) for scale.
        /// Multi-term queries are AND-combined.
        /// </remarks>
        [Obsolete("Use IArticleCatalogQueryService.SearchAsync instead", false)]
        public async Task<List<TableOfContentsItem>> Search(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<TableOfContentsItem>();
            }

            text = text.ToLower();

            var dt = DateTimeOffset.UtcNow;
            var query = DbContext.Pages
                .Where(a => a.StatusCode == 0
                            && a.Published <= dt
                            && (a.Content.ToLower().Contains(text) || a.Title.ToLower().Contains(text)))
                .AsQueryable();

            var terms = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (terms.Length > 1)
            {
                foreach (var term in terms)
                {
                    query = query.Where(a => a.Content.ToLower().Contains(term) || a.Title.ToLower().Contains(term));
                }
            }

            query = query.OrderByDescending(o => o.Title);

            var results = await query.Select(s => new TableOfContentsItem
            {
                UrlPath = "/" + s.UrlPath,
                Title = s.Title,
                Published = s.Published.Value,
                Updated = s.Updated,
                BannerImage = "/" + s.BannerImage,
                AuthorInfo = s.AuthorInfo
            }).ToListAsync();

            return results.Select(s => new TableOfContentsItem
            {
                AuthorInfo = s.AuthorInfo,
                BannerImage = string.IsNullOrEmpty(s.BannerImage) ? string.Empty : "/" + s.BannerImage,
                Published = s.Published,
                Title = s.Title,
                Updated = s.Updated,
                UrlPath = s.UrlPath == "/root" ? "/" : s.UrlPath
            }).ToList();
        }

        /// <summary>
        /// Fetch previous and next published blog posts relative to a given publish timestamp.
        /// </summary>
        /// <param name="published">The publish timestamp to compare against.</param>
        /// <returns>A tuple containing the previous and next blog posts.</returns>
        [Obsolete("Use IBlogNavigationService.GetAdjacentBlogPostsAsync instead", false)]
        public async Task<(TableOfContentsItem previous, TableOfContentsItem next)> GetAdjacentBlogPosts(DateTimeOffset published)
        {
            var prev = await DbContext.ArticleCatalog
                .Where(a => a.Published < published && a.Published != null)
                .OrderByDescending(a => a.Published)
                .Select(a => new TableOfContentsItem { Title = a.Title, UrlPath = a.UrlPath, Published = a.Published.Value })
                .FirstOrDefaultAsync();

            var next = await DbContext.ArticleCatalog
                .Where(a => a.Published > published && a.Published != null)
                .OrderBy(a => a.Published)
                .Select(a => new TableOfContentsItem { Title = a.Title, UrlPath = a.UrlPath, Published = a.Published.Value })
                .FirstOrDefaultAsync();

            return (prev, next);
        }

        /// <summary>
        /// Enriches a blog post view model with previous/next navigation links when applicable.
        /// No-op for non-blog types or unpublished content.
        /// </summary>
        /// <param name="model">Blog post view model.</param>
        /// <returns>Task.</returns>
        [Obsolete("Use IBlogNavigationService.EnrichBlogNavigationAsync instead", false)]
        public async Task EnrichBlogNavigation(ArticleViewModel model)
        {
            if (model == null || model.ArticleType != ArticleType.BlogPost || !model.Published.HasValue)
            {
                return;
            }

            var (previous, next) = await GetAdjacentBlogPosts(model.Published.Value);

            if (previous != null)
            {
                model.PreviousTitle = previous.Title;
                model.PreviousUrl = previous.UrlPath == "root" ? "/" : "/" + previous.UrlPath.TrimStart('/');
            }

            if (next != null)
            {
                model.NextTitle = next.Title;
                model.NextUrl = next.UrlPath == "root" ? "/" : "/" + next.UrlPath.TrimStart('/');
            }
        }

        /// <summary>
        /// Build a full <see cref="ArticleViewModel"/> from an <see cref="Article"/> draft/published entity.
        /// </summary>
        /// <param name="article">Source article entity.</param>
        /// <param name="lang">Language code.</param>
        /// <param name="includeLayout">Whether to include layout information.</param>
        /// <remarks>
        /// Author info is serialized (single-quoted JSON) to embed safely in attributes if needed.
        /// </remarks>
        /// <returns>A <see cref="ArticleViewModel"/>.</returns>
        protected async Task<ArticleViewModel> BuildArticleViewModel(Article article, string lang, bool includeLayout = true)
        {
            var author = string.Empty;
            if (!string.IsNullOrEmpty(article.UserId))
            {
                var authorInfo = await DbContext.AuthorInfos.AsNoTracking().FirstOrDefaultAsync(f => f.Id == article.UserId);
                if (authorInfo != null)
                {
                    author = JsonConvert.SerializeObject(authorInfo).Replace("\"", "'");
                }
            }

            var layout = includeLayout ? await GetDefaultLayout() : await GetDefaultLayout();
            return new ArticleViewModel(article, layout, isEditor, author, lang);
        }

        /// <summary>
        /// Build a full <see cref="ArticleViewModel"/> from a draft or persisted article entity.
        /// </summary>
        /// <param name="article">Source article entity.</param>
        /// <param name="lang">Language code.</param>
        /// <param name="includeLayout">Whether to include layout information.</param>
        /// <returns>A <see cref="ArticleViewModel"/>.</returns>
        public Task<ArticleViewModel> BuildArticleViewModelAsync(Article article, string lang, bool includeLayout = true)
        {
            return BuildArticleViewModel(article, lang, includeLayout: includeLayout);
        }

        /// <summary>
        /// Build a full <see cref="ArticleViewModel"/> from a persisted published snapshot (<see cref="PublishedPage"/>).
        /// </summary>
        /// <param name="article">Source published page entity.</param>
        /// <param name="lang">Language code.</param>
        /// <param name="layoutCache">Layout cache duration.</param>
        /// <param name="includeLayout">Whether to include layout information.</param>
        /// <returns>A <see cref="ArticleViewModel"/>.</returns>
        protected async Task<ArticleViewModel> BuildArticleViewModel(PublishedPage article, string lang, TimeSpan? layoutCache = null, bool includeLayout = true)
        {
            return new ArticleViewModel
            {
                ArticleNumber = article.ArticleNumber,
                BannerImage = article.BannerImage,
                LanguageCode = lang,
                LanguageName = string.Empty,
                CacheDuration = 10,
                Content = article.Content,
                StatusCode = (StatusCodeEnum)article.StatusCode,
                Id = article.Id,
                Published = article.Published ?? null,
                Title = article.Title,
                UrlPath = article.UrlPath,
                Updated = article.Updated,
                VersionNumber = article.VersionNumber,
                HeadJavaScript = article.HeaderJavaScript,
                FooterJavaScript = article.FooterJavaScript,
                Layout = includeLayout ? await GetDefaultLayout(layoutCache) : null,
                ReadWriteMode = isEditor,
                Expires = article.Expires ?? null,
                AuthorInfo = article.AuthorInfo,
                OGDescription = string.Empty,
                OGImage = string.IsNullOrEmpty(article.BannerImage)
                    ? string.Empty
                    : article.BannerImage.StartsWith("http")
                        ? article.BannerImage
                        : publisherUrl.TrimEnd('/') + "/" + article.BannerImage.TrimStart('/'),
                OGUrl = GetOGUrl(article.UrlPath),
                ArticleType = (ArticleType)article.ArticleType,
                Category = article.Category,
                Introduction = article.Introduction
            };
        }

        /// <summary>
        /// Compose an absolute Open Graph URL for a page based on publisher base URL.
        /// </summary>
        private string GetOGUrl(string urlPath)
        {
            if (string.IsNullOrWhiteSpace(publisherUrl))
            {
                return urlPath;
            }

            return publisherUrl.TrimEnd('/') + "/" + urlPath.TrimStart('/');
        }
    }
}
