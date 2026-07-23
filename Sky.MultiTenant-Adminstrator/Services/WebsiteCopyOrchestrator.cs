using System.Collections;
using System.Collections.Concurrent;
using Cosmos.BlobService;
using Cosmos.BlobService.Models;
using Cosmos.Common.Data;
using Cosmos.DynamicConfig;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace Cosmos.MultiTenant.Administrator.Services
{
    /// <summary>
    /// Orchestrates the website copy operation from a source connection to a destination connection.
    /// </summary>
    /// <remarks>
    /// This class manages the entire lifecycle of website copy jobs, including database and storage migration,
    /// validation, and connection switching. It uses a semaphore-based locking mechanism to ensure only one
    /// copy operation is in progress per website at any given time.
    /// 
    /// The class supports:
    /// - Queuing copy jobs for asynchronous processing
    /// - Dry-run validation without actually copying data
    /// - Selective migration (database, storage, or both)
    /// - Comprehensive validation of copied data
    /// - Connection switching after successful validation
    /// - Job retry capabilities for failed operations
    /// </remarks>
    public class WebsiteCopyOrchestrator : IWebsiteCopyOrchestrator
    {
        /// <summary>
        /// Static dictionary that maintains semaphore locks for each website copy job to prevent concurrent operations.
        /// </summary>
        /// <remarks>
        /// Uses Guid as the key (job ID) to map to a SemaphoreSlim that controls access to the copy process.
        /// This ensures that only one copy operation can run for a specific job at a time.
        /// </remarks>
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> WebsiteLocks = new();

        /// <summary>
        /// Factory for creating service scopes to access dependency-injected services.
        /// </summary>
        /// <remarks>
        /// Used to create isolated service scopes for background job processing to ensure proper
        /// database context lifecycle management and multi-tenancy support.
        /// </remarks>
        private readonly IServiceScopeFactory scopeFactory;

        /// <summary>
        /// Logger for recording diagnostic and error information about copy operations.
        /// </summary>
        private readonly ILogger<WebsiteCopyOrchestrator> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebsiteCopyOrchestrator"/> class.
        /// </summary>
        /// <param name="scopeFactory">Factory for creating service scopes.</param>
        /// <param name="logger">Logger instance for diagnostic output.</param>
        /// <remarks>
        /// The constructor initializes the orchestrator with required dependencies for managing
        /// the website copy lifecycle and logging operational events.
        /// </remarks>
        public WebsiteCopyOrchestrator(
            IServiceScopeFactory scopeFactory,
            ILogger<WebsiteCopyOrchestrator> logger)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        /// <summary>
        /// Starts a new website copy job asynchronously.
        /// </summary>
        /// <param name="job">The website copy job to start.</param>
        /// <param name="cancellationToken">A cancellation token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the job with
        /// updated metadata including status, creation time, and progress tracking.
        /// </returns>
        /// <remarks>
        /// This method:
        /// - Persists the job to the database with initial state (status: Queued, progress: 0%)
        /// - Sets the creation timestamp to the current UTC time
        /// - Triggers asynchronous background processing via ProcessJobAsync
        /// - Returns immediately without waiting for the copy operation to complete
        /// 
        /// The actual copy operation will execute in a background thread, allowing the caller to
        /// continue without blocking.
        /// </remarks>
        public async Task<WebsiteCopyJob> StartJobAsync(WebsiteCopyJob job, CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var configDb = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            job.Status = WebsiteCopyJobStatus.Queued;
            job.CreatedUtc = DateTimeOffset.UtcNow;
            job.ProgressPercent = 0;
            job.LastMessage = "Queued";
            configDb.WebsiteCopyJobs.Add(job);
            await configDb.SaveChangesAsync(cancellationToken);

            _ = Task.Run(() => ProcessJobAsync(job.Id));
            return job;
        }

        /// <summary>
        /// Retrieves a website copy job by its identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the job to retrieve.</param>
        /// <param name="cancellationToken">A cancellation token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the job if found;
        /// otherwise, null.
        /// </returns>
        /// <remarks>
        /// This method queries the configuration database to fetch the current state of a copy job,
        /// allowing callers to check the job's status, progress, and any error messages.
        /// </remarks>
        public async Task<WebsiteCopyJob?> GetJobAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var configDb = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            return await configDb.WebsiteCopyJobs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        /// <summary>
        /// Retries a previously failed or incomplete website copy job.
        /// </summary>
        /// <param name="id">The unique identifier of the job to retry.</param>
        /// <param name="cancellationToken">A cancellation token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is true if the retry
        /// was successfully queued; false if the job was not found, is currently running, or cannot be retried.
        /// </returns>
        /// <remarks>
        /// This method:
        /// - Validates that the job exists and is not currently running
        /// - Resets the job status to Queued and clears any previous error messages
        /// - Triggers background reprocessing via ProcessJobAsync
        /// - Increments the AttemptCount during the actual job processing
        /// 
        /// Returns false in the following scenarios:
        /// - Job not found
        /// - Job is currently in Running state (prevents concurrent execution)
        /// </remarks>
        public async Task<bool> RetryJobAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var configDb = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var job = await configDb.WebsiteCopyJobs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (job == null || job.Status == WebsiteCopyJobStatus.Running)
            {
                return false;
            }

            job.Status = WebsiteCopyJobStatus.Queued;
            job.ErrorMessage = null;
            job.LastMessage = "Retry queued";
            await configDb.SaveChangesAsync(cancellationToken);

            _ = Task.Run(() => ProcessJobAsync(id));
            return true;
        }

        /// <summary>
        /// Applies the connection switch to the source connection after a successful copy operation.
        /// </summary>
        /// <param name="id">The unique identifier of the completed copy job.</param>
        /// <param name="cancellationToken">A cancellation token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is true if the connection
        /// switch was successfully applied; false if the job was not found, incomplete, or has missing connection data.
        /// </returns>
        /// <remarks>
        /// This method:
        /// - Validates that the job exists and has a Completed status
        /// - Retrieves the source connection that will be switched
        /// - Updates the source connection's database and/or storage connection strings based on job configuration
        /// - Persists the connection changes to the database
        /// 
        /// The connection switch is a manual operation after validation is complete. It:
        /// - Updates DbConn if MoveDatabase is true and a destination database connection is available
        /// - Updates StorageConn if MoveStorage is true and a destination storage connection is available
        /// 
        /// Returns false in the following scenarios:
        /// - Job not found
        /// - Job status is not Completed
        /// - Source connection not found
        /// </remarks>
        public async Task<bool> ApplyConnectionSwitchAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var configDb = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            var job = await configDb.WebsiteCopyJobs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (job == null || job.Status != WebsiteCopyJobStatus.Completed)
            {
                return false;
            }

            var source = await configDb.Connections.FirstOrDefaultAsync(x => x.Id == job.SourceConnectionId, cancellationToken);
            if (source == null)
            {
                return false;
            }

            if (job.CopyDatabase && !string.IsNullOrWhiteSpace(job.DestinationDbConn))
            {
                source.DbConn = job.DestinationDbConn;
            }

            if (job.CopyStorage && !string.IsNullOrWhiteSpace(job.DestinationStorageConn))
            {
                source.StorageConn = job.DestinationStorageConn;
            }

            await configDb.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>
        /// Processes a queued website copy job asynchronously.
        /// </summary>
        /// <param name="jobId">The unique identifier of the job to process.</param>
        /// <remarks>
        /// This is a private background method that orchestrates the entire website copy workflow:
        /// 
        /// 1. Locking: Acquires a semaphore lock for the job to prevent concurrent execution
        /// 2. Validation: Performs preflight checks including:
        ///    - Source connection validation
        ///    - Destination connection resolution
        ///    - Existing lock verification (no concurrent copies of the same website)
        ///    - Destination empty verification (strict validation)
        /// 3. Dry-run: If enabled, validates configuration and exits without copying data
        /// 4. Copy Operations: Executes conditionally based on job configuration:
        ///    - Database copy with entity-by-entity transfer
        ///    - Storage copy with file streaming and metadata tracking
        /// 5. Validation: Compares source and destination record/file counts
        /// 6. Completion: Updates job status and metadata, tracking completion time
        /// 
        /// Error Handling:
        /// - Catches all exceptions and logs them with job context
        /// - Transitions job to Failed status with error message
        /// - Releases the semaphore lock in finally block
        /// 
        /// Progress Tracking:
        /// - Updates progress percentage at key milestones (5%, 20%, 35%, 65%, 85%, 100%)
        /// - Provides human-readable status messages for UI display
        /// - Calculates real-time progress during storage copy operations
        /// </remarks>
        private async Task ProcessJobAsync(Guid jobId)
        {
            var copyLock = WebsiteLocks.GetOrAdd(jobId, _ => new SemaphoreSlim(1, 1));
            if (!await copyLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var configDb = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
                var job = await configDb.WebsiteCopyJobs.FirstOrDefaultAsync(x => x.Id == jobId);
                if (job == null)
                {
                    return;
                }

                if (job.Status == WebsiteCopyJobStatus.Completed || job.Status == WebsiteCopyJobStatus.CompletedDryRun)
                {
                    return;
                }

                var source = await configDb.Connections.FirstOrDefaultAsync(x => x.Id == job.SourceConnectionId);
                if (source == null)
                {
                    await FailJobAsync(configDb, job, "Source connection not found.");
                    return;
                }

                job.AttemptCount += 1;
                job.Status = WebsiteCopyJobStatus.Running;
                job.StartedUtc ??= DateTimeOffset.UtcNow;
                job.Locked = true;
                await UpdateProgressAsync(configDb, job, 5, "Running preflight checks...");

                var destination = await ResolveDestinationAsync(configDb, job);
                if (destination == null)
                {
                    await FailJobAsync(configDb, job, "Destination connection is missing.");
                    return;
                }

                if (!await CanLockWebsiteAsync(configDb, job.SourceConnectionId, job.Id))
                {
                    await FailJobAsync(configDb, job, "Another copy is already in progress for this website.");
                    return;
                }

                if (job.AllowDestinationOverwrite && !job.DryRun)
                {
                    await UpdateProgressAsync(configDb, job, 12, "Clearing destination data for overwrite...");
                    await ClearDestinationDataAsync(job, destination);
                }
                else if (!job.AllowDestinationOverwrite)
                {
                    await EnsureDestinationIsEmptyAsync(job, destination);
                }

                await UpdateProgressAsync(configDb, job, 20, "Preflight checks completed.");

                if (job.DryRun)
                {
                    job.Status = WebsiteCopyJobStatus.CompletedDryRun;
                    job.ValidationCompleted = true;
                    job.CompletedUtc = DateTimeOffset.UtcNow;
                    job.Locked = false;
                    await UpdateProgressAsync(configDb, job, 100, "Dry-run completed successfully.");
                    return;
                }


                if (job.CopyDatabase && !job.DatabaseCopied)
                {
                    await UpdateProgressAsync(configDb, job, 35, "Copying database...");
                    await CopyDatabaseAsync(source.DbConn, destination.DbConn);
                    job.DatabaseCopied = true;
                    await configDb.SaveChangesAsync();
                }

                if (job.CopyStorage && !job.StorageCopied)
                {
                    await UpdateProgressAsync(configDb, job, 65, "Copying storage objects...");
                    await CopyStorageAsync(source.StorageConn, destination.StorageConn, job);
                    job.StorageCopied = true;
                    await configDb.SaveChangesAsync();
                }

                await UpdateProgressAsync(configDb, job, 85, "Validating copied data...");
                await ValidateCopyAsync(source, destination, job);

                job.ValidationCompleted = true;
                job.Status = WebsiteCopyJobStatus.Completed;
                job.CompletedUtc = DateTimeOffset.UtcNow;
                job.Locked = false;

                await UpdateManagedConnectionMetadataAsync(configDb, job, destination.Id, "Copy validation passed.");
                await UpdateProgressAsync(configDb, job, 100, "Copy completed successfully. Manual switch available.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Website copy job {JobId} failed.", jobId);
                await using var scope = scopeFactory.CreateAsyncScope();
                var configDb = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
                var job = await configDb.WebsiteCopyJobs.FirstOrDefaultAsync(x => x.Id == jobId);
                if (job != null)
                {
                    await FailJobAsync(configDb, job, ex.Message);
                }
            }
            finally
            {
                copyLock.Release();
            }
        }

        /// <summary>
        /// Checks whether a website is available for locking (i.e., no other copy operation is in progress).
        /// </summary>
        /// <param name="db">The configuration database context.</param>
        /// <param name="sourceConnectionId">The unique identifier of the source connection.</param>
        /// <param name="currentJobId">The unique identifier of the current job (excluded from the check).</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is true if the website can be locked
        /// (no other running copy jobs); false if another copy job is currently running.
        /// </returns>
        /// <remarks>
        /// This method prevents concurrent copy operations on the same website by querying for any
        /// running jobs on the same source connection, excluding the current job.
        /// </remarks>
        private static async Task<bool> CanLockWebsiteAsync(DynamicConfigDbContext db, Guid sourceConnectionId, Guid currentJobId)
        {
            return !await db.WebsiteCopyJobs.AnyAsync(x =>
                x.SourceConnectionId == sourceConnectionId &&
                x.Id != currentJobId &&
                x.Status == WebsiteCopyJobStatus.Running);
        }

        /// <summary>
        /// Resolves the destination connection for a copy job.
        /// </summary>
        /// <param name="configDb">The configuration database context.</param>
        /// <param name="job">The copy job containing destination connection information.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the destination connection
        /// if found or created; null if no destination information is available.
        /// </returns>
        /// <remarks>
        /// Resolution priority:
        /// 1. If DestinationConnectionId is specified, retrieves the existing connection from the database
        /// 2. If connection strings are provided, creates an ad-hoc Connection object (not persisted)
        /// 3. Returns null if neither a connection ID nor connection strings are available
        /// 
        /// The ad-hoc connection is used for temporary or one-time migrations without requiring
        /// a pre-configured connection in the system.
        /// </remarks>
        private static async Task<Connection?> ResolveDestinationAsync(DynamicConfigDbContext configDb, WebsiteCopyJob job)
        {
            if (job.DestinationConnectionId.HasValue)
            {
                return await configDb.Connections.FirstOrDefaultAsync(x => x.Id == job.DestinationConnectionId.Value);
            }

            if (string.IsNullOrWhiteSpace(job.DestinationDbConn) && string.IsNullOrWhiteSpace(job.DestinationStorageConn))
            {
                return null;
            }

            return new Connection
            {
                Id = Guid.NewGuid(),
                DbConn = job.DestinationDbConn ?? string.Empty,
                StorageConn = job.DestinationStorageConn ?? string.Empty,
                DomainNames = Array.Empty<string>(),
                ResourceGroup = "migration",
                WebsiteUrl = "https://destination.local"
            };
        }

        /// <summary>
        /// Ensures that the destination is empty before starting a copy operation.
        /// </summary>
        /// <param name="job">The copy job specifying which resources to check.</param>
        /// <param name="destination">The destination connection to validate.</param>
        /// <remarks>
        /// This method validates that the destination is in a clean state before migration:
        /// - If MoveDatabase is true, verifies the destination database contains no user data
        /// - If MoveStorage is true, verifies the destination storage contains no files
        /// 
        /// This is a strict validation to ensure data integrity. Any existing data in the destination
        /// will cause the validation to fail and the copy operation to be aborted.
        /// 
        /// Throws InvalidOperationException if validation fails.
        /// </remarks>
        private static async Task EnsureDestinationIsEmptyAsync(WebsiteCopyJob job, Connection destination)
        {
            if (job.CopyDatabase)
            {
                await EnsureDatabaseEmptyAsync(destination.DbConn);
            }

            if (job.CopyStorage)
            {
                await EnsureStorageEmptyAsync(destination.StorageConn);
            }
        }

        private static async Task ClearDestinationDataAsync(WebsiteCopyJob job, Connection destination)
        {
            if (job.CopyStorage)
            {
                await ClearStorageAsync(destination.StorageConn);
                await EnsureStorageEmptyAsync(destination.StorageConn);
            }

            if (job.CopyDatabase)
            {
                await ResetDatabaseSchemaAsync(destination.DbConn);
                await EnsureDatabaseEmptyAsync(destination.DbConn);
            }
        }

        /// <summary>
        /// Validates that a destination database is empty of user data.
        /// </summary>
        /// <param name="connectionString">The connection string for the destination database.</param>
        /// <remarks>
        /// This method:
        /// 1. Ensures the database schema exists
        /// 2. Identifies all entity types (excluding owned entities and those without primary keys)
        /// 3. Counts entities in each table
        /// 4. Throws InvalidOperationException if any table contains records
        /// 
        /// This strict validation ensures data integrity by preventing accidental overwrites
        /// of existing data in the destination database.
        /// 
        /// Includes retry logic with exponential backoff to handle Cosmos DB eventual consistency
        /// after container creation. Newly created containers may not be immediately queryable.
        /// 
        /// Throws InvalidOperationException if any entity type has records.
        /// </remarks>
        private static async Task EnsureDatabaseEmptyAsync(string connectionString)
        {
            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var destinationDb = new ApplicationDbContext(connectionString);
                    await destinationDb.Database.EnsureCreatedAsync();
                    var types = destinationDb.Model.GetEntityTypes()
                        .Where(t => !t.IsOwned() && t.FindPrimaryKey() != null)
                        .Select(t => t.ClrType)
                        .Distinct()
                        .ToList();

                    foreach (var type in types)
                    {
                        var count = await CountEntitiesAsync(destinationDb, type);
                        if (count > 0)
                        {
                            throw new InvalidOperationException($"Destination database must be empty. Entity {type.Name} has {count} record(s).");
                        }
                    }

                    // Success - exit retry loop
                    return;
                }
                catch (DbUpdateException ex) when (attempt < maxAttempts && ex.InnerException is CosmosException cosmosEx && cosmosEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Container not ready yet - retry with exponential backoff
                    await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }
        }

        private static async Task ResetDatabaseSchemaAsync(string connectionString)
        {
            using var destinationDb = new ApplicationDbContext(connectionString);
            await destinationDb.Database.EnsureCreatedAsync();

            if (destinationDb.Database.IsCosmos())
            {
                await DeleteAllCosmosContainersAsync(destinationDb, connectionString);
                return;
            }

            var providerName = destinationDb.Database.ProviderName ?? string.Empty;
            if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                await DropAllSqlServerTablesAsync(destinationDb);
                return;
            }

            if (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
            {
                await DropAllMySqlTablesAsync(destinationDb);
                return;
            }

            if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                await DropAllSqliteTablesAsync(destinationDb);
                return;
            }

            throw new InvalidOperationException($"Destination overwrite is not supported for provider '{providerName}'.");
        }

        private static async Task DeleteAllCosmosContainersAsync(ApplicationDbContext destinationDb, string connectionString)
        {
            var cosmosClient = destinationDb.Database.GetCosmosClient();
            var databaseName = GetCosmosDatabaseName(connectionString);
            var database = cosmosClient.GetDatabase(databaseName);

            using var iterator = database.GetContainerQueryIterator<ContainerProperties>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var container in response)
                {
                    await database.GetContainer(container.Id).DeleteContainerAsync();
                }
            }
        }

        private static async Task DropAllSqlServerTablesAsync(ApplicationDbContext destinationDb)
        {
            await destinationDb.Database.ExecuteSqlRawAsync(@"
DECLARE @dropForeignKeys NVARCHAR(MAX) = N'';
SELECT @dropForeignKeys += N'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + N'].[' + OBJECT_NAME(parent_object_id) + N'] DROP CONSTRAINT [' + name + N'];'
FROM sys.foreign_keys;
IF LEN(@dropForeignKeys) > 0 EXEC sp_executesql @dropForeignKeys;

DECLARE @dropTables NVARCHAR(MAX) = N'';
SELECT @dropTables += N'DROP TABLE [' + TABLE_SCHEMA + N'].[' + TABLE_NAME + N'];'
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE';
IF LEN(@dropTables) > 0 EXEC sp_executesql @dropTables;");
        }

        private static async Task DropAllMySqlTablesAsync(ApplicationDbContext destinationDb)
        {
            await using var connection = destinationDb.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var listCommand = connection.CreateCommand();
            listCommand.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'";

            var tableNames = new List<string>();
            await using var reader = await listCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tableNames.Add(reader.GetString(0));
            }

            if (!tableNames.Any())
            {
                return;
            }

            var quoted = string.Join(", ", tableNames.Select(name => $"`{name.Replace("`", "``", StringComparison.Ordinal)}`"));
            await destinationDb.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");
            await destinationDb.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {quoted};");
            await destinationDb.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
        }

        private static async Task DropAllSqliteTablesAsync(ApplicationDbContext destinationDb)
        {
            await using var connection = destinationDb.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var listCommand = connection.CreateCommand();
            listCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";

            var tableNames = new List<string>();
            await using var reader = await listCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tableNames.Add(reader.GetString(0));
            }

            if (!tableNames.Any())
            {
                return;
            }

            await destinationDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            foreach (var tableName in tableNames)
            {
                var escapedTableName = tableName.Replace("\"", "\"\"", StringComparison.Ordinal);
                var sql = $"DROP TABLE IF EXISTS \"{escapedTableName}\";";
                await destinationDb.Database.ExecuteSqlRawAsync(sql);
            }

            await destinationDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

            var databaseCreator = destinationDb.Database.GetService<IRelationalDatabaseCreator>();
            await databaseCreator.CreateTablesAsync();
        }

        private static string GetCosmosDatabaseName(string connectionString)
        {
            return connectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(segment => segment.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=', 2)[1]
                ?? "cosmoscms";
        }

        /// <summary>
        /// Validates that a destination storage container is empty.
        /// </summary>
        /// <param name="connectionString">The connection string for the destination storage.</param>
        /// <remarks>
        /// This method:
        /// 1. Initializes a storage context with an in-memory cache
        /// 2. Lists all files in the root directory
        /// 3. Throws InvalidOperationException if any files are present
        /// 
        /// This ensures the destination storage is in a clean state before the copy operation begins.
        /// 
        /// Throws InvalidOperationException if any files exist in the destination storage.
        /// </remarks>
        private static async Task EnsureStorageEmptyAsync(string connectionString)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var destinationStorage = new StorageContext(connectionString, memoryCache);
            var files = await destinationStorage.GetFilesAsync("/");
            if (files.Any())
            {
                throw new InvalidOperationException("Destination storage must be empty.");
            }
        }

        private static async Task ClearStorageAsync(string connectionString)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var destinationStorage = new StorageContext(connectionString, memoryCache);
            var files = await destinationStorage.GetFilesAsync("/");

            foreach (var file in files)
            {
                await destinationStorage.DeleteFileAsync(file);
            }
        }

        /// <summary>
        /// Copies all database entities from a source database to a destination database.
        /// </summary>
        /// <param name="sourceConn">The connection string for the source database.</param>
        /// <param name="destinationConn">The connection string for the destination database.</param>
        /// <remarks>
        /// This method:
        /// 1. Ensures the destination database schema is created
        /// 2. Discovers all entity types in the model (excluding owned types and those without primary keys)
        /// 3. Orders entity types by foreign key count to respect referential integrity constraints
        /// 4. Reads all entities from the source database as no-tracking queries
        /// 5. Inserts entities into the destination database in batches
        /// 6. Clears the change tracker after each batch to manage memory
        /// 
        /// The entity ordering by foreign key count ensures that parent entities are inserted
        /// before dependent child entities, maintaining referential integrity.
        /// 
        /// This is an entity-by-entity copy operation using DbContext APIs rather than
        /// raw SQL, ensuring all navigation properties and relationships are preserved.
        /// 
        /// Includes retry logic with exponential backoff to handle Cosmos DB eventual consistency
        /// after schema creation.
        /// </remarks>
        private static async Task CopyDatabaseAsync(string sourceConn, string destinationConn)
        {
            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var sourceDb = new ApplicationDbContext(sourceConn);
                    using var destinationDb = new ApplicationDbContext(destinationConn);
                    await destinationDb.Database.EnsureCreatedAsync();

                    var entityTypes = destinationDb.Model.GetEntityTypes()
                        .Where(t => !t.IsOwned() && t.FindPrimaryKey() != null)
                        .OrderBy(t => t.GetForeignKeys().Count())
                        .Select(t => t.ClrType)
                        .Distinct()
                        .ToList();

                    foreach (var clrType in entityTypes)
                    {
                        var records = await ReadEntitiesAsync(sourceDb, clrType);
                        if (records.Count == 0)
                        {
                            continue;
                        }

                        destinationDb.ChangeTracker.AutoDetectChangesEnabled = false;
                        destinationDb.AddRange(records);
                        await destinationDb.SaveChangesAsync();
                        destinationDb.ChangeTracker.Clear();
                    }

                    // Success - exit retry loop
                    return;
                }
                catch (DbUpdateException ex) when (attempt < maxAttempts && ex.InnerException is CosmosException cosmosEx && cosmosEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Container not ready yet - retry with exponential backoff
                    await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }
        }

        /// <summary>
        /// Copies all storage files from a source storage container to a destination storage container.
        /// </summary>
        /// <param name="sourceConn">The connection string for the source storage.</param>
        /// <param name="destinationConn">The connection string for the destination storage.</param>
        /// <param name="job">The copy job, which is updated with real-time progress information.</param>
        /// <remarks>
        /// This method:
        /// 1. Initializes storage contexts for both source and destination
        /// 2. Lists all files in the source storage root directory
        /// 3. For each file:
        ///    - Streams the file into a memory buffer
        ///    - Extracts relative path and file name from the full path
        ///    - Determines MIME type based on file extension (defaults to "application/octet-stream")
        ///    - Creates file upload metadata with streaming information
        ///    - Appends the file to destination storage
        /// 4. Updates job progress percentage in real-time as files are copied
        /// 
        /// Progress Calculation:
        /// - Starts at 65% (after database copy)
        /// - Increments by up to 15% based on files completed
        /// - Formula: 65 + (completed / total) * 15
        /// 
        /// Files are copied one at a time with full file content loaded into memory to handle
        /// any size constraints and support progress tracking.
        /// </remarks>
        private static async Task CopyStorageAsync(string sourceConn, string destinationConn, WebsiteCopyJob job)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sourceStorage = new StorageContext(sourceConn, memoryCache);
            var destinationStorage = new StorageContext(destinationConn, memoryCache);
            var files = await sourceStorage.GetFilesAsync("/");
            var provider = new FileExtensionContentTypeProvider();

            var completed = 0;
            foreach (var path in files)
            {
                await using var stream = await sourceStorage.GetStreamAsync(path);
                await using var temp = new MemoryStream();
                await stream.CopyToAsync(temp);
                temp.Position = 0;

                var normalized = path.TrimStart('/');
                var split = normalized.LastIndexOf('/');
                var relativePath = split >= 0 ? normalized[..split] : string.Empty;
                var fileName = split >= 0 ? normalized[(split + 1)..] : normalized;

                if (!provider.TryGetContentType(fileName, out var contentType))
                {
                    contentType = "application/octet-stream";
                }

                var metadata = new FileUploadMetaData
                {
                    UploadUid = Guid.NewGuid().ToString("N"),
                    // Storage drivers resolve blob target from RelativePath; keep full source path here.
                    RelativePath = path,
                    FileName = fileName,
                    ContentType = contentType,
                    ChunkIndex = 0,
                    TotalChunks = 1,
                    TotalFileSize = temp.Length
                };

                await destinationStorage.AppendBlob(temp, metadata, StorageConstants.UploadModeBlock);

                completed++;
                job.ProgressPercent = 65 + (int)Math.Round((double)completed / Math.Max(files.Count, 1) * 15);
            }
        }

        /// <summary>
        /// Validates that the copied data in the destination matches the source.
        /// </summary>
        /// <param name="source">The source connection.</param>
        /// <param name="destination">The destination connection.</param>
        /// <param name="job">The copy job specifying what to validate.</param>
        /// <remarks>
        /// This method performs conditional validation based on job configuration:
        /// - If MoveDatabase is true, validates that all entity types have the same record count
        /// - If MoveStorage is true, validates that file counts match between source and destination
        /// 
        /// Validation ensures data integrity by confirming that no records were lost or corrupted
        /// during the copy operation. Any count mismatches will trigger an InvalidOperationException.
        /// </remarks>
        private static async Task ValidateCopyAsync(Connection source, Connection destination, WebsiteCopyJob job)
        {
            if (job.CopyDatabase)
            {
                await ValidateDatabaseAsync(source.DbConn, destination.DbConn);
            }

            if (job.CopyStorage)
            {
                await ValidateStorageAsync(source.StorageConn, destination.StorageConn);
            }
        }

        /// <summary>
        /// Validates that database copy is complete by comparing entity record counts.
        /// </summary>
        /// <param name="sourceConn">The connection string for the source database.</param>
        /// <param name="destinationConn">The connection string for the destination database.</param>
        /// <remarks>
        /// This method:
        /// 1. Opens connections to both source and destination databases
        /// 2. Discovers all entity types (excluding owned types and those without primary keys)
        /// 3. Counts entities in each table from both databases
        /// 4. Compares counts and throws InvalidOperationException if any mismatch is found
        /// 
        /// If validation fails, the error message includes the entity type name and the count
        /// discrepancy between source and destination, helping diagnose copy issues.
        /// 
        /// Throws InvalidOperationException if any entity type has different counts.
        /// </remarks>
        private static async Task ValidateDatabaseAsync(string sourceConn, string destinationConn)
        {
            using var sourceDb = new ApplicationDbContext(sourceConn);
            using var destinationDb = new ApplicationDbContext(destinationConn);

            var entityTypes = sourceDb.Model.GetEntityTypes()
                .Where(t => !t.IsOwned() && t.FindPrimaryKey() != null)
                .Select(t => t.ClrType)
                .Distinct()
                .ToList();

            foreach (var clrType in entityTypes)
            {
                var sourceCount = await CountEntitiesAsync(sourceDb, clrType);
                var destinationCount = await CountEntitiesAsync(destinationDb, clrType);
                if (sourceCount != destinationCount)
                {
                    throw new InvalidOperationException($"Validation failed for {clrType.Name}: source count {sourceCount}, destination count {destinationCount}.");
                }
            }
        }

        /// <summary>
        /// Validates that storage copy is complete by comparing file counts.
        /// </summary>
        /// <param name="sourceConn">The connection string for the source storage.</param>
        /// <param name="destinationConn">The connection string for the destination storage.</param>
        /// <remarks>
        /// This method:
        /// 1. Initializes storage contexts for both source and destination
        /// 2. Lists all files from the root directory of both storages
        /// 3. Compares the file counts
        /// 4. Throws InvalidOperationException if counts don't match
        /// 
        /// This validation ensures that all files were successfully copied from source to destination.
        /// It does not perform deep content verification (e.g., file size or checksum comparison),
        /// only that the number of files matches.
        /// 
        /// Throws InvalidOperationException if file counts don't match.
        /// </remarks>
        private static async Task ValidateStorageAsync(string sourceConn, string destinationConn)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sourceStorage = new StorageContext(sourceConn, memoryCache);
            var destinationStorage = new StorageContext(destinationConn, memoryCache);

            var sourceFiles = await sourceStorage.GetFilesAsync("/");
            var destinationFiles = await destinationStorage.GetFilesAsync("/");

            if (sourceFiles.Count != destinationFiles.Count)
            {
                throw new InvalidOperationException($"Storage validation failed: source count {sourceFiles.Count}, destination count {destinationFiles.Count}.");
            }
        }

        /// <summary>
        /// Counts the number of entities of a specific type in the database.
        /// </summary>
        /// <param name="dbContext">The database context to query.</param>
        /// <param name="clrType">The CLR type of the entity to count.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// the count of entities of the specified type.
        /// </returns>
        /// <remarks>
        /// This method uses reflection to invoke the generic CountAsync&lt;T&gt; method
        /// for a type determined at runtime. This is necessary because the entity type is not
        /// known at compile time.
        /// 
        /// Uses the EntityFrameworkQueryableExtensions.CountAsync&lt;T&gt; method to ensure
        /// proper query translation across all database providers including Cosmos DB.
        /// </remarks>
        private static async Task<int> CountEntitiesAsync(DbContext dbContext, Type clrType)
        {
            var method = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods()
                .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.CountAsync) &&
                           m.GetGenericArguments().Length == 1 &&
                           m.GetParameters().Length == 1)
                .First();

            var genericMethod = method.MakeGenericMethod(clrType);
            var set = GetSet(dbContext, clrType);
            var task = (Task)genericMethod.Invoke(null, new[] { set })!;
            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result");
            return (int)resultProperty!.GetValue(task)!;
        }

        /// <summary>
        /// Reads all entities of a specific type from the database without change tracking.
        /// </summary>
        /// <param name="dbContext">The database context to query.</param>
        /// <param name="clrType">The CLR type of the entities to read.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a list of all entities of the specified type as untracked objects.
        /// </returns>
        /// <remarks>
        /// This method:
        /// 1. Uses reflection to get the DbSet for the runtime type
        /// 2. Applies AsNoTracking to prevent change tracking overhead
        /// 3. Materializes the query to a list asynchronously
        /// 4. Casts the results to objects for return
        /// 
        /// No-tracking queries are more efficient for read-only operations like copying data,
        /// as they don't maintain identity maps or change tracking information.
        /// </remarks>
        private static async Task<List<object>> ReadEntitiesAsync(DbContext dbContext, Type clrType)
        {
            // Get the DbSet<T>
            var set = GetSet(dbContext, clrType);

            // Call AsNoTracking<T> via reflection
            var asNoTrackingMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods()
                .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTracking) &&
                           m.GetGenericArguments().Length == 1 &&
                           m.GetParameters().Length == 1)
                .First();

            var asNoTrackingGenericMethod = asNoTrackingMethod.MakeGenericMethod(clrType);
            var noTrackingQueryable = asNoTrackingGenericMethod.Invoke(null, new[] { set });

            // Call ToListAsync<T> via reflection
            var toListAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods()
                .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) &&
                           m.GetGenericArguments().Length == 1 &&
                           m.GetParameters().Length == 1)
                .First();

            var toListAsyncGenericMethod = toListAsyncMethod.MakeGenericMethod(clrType);
            var task = (Task)toListAsyncGenericMethod.Invoke(null, new[] { noTrackingQueryable })!;
            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result");
            var list = (IEnumerable)resultProperty!.GetValue(task)!;
            return list.Cast<object>().ToList();
        }

        /// <summary>
        /// Retrieves the DbSet for a specific entity type using reflection.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="clrType">The CLR type of the entity.</param>
        /// <returns>
        /// The DbSet for the specified entity type.
        /// </returns>
        /// <remarks>
        /// This method uses reflection to dynamically invoke DbContext.Set&lt;T&gt;() with a
        /// runtime-determined type. This is necessary because entity types are discovered at
        /// runtime from the EF Core model rather than being known at compile time.
        /// 
        /// The method:
        /// 1. Gets the Set method definition from DbContext
        /// 2. Makes it generic with the clrType parameter
        /// 3. Invokes the method on the provided context
        /// 4. Returns the resulting DbSet
        /// 
        /// Throws InvalidOperationException if the method cannot be invoked.
        /// </remarks>
        private static object GetSet(DbContext dbContext, Type clrType)
        {
            var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes);
            var generic = setMethod?.MakeGenericMethod(clrType) ?? throw new InvalidOperationException("Unable to access DbContext.Set<T>().");
            return generic.Invoke(dbContext, null) ?? throw new InvalidOperationException("Unable to resolve entity set.");
        }

        /// <summary>
        /// Updates metadata for a managed connection after successful copy and validation.
        /// </summary>
        /// <param name="configDb">The configuration database context.</param>
        /// <param name="job">The copy job containing source connection information.</param>
        /// <param name="destinationConnectionId">The unique identifier of the destination connection.</param>
        /// <param name="summary">A summary message describing the validation result.</param>
        /// <remarks>
        /// This method updates the WebsiteManagedConnection entity with:
        /// - WebsiteConnectionId: Linked to the source connection of the copy job
        /// - LastCopiedUtc: Set to current UTC time
        /// - LastValidatedUtc: Set to current UTC time
        /// - IsKnownEmpty: Cleared to indicate the destination now contains data
        /// - LastValidationSummary: Set to the provided summary message
        /// 
        /// The managed connection metadata tracks the relationship between the destination
        /// and its source, along with validation timestamps and status.
        /// 
        /// If the managed connection is not found, the method silently returns without error.
        /// </remarks>
        private static async Task UpdateManagedConnectionMetadataAsync(
            DynamicConfigDbContext configDb,
            WebsiteCopyJob job,
            Guid destinationConnectionId,
            string summary)
        {
            var managed = await configDb.WebsiteManagedConnections.FirstOrDefaultAsync(x => x.Id == destinationConnectionId);
            if (managed == null)
            {
                return;
            }

            managed.WebsiteConnectionId = job.SourceConnectionId;
            managed.LastCopiedUtc = DateTimeOffset.UtcNow;
            managed.LastValidatedUtc = DateTimeOffset.UtcNow;
            managed.IsKnownEmpty = false;
            managed.LastValidationSummary = summary;
            await configDb.SaveChangesAsync();
        }

        /// <summary>
        /// Updates the progress and status message of a copy job.
        /// </summary>
        /// <param name="db">The configuration database context.</param>
        /// <param name="job">The copy job to update.</param>
        /// <param name="progress">The progress percentage (0-100).</param>
        /// <param name="message">A human-readable status message.</param>
        /// <remarks>
        /// This method is called frequently during the copy operation to track progress and
        /// communicate status to users. It:
        /// 1. Updates the job's ProgressPercent to the specified value
        /// 2. Updates the job's LastMessage with the status message
        /// 3. Persists the changes to the database
        /// 
        /// The progress percentage provides UI indication of completion, while the message
        /// provides detailed information about the current operation phase.
        /// </remarks>
        private static async Task UpdateProgressAsync(DynamicConfigDbContext db, WebsiteCopyJob job, int progress, string message)
        {
            job.ProgressPercent = progress;
            job.LastMessage = message;
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Marks a copy job as failed with an error message.
        /// </summary>
        /// <param name="db">The configuration database context.</param>
        /// <param name="job">The copy job to fail.</param>
        /// <param name="error">The error message describing why the job failed.</param>
        /// <remarks>
        /// This method is called when an error occurs during job processing. It:
        /// 1. Sets the job status to Failed
        /// 2. Records the error message for troubleshooting
        /// 3. Sets the completion timestamp to current UTC time
        /// 4. Clears the Locked flag to allow retries
        /// 5. Updates the progress message to "Job failed."
        /// 6. Persists the changes to the database
        /// 
        /// The job can be retried after failure using RetryJobAsync. The error message
        /// and progress information help users understand what went wrong.
        /// </remarks>
        private static async Task FailJobAsync(DynamicConfigDbContext db, WebsiteCopyJob job, string error)
        {
            job.Status = WebsiteCopyJobStatus.Failed;
            job.ErrorMessage = error;
            job.CompletedUtc = DateTimeOffset.UtcNow;
            job.Locked = false;
            await UpdateProgressAsync(db, job, job.ProgressPercent, "Job failed.");
        }
    }
}

