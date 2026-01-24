using Cosmos.BlobService;
using Cosmos.Cms.Common.Services.Configurations;
using Cosmos.Common.Data;
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
using Sky.Editor.Services.BlogPublishing;
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
using Sky.Editor.Features.Shared;
using Sky.Editor.Features.Articles.Create;
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
        protected AuthorInfoService AuthorInfoService = null!;
        protected ApplicationDbContext Db;
        protected ArticleEditLogic Logic = null!;
        protected CatalogService CatalogService = null!;
        protected StorageContext Storage = null!;
        protected IMemoryCache Cache = null!;
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
        protected IBlogRenderingService BlogRenderingService = null!;
        protected IViewRenderService ViewRenderService = null!;
        protected IServiceProvider Services = null!;
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
                    PageType = "blog-stream",
                    Content = t.Content,
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
                    PageType = "blog-post",
                    Content = t.Content,
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
                    Head = string.Empty,
                    HtmlHeader = string.Empty,
                    FooterHtmlContent = string.Empty
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

            // Lightweight configuration (all in-memory).
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(initialConfig)
                .AddUserSecrets(typeof(SkyCmsTestBase).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();

            HttpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            HttpContextAccessor.HttpContext!.Request.Host = new HostString("example.com");

            // FIX: Create webHostEnvironmentMock and webHostEnvironment before ServiceCollection
            var webHostEnvironmentMock = new Mock<IWebHostEnvironment>();
            var assem = Assembly.GetAssembly(typeof(TemplateService));
            var path = Path.GetDirectoryName(assem!.Location)!;
            webHostEnvironmentMock.Setup(m => m.ContentRootPath).Returns(path);
            var webHostEnvironment = webHostEnvironmentMock.Object;

            // ✅ UPDATED: Configure Azure Blob Storage - use connection string from config or fail
            var storageConnectionString = configuration.GetConnectionString("StorageConnectionString");
            
            // Fail fast if no connection string is provided
            if (string.IsNullOrEmpty(storageConnectionString))
            {
                throw new InvalidOperationException(
                    "Storage connection string is required. " +
                    "Set CONNECTIONSTRINGS__STORAGECONNECTIONSTRING environment variable or add it to user secrets.");
            }
            
            // Initialize Storage with connection validation
            try
            {
                Storage = new StorageContext(storageConnectionString, Cache);
                Console.WriteLine("✅ Successfully connected to Azure Blob Storage");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: Storage initialization failed: {ex.Message}");
                Console.WriteLine($"   Connection string starts with: {storageConnectionString.Substring(0, Math.Min(50, storageConnectionString.Length))}...");
                throw; // Fail the test immediately
            }

            // Core service graph.
            SlugService = new SlugService();
            ArticleHtmlService = new ArticleHtmlService();
            var catalogLogger = new LoggerFactory().CreateLogger<CatalogService>();
            CatalogService = new CatalogService(Db, ArticleHtmlService, Clock, catalogLogger);
            EventDispatcher = new TestDomainEventDispatcher();
            var authorInfoService = new AuthorInfoService(Db, Cache);
            BlogRenderingService = new BlogRenderingService(Db);
            ReservedPaths = new ReservedPaths(Db);
            AuthorInfoService = new AuthorInfoService(Db, Cache);

            var mockViewRenderService = new Mock<IViewRenderService>();
            mockViewRenderService.Setup(x => x.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html>test</html>");
            ViewRenderService = mockViewRenderService.Object;

            EditorSettings = new EditorSettings(configuration, Db, HttpContextAccessor, Cache, null!);

            PublishingService = new PublishingService(
                Db, 
                Storage, 
                EditorSettings,
                new LoggerFactory().CreateLogger<PublishingService>(),
                HttpContextAccessor, 
                authorInfoService,
                Clock,
                BlogRenderingService,
                ViewRenderService, 
                null!,
                new NoOpPublishingProgressReporter()); // ✅ Add progress reporter

            RedirectService = new RedirectService(Db, SlugService, Clock, PublishingService);
            TitleChangeService = new TitleChangeService(Db, SlugService, RedirectService, Clock, EventDispatcher, PublishingService, ReservedPaths, BlogRenderingService, new LoggerFactory().CreateLogger<TitleChangeService>());
            
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
            DynamicConfigurationProvider = mockDynamicConfigProvider.Object;

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
            Services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<DiagnosticSource>(new DiagnosticListener("TestListener"))
                .AddSingleton<DiagnosticListener>(new DiagnosticListener("TestListener"))
                .AddSingleton<IWebHostEnvironment>(webHostEnvironment)
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<IMemoryCache>(Cache)
                .AddSingleton<ApplicationDbContext>(sp => Db)
                .AddSingleton<StorageContext>(Storage)
                .AddSingleton<IHttpContextAccessor>(HttpContextAccessor)
                .AddSingleton<ISlugService>(SlugService)
                .AddSingleton<IArticleHtmlService>(ArticleHtmlService)
                .AddSingleton<ICatalogService>(CatalogService)
                .AddSingleton<IDomainEventDispatcher>(EventDispatcher)
                .AddSingleton<IClock>(Clock)
                .AddSingleton<IBlogRenderingService>(BlogRenderingService)
                .AddSingleton<IAuthorInfoService>(AuthorInfoService)
                .AddSingleton<IViewRenderService>(ViewRenderService)
                .AddSingleton<IReservedPaths>(ReservedPaths)
                .AddSingleton<IEditorSettings>(EditorSettings)
                .AddSingleton<IPublishingService>(PublishingService)
                .AddSingleton<IRedirectService>(RedirectService)
                .AddSingleton<ITitleChangeService>(TitleChangeService)
                // ❌ REMOVE THIS - Don't register TemplateService yet
                // .AddSingleton<ITemplateService>(TemplateService)
                .AddSingleton<ITenantArticleLogicFactory>(TenantArticleLogicFactory)
                .AddSingleton(new SiteSettings())
                .AddScoped<ICommandHandler<CreateArticleCommand, CommandResult<ArticleViewModel>>>(sp => CreateArticleHandler)
                .AddScoped<ICommandHandler<SaveArticleCommand, CommandResult<ArticleUpdateResult>>>(sp => SaveArticleHandler)
                .AddScoped<IMediator, Mediator>()
                .AddHttpClient()  // ✅ ADD THIS - Registers IHttpClientFactory
                .AddRazorPages()
                .Services
                .BuildServiceProvider();

            // ✅ GET MEDIATOR FROM SERVICE PROVIDER FIRST
            Mediator = Services.GetRequiredService<IMediator>();

            // ✅ NOW CREATE TEMPLATE SERVICE WITH CONFIGURATION AND SERVICE PROVIDER
            TemplateService = new TemplateService(
                webHostEnvironment,
                new LoggerFactory().CreateLogger<TemplateService>(),
                Db,
                DynamicConfigurationProvider);      // ✅ Pass service provider (already built)

            // ✅ NOW CREATE LOGIC WITH TEMPLATE SERVICE
            Logic = new ArticleEditLogic(
                Db,
                Cache,
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

            SaveArticleHandler = new SaveArticleHandler(
                Db,
                ArticleHtmlService,
                CatalogService,
                PublishingService,
                TitleChangeService,
                Clock,
                new NullLogger<SaveArticleHandler>());

            // ✅ ADD THIS - Get the real IHttpClientFactory from DI
            HttpClientFactory = Services.GetRequiredService<IHttpClientFactory>();

            // ✅ ADD THIS - Create real LayoutImportService with live HttpClientFactory
            LayoutImportService = new LayoutImportService(
                HttpClientFactory,
                Cache,
                new LoggerFactory().CreateLogger<LayoutImportService>());

            ArticleScheduler = new ArticleScheduler(
                new NullLogger<ArticleScheduler>(),
                EditorSettings,
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

        public virtual async ValueTask DisposeAsync()
        {
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
                Cache,                          // ✅ Add memory cache for layout caching
                DynamicConfigurationProvider);  // ✅ Add config provider for tenant-aware caching

            // Setup user context
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "test@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "TestAuth"));

            EditorController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }
    }
}
