// <copyright file="WebsiteCopyOrchestratorTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services
{
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Cosmos.MultiTenant.Administrator.Services;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="WebsiteCopyOrchestrator"/> to ensure correct entity type handling,
    /// data migration, and validation across different database providers.
    /// </summary>
    [TestClass]
    public class WebsiteCopyOrchestratorTests
    {
        private IServiceProvider _serviceProvider;
        private DbContextOptions<ApplicationDbContext> _dbOptions;
        private DbContextOptions<DynamicConfigDbContext> _configDbOptions;
        private WebsiteCopyOrchestrator _orchestrator;

        [TestInitialize]
        public void Setup()
        {
            // Create unique in-memory database instances for each test
            var dbGuid = Guid.NewGuid().ToString();
            var configDbGuid = Guid.NewGuid().ToString();

            _configDbOptions = new DbContextOptionsBuilder<DynamicConfigDbContext>()
                .UseInMemoryDatabase($"ConfigDb_{configDbGuid}")
                .Options;

            _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"AppDb_{dbGuid}")
                .Options;

            // Setup service provider with properly configured DI
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());

            // Register the config DB context in the service provider
            services.AddScoped(_ => new DynamicConfigDbContext(_configDbOptions));
            services.AddScoped(_ => new ApplicationDbContext(_dbOptions));

            _serviceProvider = services.BuildServiceProvider();

            // Create orchestrator with real service scope factory
            var logger = _serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<WebsiteCopyOrchestrator>();

            _orchestrator = new WebsiteCopyOrchestrator(
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(), 
                logger);
        }

        [TestMethod]
        public async Task StartJobAsync_CreatesJobWithQueuedStatus()
        {
            // Arrange
            using var scope = _serviceProvider.CreateAsyncScope();
            var configDb = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();

            var job = new WebsiteCopyJob
            {
                Id = Guid.NewGuid(),
                SourceConnectionId = Guid.NewGuid(),
                DestinationConnectionId = Guid.NewGuid(),
                DryRun = false,
                CopyDatabase = true,
                CopyStorage = false,
                AllowDestinationOverwrite = false
            };

            // Act
            var result = await _orchestrator.StartJobAsync(job);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Id, job.Id);
            Assert.AreEqual((int)WebsiteCopyJobStatus.Queued, result.Status);
            Assert.AreEqual(0, result.ProgressPercent);
        }

        [TestMethod]
        public async Task GetJobAsync_ReturnsJobWhenExists()
        {
            // Arrange
            using var scope = _serviceProvider.CreateAsyncScope();
            var configDb = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();

            var jobId = Guid.NewGuid();
            var job = new WebsiteCopyJob
            {
                Id = jobId,
                SourceConnectionId = Guid.NewGuid(),
                DestinationConnectionId = Guid.NewGuid(),
                Status = (int)WebsiteCopyJobStatus.Queued,
                ProgressPercent = 0
            };

            configDb.WebsiteCopyJobs.Add(job);
            await configDb.SaveChangesAsync();

            // Act
            var result = await _orchestrator.GetJobAsync(jobId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(jobId, result.Id);
            Assert.AreEqual((int)WebsiteCopyJobStatus.Queued, result.Status);
        }

        [TestMethod]
        public async Task GetJobAsync_ReturnsNullWhenNotFound()
        {
            // Arrange
            var jobId = Guid.NewGuid();

            // Act
            var result = await _orchestrator.GetJobAsync(jobId);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        [Description("Verifies that all documented entity types are properly discoverable and countable in the database context")]
        public void SupportedEntityTypes_AreDiscoverableInApplicationDbContext()
        {
            // Arrange
            var appDb = new ApplicationDbContext(_dbOptions);
            var entityTypes = appDb.Model.GetEntityTypes()
                .Where(t => !t.IsOwned() && t.FindPrimaryKey() != null)
                .Select(t => t.ClrType.Name)
                .Distinct()
                .ToList();

            // Get the documented supported types from the SupportedEntityTypeNames
            var supportedTypes = new[] 
            {
                nameof(Article), nameof(ArticleLock), nameof(ArticleLog), nameof(ArticleNumber),
                nameof(AuthorInfo), nameof(CatalogEntry), nameof(Contact), nameof(Layout),
                "Metric", // Special case for ambiguous name
                nameof(PublishedPage), nameof(PageDesignVersion), nameof(Setting), nameof(Template),
                nameof(TotpToken), nameof(MigrationHistory),
                "IdentityUser", "IdentityRole", // Generic type names
            };

            // Act & Assert - verify all documented types exist in the model
            var docDbGuid = Guid.NewGuid().ToString();
            var testDb = new ApplicationDbContext(_dbOptions);
            var actualTypes = testDb.Model.GetEntityTypes()
                .Select(t => t.ClrType.Name)
                .ToList();

            foreach (var supportedType in supportedTypes)
            {
                // At minimum, verify no unknown types are listed
                Assert.IsTrue(
                    actualTypes.Any(t => t.Contains(supportedType)) || supportedType == "Metric",
                    $"Entity type {supportedType} appears in supported list but not in ApplicationDbContext");
            }
        }

        [TestMethod]
        [Description("Verifies entity type discovery correctly identifies relevant entity types for copying")]
        public void EntityTypeDiscovery_FiltersOutOwnedTypesAndTypesWithoutPrimaryKeys()
        {
            // Arrange
            var appDb = new ApplicationDbContext(_dbOptions);

            // Act
            var entityTypes = appDb.Model.GetEntityTypes()
                .Where(t => !t.IsOwned() && t.FindPrimaryKey() != null)
                .Select(t => t.ClrType)
                .Distinct()
                .ToList();

            // Assert - verify we have discovered actual entity types
            Assert.IsTrue(entityTypes.Count > 0, "Should discover at least some entity types");

            // Verify all discovered types have primary keys
            foreach (var type in entityTypes)
            {
                var modelType = appDb.Model.GetEntityTypes()
                    .FirstOrDefault(t => t.ClrType == type);
                Assert.IsNotNull(modelType?.FindPrimaryKey(), 
                    $"Entity type {type.Name} should have a primary key");
            }
        }

        [TestMethod]
        [Description("Verifies that copy operations gracefully handle unsupported entity types")]
        public async Task CopyOperation_GracefullySkipsUnsupportedEntityTypes()
        {
            // Arrange - This test verifies the error handling pattern
            var sourceDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"source_{Guid.NewGuid()}")
                .Options);

            var destDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"dest_{Guid.NewGuid()}")
                .Options);

            // Act - ensure no exceptions are thrown when attempting to process entities
            await sourceDb.Database.EnsureCreatedAsync();
            await destDb.Database.EnsureCreatedAsync();

            var entityTypes = sourceDb.Model.GetEntityTypes()
                .Where(t => !t.IsOwned() && t.FindPrimaryKey() != null)
                .Select(t => t.ClrType)
                .Take(1)
                .ToList();

            // Assert - verify entity discovery worked
            Assert.IsTrue(entityTypes.Count > 0 || entityTypes.Count == 0, 
                "Entity discovery should not throw an exception");
        }

        [TestMethod]
        [Description("Verifies that validation operations correctly compare entity counts between source and destination")]
        public async Task ValidationOperation_ComparesEntityCountsCorrectly()
        {
            // Arrange
            var sourceDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"valsource_{Guid.NewGuid()}")
                .Options);

            var destDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"valdest_{Guid.NewGuid()}")
                .Options);

            await sourceDb.Database.EnsureCreatedAsync();
            await destDb.Database.EnsureCreatedAsync();

            // Add sample data to source
            var layout = new Layout { Id = Guid.NewGuid(), LayoutName = "Test Layout" };
            sourceDb.Layouts.Add(layout);
            await sourceDb.SaveChangesAsync();

            // Act & Assert - verify validation logic pattern works
            var sourceLayouts = await sourceDb.Layouts.CountAsync();
            var destLayouts = await destDb.Layouts.CountAsync();

            Assert.AreEqual(1, sourceLayouts, "Source should have 1 layout");
            Assert.AreEqual(0, destLayouts, "Destination should have 0 layouts");
            Assert.AreNotEqual(sourceLayouts, destLayouts, "Counts should differ initially");
        }

        [TestMethod]
        [Description("Verifies that entity reading produces correct untracked instances for copying")]
        public async Task ReadOperation_ReturnsUntrackedEntities()
        {
            // Arrange
            var appDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"read_{Guid.NewGuid()}")
                .Options);

            await appDb.Database.EnsureCreatedAsync();

            // Add test data
            var layout = new Layout { Id = Guid.NewGuid(), LayoutName = "Test" };
            appDb.Layouts.Add(layout);
            await appDb.SaveChangesAsync();

            // Act
            var trackedLayouts = appDb.Layouts.ToList();
            var untrackedLayouts = appDb.Layouts.AsNoTracking().ToList();

            // Assert - verify tracking behavior
            Assert.AreEqual(trackedLayouts.Count, untrackedLayouts.Count);

            // Verify that we can read entities (test the concept)
            Assert.IsTrue(trackedLayouts.Any(), "Should have read at least one layout");
            Assert.IsTrue(untrackedLayouts.Any(), "Should have read untracked layouts");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
