using Cosmos.BlobService;

#nullable enable

using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services.BlogPublishing;
using Cosmos.Common.Services.Caching;
using Cosmos.DynamicConfig;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Sky.Cms.Services;
using Sky.Editor.Data.Logic;
using Sky.Editor.Domain.Events;
using Sky.Editor.Infrastructure.Time;
using Sky.Editor.Services.Authors;
using Sky.Editor.Services.Catalog;
using Sky.Editor.Services.EditorSettings;
using Sky.Editor.Services.Html;
using Sky.Editor.Services.Publishing;
using Sky.Editor.Services.Redirects;
using Sky.Editor.Services.ReservedPaths;
using Sky.Editor.Services.Scheduling;
using Sky.Editor.Services.Slugs;
using Sky.Editor.Services.Templates;
using Sky.Editor.Services.Titles;
using Sky.Editor.Features.Articles.Create;
using Sky.Editor.Features.Articles.CreateVersion;
using Sky.Editor.Features.Articles.GetEditable;
using Sky.Editor.Features.Articles.Inventory;
using Cosmos.Common.Models;
using System.Diagnostics;
using System.Reflection;
using Sky.Editor.Features.Articles.Save;
using Sky.Cms.Controllers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Sky.Editor.Services.Layouts;

namespace Sky.Tests
{
    /// <summary>
    /// Base fixture for tests targeting <see cref="ArticleEditLogic"/>.
    /// Sets up an isolated in-memory EF Core context and supporting services.
    /// Provides a capture dispatcher to assert domain event publishing.
    /// </summary>
    public abstract class SkyCmsTestBase : IAsyncDisposable
    {
        /// <summary>
        /// Well-known Azurite (local Azure Storage emulator) connection string.
        /// Used as the default storage connection when no real connection string is configured.
        /// Azurite listens on http://127.0.0.1:10000 by default.
        /// </summary>
        public const string AzuriteConnectionString =
            "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
            "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
            "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";
        protected AuthorInfoService AuthorInfoService = null!;
        protected ApplicationDbContext Db;
        protected ArticleEditLogic Logic = null!;
        protected CatalogService CatalogService = null!;
        protected StorageContext Storage = null!;
        protected IMemoryCache Cache = null!;
        protected ICacheService<Layout> LayoutCacheService = null!;
        protected Guid TestUserId;
        protected ISlugService SlugService = null!;
        protected EditorSettings EditorSettings = null!;
        protected IHttpContextAccessor HttpContextAccessor = null!;
        protected TestDomainEventDispatcher EventDispatcher = null!;
        protected IPublishingService PublishingService = null!;
        protected IArticleHtmlService ArticleHtmlService = null!;
        protected IReservedPaths ReservedPaths = null!;
        protected IRedirectService RedirectService = null!;
        protected ITitleChangeService TitleChangeService = null!;
        protected IClock Clock { get; set; } = new SystemClock();
        protected UserManager<IdentityUser> UserManager = null!;
        protected RoleManager<IdentityRole> RoleManager = null!;
        protected ITemplateService TemplateService = null!;
        protected IBlogStreamRenderingService BlogStreamRenderingService = null!;
        protected IViewRenderService ViewRenderService = null!;
        protected IServiceProvider Services = null!;
        protected IServiceScope ServiceScope = null!;
        protected IArticleScheduler ArticleScheduler = null!;
        protected IDynamicConfigurationProvider DynamicConfigurationProvider = null!;
        protected ITenantArticleLogicFactory TenantArticleLogicFactory = null!;
        protected ILogger<EditorController> Logger = null!;
        protected Mock<IHubContext<Sky.Cms.Hubs.LiveEditorHub>> Hub = null!;
        protected EditorController EditorController = null!;
        protected ILayoutImportService LayoutImportService = null!;
        protected IHttpClientFactory HttpClientFactory = null!;

        // ADD THESE PROPERTIES FOR VERTICAL SLICE ARCHITECTURE
        protected IMediator Mediator = null!;
        protected ICommandHandler<CreateArticleCommand, CommandResult<ArticleViewModel>> CreateArticleHandler = null!;
        protected ICommandHandler<SaveArticleCommand, CommandResult<ArticleUpdateResult>> SaveArticleHandler = null!;
        protected ArticleEditLogic ArticleEditLogic = null!;

        private async Task EnsureBlogStreamTemplateExistsAsync()
        {
            var existingTemplate = await Db.Templates
                .FirstOrDefaultAsync(t => t.PageType == "blog-stream");
            if (existingTemplate == null)
            {
                var defaultLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
                var t = TemplateService.GetTemplateByKeyAsync("blog-stream").Result;
                var template = new Template
                {
                    Id = Guid.NewGuid(),
                    Title = "Blog Stream Template",
                    PageType = "blog-stream",
                    Content = t.Content ?? string.Empty,
                    LayoutId = defaultLayout?.Id ?? Guid.Empty
                };
                Db.Templates.Add(template);
                await Db.SaveChangesAsync();
            }
        }

        private async Task EnsureBlogPostTemplateExistsAsync()
        {
            var existingTemplate = await Db.Templates
                .FirstOrDefaultAsync(t => t.PageType == "blog-post");

            if (existingTemplate == null)
            {
                var defaultLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
                var t = TemplateService.GetTemplateByKeyAsync("blog-post").Result;
                var template = new Template
                {
                    Id = Guid.NewGuid(),
                    Title = "Blog Post Template",
                    PageType = "blog-post",
                    Content = t.Content ?? string.Empty,
                    LayoutId = defaultLayout?.Id ?? Guid.Empty
                };
                Db.Templates.Add(template);
                await Db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Initialize test context. Call from [TestInitialize].
        /// </summary>
        /// <param name="seedLayout">Seed default layout required by logic layer.</param>
        protected void InitializeTestContext(bool seedLayout = true)
        {
            TestUserId = Guid.NewGuid();

            // In-memory DB (unique per test run).
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Db = new ApplicationDbContext(options);

            if (seedLayout)
            {
                Db.Layouts.Add(new Layout
                {
                    Id = Guid.NewGuid(),
                    LayoutName = "Default",
                    IsDefault = true,
                    Published = DateTimeOffset.UtcNow.AddDays(-1), // Set Published date to the past so it's valid
                    Head = string.Empty,
                    HtmlHeader = string.Empty,
                    FooterHtmlContent = string.Empty
                });

                // Disable static web page generation in tests to avoid Azure Storage issues
                Db.Settings.Add(new Setting
                {
                    Id = Guid.NewGuid(),
                    Group = "Publishing",
                    Name = "StaticWebPages",
                    Value = "false",
                    IsRequired = false,
                    Description = "Disable static web page generation for tests"
                });

                Db.SaveChanges();
            }

            Cache = new MemoryCache(new MemoryCacheOptions());

            var initialConfig = new Dictionary<string, string>
            {
                ["ConnectionStrings:ApplicationDbContextConnection"] = $"Data Source={Path.GetTempPath()}/cosmos-test-{Guid.NewGuid()}.db;Password=strong-password;",
                ["ConnectionStrings:ConfigDbConnectionString"] = $"Data Source={Path.GetTempPath()}/cosmos-test-m-{Guid.NewGuid()}.db;Password=strong-password;",
                ["CosmosPublisherUrl"] = "https://www.sky-cms.com",
                ["AzureBlobStorageEndPoint"] = "https://www.sky-cms.com"
            };

            // ✅ FIX: Load user secrets and env vars first, then override with test-specific values
            // This ensures tests have predictable configuration while still allowing
            // secrets like StorageConnectionString to be loaded from user secrets
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets(typeof(SkyCmsTestBase).Assembly, optional: true)
                .AddEnvironmentVariables()
                .AddInMemoryCollection(initialConfig) // Added last = highest priority
                .Build();

            HttpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            HttpContextAccessor.HttpContext!.Request.Host = new HostString("example.com");

            // FIX: Create webHostEnvironmentMock and webHostEnvironment before ServiceCollection
            var webHostEnvironmentMock = new Mock<IWebHostEnvironment>();
            var assem = Assembly.GetAssembly(typeof(TemplateService));
            var path = Path.GetDirectoryName(assem!.Location)!;
            webHostEnvironmentMock.Setup(m => m.ContentRootPath).Returns(path);
            var webHostEnvironment = webHostEnvironmentMock.Object;

            // Configure Azure Blob Storage - fall back to Azurite when no real connection string is set.
            var storageConnectionString = configuration.GetConnectionString("StorageConnectionString")
                ?? configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? AzuriteConnectionString;

            Storage = new StorageContext(storageConnectionString, Cache);

            // Core service graph.
            SlugService = new SlugService();
            ArticleHtmlService = new ArticleHtmlService();
            var catalogLogger = new LoggerFactory().CreateLogger<CatalogService>();
            CatalogService = new CatalogService(Db, ArticleHtmlService, Clock, catalogLogger);
            EventDispatcher = new TestDomainEventDispatcher();
            var authorInfoCacheService = new CacheService<AuthorInfo>(
                Cache,
                new NullLogger<CacheService<AuthorInfo>>(),
                DynamicConfigurationProvider);
            var authorInfoService = new AuthorInfoService(Db, authorInfoCacheService);
            BlogStreamRenderingService = new Cosmos.Common.Services.BlogPublishing.BlogStreamRenderingService(Db);
            ReservedPaths = new ReservedPaths(Db);
            AuthorInfoService = new AuthorInfoService(Db, authorInfoCacheService);

            var mockViewRenderService = new Mock<IViewRenderService>();
            mockViewRenderService.Setup(x => x.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html>test</html>");
            ViewRenderService = mockViewRenderService.Object;

            EditorSettings = new EditorSettings(configuration, Db, HttpContextAccessor, Cache, null!);

            // ❌ DON'T CREATE PublishingService here - it needs Services provider which doesn't exist yet
            // PublishingService will be created after Services is built

            RedirectService = new RedirectService(Db, SlugService, Clock, null!); // Will set PublishingService later
            TitleChangeService = new TitleChangeService(Db, SlugService, RedirectService, Clock, EventDispatcher, null!, ReservedPaths, BlogStreamRenderingService, new LoggerFactory().CreateLogger<TitleChangeService>()); // Will set PublishingService later

            // ❌ REMOVE THIS - Don't create TemplateService here, it needs Mediator which doesn't exist yet
            // TemplateService = new TemplateService(
            //     webHostEnvironment, new LoggerFactory().CreateLogger<TemplateService>(),
            //     Db);
            // TemplateService.EnsureDefaultTemplatesExistAsync().Wait();

            // ❌ REMOVE THIS - Don't create Logic here, it needs TemplateService which doesn't exist yet
            // Logic = new ArticleEditLogic(Db, Cache, Storage, new NullLogger<ArticleEditLogic>(), EditorSettings, Clock, SlugService, ArticleHtmlService, CatalogService, PublishingService, TitleChangeService, RedirectService, TemplateService);

            // ✅ MOCK DynamicConfigurationProvider to avoid database calls in tests
            var mockDynamicConfigProvider = new Mock<IDynamicConfigurationProvider>();
            // Return a fixed tenant ID for tests
            mockDynamicConfigProvider.Setup(x => x.GetCurrentTenantIdAsync())
                .ReturnsAsync(Guid.NewGuid());
            // Return null for connection strings (tests use in-memory database)
            mockDynamicConfigProvider.Setup(x => x.GetDatabaseConnectionStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null);
            mockDynamicConfigProvider.Setup(x => x.GetStorageConnectionStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null);
            // Return empty list for GetAllDomainNamesAsync (tests don't need tenant domains)
            mockDynamicConfigProvider.Setup(x => x.GetAllDomainNamesAsync())
                .ReturnsAsync(new List<string>());
            // ✅ Delegate GetConfigurationValue to the actual IConfiguration
            mockDynamicConfigProvider.Setup(x => x.GetConfigurationValue(It.IsAny<string>()))
                .Returns((string key) => configuration.GetValue<string>(key));
            // ✅ Implement GetTenantDomainNameFromRequest to use HttpContext headers
            mockDynamicConfigProvider.Setup(x => x.GetTenantDomainNameFromRequest())
                .Returns(() =>
                {
                    var context = HttpContextAccessor.HttpContext;
                    if (context == null) return string.Empty;

                    // Check for x-origin-hostname header first (mimicking real implementation)
                    var xOriginHostname = context.Request.Headers["x-origin-hostname"].ToString();
                    if (!string.IsNullOrWhiteSpace(xOriginHostname))
                    {
                        return xOriginHostname.ToLowerInvariant();
                    }

                    // Fall back to Host header
                    return context.Request.Host.Host.ToLowerInvariant();
                });
            DynamicConfigurationProvider = mockDynamicConfigProvider.Object;

            LayoutCacheService = new CacheService<Layout>(
                Cache,
                new NullLogger<CacheService<Layout>>(),
                DynamicConfigurationProvider);

            // CREATE MOCK TENANT ARTICLE LOGIC FACTORY
            var mockTenantArticleLogicFactory = new Mock<ITenantArticleLogicFactory>();
            mockTenantArticleLogicFactory
                .Setup(f => f.CreateForTenantAsync(It.IsAny<string>()))
                .ReturnsAsync(() => Logic); // Use lambda to defer evaluation
            TenantArticleLogicFactory = mockTenantArticleLogicFactory.Object;

            // ❌ REMOVE THIS - Don't create handlers here, they need TemplateService which doesn't exist yet
            // CreateArticleHandler and SaveArticleHandler will be created after TemplateService is initialized

            // 🔧 FIX: SETUP IDENTITY MANAGERS WITH TOKEN PROVIDERS AND PASSWORD POLICY
            var userStore = new UserStore<IdentityUser>(Db);

            // ✅ Configure Identity options with strong password policy
            var identityOptions = Options.Create(new IdentityOptions
            {
                Password = new PasswordOptions
                {
                    RequireDigit = true,                // Require at least one digit (0-9)
                    RequireLowercase = true,            // Require at least one lowercase letter (a-z)
                    RequireUppercase = true,            // Require at least one uppercase letter (A-Z)
                    RequireNonAlphanumeric = true,      // Require at least one special character (!@#$%^&*)
                    RequiredLength = 8,                 // Minimum 8 characters
                    RequiredUniqueChars = 1            // Minimum 1 unique character
                },
                Lockout = new LockoutOptions
                {
                    AllowedForNewUsers = true,
                    DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5),
                    MaxFailedAccessAttempts = 5
                },
                SignIn = new SignInOptions
                {
                    RequireConfirmedEmail = true,
                    RequireConfirmedAccount = false
                },
                User = new UserOptions
                {
                    RequireUniqueEmail = true
                }
            });

            // Create token provider options
            var dataProtectionProviderOptions = Options.Create(new DataProtectionTokenProviderOptions
            {
                TokenLifespan = TimeSpan.FromHours(24)
            });

            // Create the token providers
            var tokenProviders = new List<IUserTwoFactorTokenProvider<IdentityUser>>
            {
                new DataProtectorTokenProvider<IdentityUser>(
                    new EphemeralDataProtectionProvider(new NullLoggerFactory()),
                    Options.Create(new DataProtectionTokenProviderOptions()),
                    new NullLogger<DataProtectorTokenProvider<IdentityUser>>())
            };

            // ✅ Create password validators that enforce the policy
            var passwordValidators = new List<IPasswordValidator<IdentityUser>>
            {
                new PasswordValidator<IdentityUser>()
            };

            UserManager = new UserManager<IdentityUser>(
                userStore,
                identityOptions,                        // ✅ Use configured options instead of empty Options.Create()
                new PasswordHasher<IdentityUser>(),
                Array.Empty<IUserValidator<IdentityUser>>(),
                passwordValidators,                     // ✅ Use password validators instead of empty array
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                new NullLogger<UserManager<IdentityUser>>());

            // ✅ Register the default token provider
            UserManager.RegisterTokenProvider("Default", tokenProviders[0]);

            var roleStore = new RoleStore<IdentityRole>(Db);
            RoleManager = new RoleManager<IdentityRole>(
                roleStore,
                Array.Empty<IRoleValidator<IdentityRole>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                new NullLogger<RoleManager<IdentityRole>>());

            // CREATE LOGGER FOR EDITORCONTROLLER
            Logger = new NullLogger<Sky.Cms.Controllers.EditorController>();

            // CREATE MOCK SIGNALR HUB
            Hub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<Sky.Cms.Hubs.LiveEditorHub>>();
            var mockHubClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
            var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();
            mockHubClients.Setup(clients => clients.All).Returns(mockClientProxy.Object);
            Hub.Setup(h => h.Clients).Returns(mockHubClients.Object);

            // BUILD FINAL SERVICE PROVIDER WITH ALL SERVICES INCLUDING FEATURE HANDLERS
            // Note: PublishingService is created AFTER this service provider is built
            var serviceCollection = new ServiceCollection()
                .AddLogging()
                .AddSingleton<DiagnosticSource>(new DiagnosticListener("TestListener"))
                .AddSingleton<DiagnosticListener>(new DiagnosticListener("TestListener"))
                .AddSingleton<IWebHostEnvironment>(webHostEnvironment)
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<IMemoryCache>(Cache)
                .AddSingleton<ApplicationDbContext>(sp => Db)
                .AddSingleton<ISlugService>(SlugService)
                .AddSingleton<IArticleHtmlService>(ArticleHtmlService)
                .AddSingleton<ICatalogService>(CatalogService)
                .AddSingleton<IDomainEventDispatcher>(EventDispatcher)
                .AddSingleton<IClock>(Clock);

            serviceCollection
                .AddSingleton<IBlogStreamRenderingService>(BlogStreamRenderingService)
                .AddSingleton<IAuthorInfoService>(AuthorInfoService)
                .AddScoped<IViewRenderService>(sp => ViewRenderService) // Change to scoped for CreateStaticPages
                .AddScoped<IStorageContext>(sp => Storage) // Register IStorageContext for CreateStaticPages
                .AddSingleton<IReservedPaths>(ReservedPaths)
                .AddSingleton<IEditorSettings>(EditorSettings)
                .AddHttpClient() // Register IHttpClientFactory
                .AddScoped<IMediator, Cosmos.Common.Features.Shared.Mediator>() // Register Mediator as Scoped (matching production)
                .AddScoped<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>(sp =>
                    new Cosmos.Common.Features.Articles.Shared.ArticleCatalogQueryService(
                        Db,
                        configuration.GetValue<string>("CosmosPublisherUrl") ?? "https://www.sky-cms.com",
                        configuration.GetValue<string>("AzureBlobStorageEndPoint") ?? "https://www.sky-cms.com"));

            // Register blog post command handlers
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Blogs.CreatePost.CreateBlogPostCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Blogs.CreatePost.CreateBlogPostCommandResult>>>(sp =>
                new Sky.Editor.Features.Blogs.CreatePost.CreateBlogPostCommandHandler(
                    Db,
                    sp.GetRequiredService<IMediator>(),
                    new NullLogger<Sky.Editor.Features.Blogs.CreatePost.CreateBlogPostCommandHandler>()));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Blogs.UpdatePost.UpdateBlogPostCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Blogs.UpdatePost.UpdateBlogPostCommandResult>>>(sp =>
                new Sky.Editor.Features.Blogs.UpdatePost.UpdateBlogPostCommandHandler(
                    Db,
                    new NullLogger<Sky.Editor.Features.Blogs.UpdatePost.UpdateBlogPostCommandHandler>()));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Blogs.DeletePost.DeleteBlogPostCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Blogs.DeletePost.DeleteBlogPostCommandResult>>>(sp =>
                new Sky.Editor.Features.Blogs.DeletePost.DeleteBlogPostCommandHandler(
                    Db,
                    new NullLogger<Sky.Editor.Features.Blogs.DeletePost.DeleteBlogPostCommandHandler>()));

            // Register article delete command handler
            // Note: This handler requires PublishingService which is created after Services is built
            // So we'll register it using a lazy factory approach (see below)
            Func<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Delete.DeleteArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Features.Shared.Unit>>> deleteArticleHandlerFactory = null!;
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Delete.DeleteArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Features.Shared.Unit>>>(sp =>
                deleteArticleHandlerFactory());

            // Register article restore command handler
            // This handler only needs DbContext and SlugService which are already available
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Restore.RestoreArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Features.Shared.Unit>>>(sp =>
                new Sky.Editor.Features.Articles.Restore.RestoreArticleHandler(
                    Db,
                    SlugService,
                    new NullLogger<Sky.Editor.Features.Articles.Restore.RestoreArticleHandler>()));

            // Register article trash command handler (permanent delete)
            // Note: This handler requires PublishingService which is created after Services is built
            // So we'll register it using a lazy factory approach (see below)
            Func<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Trash.TrashArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Features.Shared.Unit>>> trashArticleHandlerFactory = null!;
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Trash.TrashArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Features.Shared.Unit>>>(sp =>
                trashArticleHandlerFactory());

            // Register template command handlers
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Templates.Create.CreatePageDesignVersionCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Data.PageDesignVersion>>>(sp =>
                new Sky.Editor.Features.Templates.Create.CreatePageDesignVersionHandler(
                    Db,
                    ArticleHtmlService,
                    Clock,
                    new NullLogger<Sky.Editor.Features.Templates.Create.CreatePageDesignVersionHandler>()));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Templates.Save.SavePageDesignVersionCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Data.PageDesignVersion>>>(sp =>
                new Sky.Editor.Features.Templates.Save.SavePageDesignVersionHandler(
                    Db,
                    ArticleHtmlService,
                    Clock,
                    new NullLogger<Sky.Editor.Features.Templates.Save.SavePageDesignVersionHandler>()));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Templates.Delete.DeleteTemplateCommand, Cosmos.Common.Features.Shared.CommandResult<bool>>>(sp =>
                new Sky.Editor.Features.Templates.Delete.DeleteTemplateHandler(Db));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Templates.UpdateMetadata.UpdateTemplateMetadataCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Data.Template>>>(sp =>
                new Sky.Editor.Features.Templates.UpdateMetadata.UpdateTemplateMetadataHandler(
                    Db,
                    new NullLogger<Sky.Editor.Features.Templates.UpdateMetadata.UpdateTemplateMetadataHandler>()));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Templates.GetEditable.GetEditablePageDesignVersionCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Templates.GetEditable.GetEditablePageDesignVersionResult>>>(sp =>
                new Sky.Editor.Features.Templates.GetEditable.GetEditablePageDesignVersionHandler(
                    Db,
                    ArticleHtmlService,
                    Clock,
                    new NullLogger<Sky.Editor.Features.Templates.GetEditable.GetEditablePageDesignVersionHandler>()));

            // PublishPageDesignVersionHandler needs PublishingService and Mediator which will be created later
            // We'll register it using a lazy factory like we did for article handlers
            Func<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Templates.Publishing.PublishPageDesignVersionCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Data.Template>>> publishPageDesignVersionHandlerFactory = null!;
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Templates.Publishing.PublishPageDesignVersionCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Data.Template>>>(sp =>
                publishPageDesignVersionHandlerFactory());

            // Register layout command handlers
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Layouts.GetEditable.GetEditableLayoutForEditCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Layouts.GetEditable.GetEditableLayoutForEditResult>>>(sp =>
                new Sky.Editor.Features.Layouts.GetEditable.GetEditableLayoutForEditHandler(
                    Db));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Layouts.Create.CreateLayoutCommand, Cosmos.Common.Features.Shared.CommandResult<System.Guid>>>(sp =>
                new Sky.Editor.Features.Layouts.Create.CreateLayoutHandler(
                    Db,
                    new NullLogger<Sky.Editor.Features.Layouts.Create.CreateLayoutHandler>()));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Layouts.Delete.DeleteLayoutCommand, Cosmos.Common.Features.Shared.CommandResult<bool>>>(sp =>
                new Sky.Editor.Features.Layouts.Delete.DeleteLayoutHandler(
                    Db,
                    new NullLogger<Sky.Editor.Features.Layouts.Delete.DeleteLayoutHandler>()));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Layouts.Publish.PublishLayoutCommand, Cosmos.Common.Features.Shared.CommandResult<bool>>>(sp =>
                new Sky.Editor.Features.Layouts.Publish.PublishLayoutHandler(
                    Db,
                    new NullLogger<Sky.Editor.Features.Layouts.Publish.PublishLayoutHandler>()));

            // Register layout query handlers
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Layouts.Queries.GetDefaultLayoutQuery, Cosmos.Common.Models.LayoutViewModel>>(sp =>
                new Cosmos.Common.Features.Layouts.Queries.GetDefaultLayoutQueryHandler(
                    Db,
                    Cache));

            // PromoteLayoutHandler and ImportLayoutHandler need LayoutVersioningService which will be created later
            // We'll register these using lazy factories like we did for article handlers
            Func<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Layouts.Promote.PromoteLayoutCommand, Cosmos.Common.Features.Shared.CommandResult<int>>> promoteLayoutHandlerFactory = null!;
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Layouts.Promote.PromoteLayoutCommand, Cosmos.Common.Features.Shared.CommandResult<int>>>(sp =>
                promoteLayoutHandlerFactory());

            Func<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Layouts.Import.ImportLayoutCommand, Cosmos.Common.Features.Shared.CommandResult<bool>>> importLayoutHandlerFactory = null!;
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Layouts.Import.ImportLayoutCommand, Cosmos.Common.Features.Shared.CommandResult<bool>>>(sp =>
                importLayoutHandlerFactory());

            // Register template query handlers
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Sky.Editor.Features.Templates.Get.GetTemplateQuery, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Templates.Get.GetTemplateQueryResult>>>(sp =>
                new Sky.Editor.Features.Templates.Get.GetTemplateQueryHandler(
                    Db,
                    new NullLogger<Sky.Editor.Features.Templates.Get.GetTemplateQueryHandler>()));

            // Register article query handlers needed by EditorController
            // ✅ Register with non-nullable ArticleViewModel to match EditorController expectations
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.EditorQueries.GetArticleByIdQuery, Cosmos.Common.Models.ArticleViewModel>>(sp =>
                new Cosmos.Common.Features.Articles.EditorQueries.GetArticleByIdQueryHandler(
                    Mediator,
                    Db,
                    Cache,
                    configuration));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.EditorQueries.GetArticleByArticleNumberQuery, Cosmos.Common.Models.ArticleViewModel>>(sp =>
                new Cosmos.Common.Features.Articles.EditorQueries.GetArticleByArticleNumberQueryHandler(
                    Mediator,
                    Db,
                    Cache,
                    configuration));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.EditorQueries.GetArticleRedirectsQuery, System.Collections.Generic.IEnumerable<Cosmos.Common.Models.RedirectItemViewModel>>>(sp =>
                new Cosmos.Common.Features.Articles.EditorQueries.GetArticleRedirectsQueryHandler(Db));

            // ✅ Register GetArticleCatalogEntryQueryHandler for EditorController.Permissions
            // NOTE: Use non-nullable CatalogEntry because nullable reference types are compile-time only
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.EditorQueries.GetArticleCatalogEntryQuery, Cosmos.Common.Data.CatalogEntry>>(sp =>
                new Cosmos.Common.Features.Articles.EditorQueries.GetArticleCatalogEntryQueryHandler(Db));

            // ✅ Register GetLastPublishedDateQueryHandler for EditorController.Designer
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.EditorQueries.GetLastPublishedDateQuery, System.DateTimeOffset?>>(sp =>
                new Cosmos.Common.Features.Articles.EditorQueries.GetLastPublishedDateQueryHandler(Db));

            // ✅ Register GetArticleByUrlQueryHandler for LayoutsController.ExportLayout and EditPreview
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.EditorQueries.GetArticleByUrlQuery, Cosmos.Common.Models.ArticleViewModel?>>(sp =>
                new Cosmos.Common.Features.Articles.EditorQueries.GetArticleByUrlQueryHandler(
                    Mediator,
                    Db,
                    Cache,
                    configuration));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<GetEditorInventoryQuery, System.Collections.Generic.List<Sky.Editor.Models.EditorInventoryItem>>>(sp =>
                new GetEditorInventoryQueryHandler(Db));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<GetEditableArticleForEditCommand, Cosmos.Common.Features.Shared.CommandResult<GetEditableArticleForEditResult>>>(sp =>
                new GetEditableArticleForEditHandler(
                    Db,
                    sp.GetRequiredService<IMediator>()));


            // Register article catalog query handlers for HomeControllerBase
            serviceCollection.AddScoped<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>(sp =>
                new Cosmos.Common.Features.Articles.Shared.ArticleCatalogQueryService(
                    Db,
                    EditorSettings.PublisherUrl.ToString(),
                    configuration.GetValue<string>("AzureBlobStorageEndPoint") ?? string.Empty));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.Queries.GetTableOfContentsQuery, Cosmos.Common.Models.TableOfContents>>(sp =>
                new Cosmos.Common.Features.Articles.Queries.GetTableOfContentsQueryHandler(
                    sp.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>()));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.Queries.SearchPublishedArticlesQuery, System.Collections.Generic.List<Cosmos.Common.Models.TableOfContentsItem>>>(sp =>
                new Cosmos.Common.Features.Articles.Queries.SearchPublishedArticlesQueryHandler(
                    sp.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>()));

            // ✅ LAZY FACTORY: Register SaveArticleHandler using a factory that will be populated later
            // SaveArticleHandler depends on PublishingService and TitleChangeService which are created AFTER this service provider is built
            Func<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Save.SaveArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Articles.Save.ArticleUpdateResult>>> saveArticleHandlerFactory = null!;
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Save.SaveArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Articles.Save.ArticleUpdateResult>>>(sp =>
                saveArticleHandlerFactory());

            // ✅ LAZY FACTORY: Register CreateArticleHandler using a factory that will be populated later
            // CreateArticleHandler depends on TemplateService which is created AFTER this service provider is built
            Func<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Create.CreateArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Models.ArticleViewModel>>> createArticleHandlerFactory = null!;
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Create.CreateArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Cosmos.Common.Models.ArticleViewModel>>>(sp =>
                createArticleHandlerFactory());

            // ✅ LAZY FACTORY: Register CreateArticleVersionHandler using a factory that will be populated later
            // CreateArticleVersionHandler depends on ArticleLogic which is created AFTER this service provider is built
            Func<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.CreateVersion.CreateArticleVersionCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Articles.CreateVersion.CreateArticleVersionCommandResult>>> createArticleVersionHandlerFactory = null!;
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.CreateVersion.CreateArticleVersionCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Articles.CreateVersion.CreateArticleVersionCommandResult>>>(sp =>
                createArticleVersionHandlerFactory());

            // ✅ LAZY FACTORY: Register PublishArticleHandler using a factory that will be populated later
            // PublishArticleHandler depends on PublishingService and CatalogService which are created AFTER this service provider is built
            Func<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Publish.PublishArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Articles.Publish.PublishArticleCommandResult>>> publishArticleHandlerFactory = null!;
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.ICommandHandler<Sky.Editor.Features.Articles.Publish.PublishArticleCommand, Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Articles.Publish.PublishArticleCommandResult>>>(sp =>
                publishArticleHandlerFactory());



            // Register query handlers
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.Queries.GetTableOfContentsQuery, Cosmos.Common.Models.TableOfContents>>(sp =>
                new Cosmos.Common.Features.Articles.Queries.GetTableOfContentsQueryHandler(
                    sp.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>()));

            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.Queries.SearchPublishedArticlesQuery, System.Collections.Generic.List<Cosmos.Common.Models.TableOfContentsItem>>>(sp =>
                new Cosmos.Common.Features.Articles.Queries.SearchPublishedArticlesQueryHandler(
                    sp.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>()));

            // ✅ Register GetArticleFolderContentsQueryHandler for HomeControllerBase.CCMS_GetArticleFolderContents
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.Queries.GetArticleFolderContentsQuery, System.Collections.Generic.List<Cosmos.BlobService.FileManagerEntry>>>(sp =>
                new Cosmos.Common.Features.Articles.Queries.GetArticleFolderContentsQueryHandler(
                    Storage));

            // ✅ Register AuthorizeUserForArticleQueryHandler for PubControllerBase authorization checks
            serviceCollection.AddScoped<Cosmos.Common.Features.Shared.IQueryHandler<Cosmos.Common.Features.Articles.Queries.AuthorizeUserForArticleQuery, bool>>(sp =>
                new Cosmos.Common.Features.Articles.Queries.AuthorizeUserForArticleQueryHandler(
                    Db));

            Services = serviceCollection.BuildServiceProvider();

            // ✅ CREATE A SCOPE AND GET MEDIATOR FROM IT (scoped services need to be resolved from a scope)
            ServiceScope = Services.CreateScope();
            Mediator = ServiceScope.ServiceProvider.GetRequiredService<IMediator>();

            // ✅ NOW CREATE TEMPLATE SERVICE WITH CONFIGURATION AND SERVICE PROVIDER
            TemplateService = new TemplateService(
                webHostEnvironment,
                new LoggerFactory().CreateLogger<TemplateService>(),
                Db,
                DynamicConfigurationProvider);      // ✅ Pass service provider (already built)

            // ✅ CREATE PublishingService WITH Services as the provider (needed for CreateStaticPages)
            PublishingService = new PublishingService(
                Db,
                Storage,
                EditorSettings,
                new LoggerFactory().CreateLogger<PublishingService>(),
                HttpContextAccessor,
                authorInfoService,
                Clock,
                BlogStreamRenderingService,
                ViewRenderService,
                Services, // ✅ Pass the service provider
                new NoOpPublishingProgressReporter(),
                Services.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>());

            // ✅ NOW UPDATE RedirectService and TitleChangeService with the PublishingService
            RedirectService = new RedirectService(Db, SlugService, Clock, PublishingService);
            TitleChangeService = new TitleChangeService(Db, SlugService, RedirectService, Clock, EventDispatcher, PublishingService, ReservedPaths, BlogStreamRenderingService, new LoggerFactory().CreateLogger<TitleChangeService>());

            // ✅ NOW CREATE LOGIC WITH TEMPLATE SERVICE
            Logic = new ArticleEditLogic(
                Db,
                authorInfoCacheService,
                Storage,
                new NullLogger<ArticleEditLogic>(),
                EditorSettings,
                Clock,
                SlugService,
                ArticleHtmlService,
                CatalogService,
                PublishingService,
                TitleChangeService,
                RedirectService,
                TemplateService);

            // ✅ CREATE ArticleEditLogic for handlers that need it
            ArticleEditLogic = new Sky.Editor.Data.Logic.ArticleEditLogic(
                Db,
                authorInfoCacheService,
                Storage,
                new NullLogger<Sky.Editor.Data.Logic.ArticleEditLogic>(),
                EditorSettings,
                Clock,
                SlugService,
                ArticleHtmlService,
                CatalogService,
                PublishingService,
                TitleChangeService,
                RedirectService,
                TemplateService);

            // ✅ NOW CREATE FEATURE HANDLERS WITH TEMPLATE SERVICE
            CreateArticleHandler = new CreateArticleHandler(
                Db,
                ArticleHtmlService,
                CatalogService,
                PublishingService,
                TitleChangeService,
                TemplateService, // Now TemplateService is available!
                Clock,
                new NullLogger<CreateArticleHandler>());

            var saveArticleHandlerInstance = new SaveArticleHandler(
                Db,
                ArticleHtmlService,
                CatalogService,
                PublishingService,
                TitleChangeService,
                Clock,
                new NullLogger<SaveArticleHandler>());

            SaveArticleHandler = saveArticleHandlerInstance;

            // ✅ CREATE CreateArticleVersionHandler
            var createArticleVersionHandler = new Sky.Editor.Features.Articles.CreateVersion.CreateArticleVersionHandler(
                Db,
                new NullLogger<Sky.Editor.Features.Articles.CreateVersion.CreateArticleVersionHandler>());

            // ✅ CREATE PublishArticleHandler
            var publishArticleHandler = new Sky.Editor.Features.Articles.Publish.PublishArticleHandler(
                Db,
                Clock,
                PublishingService,
                CatalogService,
                new NullLogger<Sky.Editor.Features.Articles.Publish.PublishArticleHandler>());

            // ✅ CREATE DeleteArticleHandler
            var deleteArticleHandler = new Sky.Editor.Features.Articles.Delete.DeleteArticleHandler(
                Db,
                PublishingService,
                Storage,
                EditorSettings,
                new NullLogger<Sky.Editor.Features.Articles.Delete.DeleteArticleHandler>());

            // ✅ CREATE TrashArticleHandler (permanent delete)
            var trashArticleHandler = new Sky.Editor.Features.Articles.Trash.TrashArticleHandler(
                Db,
                PublishingService,
                Storage,
                new NullLogger<Sky.Editor.Features.Articles.Trash.TrashArticleHandler>());

            // ✅ CREATE PublishPageDesignVersionHandler
            var publishPageDesignVersionHandler = new Sky.Editor.Features.Templates.Publishing.PublishPageDesignVersionHandler(
                Db,
                PublishingService,
                Clock,
                new NullLogger<Sky.Editor.Features.Templates.Publishing.PublishPageDesignVersionHandler>(),
                Mediator);

            // ✅ NOW POPULATE THE LAZY FACTORIES so the Mediator can resolve the handlers
            saveArticleHandlerFactory = () => SaveArticleHandler;
            createArticleHandlerFactory = () => CreateArticleHandler;
            createArticleVersionHandlerFactory = () => createArticleVersionHandler;
            publishArticleHandlerFactory = () => publishArticleHandler;
            deleteArticleHandlerFactory = () => deleteArticleHandler;
            trashArticleHandlerFactory = () => trashArticleHandler;
            publishPageDesignVersionHandlerFactory = () => publishPageDesignVersionHandler;

            // ✅ ADD THIS - Get the real IHttpClientFactory from DI
            HttpClientFactory = Services.GetRequiredService<IHttpClientFactory>();

            // ✅ ADD THIS - Create real LayoutImportService with live HttpClientFactory
            LayoutImportService = new LayoutImportService(
                HttpClientFactory,
                Cache,
                new LoggerFactory().CreateLogger<LayoutImportService>());

            // ✅ CREATE LayoutVersioningService (needed by PromoteLayoutHandler and ImportLayoutHandler)
            var layoutVersioningService = new Sky.Editor.Services.Layouts.LayoutVersioningService(
                Db,
                ArticleHtmlService,
                new NullLogger<Sky.Editor.Services.Layouts.LayoutVersioningService>());

            // ✅ CREATE PromoteLayoutHandler
            var promoteLayoutHandler = new Sky.Editor.Features.Layouts.Promote.PromoteLayoutHandler(
                Db,
                layoutVersioningService,
                new NullLogger<Sky.Editor.Features.Layouts.Promote.PromoteLayoutHandler>());

            // ✅ CREATE ImportLayoutHandler
            var importLayoutHandler = new Sky.Editor.Features.Layouts.Import.ImportLayoutHandler(
                Db,
                Mediator,
                LayoutImportService,
                layoutVersioningService,
                new NullLogger<Sky.Editor.Features.Layouts.Import.ImportLayoutHandler>());

            // ✅ POPULATE THE LAYOUT HANDLER LAZY FACTORIES
            promoteLayoutHandlerFactory = () => promoteLayoutHandler;
            importLayoutHandlerFactory = () => importLayoutHandler;

            ArticleScheduler = new ArticleScheduler(
                new NullLogger<ArticleScheduler>(),
                configuration,
                Clock,
                Services);

            // ✅ NOW THESE CAN RUN (after TemplateService is created)
            EnsureBlogStreamTemplateExistsAsync().Wait();
            EnsureBlogPostTemplateExistsAsync().Wait();

            AfterInitialize();
        }

        /// <summary>
        /// Override for additional seeding in derived test classes.
        /// </summary>
        protected virtual void AfterInitialize() { }

        protected Task<int> ArticleCountAsync() => Db.Articles.CountAsync();

        /// <summary>
        /// Helper method to save an article using the SaveArticleCommand via mediator.
        /// Replaces the deprecated Logic.SaveArticle() calls in tests.
        /// </summary>
        /// <param name="article">The article view model to save.</param>
        /// <param name="userId">The user ID performing the save.</param>
        /// <returns>Article update result.</returns>
        protected async Task<CommandResult<ArticleUpdateResult>> SaveArticleAsync(ArticleViewModel article, Guid userId)
        {
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

            return await Mediator.SendAsync(command);
        }

        /// <summary>
        /// Helper method to create an article using the CreateArticleCommand via mediator.
        /// Replaces the deprecated CreateArticleAsync() calls in tests.
        /// </summary>
        /// <param name="title">Title of the article to create.</param>
        /// <param name="userId">The user ID creating the article.</param>
        /// <param name="templateId">Optional template ID to use for the article.</param>
        /// <param name="blogKey">Optional blog key (default: empty).</param>
        /// <param name="articleType">Optional article type (default: General).</param>
        /// <returns>Created article view model.</returns>
        protected async Task<ArticleViewModel> CreateArticleAsync(
            string title,
            Guid userId,
            Guid? templateId = null,
            string blogKey = "",
            Cosmos.Cms.Common.ArticleType articleType = Cosmos.Cms.Common.ArticleType.General)
        {
            var command = new CreateArticleCommand
            {
                Title = title,
                UserId = userId,
                TemplateId = templateId,
                BlogKey = blogKey,
                ArticleType = articleType
            };

            var result = await Mediator.SendAsync(command);
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
        /// Helper method to create an article version using the CreateArticleVersionCommand via mediator.
        /// Replaces the deprecated NewVersion() calls in tests.
        /// </summary>
        /// <param name="articleNumber">The article number to create a version for.</param>
        /// <returns>Created article (new version).</returns>
        protected async Task<ArticleViewModel> CreateArticleVersionAsync(
            int articleNumber)
        {
            var command = new CreateArticleVersionCommand
            {
                ArticleNumber = articleNumber
            };

            var result = await Mediator.SendAsync(command);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to create article version: {result.ErrorMessage}");
            }

            return result.Data.Article;
        }

        /// <summary>
        /// Helper method to delete an article using the DeleteArticleCommand via mediator.
        /// Replaces the deprecated DeleteArticle() calls in tests.
        /// </summary>
        /// <param name="articleNumber">The article number to delete.</param>
        /// <returns>Unit result.</returns>
        protected async Task DeleteArticleAsync(int articleNumber)
        {
            var command = new Sky.Editor.Features.Articles.Delete.DeleteArticleCommand
            {
                ArticleNumber = articleNumber
            };

            var result = await Mediator.SendAsync(command);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to delete article: {result.ErrorMessage}");
            }
        }

        /// <summary>
        /// Helper method to restore an article using the RestoreArticleCommand via mediator.
        /// Replaces the deprecated RestoreArticle() calls in tests.
        /// </summary>
        /// <param name="articleNumber">The article number to restore.</param>
        /// <param name="userId">The user ID performing the restore.</param>
        /// <returns>Unit result.</returns>
        protected async Task RestoreArticleAsync(int articleNumber, string userId)
        {
            var command = new Sky.Editor.Features.Articles.Restore.RestoreArticleCommand
            {
                ArticleNumber = articleNumber,
                UserId = userId
            };

            var result = await Mediator.SendAsync(command);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to restore article: {result.ErrorMessage}");
            }
        }

        /// <summary>
        /// Creates a new tenant test context for multi-tenant isolation testing.
        /// </summary>
        /// <param name="tenantDomain">Tenant domain name (e.g., "tenant1.example.com").</param>
        /// <param name="sharedDatabaseName">Optional shared database name for testing data isolation across tenants in same DB.</param>
        /// <param name="sharedCache">Optional shared cache for testing cache isolation.</param>
        /// <returns>Initialized tenant test context.</returns>
        protected async Task<TenantTestContext> CreateTenantContextAsync(
            string tenantDomain,
            string sharedDatabaseName = null,
            IMemoryCache sharedCache = null)
        {
            var tenantId = Guid.NewGuid();
            var context = new TenantTestContext(
                tenantId,
                tenantDomain,
                sharedDatabaseName,
                sharedCache ?? Cache,
                Storage);

            await context.InitializeAsync(seedLayout: true, baseTestContext: this);
            return context;
        }

        /// <summary>
        /// Creates multiple tenant contexts for testing tenant isolation.
        /// All tenants share the same in-memory database to test data isolation.
        /// </summary>
        /// <param name="tenantDomains">Array of tenant domain names.</param>
        /// <param name="useSharedCache">Whether tenants share a cache instance.</param>
        /// <returns>Array of initialized tenant contexts.</returns>
        protected async Task<TenantTestContext[]> CreateMultipleTenantContextsAsync(
            string[] tenantDomains,
            bool useSharedCache = false)
        {
            // Create shared database name for all tenants
            var sharedDbName = $"MultiTenant_{Guid.NewGuid()}";
            var sharedCache = useSharedCache ? Cache : null;

            var contexts = new TenantTestContext[tenantDomains.Length];
            for (int i = 0; i < tenantDomains.Length; i++)
            {
                contexts[i] = await CreateTenantContextAsync(
                    tenantDomains[i],
                    sharedDbName,
                    sharedCache);
            }

            return contexts;
        }

        public virtual async ValueTask DisposeAsync()
        {
            if (ServiceScope != null)
                ServiceScope.Dispose();
            if (Db != null)
                await Db.DisposeAsync();
            Cache.Dispose();
        }

        /// <summary>
        /// Captures domain events for assertions in tests.
        /// </summary>
        protected sealed class TestDomainEventDispatcher : IDomainEventDispatcher
        {
            private readonly List<IDomainEvent> events = new();

            public IReadOnlyList<IDomainEvent> Events => events;

            public Task DispatchAsync(IEnumerable<IDomainEvent> events)
            {
                if (events != null) this.events.AddRange(events);
                return Task.CompletedTask;
            }

            public Task DispatchAsync(IDomainEvent @event)
            {
                if (@event != null) events.Add(@event);
                return Task.CompletedTask;
            }

            public Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken)
            {
                if (@event == null) return Task.CompletedTask;
                cancellationToken.ThrowIfCancellationRequested();
                events.Add(@event);
                return Task.CompletedTask;
            }

            public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken)
            {
                if (domainEvents != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    this.events.AddRange(domainEvents);
                }
                return Task.CompletedTask;
            }

            public T Last<T>() where T : class, IDomainEvent =>
                events.LastOrDefault(e => e is T) as T;

            public void Clear() => events.Clear();
        }

        [TestInitialize]
        public void Setup()
        {
            InitializeTestContext();

            // CREATE AN ACTUAL USER IN THE DATABASE
            var user = new IdentityUser
            {
                Id = TestUserId.ToString(),
                UserName = "test@example.com",
                Email = "test@example.com",
                NormalizedUserName = "TEST@EXAMPLE.COM",
                NormalizedEmail = "TEST@EXAMPLE.COM"
            };
            UserManager.CreateAsync(user).Wait();

            // Create controller with all dependencies
            EditorController = new EditorController(
                Logger,
                Db,
                UserManager,
                RoleManager,
                Logic,
                EditorSettings,
                ViewRenderService,
                Storage,
                Hub.Object,
                PublishingService,
                ArticleHtmlService,
                ReservedPaths,
                TitleChangeService,
                TemplateService,
                Mediator,
                new CacheService<Layout>(
                    Cache,
                    new NullLogger<CacheService<Layout>>(),
                    DynamicConfigurationProvider),
                DynamicConfigurationProvider);
        }
    }
}
