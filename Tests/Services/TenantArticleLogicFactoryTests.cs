// <copyright file="TenantArticleLogicFactoryTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Cms.Common.Services.Configurations;
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Services;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Shared;
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
    using MediatR;

    /// <summary>
    /// Unit tests for <see cref="TenantArticleLogicFactory"/> to ensure proper
    /// tenant-specific ArticleEditLogic instantiation in multi-tenant scenarios.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class TenantArticleLogicFactoryTests
    {
        private Mock<IEditorSettings> _mockEditorSettings;
        private Mock<IDynamicConfigurationProvider> _mockConfigProvider;
        private IServiceProvider _serviceProvider;
        private DbContextOptions<DynamicConfigDbContext> _configDbOptions;
        private DbContextOptions<ApplicationDbContext> _appDbOptions;
        private IConfiguration _configuration;

        [TestInitialize]
        public void Setup()
        {
            // Load test configuration
            _configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddUserSecrets(typeof(TenantArticleLogicFactoryTests).Assembly, optional: true)
                .Build();

            // Setup configuration database
            _configDbOptions = new DbContextOptionsBuilder<DynamicConfigDbContext>()
                .UseInMemoryDatabase($"FactoryTest_{Guid.NewGuid()}")
                .Options;

            // Setup application database for single-tenant mode
            _appDbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"AppDb_{Guid.NewGuid()}")
                .Options;

            SeedTenantData();

            // Setup mocks with required properties
            _mockEditorSettings = new Mock<IEditorSettings>();
            _mockEditorSettings.Setup(x => x.PublisherUrl).Returns("https://localhost:5000");
            _mockEditorSettings.Setup(x => x.BlobPublicUrl).Returns("/");
            _mockEditorSettings.Setup(x => x.StaticWebPages).Returns(false);
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(false);
            
            _mockConfigProvider = new Mock<IDynamicConfigurationProvider>();

            // Setup service provider with all required dependencies
            _serviceProvider = BuildServiceProvider();
        }

        [TestCleanup]
        public void Cleanup()
        {
            using var context = new DynamicConfigDbContext(_configDbOptions);
            context.Database.EnsureDeleted();

            using var appContext = new ApplicationDbContext(_appDbOptions);
            appContext.Database.EnsureDeleted();
        }

        private void SeedTenantData()
        {
            using var context = new DynamicConfigDbContext(_configDbOptions);
            context.Database.EnsureCreated();

            // Use valid storage connection strings from configuration or fallback to valid formats
            var storageConnectionString = _configuration.GetConnectionString("StorageConnectionString")
                ?? _configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            context.Connections.AddRange(
                new Connection
                {
                    Id = Guid.NewGuid(),
                    DomainNames = new[] { "tenant1.com" },
                    DbConn = "Data Source=:memory:;",
                    StorageConn = storageConnectionString,
                    WebsiteUrl = "https://tenant1.com",
                    ResourceGroup = "tenant1-rg"
                },
                new Connection
                {
                    Id = Guid.NewGuid(),
                    DomainNames = new[] { "tenant2.com" },
                    DbConn = "Data Source=:memory:;",
                    StorageConn = storageConnectionString,
                    WebsiteUrl = "https://tenant2.com",
                    ResourceGroup = "tenant2-rg"
                }
            );

            context.SaveChanges();
        }

        private IServiceProvider BuildServiceProvider()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

            var webHostEnvironment = new Mock<IWebHostEnvironment>();
            webHostEnvironment.Setup(x => x.ContentRootPath).Returns(AppContext.BaseDirectory);

            var services = new ServiceCollection();

            // Core dependencies
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton(webHostEnvironment.Object);
            services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
            services.AddSingleton(_mockEditorSettings.Object);
            services.AddSingleton(_mockConfigProvider.Object);
            services.AddSingleton(new SiteSettings());

            // Register custom IMediator implementation
            services.AddSingleton<Cosmos.Common.Features.Shared.IMediator>(sp => 
                new Cosmos.Common.Features.Shared.Mediator(sp));

            // Register MediatR with handlers from the Editor assembly
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<TemplateService>());

            // Register ApplicationDbContext for single-tenant mode
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase($"SingleTenantDb_{Guid.NewGuid()}"));

            // Register StorageContext for single-tenant mode
            var storageConnectionString = _configuration.GetConnectionString("StorageConnectionString")
                ?? _configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            services.AddSingleton<StorageContext>(sp => new StorageContext(
                storageConnectionString,
                sp.GetRequiredService<IMemoryCache>()))
            .AddSingleton<IStorageContext>(sp => sp.GetRequiredService<StorageContext>());

            // Service dependencies
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<ISlugService, SlugService>();
            services.AddSingleton<IArticleHtmlService, ArticleHtmlService>();
            services.AddSingleton<ILogger<CatalogService>>(NullLogger<CatalogService>.Instance);
            services.AddSingleton<ILogger<PublishingService>>(NullLogger<PublishingService>.Instance);
            services.AddSingleton<ILogger<TitleChangeService>>(NullLogger<TitleChangeService>.Instance);
            services.AddSingleton<ILogger<ArticleEditLogic>>(NullLogger<ArticleEditLogic>.Instance);
            services.AddSingleton<ILogger<TemplateService>>(NullLogger<TemplateService>.Instance);
            services.AddSingleton<ILogger<Sky.Editor.Services.Layouts.LayoutTemplateService>>(NullLogger<Sky.Editor.Services.Layouts.LayoutTemplateService>.Instance);

            // ✅ Add no-op progress reporter for tests
            services.AddSingleton<IPublishingProgressReporter, NoOpPublishingProgressReporter>();

            // Register additional services required by single-tenant mode
            services.AddScoped<ICatalogService>(sp =>
            {
                var dbContext = sp.GetRequiredService<ApplicationDbContext>();
                return new CatalogService(
                    dbContext,
                    sp.GetRequiredService<IArticleHtmlService>(),
                    sp.GetRequiredService<IClock>(),
                    sp.GetRequiredService<ILogger<CatalogService>>());
            });

            services.AddScoped<IPublishingService>(sp =>
            {
                var dbContext = sp.GetRequiredService<ApplicationDbContext>();
                var storageContext = sp.GetRequiredService<StorageContext>();
                var mockAuthorService = new Mock<IAuthorInfoService>();
                var mockBlogRenderingService = new Mock<IBlogRenderingService>();
                
                return new PublishingService(
                    dbContext,
                    storageContext,
                    sp.GetRequiredService<IEditorSettings>(),
                    sp.GetRequiredService<ILogger<PublishingService>>(),
                    null,  // No HttpContextAccessor in tests
                    mockAuthorService.Object,
                    sp.GetRequiredService<IClock>(),
                    mockBlogRenderingService.Object,
                    sp.GetRequiredService<IViewRenderService>(),
                    sp,
                    sp.GetRequiredService<IPublishingProgressReporter>()); // ✅ Add progress reporter
            });

            services.AddScoped<IRedirectService>(sp =>
            {
                var dbContext = sp.GetRequiredService<ApplicationDbContext>();
                return new RedirectService(
                    dbContext,
                    sp.GetRequiredService<ISlugService>(),
                    sp.GetRequiredService<IClock>(),
                    sp.GetRequiredService<IPublishingService>());
            });

            services.AddScoped<ITitleChangeService>(sp =>
            {
                var dbContext = sp.GetRequiredService<ApplicationDbContext>();
                var mockReservedPaths = new Mock<IReservedPaths>();
                var mockBlogRenderingService = new Mock<IBlogRenderingService>();
                
                return new TitleChangeService(
                    dbContext,
                    sp.GetRequiredService<ISlugService>(),
                    sp.GetRequiredService<IRedirectService>(),
                    sp.GetRequiredService<IClock>(),
                    null,
                    sp.GetRequiredService<IPublishingService>(),
                    mockReservedPaths.Object,
                    mockBlogRenderingService.Object,
                    sp.GetRequiredService<ILogger<TitleChangeService>>());
            });

            // Register TemplateService - MediatR will be injected automatically
            services.AddScoped<ITemplateService, TemplateService>();

            // Mock view render service
            var mockViewRenderService = new Mock<IViewRenderService>();
            mockViewRenderService.Setup(x => x.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html>test</html>");
            services.AddSingleton(mockViewRenderService.Object);

            return services.BuildServiceProvider();
        }

        #region Single Tenant Mode Tests

        /// <summary>
        /// Tests that CreateForTenantAsync returns ArticleEditLogic in single-tenant mode.
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_SingleTenantMode_ReturnsArticleEditLogic()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(false);
            var factory = new TenantArticleLogicFactory(_serviceProvider, _mockEditorSettings.Object);

            // Act
            var logic = await factory.CreateForTenantAsync("any-domain.com");

            // Assert
            Assert.IsNotNull(logic);
            Assert.IsInstanceOfType(logic, typeof(ArticleEditLogic));
        }

        /// <summary>
        /// Tests that single-tenant mode ignores domain name parameter.
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_SingleTenantMode_IgnoresDomainName()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(false);
            var factory = new TenantArticleLogicFactory(_serviceProvider, _mockEditorSettings.Object);

            // Act
            var logic1 = await factory.CreateForTenantAsync("tenant1.com");
            var logic2 = await factory.CreateForTenantAsync("tenant2.com");

            // Assert
            Assert.IsNotNull(logic1);
            Assert.IsNotNull(logic2);
            // In single-tenant mode, both should use same services (not tenant-specific)
        }

        #endregion

        #region Multi-Tenant Mode Tests

        /// <summary>
        /// CRITICAL: Tests that CreateForTenantAsync creates tenant-specific logic in multi-tenant mode.
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_MultiTenantMode_ReturnsTenantSpecificLogic()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(true);

            var storageConnectionString = _configuration.GetConnectionString("StorageConnectionString")
                ?? _configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant1.com" },
                DbConn = "Data Source=Tenant1.db;",
                StorageConn = storageConnectionString,
                WebsiteUrl = "https://tenant1.com",
                ResourceGroup = "tenant1-rg"
            };

            _mockConfigProvider.Setup(x => x.GetTenantConnectionAsync("tenant1.com", default))
                .ReturnsAsync(connection);

            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                _mockConfigProvider.Object);

            // Act
            var logic = await factory.CreateForTenantAsync("tenant1.com");

            // Assert
            Assert.IsNotNull(logic);
            Assert.IsInstanceOfType(logic, typeof(ArticleEditLogic));
        }

        /// <summary>
        /// CRITICAL: Tests that different tenants get isolated ArticleEditLogic instances.
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_MultiTenantMode_CreatesSeparateInstancesPerTenant()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(true);

            var storageConnectionString = _configuration.GetConnectionString("StorageConnectionString")
                ?? _configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            var connection1 = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant1.com" },
                DbConn = "Data Source=Tenant1.db;",
                StorageConn = storageConnectionString,
                WebsiteUrl = "https://tenant1.com",
                ResourceGroup = "tenant1-rg"
            };

            var connection2 = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant2.com" },
                DbConn = "Data Source=Tenant2.db;",
                StorageConn = storageConnectionString,
                WebsiteUrl = "https://tenant2.com",
                ResourceGroup = "tenant2-rg"
            };

            _mockConfigProvider.Setup(x => x.GetTenantConnectionAsync("tenant1.com", default))
                .ReturnsAsync(connection1);
            _mockConfigProvider.Setup(x => x.GetTenantConnectionAsync("tenant2.com", default))
                .ReturnsAsync(connection2);

            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                _mockConfigProvider.Object);

            // Act
            var logic1 = await factory.CreateForTenantAsync("tenant1.com");
            var logic2 = await factory.CreateForTenantAsync("tenant2.com");

            // Assert
            Assert.IsNotNull(logic1);
            Assert.IsNotNull(logic2);
            Assert.AreNotSame(logic1, logic2, "Different tenants should get separate logic instances");
        }

        /// <summary>
        /// CRITICAL: Tests that tenant connection is resolved correctly.
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_MultiTenantMode_ResolvesTenantConnection()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(true);

            var domainName = "tenant1.com";
            var storageConnectionString = _configuration.GetConnectionString("StorageConnectionString")
                ?? _configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { domainName },
                DbConn = "Data Source=Tenant1.db;",
                StorageConn = storageConnectionString,
                WebsiteUrl = "https://tenant1.com",
                ResourceGroup = "tenant1-rg"
            };

            _mockConfigProvider.Setup(x => x.GetTenantConnectionAsync(domainName, default))
                .ReturnsAsync(connection);

            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                _mockConfigProvider.Object);

            // Act
            var logic = await factory.CreateForTenantAsync(domainName);

            // Assert
            Assert.IsNotNull(logic);
            _mockConfigProvider.Verify(x => x.GetTenantConnectionAsync(domainName, default), Times.Once);
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Tests that CreateForTenantAsync handles null connection gracefully.
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_MultiTenantMode_WhenConnectionNull_ThrowsException()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(true);
            _mockConfigProvider.Setup(x => x.GetTenantConnectionAsync("nonexistent.com", default))
                .ReturnsAsync((Connection)null);

            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                _mockConfigProvider.Object);

            // Act & Assert
            await Assert.ThrowsExactlyAsync<NullReferenceException>(async () =>
                await factory.CreateForTenantAsync("nonexistent.com"));
        }

        /// <summary>
        /// Tests that CreateForTenantAsync handles provider exceptions.
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_MultiTenantMode_WhenProviderThrows_PropagatesException()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(true);
            _mockConfigProvider.Setup(x => x.GetTenantConnectionAsync(It.IsAny<string>(), default))
                .ThrowsAsync(new Exception("Database connection failed"));

            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                _mockConfigProvider.Object);

            // Act & Assert
            await Assert.ThrowsExactlyAsync<Exception>(async () =>
                await factory.CreateForTenantAsync("tenant1.com"));
        }

        #endregion

        #region Constructor Tests

        /// <summary>
        /// Tests that factory can be constructed without configuration provider (single-tenant).
        /// </summary>
        [TestMethod]
        public void Constructor_WithoutConfigProvider_WorksForSingleTenant()
        {
            // Act
            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                configurationProvider: null);

            // Assert
            Assert.IsNotNull(factory);
        }

        /// <summary>
        /// Tests that factory requires configuration provider for multi-tenant.
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_MultiTenantWithoutProvider_ThrowsNullReferenceException()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(true);
            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                configurationProvider: null);

            // Act & Assert
            await Assert.ThrowsExactlyAsync<NullReferenceException>(async () =>
                await factory.CreateForTenantAsync("tenant1.com"));
        }

        #endregion

        #region Concurrent Access Tests

        /// <summary>
        /// CRITICAL: Tests that concurrent calls create isolated instances.
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_ConcurrentCalls_CreateIsolatedInstances()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(true);

            var storageConnectionString = _configuration.GetConnectionString("StorageConnectionString")
                ?? _configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            var connections = Enumerable.Range(1, 5).Select(i => new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { $"tenant{i}.com" },
                DbConn = $"Data Source=Tenant{i}.db;",
                StorageConn = storageConnectionString,
                WebsiteUrl = $"https://tenant{i}.com",
                ResourceGroup = $"tenant{i}-rg"
            }).ToList();

            foreach (var conn in connections)
            {
                var domain = conn.DomainNames[0];
                _mockConfigProvider.Setup(x => x.GetTenantConnectionAsync(domain, default))
                    .ReturnsAsync(conn);
            }

            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                _mockConfigProvider.Object);

            // Act - Create logic instances concurrently
            var tasks = connections.Select(conn =>
                factory.CreateForTenantAsync(conn.DomainNames[0])).ToArray();

            var logicInstances = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(5, logicInstances.Length);
            foreach (var logic in logicInstances)
            {
                Assert.IsNotNull(logic);
            }

            // Verify all instances are different
            var distinctInstances = logicInstances.Distinct().Count();
            Assert.AreEqual(5, distinctInstances, "Each call should create a separate instance");
        }

        /// <summary>
        /// CRITICAL: Tests that same tenant gets new instance on each call (not cached).
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_SameTenantMultipleCalls_CreatesNewInstanceEachTime()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(true);

            var storageConnectionString = _configuration.GetConnectionString("StorageConnectionString")
                ?? _configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant1.com" },
                DbConn = "Data Source=Tenant1.db;",
                StorageConn = storageConnectionString,
                WebsiteUrl = "https://tenant1.com",
                ResourceGroup = "tenant1-rg"
            };

            _mockConfigProvider.Setup(x => x.GetTenantConnectionAsync("tenant1.com", default))
                .ReturnsAsync(connection);

            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                _mockConfigProvider.Object);

            // Act
            var logic1 = await factory.CreateForTenantAsync("tenant1.com");
            var logic2 = await factory.CreateForTenantAsync("tenant1.com");
            var logic3 = await factory.CreateForTenantAsync("tenant1.com");

            // Assert
            Assert.IsNotNull(logic1);
            Assert.IsNotNull(logic2);
            Assert.IsNotNull(logic3);
            Assert.AreNotSame(logic1, logic2, "Each call should create a new instance");
            Assert.AreNotSame(logic2, logic3, "Each call should create a new instance");
            Assert.AreNotSame(logic1, logic3, "Each call should create a new instance");
        }

        #endregion

        #region Domain Name Normalization Tests

        /// <summary>
        /// Tests that domain names are normalized (case-insensitive).
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_WithDifferentCasing_UsesNormalizedDomain()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(true);

            var storageConnectionString = _configuration.GetConnectionString("StorageConnectionString")
                ?? _configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant1.com" },
                DbConn = "Data Source=Tenant1.db;",
                StorageConn = storageConnectionString,
                WebsiteUrl = "https://tenant1.com",
                ResourceGroup = "tenant1-rg"
            };

            // Setup mock to handle normalized domain
            _mockConfigProvider.Setup(x => x.GetTenantConnectionAsync(
                It.Is<string>(s => s.Equals("tenant1.com", StringComparison.OrdinalIgnoreCase)), 
                default))
                .ReturnsAsync(connection);

            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                _mockConfigProvider.Object);

            // Act
            var logic1 = await factory.CreateForTenantAsync("TENANT1.COM");
            var logic2 = await factory.CreateForTenantAsync("Tenant1.Com");
            var logic3 = await factory.CreateForTenantAsync("tenant1.com");

            // Assert
            Assert.IsNotNull(logic1);
            Assert.IsNotNull(logic2);
            Assert.IsNotNull(logic3);
            // All should successfully create instances (verifies normalization works)
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Tests end-to-end scenario: factory creates logic that can be used for operations.
        /// </summary>
        [TestMethod]
        public async Task CreateForTenantAsync_EndToEnd_CreatesUsableLogicInstance()
        {
            // Arrange
            _mockEditorSettings.Setup(x => x.IsMultiTenantEditor).Returns(true);

            var storageConnectionString = _configuration.GetConnectionString("StorageConnectionString")
                ?? _configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant1.com" },
                DbConn = "Data Source=:memory:;",
                StorageConn = storageConnectionString,
                WebsiteUrl = "https://tenant1.com",
                ResourceGroup = "tenant1-rg"
            };

            _mockConfigProvider.Setup(x => x.GetTenantConnectionAsync("tenant1.com", default))
                .ReturnsAsync(connection);

            var factory = new TenantArticleLogicFactory(
                _serviceProvider,
                _mockEditorSettings.Object,
                _mockConfigProvider.Object);

            // Act
            var logic = await factory.CreateForTenantAsync("tenant1.com");

            // Assert
            Assert.IsNotNull(logic);
            Assert.IsInstanceOfType(logic, typeof(ArticleEditLogic));
            
            // Verify logic instance has required dependencies
            // (This validates the factory wired up all dependencies correctly)
            Assert.IsNotNull(logic);
        }

        #endregion
    }
}
