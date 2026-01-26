// <copyright file="CloudFrontCdnServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.CDN
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common.Models;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Integration tests for CloudFront CDN using an actual live service.
    /// </summary>
    /// <remarks>
    /// These tests require actual AWS CloudFront credentials configured in user secrets.
    /// The deployment script automatically configures these secrets.
    /// 
    /// To run these tests:
    /// 1. Deploy CloudFront infrastructure: CloudFrontTestSetup\deploy.ps1
    /// 2. Remove the [Ignore] attribute from the test class
    /// 3. Run: dotnet test --filter "TestCategory=CloudFront"
    /// </remarks>
    [Ignore("Requires live AWS CloudFront credentials - run CloudFrontTestSetup\\deploy.ps1 and remove this attribute")]
    [TestClass]
    [DoNotParallelize]
    public class CloudFrontCdnLiveServiceTests
    {
        private ApplicationDbContext dbContext;
        private DefaultHttpContext httpContext;
        private IConfiguration configuration;
        private CloudFrontCdnConfig cloudFrontConfig;

        [TestInitialize]
        public void Setup()
        {
            // Load configuration from user secrets (set by deploy.ps1)
            configuration = new ConfigurationBuilder()
                .AddUserSecrets<CloudFrontCdnLiveServiceTests>()
                .AddEnvironmentVariables()
                .Build();

            // Get CloudFront configuration
            var distributionId = configuration["AWS:CloudFront:DistributionId"];
            var accessKeyId = configuration["AWS:CloudFront:AccessKeyId"];
            var secretAccessKey = configuration["AWS:CloudFront:SecretAccessKey"];
            var region = configuration["AWS:CloudFront:Region"] ?? "us-east-1";

            // Validate configuration
            if (string.IsNullOrEmpty(distributionId))
            {
                Assert.Inconclusive("AWS CloudFront DistributionId not configured in user secrets. Run deploy.ps1 first.");
            }

            if (string.IsNullOrEmpty(accessKeyId))
            {
                Assert.Inconclusive("AWS AccessKeyId not configured in user secrets. Run deploy.ps1 first.");
            }

            if (string.IsNullOrEmpty(secretAccessKey))
            {
                Assert.Inconclusive("AWS SecretAccessKey not configured in user secrets. Run deploy.ps1 first.");
            }

            cloudFrontConfig = new CloudFrontCdnConfig
            {
                DistributionId = distributionId,
                AccessKeyId = accessKeyId,
                SecretAccessKey = secretAccessKey,
                Region = region
            };

            // Create in-memory database for SkyCMS CDN service testing
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"CloudFrontTest_{Guid.NewGuid()}")
                .Options;

            dbContext = new ApplicationDbContext(options);

            // Configure SkyCMS CDN settings using the Settings table
            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(cloudFrontConfig)
            };

            dbContext.Settings.Add(new Setting
            {
                Group = CdnService.CDNGROUPNAME,
                Name = "CloudFront",
                Value = JsonConvert.SerializeObject(cdnSetting)
            });

            dbContext.SaveChanges();

            // Setup HTTP context
            httpContext = new DefaultHttpContext();
        }

        [TestCleanup]
        public void Cleanup()
        {
            dbContext?.Dispose();
        }

        #region SkyCMS CloudFront Driver Tests

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("CloudFront")]
        public void CloudFrontDriver_IsConfigured_ReturnsTrue()
        {
            // Arrange
            var logger = new NullLogger<CdnService>();
            var service = CdnService.GetCdnService(dbContext, logger, httpContext);

            // Act
            var isConfigured = service.IsConfigured();
            var isCloudFrontConfigured = service.IsConfigured(CdnProviderEnum.CloudFront);

            // Assert
            Assert.IsTrue(isConfigured, "CDN service should be configured");
            Assert.IsTrue(isCloudFrontConfigured, "CloudFront should be configured");
            Assert.AreEqual("Sky CMD CDN", service.ProviderName);
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("CloudFront")]
        public async Task CloudFrontDriver_PurgeSinglePath_Succeeds()
        {
            // Arrange
            var logger = new NullLogger<CdnService>();
            var service = CdnService.GetCdnService(dbContext, logger, httpContext);

            var testPath = $"/test/skycms-purge-{Guid.NewGuid()}.html";
            var paths = new List<string> { testPath };

            // Act
            var results = await service.PurgeCdn(paths);

            // Assert
            Assert.IsNotNull(results, "Results should not be null");
            Assert.AreEqual(1, results.Count, "Should have one result");
            Assert.IsTrue(results[0].IsSuccessStatusCode, $"Purge should succeed: {results[0].Message}");
            Assert.AreEqual("CloudFront", results[0].ProviderName);
            Assert.IsFalse(string.IsNullOrEmpty(results[0].Id), "Should have an invalidation ID");

            TestContext.WriteLine($"Purged: {testPath}");
            TestContext.WriteLine($"Invalidation ID: {results[0].Id}");
            TestContext.WriteLine($"Status: {results[0].Message}");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("CloudFront")]
        public async Task CloudFrontDriver_PurgeMultiplePaths_Succeeds()
        {
            // Arrange
            var logger = new NullLogger<CdnService>();
            var service = CdnService.GetCdnService(dbContext, logger, httpContext);

            var testId = Guid.NewGuid();
            var paths = new List<string>
            {
                $"/test/skycms-multi-1-{testId}.html",
                $"/test/skycms-multi-2-{testId}.css",
                $"/test/skycms-multi-3-{testId}.js"
            };

            // Act
            var results = await service.PurgeCdn(paths);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count, "CloudFront batches all paths into one invalidation");
            Assert.IsTrue(results[0].IsSuccessStatusCode, $"Purge should succeed: {results[0].Message}");
            Assert.IsFalse(string.IsNullOrEmpty(results[0].Id), "Should have an invalidation ID");

            TestContext.WriteLine($"Purged {paths.Count} paths in one invalidation");
            TestContext.WriteLine($"Invalidation ID: {results[0].Id}");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("CloudFront")]
        public async Task CloudFrontDriver_PurgeWithWildcard_Succeeds()
        {
            // Arrange
            var logger = new NullLogger<CdnService>();
            var service = CdnService.GetCdnService(dbContext, logger, httpContext);

            var paths = new List<string>
            {
                "/test-spa/*",
                "/test-spa/index.html"
            };

            // Act
            var results = await service.PurgeCdn(paths);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].IsSuccessStatusCode, $"Wildcard purge should succeed: {results[0].Message}");
            Assert.IsFalse(string.IsNullOrEmpty(results[0].Id), "Should have an invalidation ID");

            TestContext.WriteLine("Wildcard purge successful");
            TestContext.WriteLine($"Invalidation ID: {results[0].Id}");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("CloudFront")]
        public async Task CloudFrontDriver_PurgeAll_Succeeds()
        {
            // Arrange
            var logger = new NullLogger<CdnService>();
            var service = CdnService.GetCdnService(dbContext, logger, httpContext);

            // Act - Calling with no paths should purge everything
            var results = await service.PurgeCdn();

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].IsSuccessStatusCode, $"Purge all should succeed: {results[0].Message}");
            Assert.IsFalse(string.IsNullOrEmpty(results[0].Id), "Should have an invalidation ID");

            TestContext.WriteLine("Purge all successful (/*) ");
            TestContext.WriteLine($"Invalidation ID: {results[0].Id}");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("CloudFront")]
        public async Task CloudFrontDriver_SimulateSpaDeployment_Succeeds()
        {
            // Arrange
            var logger = new NullLogger<CdnService>();
            var service = CdnService.GetCdnService(dbContext, logger, httpContext);

            // Simulate a real SPA deployment purge
            var spaPath = "my-app";
            var paths = new List<string>
            {
                $"/{spaPath}/*",  // Purge all SPA assets
                $"/{spaPath}"     // Purge the root
            };

            // Act
            var results = await service.PurgeCdn(paths);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.All(r => r.IsSuccessStatusCode), "All purges should succeed");
            Assert.IsTrue(results.All(r => !string.IsNullOrEmpty(r.Id)), "All should have invalidation IDs");

            TestContext.WriteLine($"SPA deployment purge completed for: {spaPath}");
            foreach (var result in results)
            {
                TestContext.WriteLine($"  Invalidation ID: {result.Id} - {result.Message}");
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("CloudFront")]
        public async Task CloudFrontDriver_EstimatedFlushTime_IsReasonable()
        {
            // Arrange
            var logger = new NullLogger<CdnService>();
            var service = CdnService.GetCdnService(dbContext, logger, httpContext);
            var paths = new List<string> { $"/test/timing-{Guid.NewGuid()}.html" };

            // Act
            var beforePurge = DateTimeOffset.UtcNow;
            var results = await service.PurgeCdn(paths);
            var afterPurge = DateTimeOffset.UtcNow;

            // Assert
            Assert.IsTrue(results[0].IsSuccessStatusCode);
            Assert.IsTrue(results[0].EstimatedFlushDateTime > beforePurge, "Flush time should be in the future");
            Assert.IsTrue(results[0].EstimatedFlushDateTime <= afterPurge.AddMinutes(10), "Flush time should be within 10 minutes");

            var estimatedWait = results[0].EstimatedFlushDateTime - afterPurge;
            TestContext.WriteLine($"Estimated flush time: {estimatedWait.TotalMinutes:F1} minutes from now");
        }

        #endregion

        public TestContext TestContext { get; set; }
    }
}