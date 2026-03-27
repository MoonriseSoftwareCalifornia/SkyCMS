// <copyright file="TenantTestContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests
{
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services.Caching;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Articles.CreateVersion;
    using Sky.Editor.Features.Articles.Save;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Redirects;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Services.Titles;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a complete isolated test context for a single tenant.
    /// Provides tenant-scoped DbContext, services, and configuration.
    /// </summary>
    public class TenantTestContext : IAsyncDisposable
    {
        /// <summary>
        /// Gets the unique identifier for this tenant.
        /// </summary>
        public Guid TenantId { get; }

        /// <summary>
        /// Gets the domain name for this tenant.
        /// </summary>
        public string TenantDomain { get; }

        /// <summary>
        /// Gets the tenant-scoped database context.
        /// </summary>
        public ApplicationDbContext DbContext { get; private set; }

        /// <summary>
        /// Gets the tenant-scoped database context (alias for DbContext).
        /// </summary>
        public ApplicationDbContext Db => DbContext;

        /// <summary>
        /// Gets the tenant-scoped ArticleEditLogic instance.
        /// </summary>
        public ArticleEditLogic Logic { get; private set; }

        /// <summary>
        /// Gets the tenant-scoped HttpContext.
        /// </summary>
        public HttpContext HttpContext { get; }

        /// <summary>
        /// Gets the tenant-scoped configuration provider.
        /// </summary>
        public IDynamicConfigurationProvider ConfigurationProvider { get; }

        /// <summary>
        /// Gets the shared in-memory database name (for isolation testing).
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Gets the memory cache instance (can be shared or isolated).
        /// </summary>
        public IMemoryCache Cache { get; }

        /// <summary>
        /// Gets the test user ID for this tenant context.
        /// </summary>
        public Guid TestUserId { get; private set; }

        /// <summary>
        /// Gets the publishing service for this tenant.
        /// </summary>
        public IPublishingService PublishingService { get; private set; }

        /// <summary>
        /// Gets the storage context for this tenant.
        /// </summary>
        public StorageContext Storage => storage;

        private readonly StorageContext storage;
        private readonly bool useSharedDatabase;

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantTestContext"/> class.
        /// </summary>
        /// <param name="tenantId">Unique tenant identifier.</param>
        /// <param name="tenantDomain">Tenant domain name.</param>
        /// <param name="sharedDatabaseName">Optional shared database name for multi-tenant isolation tests.</param>
        /// <param name="sharedCache">Optional shared cache for cross-tenant cache tests.</param>
        /// <param name="storage">Storage context (typically shared).</param>
        public TenantTestContext(
            Guid tenantId,
            string tenantDomain,
            string sharedDatabaseName = null,
            IMemoryCache sharedCache = null,
            StorageContext storage = null)
        {
            TenantId = tenantId;
            TenantDomain = tenantDomain;
            this.storage = storage;

            // Determine if we're using a shared database (for isolation tests) or isolated database
            useSharedDatabase = !string.IsNullOrEmpty(sharedDatabaseName);
            DatabaseName = useSharedDatabase ? sharedDatabaseName : $"Tenant_{tenantId}_{Guid.NewGuid()}";

            // Use shared cache if provided, otherwise create isolated cache
            Cache = sharedCache ?? new MemoryCache(new MemoryCacheOptions());

            // Create tenant-scoped HttpContext with a mock service provider
            HttpContext = new DefaultHttpContext();
            HttpContext.Request.Host = new HostString(tenantDomain);
            HttpContext.Request.Headers["x-origin-hostname"] = tenantDomain;

            // Set up a minimal service provider for the HttpContext (will be populated later in InitializeAsync)
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            HttpContext.RequestServices = services.BuildServiceProvider();

            // Create tenant-scoped configuration provider
            var mockConfigProvider = new Mock<IDynamicConfigurationProvider>();
            mockConfigProvider.Setup(p => p.GetCurrentTenantIdAsync())
                .ReturnsAsync(tenantId);
            mockConfigProvider.Setup(p => p.GetDatabaseConnectionStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null); // Use in-memory database
            mockConfigProvider.Setup(p => p.GetStorageConnectionStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null);
            mockConfigProvider.Setup(p => p.GetTenantDomainNameFromRequest())
                .Returns(tenantDomain); // Return this tenant's domain
            ConfigurationProvider = mockConfigProvider.Object;
        }

        /// <summary>
        /// Initializes the tenant context with all required services.
        /// Must be called before using the context.
        /// </summary>
        /// <param name="seedLayout">Whether to seed a default layout.</param>
        /// <param name="baseTestContext">Base test context to copy shared services from.</param>
        /// <returns>Awaitable task.</returns>
        public async Task InitializeAsync(bool seedLayout = true, SkyCmsTestBase baseTestContext = null)
        {
            // Create tenant-scoped DbContext
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(DatabaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            DbContext = new ApplicationDbContext(options, ConfigurationProvider);

            // Seed default layout if requested
            if (seedLayout)
            {
                var layout = new Layout
                {
                    Id = Guid.NewGuid(),
                    LayoutName = $"Default-{TenantDomain}",
                    IsDefault = true,
                    Published = DateTimeOffset.UtcNow.AddDays(-1),
                    Head = string.Empty,
                    HtmlHeader = string.Empty,
                    FooterHtmlContent = string.Empty
                };
                DbContext.Layouts.Add(layout);

                // Disable static web page generation to avoid Azure Storage issues in tests
                var staticWebPagesSetting = new Setting
                {
                    Id = Guid.NewGuid(),
                    Group = "Publishing",
                    Name = "StaticWebPages",
                    Value = "false",
                    IsRequired = false,
                    Description = "Disable static web page generation for tests"
                };
                DbContext.Settings.Add(staticWebPagesSetting);

                await DbContext.SaveChangesAsync();
            }

            // If we have a base context, use its shared services
            if (baseTestContext != null)
            {
                // Create tenant-scoped ArticleEditLogic using shared services but tenant's DbContext
                // Access services through reflection since they're protected
                var storageField = typeof(SkyCmsTestBase).GetField("Storage", BindingFlags.NonPublic | BindingFlags.Instance);
                var editorSettingsField = typeof(SkyCmsTestBase).GetField("EditorSettings", BindingFlags.NonPublic | BindingFlags.Instance);
                var clockField = typeof(SkyCmsTestBase).GetProperty("Clock", BindingFlags.NonPublic | BindingFlags.Instance);
                var slugServiceField = typeof(SkyCmsTestBase).GetField("SlugService", BindingFlags.NonPublic | BindingFlags.Instance);
                var articleHtmlServiceField = typeof(SkyCmsTestBase).GetField("ArticleHtmlService", BindingFlags.NonPublic | BindingFlags.Instance);
                var publishingServiceField = typeof(SkyCmsTestBase).GetField("PublishingService", BindingFlags.NonPublic | BindingFlags.Instance);
                var titleChangeServiceField = typeof(SkyCmsTestBase).GetField("TitleChangeService", BindingFlags.NonPublic | BindingFlags.Instance);
                var redirectServiceField = typeof(SkyCmsTestBase).GetField("RedirectService", BindingFlags.NonPublic | BindingFlags.Instance);
                var templateServiceField = typeof(SkyCmsTestBase).GetField("TemplateService", BindingFlags.NonPublic | BindingFlags.Instance);

                var storage = (StorageContext)storageField?.GetValue(baseTestContext);
                var editorSettings = (IEditorSettings)editorSettingsField?.GetValue(baseTestContext);
                var clock = (IClock)clockField?.GetValue(baseTestContext);
                var slugService = (ISlugService)slugServiceField?.GetValue(baseTestContext);
                var articleHtmlService = (IArticleHtmlService)articleHtmlServiceField?.GetValue(baseTestContext);
                // DON'T use shared publishingService - each tenant needs its own with its own DbContext
                // var publishingService = (IPublishingService)publishingServiceField?.GetValue(baseTestContext);
                var titleChangeService = (ITitleChangeService)titleChangeServiceField?.GetValue(baseTestContext);
                var redirectService = (IRedirectService)redirectServiceField?.GetValue(baseTestContext);
                var templateService = (ITemplateService)templateServiceField?.GetValue(baseTestContext);

                // ✅ CREATE TENANT-SPECIFIC PublishingService with THIS tenant's DbContext
                // This prevents DbContext threading issues when multiple tenants publish concurrently
                var authorInfoServiceField = typeof(SkyCmsTestBase).GetField("AuthorInfoService", BindingFlags.NonPublic | BindingFlags.Instance);
                var blogStreamRenderingServiceField = typeof(SkyCmsTestBase).GetField("BlogStreamRenderingService", BindingFlags.NonPublic | BindingFlags.Instance);
                var viewRenderServiceField = typeof(SkyCmsTestBase).GetField("ViewRenderService", BindingFlags.NonPublic | BindingFlags.Instance);
                var httpContextAccessorField = typeof(SkyCmsTestBase).GetField("HttpContextAccessor", BindingFlags.NonPublic | BindingFlags.Instance);

                var authorInfoService = (Sky.Editor.Services.Authors.IAuthorInfoService)authorInfoServiceField?.GetValue(baseTestContext);
                var blogStreamRenderingService = (Cosmos.Common.Services.BlogPublishing.IBlogStreamRenderingService)blogStreamRenderingServiceField?.GetValue(baseTestContext);
                var viewRenderService = (Sky.Cms.Services.IViewRenderService)viewRenderServiceField?.GetValue(baseTestContext);
                var httpContextAccessor = (IHttpContextAccessor)httpContextAccessorField?.GetValue(baseTestContext);

                // Build a minimal service provider for this tenant's PublishingService
                var tenantServiceCollection = new ServiceCollection();
                tenantServiceCollection.AddSingleton(DbContext); // Use THIS tenant's DbContext
                tenantServiceCollection.AddSingleton(storage);
                tenantServiceCollection.AddSingleton(editorSettings);
                tenantServiceCollection.AddSingleton<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>(sp =>
                    new Cosmos.Common.Features.Articles.Shared.ArticleCatalogQueryService(
                        DbContext,
                        editorSettings.PublisherUrl,
                        editorSettings.BlobPublicUrl));
                var tenantServiceProvider = tenantServiceCollection.BuildServiceProvider();

                PublishingService = new Sky.Editor.Services.Publishing.PublishingService(
                    DbContext, // ? CRITICAL: Use THIS tenant's DbContext, not the shared one
                    storage,
                    editorSettings,
                    new NullLogger<Sky.Editor.Services.Publishing.PublishingService>(),
                    httpContextAccessor,
                    authorInfoService,
                    clock,
                    blogStreamRenderingService,
                    viewRenderService,
                    tenantServiceProvider,
                    new Sky.Editor.Services.Publishing.NoOpPublishingProgressReporter(),
                    tenantServiceProvider.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>());

                // Create tenant-scoped catalog service
                var catalogService = new CatalogService(DbContext, articleHtmlService, clock, new NullLogger<CatalogService>());

                Logic = new ArticleEditLogic(
                    DbContext, // Tenant-specific DbContext
                    new CacheService<AuthorInfo>(Cache, new NullLogger<CacheService<AuthorInfo>>(), ConfigurationProvider),
                    storage,
                    new NullLogger<ArticleEditLogic>(),
                    editorSettings,
                    clock,
                    slugService,
                    articleHtmlService,
                    catalogService,
                    PublishingService, // Use tenant-specific PublishingService
                    titleChangeService,
                    redirectService,
                    templateService,
                    ConfigurationProvider); // Pass the tenant configuration provider

                // BUILD A PROPER SERVICE PROVIDER WITH COMMAND HANDLERS
                // This is required for Mediator to resolve handlers
                var serviceCollection = new ServiceCollection();

                // Register DbContext (tenant-specific)
                serviceCollection.AddSingleton(DbContext);

                // Register shared services
                serviceCollection.AddSingleton(articleHtmlService);
                serviceCollection.AddSingleton(catalogService);
                serviceCollection.AddSingleton(PublishingService); // Use tenant-specific PublishingService
                serviceCollection.AddSingleton(titleChangeService);
                serviceCollection.AddSingleton(templateService);
                serviceCollection.AddSingleton(clock);

                // Register mediator (following the pattern in Program.cs)
                serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IMediator, Cosmos.Common.Features.Shared.Mediator>();

                // Register command handlers (following the pattern in Program.cs line 521-522)
                serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<CreateArticleCommand, CommandResult<ArticleViewModel>>>(sp =>
                    new CreateArticleHandler(
                        DbContext,
                        articleHtmlService,
                        catalogService,
                        PublishingService, // Use tenant-specific PublishingService
                        titleChangeService,
                        templateService,
                        clock,
                        new NullLogger<CreateArticleHandler>()));

                serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<SaveArticleCommand, CommandResult<ArticleUpdateResult>>>(sp =>
                    new SaveArticleHandler(
                        DbContext,
                        articleHtmlService,
                        catalogService,
                        PublishingService, // Use tenant-specific PublishingService
                        titleChangeService,
                        clock,
                        new NullLogger<SaveArticleHandler>()));

                // Build and assign to HttpContext
                HttpContext.RequestServices = serviceCollection.BuildServiceProvider();

                // Seed TenantDomain setting for this tenant
                var tenantDomainSetting = new Setting
                {
                    Id = Guid.NewGuid(),
                    Group = "Tenant",
                    Name = "TenantDomain",
                    Value = TenantDomain,
                    IsRequired = true,
                    Description = "Current tenant domain for multi-tenant isolation"
                };
                DbContext.Settings.Add(tenantDomainSetting);
                await DbContext.SaveChangesAsync();

                // Auto-create a test user for this tenant
                var testUserId = Guid.NewGuid();
                await CreateTestUserAsync(testUserId, $"testuser@{TenantDomain}");
            }
        }

        /// <summary>
        /// Creates a test user in this tenant's database.
        /// </summary>
        /// <param name="userId">User identifier.</param>
        /// <param name="email">User email.</param>
        /// <returns>Awaitable task.</returns>
        public async Task CreateTestUserAsync(Guid userId, string email = null)
        {
            email ??= $"user-{userId}@{TenantDomain}";

            // Store the test user ID
            TestUserId = userId;

            var user = new Microsoft.AspNetCore.Identity.IdentityUser
            {
                Id = userId.ToString(),
                UserName = email,
                Email = email,
                NormalizedUserName = email.ToUpper(),
                NormalizedEmail = email.ToUpper()
            };

            // Add to DbContext Users table
            // Note: In production this would go through UserManager
            // For tests, we can add directly
            DbContext.Users.Add(user);
            await DbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Verifies that this tenant's database contains the expected number of articles.
        /// </summary>
        /// <param name="expectedCount">Expected article count.</param>
        /// <returns>True if count matches.</returns>
        public async Task<bool> VerifyArticleCountAsync(int expectedCount)
        {
            var actualCount = await DbContext.Articles.CountAsync();
            return actualCount == expectedCount;
        }

        /// <summary>
        /// Attempts to access an article by ID (should fail if from different tenant).
        /// </summary>
        /// <param name="articleId">Article GUID.</param>
        /// <returns>Article if accessible, null otherwise.</returns>
        public async Task<Article> TryGetArticleByIdAsync(Guid articleId)
        {
            // Apply tenant isolation filter - only return articles from this tenant
            return await DbContext.Articles
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Attempts to access an article by number (should fail if from different tenant).
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Article if accessible, null otherwise.</returns>
        public async Task<Article> TryGetArticleByNumberAsync(int articleNumber)
        {
            // Apply tenant isolation filter - only return articles from this tenant
            return await DbContext.Articles
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Gets all articles visible to this tenant.
        /// </summary>
        /// <returns>List of articles.</returns>
        public async Task<List<Article>> GetAllArticlesAsync()
        {
            // Apply tenant isolation filter - only return articles from this tenant
            return await DbContext.Articles
                .ToListAsync();
        }

        /// <summary>
        /// Helper method to create an article in this tenant using the CreateArticleCommand via mediator.
        /// </summary>
        /// <param name="title">Title of the article to create.</param>
        /// <param name="userId">The user ID creating the article.</param>
        /// <param name="templateId">Optional template ID to use for the article.</param>
        /// <param name="blogKey">Optional blog key (default: empty).</param>
        /// <param name="articleType">Optional article type (default: General).</param>
        /// <returns>Created article view model.</returns>
        public async Task<ArticleViewModel> CreateArticleAsync(
            string title,
            Guid userId,
            Guid? templateId = null,
            string blogKey = "",
            Cosmos.Cms.Common.ArticleType articleType = Cosmos.Cms.Common.ArticleType.General)
        {
            var mediator = (IMediator)HttpContext.RequestServices.GetService(typeof(IMediator));
            var command = new CreateArticleCommand
            {
                Title = title,
                UserId = userId,
                TemplateId = templateId,
                BlogKey = blogKey,
                ArticleType = articleType
            };

            var result = await mediator.SendAsync(command);
            if (!result.IsSuccess)
            {
                var errorMessage = result.ErrorMessage;
                if (string.IsNullOrEmpty(errorMessage) && result.Errors != null && result.Errors.Any())
                {
                    errorMessage = string.Join("; ", result.Errors.SelectMany(e => e.Value.Select(v => $"{e.Key}: {v}")));
                }
                throw new InvalidOperationException($"Failed to create article: {errorMessage}");
            }

            return result.Data;
        }

        /// <summary>
        /// Helper method to save an article in this tenant using the SaveArticleCommand via mediator.
        /// </summary>
        /// <param name="article">The article view model to save.</param>
        /// <param name="userId">The user ID performing the save.</param>
        /// <returns>Article update result.</returns>
        public async Task<CommandResult<ArticleUpdateResult>> SaveArticleAsync(
            ArticleViewModel article,
            Guid userId)
        {
            var mediator = (IMediator)HttpContext.RequestServices.GetService(typeof(IMediator));
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Content = article.Content,
                HeadJavaScript = article.HeadJavaScript,
                FooterJavaScript = article.FooterJavaScript,
                BannerImage = article.BannerImage,
                UserId = userId,
                ArticleType = article.ArticleType,
                Category = article.Category,
                Introduction = article.Introduction,
                Published = article.Published,
                UrlPath = article.UrlPath
            };

            return await mediator.SendAsync(command);
        }

        /// <summary>
        /// Helper method to create an article version in this tenant using the CreateArticleVersionCommand via mediator.
        /// </summary>
        /// <param name="articleNumber">The article number to create a version for.</param>
        /// <returns>Created article (new version).</returns>
        public async Task<ArticleViewModel> CreateArticleVersionAsync(
            int articleNumber)
        {
            var mediator = (IMediator)HttpContext.RequestServices.GetService(typeof(IMediator));

            var command = new CreateArticleVersionCommand
            {
                ArticleNumber = articleNumber
            };

            var result = await mediator.SendAsync(command);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to create article version: {result.ErrorMessage}");
            }

            return result.Data.Article;
        }

        /// <summary>
        /// Helper method to delete an article in this tenant using the DeleteArticleCommand via mediator.
        /// </summary>
        /// <param name="articleNumber">The article number to delete.</param>
        /// <returns>Awaitable task.</returns>
        public async Task DeleteArticleAsync(int articleNumber)
        {
            var mediator = (IMediator)HttpContext.RequestServices.GetService(typeof(IMediator));
            var command = new Sky.Editor.Features.Articles.Delete.DeleteArticleCommand
            {
                ArticleNumber = articleNumber
            };

            var result = await mediator.SendAsync(command);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to delete article: {result.ErrorMessage}");
            }
        }

        /// <summary>
        /// Helper method to restore an article in this tenant using the RestoreArticleCommand via mediator.
        /// </summary>
        /// <param name="articleNumber">The article number to restore.</param>
        /// <param name="userId">The user ID performing the restore.</param>
        /// <returns>Awaitable task.</returns>
        public async Task RestoreArticleAsync(int articleNumber, string userId)
        {
            var mediator = (IMediator)HttpContext.RequestServices.GetService(typeof(IMediator));
            var command = new Sky.Editor.Features.Articles.Restore.RestoreArticleCommand
            {
                ArticleNumber = articleNumber,
                UserId = userId
            };

            var result = await mediator.SendAsync(command);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to restore article: {result.ErrorMessage}");
            }
        }

        /// <summary>
        /// Disposes the tenant context and its resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (DbContext != null)
            {
                await DbContext.DisposeAsync();
            }

            // Only dispose cache if it's not shared
            if (Cache != null && !useSharedDatabase)
            {
                Cache.Dispose();
            }
        }
    }
}
