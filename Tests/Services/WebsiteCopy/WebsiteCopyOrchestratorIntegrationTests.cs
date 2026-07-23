// <copyright file="WebsiteCopyOrchestratorIntegrationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.WebsiteCopy
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Cosmos.MultiTenant.Administrator.Services;
    using Microsoft.Azure.Cosmos;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests for <see cref="WebsiteCopyOrchestrator"/> that exercise real database
    /// and blob-storage connections. All tests are skipped via
    /// <see cref="Assert.Inconclusive"/> when the required user-secret connection strings are absent.
    /// </summary>
    /// <remarks>
    /// Required user secrets (key names under ConnectionStrings):
    /// <list type="bullet">
    ///   <item><description>AdminDbCopySource   — source application database</description></item>
    ///   <item><description>AdminBlobCopySource — source blob storage account</description></item>
    ///   <item><description>AdminDbCopyDestination   — destination application database (must be empty before each test)</description></item>
    ///   <item><description>AdminBlobCopyDestination — destination blob storage account (must be empty before each test)</description></item>
    /// </list>
    ///
    /// One-time seeding: <see cref="ClassInitialize"/> seeds the source database and storage
    /// with representative content the first time tests run. Subsequent runs re-use the seeded data.
    ///
    /// Per-test cleanup: <see cref="TestInitialize"/> drops and recreates the destination database
    /// and deletes all blobs from the destination storage account before each test.
    /// </remarks>
    [TestClass]
    [TestCategory("Integration")]
    [TestCategory("WebsiteCopy")]
    public class WebsiteCopyOrchestratorIntegrationTests
    {
        // ─── Static: loaded once per class ────────────────────────────────────────

        private static string? sourceDbConn;
        private static string? sourceBlobConn;
        private static string? destDbConn;
        private static string? destBlobConn;
        private static bool connectionsAvailable;
        private static TestContext? classContext;

        // ─── Per-test instance state ───────────────────────────────────────────────

        private ServiceProvider serviceProvider = null!;
        private WebsiteCopyOrchestrator orchestrator = null!;
        private Connection sourceConnection = null!;
        private MemoryCache cache = null!;
        private InMemoryDatabaseRoot configDbRoot = null!;

        // ─── One-time class setup ──────────────────────────────────────────────────

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            classContext = context;

            var config = new ConfigurationBuilder()
                .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
                .AddEnvironmentVariables()
                .Build();

            sourceDbConn = config.GetConnectionString("AdminDbCopySource");
            sourceBlobConn = config.GetConnectionString("AdminBlobCopySource");
            destDbConn = config.GetConnectionString("AdminDbCopyDestination");
            destBlobConn = config.GetConnectionString("AdminBlobCopyDestination");

            connectionsAvailable =
                !string.IsNullOrWhiteSpace(sourceDbConn)
                && !string.IsNullOrWhiteSpace(sourceBlobConn)
                && !string.IsNullOrWhiteSpace(destDbConn)
                && !string.IsNullOrWhiteSpace(destBlobConn);

            if (!connectionsAvailable)
            {
                context.WriteLine(
                    "WebsiteCopyOrchestrator integration tests will be skipped. " +
                    "Configure these connection strings in user secrets to enable them:\n" +
                    "  ConnectionStrings:AdminDbCopySource\n" +
                    "  ConnectionStrings:AdminBlobCopySource\n" +
                    "  ConnectionStrings:AdminDbCopyDestination\n" +
                    "  ConnectionStrings:AdminBlobCopyDestination");
                return;
            }

            await SeedSourceAsync();
        }

        /// <summary>
        /// Seeds the source database and storage with representative content.
        /// Idempotent — skips seeding if data already exists.
        /// </summary>
        private static async Task SeedSourceAsync()
        {
            // ── Source database ────────────────────────────────────────────────────
            using var sourceDb = new ApplicationDbContext(sourceDbConn!);
            await sourceDb.Database.EnsureCreatedAsync();

            // Check if seeding is needed by attempting to count layouts
            // Note: Using CosmosAnyAsync() instead of AnyAsync() for Cosmos DB compatibility
            var layoutCount = await sourceDb.Layouts.CountAsync();
            if (layoutCount == 0)
            {
                sourceDb.Layouts.Add(new Layout
                {
                    Id = Guid.NewGuid(),
                    LayoutName = "Integration Test Layout",
                    IsDefault = true,
                    Head = "<meta charset='utf-8'>",
                    HtmlHeader = "<header>Test Header</header>",
                    FooterHtmlContent = "<footer>Test Footer</footer>",
                    Published = DateTimeOffset.UtcNow.AddDays(-1)
                });

                sourceDb.Settings.Add(new Setting
                {
                    Id = Guid.NewGuid(),
                    Group = "IntegrationTest",
                    Name = "SeedSetting",
                    Value = "SeedValue",
                    IsRequired = false,
                    Description = "Seeded for WebsiteCopyOrchestrator integration tests"
                });

                await sourceDb.SaveChangesAsync();
                classContext?.WriteLine("Source database seeded with Layout and Setting.");
            }
            else
            {
                classContext?.WriteLine("Source database already contains data — skipping seed.");
            }

            // ── Source blob storage ────────────────────────────────────────────────
            var memCache = new MemoryCache(new MemoryCacheOptions());
            try
            {
                var sourceStorage = new StorageContext(sourceBlobConn!, memCache);
                var existing = await sourceStorage.GetFilesAsync("/");

                const string seedMarkerFolder = "copy-test-seed";
                if (!existing.Exists(f => f.Contains(seedMarkerFolder, StringComparison.OrdinalIgnoreCase)))
                {
                    var textBytes = Encoding.UTF8.GetBytes("Integration test seed file — do not delete.");
                    await using var stream = new MemoryStream(textBytes);
                    await sourceStorage.AppendBlob(
                        stream,
                        new FileUploadMetaData
                        {
                            UploadUid = Guid.NewGuid().ToString("N"),
                            RelativePath = seedMarkerFolder,
                            FileName = "seed.txt",
                            ContentType = "text/plain",
                            ChunkIndex = 0,
                            TotalChunks = 1,
                            TotalFileSize = textBytes.Length
                        },
                        StorageConstants.UploadModeBlock);

                    classContext?.WriteLine("Source storage seeded with seed.txt.");
                }
                else
                {
                    classContext?.WriteLine("Source storage already seeded — skipping.");
                }
            }
            finally
            {
                memCache.Dispose();
            }
        }

        // ─── Per-test setup / teardown ─────────────────────────────────────────────

        [TestInitialize]
        public async Task TestInitialize()
        {
            if (!connectionsAvailable)
            {
                Assert.Inconclusive(
                    "Integration test skipped: one or more copy connection strings are not configured in user secrets. " +
                    "Required keys: ConnectionStrings:AdminDbCopySource, ConnectionStrings:AdminBlobCopySource, " +
                    "ConnectionStrings:AdminDbCopyDestination, ConnectionStrings:AdminBlobCopyDestination.");
            }

            cache = new MemoryCache(new MemoryCacheOptions());
            configDbRoot = new InMemoryDatabaseRoot();

            // The orchestrator's config database is in-memory; only the ApplicationDbContext
            // and StorageContext (which the orchestrator constructs directly from connection strings)
            // need real connections.
            var services = new ServiceCollection();
            var configDbName = $"WebsiteCopyOrchestratorIntegration_{Guid.NewGuid()}";
            services.AddDbContext<DynamicConfigDbContext>(opt =>
                opt.UseInMemoryDatabase(configDbName, configDbRoot));
            services.AddLogging();
            serviceProvider = services.BuildServiceProvider();

            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            orchestrator = new WebsiteCopyOrchestrator(
                scopeFactory,
                NullLogger<WebsiteCopyOrchestrator>.Instance);

            // Register the source connection (pointing at real infrastructure) in the in-memory config DB
            sourceConnection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "integration-test.local" },
                DbConn = sourceDbConn!,
                StorageConn = sourceBlobConn!,
                WebsiteUrl = "https://integration-test.local",
                ResourceGroup = "integration-rg"
            };
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            db.Connections.Add(sourceConnection);
            await db.SaveChangesAsync();

            // Guarantee destination starts empty for every test
            await CleanDestinationDbAsync();
            await CleanDestinationStorageAsync();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            serviceProvider?.Dispose();
            cache?.Dispose();
        }

        // ─── Destination cleanup helpers ───────────────────────────────────────────

        private static async Task CleanDestinationDbAsync()
        {
            using var destDb = new ApplicationDbContext(destDbConn!);
            await destDb.Database.EnsureDeletedAsync();
            // Schema will be recreated on demand by CopyDatabaseAsync → EnsureCreatedAsync
        }

        private async Task CleanDestinationStorageAsync()
        {
            try
            {
                var destStorage = new StorageContext(destBlobConn!, cache);
                var files = await destStorage.GetFilesAsync("/");
                foreach (var file in files)
                {
                    await destStorage.DeleteFileAsync(file);
                }
            }
            catch (Exception ex)
            {
                // Destination storage may already be empty or the container may not exist yet
                classContext?.WriteLine($"Note: destination storage cleanup skipped — {ex.Message}");
            }
        }

        private static async Task AddPreExistingDestinationLayoutAsync(string destinationConnectionString)
        {
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    using var destDb = new ApplicationDbContext(destinationConnectionString);
                    await destDb.Database.EnsureCreatedAsync();
                    destDb.Layouts.Add(new Layout
                    {
                        Id = Guid.NewGuid(),
                        LayoutName = "Pre-existing Layout",
                        IsDefault = false,
                        Head = string.Empty,
                        HtmlHeader = string.Empty,
                        FooterHtmlContent = string.Empty
                    });

                    await destDb.SaveChangesAsync();
                    return;
                }
                catch (DbUpdateException ex) when (attempt < 5 && ex.InnerException is CosmosException cosmosEx && cosmosEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }
        }

        // ─── Polling helper ────────────────────────────────────────────────────────

        private async Task<WebsiteCopyJob?> WaitForTerminalStatusAsync(
            Guid jobId,
            int timeoutSeconds = 120)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTimeOffset.UtcNow < deadline)
            {
                var job = await orchestrator.GetJobAsync(jobId);
                if (job?.Status is WebsiteCopyJobStatus.Completed
                    or WebsiteCopyJobStatus.CompletedDryRun
                    or WebsiteCopyJobStatus.Failed)
                {
                    return job;
                }

                await Task.Delay(500);
            }

            return await orchestrator.GetJobAsync(jobId);
        }

        // ─── Integration tests ─────────────────────────────────────────────────────

        /// <summary>
        /// Full copy: database and storage are both copied and validated.
        /// </summary>
        [TestMethod]
        public async Task ProcessJob_FullDatabaseAndStorageCopy_Completes()
        {
            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = sourceConnection.Id,
                DestinationDbConn = destDbConn,
                DestinationStorageConn = destBlobConn,
                CopyDatabase = true,
                CopyStorage = true
            });

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual(
                WebsiteCopyJobStatus.Completed,
                completed.Status,
                $"Expected Completed but got {completed.Status}. ErrorMessage: {completed.ErrorMessage}");
            Assert.IsTrue(completed.DatabaseCopied, "DatabaseCopied should be true after a full copy.");
            Assert.IsTrue(completed.StorageCopied, "StorageCopied should be true after a full copy.");
            Assert.IsTrue(completed.ValidationCompleted, "ValidationCompleted should be true after validation passes.");
            Assert.IsNotNull(completed.CompletedUtc, "CompletedUtc should be set on completion.");
            Assert.IsFalse(completed.Locked, "Locked flag should be cleared on completion.");
            Assert.AreEqual(1, completed.AttemptCount);
        }

        /// <summary>
        /// Database-only copy: storage is not copied, database is copied and validated.
        /// </summary>
        [TestMethod]
        public async Task ProcessJob_DatabaseOnlyCopy_Completes()
        {
            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = sourceConnection.Id,
                DestinationDbConn = destDbConn,
                DestinationStorageConn = destBlobConn,
                CopyDatabase = true,
                CopyStorage = false
            });

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual(
                WebsiteCopyJobStatus.Completed,
                completed.Status,
                $"Expected Completed but got {completed.Status}. ErrorMessage: {completed.ErrorMessage}");
            Assert.IsTrue(completed.DatabaseCopied);
            Assert.IsFalse(completed.StorageCopied, "StorageCopied should remain false when CopyStorage=false.");
            Assert.IsTrue(completed.ValidationCompleted);
        }

        /// <summary>
        /// Storage-only copy: only storage should be copied.
        ///
        /// NOTE: This test currently exposes a known bug — CopyDatabaseAsync is called
        /// unconditionally in ProcessJobAsync regardless of the CopyDatabase flag (the
        /// block around line 312 of WebsiteCopyOrchestrator.cs lacks an "if (job.CopyDatabase)"
        /// guard). The assertion on DatabaseCopied will fail until that bug is fixed.
        /// </summary>
        [TestMethod]
        public async Task ProcessJob_StorageOnlyCopy_CopiesStorage()
        {
            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = sourceConnection.Id,
                DestinationDbConn = destDbConn,
                DestinationStorageConn = destBlobConn,
                CopyDatabase = false,
                CopyStorage = true
            });

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual(
                WebsiteCopyJobStatus.Completed,
                completed.Status,
                $"Expected Completed but got {completed.Status}. ErrorMessage: {completed.ErrorMessage}");
            Assert.IsTrue(completed.StorageCopied, "StorageCopied should be true.");

            // Bug: DatabaseCopied is currently set to true even when CopyDatabase=false because
            // CopyDatabaseAsync is called unconditionally. Fix ProcessJobAsync to wrap the
            // database-copy block in "if (job.CopyDatabase)" and this assertion will pass.
            Assert.IsFalse(
                completed.DatabaseCopied,
                "DatabaseCopied should be false when CopyDatabase=false. " +
                "If this assertion fails, the unconditional CopyDatabaseAsync call in " +
                "ProcessJobAsync needs an 'if (job.CopyDatabase)' guard.");
        }

        /// <summary>
        /// Verifies that a job fails with a clear error when the destination database already
        /// contains data (strict empty-destination enforcement).
        /// </summary>
        [TestMethod]
        public async Task ProcessJob_FailsJob_WhenDestinationDatabaseNotEmpty()
        {
            // Pre-populate destination to trip the empty check
            await AddPreExistingDestinationLayoutAsync(destDbConn!);

            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = sourceConnection.Id,
                DestinationDbConn = destDbConn,
                DestinationStorageConn = destBlobConn,
                CopyDatabase = true,
                CopyStorage = false
            });

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual(WebsiteCopyJobStatus.Failed, completed.Status);
            Assert.IsTrue(
                completed.ErrorMessage?.Contains("must be empty", StringComparison.OrdinalIgnoreCase) == true
                || completed.ErrorMessage?.Contains("record(s)", StringComparison.OrdinalIgnoreCase) == true,
                $"Expected an 'empty' or 'record(s)' error message but got: {completed.ErrorMessage}");
        }

        /// <summary>
        /// Verifies that a job fails with a clear error when the destination storage already
        /// contains files (strict empty-destination enforcement).
        /// </summary>
        [TestMethod]
        public async Task ProcessJob_FailsJob_WhenDestinationStorageNotEmpty()
        {
            // Pre-upload a file to the destination storage to trip the empty check
            var destStorage = new StorageContext(destBlobConn!, cache);
            var preBytes = Encoding.UTF8.GetBytes("pre-existing content");
            await using var preStream = new MemoryStream(preBytes);
            await destStorage.AppendBlob(
                preStream,
                new FileUploadMetaData
                {
                    UploadUid = Guid.NewGuid().ToString("N"),
                    RelativePath = "pre-existing.txt",
                    FileName = "pre-existing.txt",
                    ContentType = "text/plain",
                    ChunkIndex = 0,
                    TotalChunks = 1,
                    TotalFileSize = preBytes.Length
                },
                StorageConstants.UploadModeBlock);

            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = sourceConnection.Id,
                DestinationDbConn = destDbConn,
                DestinationStorageConn = destBlobConn,
                CopyDatabase = false,
                CopyStorage = true
            });

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual(WebsiteCopyJobStatus.Failed, completed.Status);
            Assert.IsTrue(
                completed.ErrorMessage?.Contains("empty", StringComparison.OrdinalIgnoreCase) == true,
                $"Expected 'empty' in error message but got: {completed.ErrorMessage}");
        }

        /// <summary>
        /// End-to-end: copy completes, then ApplyConnectionSwitchAsync points the source
        /// connection at the destination infrastructure.
        /// </summary>
        [TestMethod]
        public async Task ProcessJob_FullCopy_ThenConnectionSwitch_UpdatesSourceConnection()
        {
            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = sourceConnection.Id,
                DestinationDbConn = destDbConn,
                DestinationStorageConn = destBlobConn,
                CopyDatabase = true,
                CopyStorage = true
            });

            var completed = await WaitForTerminalStatusAsync(job.Id);
            Assert.IsNotNull(completed);
            Assert.AreEqual(
                WebsiteCopyJobStatus.Completed,
                completed.Status,
                $"Copy must succeed before testing the switch. ErrorMessage: {completed.ErrorMessage}");

            var switched = await orchestrator.ApplyConnectionSwitchAsync(completed.Id);

            Assert.IsTrue(switched, "ApplyConnectionSwitchAsync should return true after a Completed job.");

            using var verifyScope = serviceProvider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var updatedSource = await verifyDb.Connections.FirstOrDefaultAsync(x => x.Id == sourceConnection.Id);

            Assert.IsNotNull(updatedSource);
            Assert.AreEqual(destDbConn, updatedSource.DbConn, "DbConn should point to the destination after switch.");
            Assert.AreEqual(destBlobConn, updatedSource.StorageConn, "StorageConn should point to the destination after switch.");
        }
    }
}
