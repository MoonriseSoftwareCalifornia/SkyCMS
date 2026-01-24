// <copyright file="BaseController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using Cosmos.Common.Data;
    using Cosmos.Common.Models;
    using Cosmos.DynamicConfig;
    using HtmlAgilityPack;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;

    /// <summary>
    /// Base controller.
    /// </summary>
    public abstract class BaseController : Controller
    {
        private readonly UserManager<IdentityUser> baseUserManager;
        private readonly ApplicationDbContext dbContext;
        private readonly IMemoryCache? memoryCache;
        private readonly IDynamicConfigurationProvider? configProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseController"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="memoryCache">Memory cache (optional, for layout caching).</param>
        /// <param name="configProvider">Dynamic configuration provider (optional, for tenant-aware caching).</param>
        internal BaseController(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            IMemoryCache? memoryCache = null,
            IDynamicConfigurationProvider? configProvider = null)
        {
            this.dbContext = dbContext;
            baseUserManager = userManager;
            this.memoryCache = memoryCache;
            this.configProvider = configProvider;
        }

        /// <summary>
        ///     Server-side validation of HTML.
        /// </summary>
        /// <param name="fieldName">Field name to validate.</param>
        /// <param name="inputHtml">HTML data to check.</param>
        /// <returns>HTML content.</returns>
        /// <remarks>
        ///     <para>
        ///         The purpose of this method is to validate HTML prior to be saved to the database.
        ///         It uses an instance of <see cref="HtmlAgilityPack.HtmlDocument" /> to check HTML formatting.
        ///     </para>
        /// </remarks>
        internal string BaseValidateHtml(string fieldName, string inputHtml)
        {
            if (!string.IsNullOrEmpty(inputHtml))
            {
                var contentHtmlDocument = new HtmlDocument();
                contentHtmlDocument.LoadHtml(HttpUtility.HtmlDecode(inputHtml));
                return contentHtmlDocument.ParsedText.Trim();
            }

            return string.Empty;
        }

        /// <summary>
        ///     Get Layout List Items.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        internal async Task<List<SelectListItem>> BaseGetLayoutListItems()
        {
            var layouts = await dbContext.Layouts.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.LayoutName
            }).ToListAsync();
            if (layouts != null)
            {
                return layouts;
            }

            var layoutViewModel = new LayoutViewModel();

            dbContext.Layouts.Add(layoutViewModel.GetLayout());
            await dbContext.SaveChangesAsync();

            return await dbContext.Layouts.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.LayoutName
            }).ToListAsync();
        }

        /// <summary>
        /// Gets the user ID of the currently logged in user.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected async Task<string> GetUserId()
        {
            // Get the user's ID for logging.
            var user = await baseUserManager.GetUserAsync(User);
            return user.Id;
        }

        /// <summary>
        /// Gets the current default (published) layout for the application.
        /// </summary>
        /// <returns>The default layout, or null if no default layout exists.</returns>
        /// <remarks>
        /// This method supports three caching scenarios:
        /// <list type="number">
        /// <item>Multi-tenant with cache: Uses tenant-scoped cache key "default-layout:{tenantId}"</item>
        /// <item>Single-tenant with cache: Uses global cache key "default-layout"</item>
        /// <item>No cache: Direct database query (backward compatible)</item>
        /// </list>
        /// Cache duration: 30 seconds sliding expiration, 2 minutes absolute expiration.
        /// </remarks>
        protected async Task<Layout?> GetCurrentLayoutAsync()
        {
            // Scenario 1: Multi-tenant with tenant-aware caching
            if (memoryCache != null && configProvider != null)
            {
                var tenantId = await configProvider.GetCurrentTenantIdAsync();
                var cacheKey = $"default-layout:{tenantId ?? Guid.Empty}";

                if (memoryCache.TryGetValue<Layout>(cacheKey, out var cachedLayout))
                {
                    return cachedLayout;
                }

                var layout = await FetchCurrentLayoutAsync();

                if (layout != null)
                {
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromSeconds(10))
                        .SetPriority(CacheItemPriority.Normal);

                    memoryCache.Set(cacheKey, layout, cacheOptions);
                }

                return layout;
            }

            // Scenario 2: Single-tenant with simple caching (no tenant scoping)
            if (memoryCache != null)
            {
                const string cacheKey = "default-layout";

                if (memoryCache.TryGetValue<Layout>(cacheKey, out var cachedLayout))
                {
                    return cachedLayout;
                }

                var layout = await FetchCurrentLayoutAsync();

                if (layout != null)
                {
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromSeconds(10))
                        .SetPriority(CacheItemPriority.Normal);

                    memoryCache.Set(cacheKey, layout, cacheOptions);
                }

                return layout;
            }

            // Scenario 3: No caching - direct database query (current behavior, backward compatible)
            return await FetchCurrentLayoutAsync();
        }

        /// <summary>
        /// Fetches the current default layout from the database.
        /// </summary>
        /// <returns>The latest version of the default layout that is currently published, or null if none exists.</returns>
        private async Task<Layout?> FetchCurrentLayoutAsync()
        {
            return await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);
        }

        /// <summary>
        /// Gets templates filtered for the current default layout.
        /// </summary>
        /// <returns>Queryable collection of templates for the current layout.</returns>
        /// <remarks>
        /// This method handles both migrated and unmigrated data by checking both LayoutNumber and LayoutId.
        /// Templates with LayoutNumber == 0 are considered unmigrated and are matched by LayoutId only.
        /// </remarks>
        protected async Task<IQueryable<Template>> GetTemplatesForCurrentLayoutAsync()
        {
            var layout = await GetCurrentLayoutAsync();
            
            if (layout == null)
            {
                // Return empty queryable if no default layout exists
                return Enumerable.Empty<Template>().AsQueryable();
            }

            return dbContext.Templates
                .Where(t => t.LayoutNumber == layout.LayoutNumber || 
                            (t.LayoutNumber == 0 && t.LayoutId == layout.Id)); // Handles unmigrated data
        }
    }
}
