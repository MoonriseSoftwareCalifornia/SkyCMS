// <copyright file="TenantArticleLogicFactory.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Scheduling
{
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Services.BlogPublishing;
    using Cosmos.Common.Services.Caching;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Sky.Cms.Services;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Authors;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Layouts;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Redirects;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.StaticFiles;
    using Sky.Editor.Services.TableOfContents;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Services.Titles;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Tenant article logic factory.
    /// </summary>
    public class TenantArticleLogicFactory : ITenantArticleLogicFactory
    {
        private readonly IServiceProvider serviceProvider;
        private readonly IEditorSettings settings;
        private readonly IDynamicConfigurationProvider configurationProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantArticleLogicFactory"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider.</param>
        /// <param name="settings">Editor settings.</param>
        /// <param name="configurationProvider">Dynamic configuration provider.</param>
        public TenantArticleLogicFactory(
            IServiceProvider serviceProvider,
            IEditorSettings settings,
            IDynamicConfigurationProvider configurationProvider = null)
        {
            this.serviceProvider = serviceProvider;
            this.settings = settings;
            this.configurationProvider = configurationProvider;
        }

        /// <inheritdoc/>
        public async Task<ArticleEditLogic> CreateForTenantAsync(string domainName)
        {
            var scopedServices = serviceProvider;
            var memoryCache = scopedServices.GetRequiredService<IMemoryCache>();

            if (settings.IsMultiTenantEditor)
            {
                return await CreateMultiTenantLogicAsync(domainName, scopedServices, memoryCache);
            }

            return CreateSingleTenantLogic(scopedServices, memoryCache);
        }

        private async Task<ArticleEditLogic> CreateMultiTenantLogicAsync(
            string domainName,
            IServiceProvider scopedServices,
            IMemoryCache memoryCache)
        {
            var connection = await configurationProvider.GetTenantConnectionAsync(domainName);

            var dbContext = new ApplicationDbContext(connection.DbConn);
            var storageContext = new StorageContext(connection.StorageConn, memoryCache);

            var authorService = new AuthorInfoService(dbContext, scopedServices.GetRequiredService<ICacheService<AuthorInfo>>());
            var blogRenderingService = new BlogStreamRenderingService(dbContext);
            var reservedPaths = new ReservedPaths.ReservedPaths(dbContext);

            var catalogService = new CatalogService(
                dbContext,
                scopedServices.GetRequiredService<IArticleHtmlService>(),
                scopedServices.GetRequiredService<IClock>(),
                scopedServices.GetRequiredService<ILogger<CatalogService>>());

            var editorSettings = new EditorSettings(
                scopedServices.GetRequiredService<IConfiguration>(),
                dbContext,
                null,
                memoryCache,
                scopedServices,
                domainName);

            // Create no-op progress reporter for background jobs (no HTTP context)
            var progressReporter = new NoOpPublishingProgressReporter();

            // For PublishingService and BlogPublishingService, use the service provider to avoid circular dependency issues
            // These services are already registered in DI and can resolve their dependencies properly
            var publishingService = scopedServices.GetRequiredService<IPublishingService>();

            var redirectService = new RedirectService(
                dbContext,
                scopedServices.GetRequiredService<ISlugService>(),
                scopedServices.GetRequiredService<IClock>(),
                publishingService);

            var templateService = new TemplateService(
                scopedServices.GetRequiredService<IWebHostEnvironment>(),
                scopedServices.GetRequiredService<ILogger<TemplateService>>(),
                dbContext,
                scopedServices.GetRequiredService<IDynamicConfigurationProvider>());

            var layoutPreviewService = new LayoutTemplateService(
                scopedServices.GetRequiredService<IWebHostEnvironment>(),
                scopedServices.GetRequiredService<ILogger<LayoutTemplateService>>());

            var titleChangeContext = new TitleChangeContext(
                dbContext,
                scopedServices.GetRequiredService<IClock>(),
                null);  // No event dispatcher in background jobs

            var titleChangeService = new TitleChangeService(
                dbContext,
                scopedServices.GetRequiredService<ISlugService>(),
                redirectService,
                scopedServices.GetRequiredService<IClock>(),
                null,  // No domain event dispatcher in background jobs
                publishingService,
                reservedPaths,
                blogRenderingService,
                scopedServices.GetRequiredService<ILogger<TitleChangeService>>());

            return new ArticleEditLogic(
                dbContext,
                scopedServices.GetRequiredService<ICacheService<AuthorInfo>>(),
                storageContext,
                scopedServices.GetRequiredService<ILogger<ArticleEditLogic>>(),
                scopedServices.GetRequiredService<IEditorSettings>(),
                scopedServices.GetRequiredService<IClock>(),
                scopedServices.GetRequiredService<ISlugService>(),
                scopedServices.GetRequiredService<IArticleHtmlService>(),
                catalogService,
                publishingService,
                titleChangeService,
                redirectService,
                templateService);
        }

        private ArticleEditLogic CreateSingleTenantLogic(
            IServiceProvider scopedServices,
            IMemoryCache memoryCache)
        {
            return new ArticleEditLogic(
                scopedServices.GetRequiredService<ApplicationDbContext>(),
                scopedServices.GetRequiredService<ICacheService<AuthorInfo>>(),
                scopedServices.GetRequiredService<IStorageContext>(),
                scopedServices.GetRequiredService<ILogger<ArticleEditLogic>>(),
                scopedServices.GetRequiredService<IEditorSettings>(),
                scopedServices.GetRequiredService<IClock>(),
                scopedServices.GetRequiredService<ISlugService>(),
                scopedServices.GetRequiredService<IArticleHtmlService>(),
                scopedServices.GetRequiredService<ICatalogService>(),
                scopedServices.GetRequiredService<IPublishingService>(),
                scopedServices.GetRequiredService<ITitleChangeService>(),
                scopedServices.GetRequiredService<IRedirectService>(),
                scopedServices.GetRequiredService<ITemplateService>());
        }
    }
}
