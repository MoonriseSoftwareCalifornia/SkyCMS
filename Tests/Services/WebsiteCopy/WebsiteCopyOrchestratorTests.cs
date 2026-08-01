// <copyright file="WebsiteCopyOrchestratorTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.WebsiteCopy
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Cosmos.MultiTenant.Administrator.Services;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="WebsiteCopyOrchestrator"/> using an in-memory
    /// <see cref="DynamicConfigDbContext"/>. No real database or storage connections are required.
    /// </summary>
    [TestClass]
    [TestCategory("WebsiteCopy")]
    public class WebsiteCopyOrchestratorTests
    {
        private readonly List<string> tempDatabasePaths = new();
        private ServiceProvider serviceProvider = null!;
        private WebsiteCopyOrchestrator orchestrator = null!;
        private Guid sourceConnectionId;

        [TestInitialize]
        public void Initialize()
        {
            // The database name must be captured before the lambda so that every DynamicConfigDbContext
            // instance resolved from DI scopes shares the same in-memory database.
            var dbName = $"WebsiteCopyOrchestratorTests_{Guid.NewGuid()}";
            var services = new ServiceCollection();
            services.AddDbContext<DynamicConfigDbContext>(opt =>
                opt.UseInMemoryDatabase(dbName));
            services.AddLogging();
            serviceProvider = services.BuildServiceProvider();

            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            orchestrator = new WebsiteCopyOrchestrator(
                scopeFactory,
                NullLogger<WebsiteCopyOrchestrator>.Instance);

            sourceConnectionId = Guid.NewGuid();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            db.Connections.Add(BuildSourceConnection(sourceConnectionId));
            db.SaveChanges();
        }

        [TestCleanup]
        public void Cleanup()
        {
            serviceProvider?.Dispose();

            foreach (var path in tempDatabasePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    // Best-effort temp file cleanup for test databases.
                }
            }
        }

        // ─── Helpers ───────────────────────────────────────────────────────────────

        private static Connection BuildSourceConnection(Guid id) => new()
        {
            Id = id,
            DomainNames = new[] { "unit-test.local" },
            DbConn = "Data Source=unit-source.db",
            StorageConn = "DefaultEndpointsProtocol=https;AccountName=src;AccountKey=AAAA==",
            WebsiteUrl = "https://unit-test.local",
            ResourceGroup = "unit-rg"
        };

        private static Connection BuildConnection(Guid id, string dbConn, string websiteUrl) => new()
        {
            Id = id,
            DomainNames = new[] { websiteUrl.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase) },
            DbConn = dbConn,
            StorageConn = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=AAAA==",
            WebsiteUrl = websiteUrl,
            ResourceGroup = "unit-rg"
        };

        private string CreateTempSqliteConnectionString(string name)
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.db");
            tempDatabasePaths.Add(filePath);
            return $"Data Source={filePath}";
        }

        private static async Task SeedIdentityUserAsync(string connectionString, string email)
        {
            using var db = new ApplicationDbContext(connectionString);
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new IdentityUser
            {
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true
            });
            await db.SaveChangesAsync();
        }

        private static async Task<int> CountUsersAsync(string connectionString)
        {
            using var db = new ApplicationDbContext(connectionString);
            await db.Database.EnsureCreatedAsync();
            return await db.Users.CountAsync();
        }

        /// <summary>
        /// Builds a dry-run job that requires no real connections.
        /// Both CopyDatabase and CopyStorage are false so that EnsureDestinationIsEmptyAsync
        /// is a no-op, and DryRun=true exits before any actual copy is attempted.
        /// </summary>
        private WebsiteCopyJob BuildDryRunJob(Guid? overrideSourceId = null) => new()
        {
            SourceConnectionId = overrideSourceId ?? sourceConnectionId,
            DestinationDbConn = "fake-dest-db",  // at least one non-empty string required for destination resolution
            CopyDatabase = false,
            CopyStorage = false,
            DryRun = true
        };

        /// <summary>
        /// Directly inserts a job in a given status into the config database,
        /// bypassing StartJobAsync so tests can pre-condition state.
        /// </summary>
        private async Task<WebsiteCopyJob> SeedJobAsync(
            WebsiteCopyJobStatus status,
            string? errorMessage = null,
            Guid? sourceId = null)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var job = new WebsiteCopyJob
            {
                Id = Guid.NewGuid(),
                SourceConnectionId = sourceId ?? sourceConnectionId,
                DestinationDbConn = "fake-dest",
                Status = (int)status,
                ErrorMessage = errorMessage,
                CopyDatabase = false,
                CopyStorage = false
            };
            db.WebsiteCopyJobs.Add(job);
            await db.SaveChangesAsync();
            return job;
        }

        /// <summary>
        /// Polls GetJobAsync until the job reaches a terminal state or the timeout elapses.
        /// </summary>
        private async Task<WebsiteCopyJob?> WaitForTerminalStatusAsync(
            Guid jobId,
            int timeoutSeconds = 5)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTimeOffset.UtcNow < deadline)
            {
                var job = await orchestrator.GetJobAsync(jobId);
                if (job?.Status is (int)WebsiteCopyJobStatus.Completed
                    or (int)WebsiteCopyJobStatus.CompletedDryRun
                    or (int)WebsiteCopyJobStatus.Failed
                    or (int)WebsiteCopyJobStatus.Cancelled)
                {
                    return job;
                }

                await Task.Delay(100);
            }

            return await orchestrator.GetJobAsync(jobId);
        }

        // ─── StartJobAsync ─────────────────────────────────────────────────────────

        [TestMethod]
        public async Task StartJobAsync_ReturnsJobWithQueuedStatus()
        {
            var job = await orchestrator.StartJobAsync(BuildDryRunJob());

            Assert.AreEqual((int)WebsiteCopyJobStatus.Queued, job.Status);
        }

        [TestMethod]
        public async Task StartJobAsync_SetsProgressToZero()
        {
            var job = await orchestrator.StartJobAsync(BuildDryRunJob());

            Assert.AreEqual(0, job.ProgressPercent);
        }

        [TestMethod]
        public async Task StartJobAsync_SetsCreatedUtcToApproximatelyNow()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-1);

            var job = await orchestrator.StartJobAsync(BuildDryRunJob());

            Assert.IsTrue(job.CreatedUtc >= before, "CreatedUtc should not be in the past.");
            Assert.IsTrue(job.CreatedUtc <= DateTimeOffset.UtcNow.AddSeconds(2), "CreatedUtc should not be in the future.");
        }

        [TestMethod]
        public async Task StartJobAsync_PersistsJobToDatabase()
        {
            var started = await orchestrator.StartJobAsync(BuildDryRunJob());

            var retrieved = await orchestrator.GetJobAsync(started.Id);

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(started.Id, retrieved.Id);
        }

        [TestMethod]
        public async Task StartJobAsync_SetsLastMessageToQueued()
        {
            var job = await orchestrator.StartJobAsync(BuildDryRunJob());

            Assert.AreEqual("Queued", job.LastMessage);
        }

        // ─── GetJobAsync ───────────────────────────────────────────────────────────

        [TestMethod]
        public async Task GetJobAsync_ReturnsNull_WhenJobNotFound()
        {
            var result = await orchestrator.GetJobAsync(Guid.NewGuid());

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetJobAsync_ReturnsJob_WhenJobExists()
        {
            var started = await orchestrator.StartJobAsync(BuildDryRunJob());

            var retrieved = await orchestrator.GetJobAsync(started.Id);

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(started.Id, retrieved.Id);
            Assert.AreEqual(sourceConnectionId, retrieved.SourceConnectionId);
        }

        // ─── RetryJobAsync ─────────────────────────────────────────────────────────

        [TestMethod]
        public async Task RetryJobAsync_ReturnsFalse_WhenJobNotFound()
        {
            var result = await orchestrator.RetryJobAsync(Guid.NewGuid());

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task RetryJobAsync_ReturnsFalse_WhenJobIsRunning()
        {
            var seeded = await SeedJobAsync(WebsiteCopyJobStatus.Running);

            var result = await orchestrator.RetryJobAsync(seeded.Id);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task RetryJobAsync_ReturnsTrue_WhenJobIsFailed()
        {
            var seeded = await SeedJobAsync(WebsiteCopyJobStatus.Failed, "previous error");

            var result = await orchestrator.RetryJobAsync(seeded.Id);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task RetryJobAsync_ReturnsTrue_WhenJobIsCompleted()
        {
            var seeded = await SeedJobAsync(WebsiteCopyJobStatus.Completed);

            var result = await orchestrator.RetryJobAsync(seeded.Id);

            Assert.IsTrue(result, "Completed jobs should be re-tryable.");
        }

        [TestMethod]
        public async Task RetryJobAsync_ResetsStatusToQueued_WhenJobIsFailed()
        {
            var seeded = await SeedJobAsync(WebsiteCopyJobStatus.Failed, "previous error");

            await orchestrator.RetryJobAsync(seeded.Id);
            var updated = await orchestrator.GetJobAsync(seeded.Id);

            Assert.IsNotNull(updated);
            Assert.AreEqual((int)WebsiteCopyJobStatus.Queued, updated.Status);
        }

        [TestMethod]
        public async Task RetryJobAsync_ClearsErrorMessage_WhenRetrying()
        {
            var seeded = await SeedJobAsync(WebsiteCopyJobStatus.Failed, "previous error");

            await orchestrator.RetryJobAsync(seeded.Id);
            var updated = await orchestrator.GetJobAsync(seeded.Id);

            Assert.IsNotNull(updated);
            Assert.IsNull(updated.ErrorMessage);
        }

        [TestMethod]
        public async Task RetryJobAsync_SetsLastMessageToRetryQueued()
        {
            var seeded = await SeedJobAsync(WebsiteCopyJobStatus.Failed, "previous error");

            await orchestrator.RetryJobAsync(seeded.Id);
            var updated = await orchestrator.GetJobAsync(seeded.Id);

            Assert.IsNotNull(updated);
            Assert.AreEqual("Retry queued", updated.LastMessage);
        }

        // ─── ApplyConnectionSwitchAsync ────────────────────────────────────────────

        [TestMethod]
        public async Task ApplyConnectionSwitchAsync_ReturnsFalse_WhenJobNotFound()
        {
            var result = await orchestrator.ApplyConnectionSwitchAsync(Guid.NewGuid());

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ApplyConnectionSwitchAsync_ReturnsFalse_WhenJobIsQueued()
        {
            var seeded = await SeedJobAsync(WebsiteCopyJobStatus.Queued);

            var result = await orchestrator.ApplyConnectionSwitchAsync(seeded.Id);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ApplyConnectionSwitchAsync_ReturnsFalse_WhenJobIsFailed()
        {
            var seeded = await SeedJobAsync(WebsiteCopyJobStatus.Failed);

            var result = await orchestrator.ApplyConnectionSwitchAsync(seeded.Id);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ApplyConnectionSwitchAsync_ReturnsFalse_WhenSourceConnectionNotFound()
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();

            // Job referencing a source connection that does not exist in the config DB
            var orphanJob = new WebsiteCopyJob
            {
                Id = Guid.NewGuid(),
                SourceConnectionId = Guid.NewGuid(),
                Status = (int)WebsiteCopyJobStatus.Completed,
                CopyDatabase = true,
                DestinationDbConn = "new-db"
            };
            db.WebsiteCopyJobs.Add(orphanJob);
            await db.SaveChangesAsync();

            var result = await orchestrator.ApplyConnectionSwitchAsync(orphanJob.Id);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ApplyConnectionSwitchAsync_UpdatesDbConn_WhenCopyDatabaseTrue()
        {
            const string newDbConn = "Server=newdb;Database=dest;";

            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
                db.WebsiteCopyJobs.Add(new WebsiteCopyJob
                {
                    Id = Guid.NewGuid(),
                    SourceConnectionId = sourceConnectionId,
                    Status = (int)WebsiteCopyJobStatus.Completed,
                    CopyDatabase = true,
                    CopyStorage = false,
                    DestinationDbConn = newDbConn
                });
                await db.SaveChangesAsync();
            }

            // Fetch the job ID we just created
            using var readScope = serviceProvider.CreateScope();
            var readDb = readScope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var job = await readDb.WebsiteCopyJobs.FirstAsync(x => x.DestinationDbConn == newDbConn);

            var result = await orchestrator.ApplyConnectionSwitchAsync(job.Id);

            Assert.IsTrue(result);
            using var verifyScope = serviceProvider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var updatedSource = await verifyDb.Connections.FirstOrDefaultAsync(x => x.Id == sourceConnectionId);
            Assert.IsNotNull(updatedSource);
            Assert.AreEqual(newDbConn, updatedSource.DbConn);
        }

        [TestMethod]
        public async Task ApplyConnectionSwitchAsync_UpdatesStorageConn_WhenCopyStorageTrue()
        {
            const string newStorageConn = "DefaultEndpointsProtocol=https;AccountName=newdest;AccountKey=BBBB==";

            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
                db.WebsiteCopyJobs.Add(new WebsiteCopyJob
                {
                    Id = Guid.NewGuid(),
                    SourceConnectionId = sourceConnectionId,
                    Status = (int)WebsiteCopyJobStatus.Completed,
                    CopyDatabase = false,
                    CopyStorage = true,
                    DestinationStorageConn = newStorageConn
                });
                await db.SaveChangesAsync();
            }

            using var readScope = serviceProvider.CreateScope();
            var readDb = readScope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var job = await readDb.WebsiteCopyJobs.FirstAsync(x => x.DestinationStorageConn == newStorageConn);

            var result = await orchestrator.ApplyConnectionSwitchAsync(job.Id);

            Assert.IsTrue(result);
            using var verifyScope = serviceProvider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var updatedSource = await verifyDb.Connections.FirstOrDefaultAsync(x => x.Id == sourceConnectionId);
            Assert.IsNotNull(updatedSource);
            Assert.AreEqual(newStorageConn, updatedSource.StorageConn);
        }

        [TestMethod]
        public async Task ApplyConnectionSwitchAsync_UpdatesBothConnections_WhenBothFlagsTrue()
        {
            const string newDbConn = "Server=newdb;Database=both;";
            const string newStorageConn = "DefaultEndpointsProtocol=https;AccountName=both;AccountKey=CC==";

            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
                db.WebsiteCopyJobs.Add(new WebsiteCopyJob
                {
                    Id = Guid.NewGuid(),
                    SourceConnectionId = sourceConnectionId,
                    Status = (int)WebsiteCopyJobStatus.Completed,
                    CopyDatabase = true,
                    CopyStorage = true,
                    DestinationDbConn = newDbConn,
                    DestinationStorageConn = newStorageConn
                });
                await db.SaveChangesAsync();
            }

            using var readScope = serviceProvider.CreateScope();
            var readDb = readScope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var job = await readDb.WebsiteCopyJobs.FirstAsync(x => x.DestinationDbConn == newDbConn);

            var result = await orchestrator.ApplyConnectionSwitchAsync(job.Id);

            Assert.IsTrue(result);
            using var verifyScope = serviceProvider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var updatedSource = await verifyDb.Connections.FirstOrDefaultAsync(x => x.Id == sourceConnectionId);
            Assert.IsNotNull(updatedSource);
            Assert.AreEqual(newDbConn, updatedSource.DbConn);
            Assert.AreEqual(newStorageConn, updatedSource.StorageConn);
        }

        [TestMethod]
        public async Task ApplyConnectionSwitchAsync_DoesNotUpdateDbConn_WhenCopyDatabaseFalse()
        {
            const string originalDbConn = "Data Source=unit-source.db";

            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
                db.WebsiteCopyJobs.Add(new WebsiteCopyJob
                {
                    Id = Guid.NewGuid(),
                    SourceConnectionId = sourceConnectionId,
                    Status = (int)WebsiteCopyJobStatus.Completed,
                    CopyDatabase = false,
                    CopyStorage = false,
                    DestinationDbConn = "should-not-be-applied"
                });
                await db.SaveChangesAsync();
            }

            using var readScope = serviceProvider.CreateScope();
            var readDb = readScope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var job = await readDb.WebsiteCopyJobs.FirstAsync(x => x.DestinationDbConn == "should-not-be-applied");

            await orchestrator.ApplyConnectionSwitchAsync(job.Id);

            using var verifyScope = serviceProvider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var updatedSource = await verifyDb.Connections.FirstOrDefaultAsync(x => x.Id == sourceConnectionId);
            Assert.IsNotNull(updatedSource);
            Assert.AreEqual(originalDbConn, updatedSource.DbConn, "DbConn must not change when CopyDatabase is false.");
        }

        // ─── ProcessJobAsync (via StartJobAsync + polling) ─────────────────────────

        [TestMethod]
        public async Task ProcessJob_FailsWithSourceMissingError_WhenSourceConnectionNotFound()
        {
            var nonExistentSourceId = Guid.NewGuid();
            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = nonExistentSourceId,
                DestinationDbConn = "fake-dest",
                CopyDatabase = false,
                CopyStorage = false
            });

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual((int)WebsiteCopyJobStatus.Failed, completed.Status);
            Assert.IsTrue(
                completed.ErrorMessage?.Contains("Source connection not found", StringComparison.OrdinalIgnoreCase) == true,
                $"Expected 'Source connection not found' in error message but got: {completed.ErrorMessage}");
        }

        [TestMethod]
        public async Task ProcessJob_FailsWithDestinationMissingError_WhenNoDestinationInfoProvided()
        {
            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = sourceConnectionId,
                DestinationConnectionId = null,
                DestinationDbConn = null,
                DestinationStorageConn = null,
                CopyDatabase = false,
                CopyStorage = false
            });

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual((int)WebsiteCopyJobStatus.Failed, completed.Status);
            Assert.IsTrue(
                completed.ErrorMessage?.Contains("Destination connection is missing", StringComparison.OrdinalIgnoreCase) == true,
                $"Expected 'Destination connection is missing' in error message but got: {completed.ErrorMessage}");
        }

        [TestMethod]
        public async Task ProcessJob_FailsWithConcurrentJobError_WhenAnotherJobRunningForSameSource()
        {
            // Arrange: insert a Running job for the same source to block the new one
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
                db.WebsiteCopyJobs.Add(new WebsiteCopyJob
                {
                    Id = Guid.NewGuid(),
                    SourceConnectionId = sourceConnectionId,
                    Status = (int)WebsiteCopyJobStatus.Running,
                    DestinationDbConn = "existing-dest",
                    Locked = true
                });
                await db.SaveChangesAsync();
            }

            // Act: start a new job for the same source
            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = sourceConnectionId,
                DestinationDbConn = "new-dest",
                CopyDatabase = false,
                CopyStorage = false
            });

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual((int)WebsiteCopyJobStatus.Failed, completed.Status);
            Assert.IsTrue(
                completed.ErrorMessage?.Contains("Another copy is already in progress", StringComparison.OrdinalIgnoreCase) == true,
                $"Expected 'Another copy is already in progress' in error message but got: {completed.ErrorMessage}");
        }

        [TestMethod]
        public async Task ProcessJob_FailsWhenDestinationHasData_AndOverwriteDisabled()
        {
            var sourceConnectionGuid = Guid.NewGuid();
            var destinationConnectionGuid = Guid.NewGuid();
            var sourceDbConn = CreateTempSqliteConnectionString("websitecopy-source");
            var destinationDbConn = CreateTempSqliteConnectionString("websitecopy-destination");

            await SeedIdentityUserAsync(destinationDbConn, "existing-destination@local.test");

            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
                db.Connections.Add(BuildConnection(sourceConnectionGuid, sourceDbConn, "https://overwrite-off-source.local"));
                db.Connections.Add(BuildConnection(destinationConnectionGuid, destinationDbConn, "https://overwrite-off-destination.local"));
                await db.SaveChangesAsync();
            }

            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = sourceConnectionGuid,
                DestinationConnectionId = destinationConnectionGuid,
                CopyDatabase = true,
                CopyStorage = false,
                AllowDestinationOverwrite = false
            });

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual((int)WebsiteCopyJobStatus.Failed, completed.Status);
            Assert.IsTrue(
                completed.ErrorMessage?.Contains("Destination database must be empty", StringComparison.OrdinalIgnoreCase) == true,
                $"Expected destination-not-empty failure but got: {completed.ErrorMessage}");
        }

        [TestMethod]
        public async Task ProcessJob_CompletesAndCleansDestinationDatabase_WhenOverwriteEnabled()
        {
            var sourceConnectionGuid = Guid.NewGuid();
            var destinationConnectionGuid = Guid.NewGuid();
            var sourceDbConn = CreateTempSqliteConnectionString("websitecopy-source");
            var destinationDbConn = CreateTempSqliteConnectionString("websitecopy-destination");

            await SeedIdentityUserAsync(sourceDbConn, "source-user-1@local.test");
            await SeedIdentityUserAsync(sourceDbConn, "source-user-2@local.test");
            await SeedIdentityUserAsync(destinationDbConn, "existing-destination@local.test");

            var expectedSourceUserCount = await CountUsersAsync(sourceDbConn);

            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
                db.Connections.Add(BuildConnection(sourceConnectionGuid, sourceDbConn, "https://overwrite-on-source.local"));
                db.Connections.Add(BuildConnection(destinationConnectionGuid, destinationDbConn, "https://overwrite-on-destination.local"));
                await db.SaveChangesAsync();
            }

            var job = await orchestrator.StartJobAsync(new WebsiteCopyJob
            {
                SourceConnectionId = sourceConnectionGuid,
                DestinationConnectionId = destinationConnectionGuid,
                CopyDatabase = true,
                CopyStorage = false,
                AllowDestinationOverwrite = true
            });

            var completed = await WaitForTerminalStatusAsync(job.Id, timeoutSeconds: 10);

            Assert.IsNotNull(completed);
            Assert.AreEqual((int)WebsiteCopyJobStatus.Completed, completed.Status);
            Assert.IsTrue(completed.DatabaseCopied, "DatabaseCopied should be true when overwrite path succeeds.");
            Assert.IsTrue(completed.ValidationCompleted, "ValidationCompleted should be true when overwrite path succeeds.");

            var destinationUserCount = await CountUsersAsync(destinationDbConn);
            Assert.AreEqual(expectedSourceUserCount, destinationUserCount, "Destination user count should match source after overwrite cleanup and copy.");
        }

        [TestMethod]
        public async Task ProcessJob_CompletesAsDryRun_WhenDryRunEnabled()
        {
            var job = await orchestrator.StartJobAsync(BuildDryRunJob());

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual((int)WebsiteCopyJobStatus.CompletedDryRun, completed.Status);
            Assert.IsTrue(completed.ValidationCompleted, "ValidationCompleted should be true after dry run.");
            Assert.IsFalse(completed.Locked, "Locked flag should be cleared after completion.");
            Assert.IsNotNull(completed.CompletedUtc, "CompletedUtc should be set after dry run.");
        }

        [TestMethod]
        public async Task ProcessJob_IncreasesAttemptCount_OnFirstRun()
        {
            var job = await orchestrator.StartJobAsync(BuildDryRunJob());

            var completed = await WaitForTerminalStatusAsync(job.Id);

            Assert.IsNotNull(completed);
            Assert.AreEqual(1, completed.AttemptCount, "AttemptCount should be 1 after the first attempt.");
        }
    }
}
