using Cosmos.BlobService;
using Cosmos.Cms.Common.Services.Configurations;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.DynamicConfig;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Cosmos.Common.Models;
using System.Diagnostics;
using System.Reflection;

namespace Sky.Tests
{
    /// <summary>
    /// Base fixture for search-related tests.
    /// Sets up an isolated in-memory EF Core context and minimal supporting services.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public abstract class SearchTestBase
    {
        protected ApplicationDbContext? DbContext { get; set; }
        protected Mock<IDynamicConfigurationProvider>? MockConfigurationProvider { get; set; }
        protected Mock<IHttpContextAccessor>? MockHttpContextAccessor { get; set; }
        protected Mock<ILogger>? MockLogger { get; set; }
        protected IServiceProvider? ServiceProvider { get; set; }
        
        /// <summary>
        /// Gets the test database name (unique per test run).
        /// </summary>
        protected string DatabaseName { get; private set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets the default tenant domain for testing.
        /// </summary>
        protected string TestTenantDomain { get; private set; } = "test.example.com";

        [TestInitialize]
        public virtual async Task InitializeTestAsync()
        {
            // Create unique database name for this test
            DatabaseName = $"TestDb_{Guid.NewGuid():N}_{DateTime.UtcNow.Ticks}";

            // Setup configuration
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"DataSource={DatabaseName}.db",
                    ["AppSettings:SiteSettings:AllowSetup"] = "true",
                    ["AppSettings:SendGridApiKey"] = "test-key",
                    ["AppSettings:SecretKey"] = "test-secret-key-for-testing-purposes-only",
                    ["SiteSettings:AllowSetup"] = "true"
                })
                .Build();

            // Setup services
            var services = new ServiceCollection();
            
            // Add EF Core with in-memory database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(DatabaseName)
                    .EnableSensitiveDataLogging()
                    .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole())));

            // Add minimal required services
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(builder => builder.AddConsole());
            services.AddMemoryCache();

            // Setup mocks
            MockConfigurationProvider = new Mock<IDynamicConfigurationProvider>();
            
            // FIXED: TryGetTenantDomainAsync() no longer exists
            // Use GetTenantDomainNameFromRequest() instead
            MockConfigurationProvider
                .Setup(x => x.GetTenantDomainNameFromRequest())
                .Returns(TestTenantDomain);
            
            MockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString(TestTenantDomain);
            context.Request.Headers["x-origin-hostname"] = TestTenantDomain;
            MockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            MockLogger = new Mock<ILogger>();

            // Register mocks
            services.AddSingleton(MockConfigurationProvider.Object);
            services.AddSingleton(MockHttpContextAccessor.Object);
            services.AddSingleton(MockLogger.Object);

            // Build service provider
            ServiceProvider = services.BuildServiceProvider();
            
            // Get DbContext
            DbContext = ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Ensure database is created
            await DbContext.Database.EnsureCreatedAsync();
            
            // Setup test data
            await SetupTestDataAsync();
        }

        [TestCleanup]
        public virtual async Task CleanupTestAsync()
        {
            if (DbContext != null)
            {
                await DbContext.Database.EnsureDeletedAsync();
                await DbContext.DisposeAsync();
            }

            if (ServiceProvider is IDisposable disposableServiceProvider)
            {
                disposableServiceProvider.Dispose();
            }
        }

        /// <summary>
        /// Sets up initial test data. Override in derived classes as needed.
        /// </summary>
        protected virtual async Task SetupTestDataAsync()
        {
            if (DbContext == null) return;

            // Create test articles for search functionality
            var testArticles = new[]
            {
                new Article
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Article 1",
                    Content = "This is the content of test article 1 with some searchable text.",
                    StatusCode = (int)StatusCodeEnum.Active,  // FIXED: Added namespace import
                    Published = DateTime.UtcNow.AddDays(-1),
                    UrlPath = "test-article-1",
                    Updated = DateTime.UtcNow.AddDays(-1),
                    ArticleNumber = 1
                },
                new Article
                {
                    Id = Guid.NewGuid(), 
                    Title = "Test Article 2",
                    Content = "This is the content of test article 2 with different searchable content.",
                    StatusCode = (int)StatusCodeEnum.Active,  // FIXED: Added namespace import
                    Published = DateTime.UtcNow.AddDays(-2),
                    UrlPath = "test-article-2", 
                    Updated = DateTime.UtcNow.AddDays(-2),
                    ArticleNumber = 2
                },
                new Article
                {
                    Id = Guid.NewGuid(),
                    Title = "Unpublished Article",
                    Content = "This article is not published and should not appear in search results.",
                    StatusCode = (int)StatusCodeEnum.Inactive,  // FIXED: Added namespace import
                    Published = null,
                    UrlPath = "unpublished-article",
                    Updated = DateTime.UtcNow.AddDays(-3),
                    ArticleNumber = 3
                }
            };

            DbContext.Articles.AddRange(testArticles);
            await DbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Helper method to create a test article.
        /// </summary>
        protected Article CreateTestArticle(string title, string content, bool isPublished = true)
        {
            return new Article
            {
                Id = Guid.NewGuid(),
                Title = title,
                Content = content,
                StatusCode = isPublished ? (int)StatusCodeEnum.Active : (int)StatusCodeEnum.Inactive,  // FIXED
                Published = isPublished ? DateTime.UtcNow.AddDays(-1) : null,
                UrlPath = title.ToLower().Replace(" ", "-"),
                Updated = DateTime.UtcNow.AddDays(-1),
                ArticleNumber = Random.Shared.Next(1000, 9999)
            };
        }

        /// <summary>
        /// Helper method to add articles to the database.
        /// </summary>
        protected async Task AddArticlesAsync(params Article[] articles)
        {
            if (DbContext == null) return;
            
            DbContext.Articles.AddRange(articles);
            await DbContext.SaveChangesAsync();
        }
    }
}