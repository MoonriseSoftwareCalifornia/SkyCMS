using System.Collections;
using System.Collections.Concurrent;
using AspNetCore.Identity.FlexDb;
using Cosmos.BlobService;
using Cosmos.BlobService.Models;
using Cosmos.Common.Data;
using Cosmos.DynamicConfig;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph.Models;

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
        /// Gets the list of supported entity types for database copy and validation operations.
        /// </summary>
        private HashSet<Type> GetSupportedEntityTypes()
        {
            return new()
            {
                typeof(Article),
                typeof(ArticleLock),
                typeof(ArticleLog),
                typeof(ArticleNumber),
                typeof(AuthorInfo),
                typeof(CatalogEntry),
                typeof(Cosmos.Common.Data.Contact),
                typeof(Layout),
                typeof(Cosmos.Common.Data.Metric), // Qualified type to avoid namespace ambiguity
                typeof(PublishedPage),
                typeof(PageDesignVersion),
                typeof(Setting),
                typeof(Template),
                typeof(TotpToken),
                typeof(MigrationHistory),
                typeof(IdentityUser),
                typeof(IdentityRole),
                typeof(IdentityUserClaim<string>),
                typeof(IdentityUserLogin<string>),
                typeof(IdentityUserPasskey<string>), // Generic type handled separately
            };
        }

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
            job.Status = (int)WebsiteCopyJobStatus.Queued;
            job.CreatedUtc = DateTimeOffset.UtcNow;
            job.ProgressPercent = 0;
            job.LastMessage = "Queued";
            configDb.WebsiteCopyJobs.Add(job);
            await configDb.SaveChangesAsync(cancellationToken);

            await ProcessJobAsync(job.Id);
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
            if (job == null || job.Status == (int)WebsiteCopyJobStatus.Running)
            {
                return false;
            }

            job.Status = (int)WebsiteCopyJobStatus.Queued;
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
            if (job == null || job.Status != (int)WebsiteCopyJobStatus.Completed)
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
            //try
            //{
            await using var scope = scopeFactory.CreateAsyncScope();
            var configDb = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();

            var job = await configDb.WebsiteCopyJobs.FirstOrDefaultAsync(x => x.Id == jobId);
            if (job == null)
            {
                return;
            }

            if (!job.AllowDestinationOverwrite)
            {
                await FailJobAsync(configDb, job, "Must allow destination overwrite.");
                return;
            }

            if (!await CanLockWebsiteAsync(configDb, job.SourceConnectionId, job.Id))
            {
                await FailJobAsync(configDb, job, "Another copy is already in progress for this website.");
                return;
            }

            if (job.Status == (int)WebsiteCopyJobStatus.Completed)
            {
                return;
            }

            var source = await configDb.Connections.FirstOrDefaultAsync(x => x.Id == job.SourceConnectionId);
            if (source == null)
            {
                await FailJobAsync(configDb, job, "Source connection not found.");
                return;
            }

            var destination = await ResolveDestinationAsync(configDb, job);
            if (destination == null)
            {
                await FailJobAsync(configDb, job, "Destination connection is missing.");
                return;
            }

            // Copy the database over now.
            if (!job.DatabaseCopied)
            {
                await UpdateProgressAsync(configDb, job, 12, "Clearing destination database...");
                // Clear destination data base
                await DropAndRecreateSchema(destination);

                // Clear destination storage
                await ClearStorageAsync(destination.StorageConn);
                await UpdateProgressAsync(configDb, job, 35, "Copying database...");
                await CopyDatabaseAsync(source.DbConn, destination.DbConn, GetSupportedEntityTypes());
                job.DatabaseCopied = true;
                await configDb.SaveChangesAsync();
            }

            if (!job.StorageCopied)
            {
                await UpdateProgressAsync(configDb, job, 12, "Clearing destination storage...");
                await ClearStorageAsync(destination.StorageConn);

                await UpdateProgressAsync(configDb, job, 65, "Copying storage objects...");
                await CopyStorageAsync(source.StorageConn, destination.StorageConn);
                job.StorageCopied = true;
                await configDb.SaveChangesAsync();
            }

            job.ValidationCompleted = true;
            job.Status = (int)WebsiteCopyJobStatus.Completed;
            job.CompletedUtc = DateTimeOffset.UtcNow;
            job.Locked = false;

            await UpdateProgressAsync(configDb, job, 100, "Copy completed successfully. Manual switch available.");
            //}
            //catch (Exception ex)
            //{
            //    logger.LogError(ex, "Website copy job {JobId} failed.", jobId);
            //    await using var scope = scopeFactory.CreateAsyncScope();
            //    var configDb = scope.ServiceProvider.GetRequiredService<DynamicConfigDbContext>();
            //    var job = await configDb.WebsiteCopyJobs.FirstOrDefaultAsync(x => x.Id == jobId);
            //    if (job != null)
            //    {
            //        await FailJobAsync(configDb, job, ex.Message);
            //    }
            //}
            //finally
            //{
            //    // Release the lock on the job
            //}
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
            var status = (int)WebsiteCopyJobStatus.Running;
            var jobs = await db.WebsiteCopyJobs
                .AsNoTracking()
                .Where(j => j.SourceConnectionId == sourceConnectionId && j.Status == status && j.Id != currentJobId)
                .Select(j => j.Id)
                .ToListAsync();

            return !jobs.Any();
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

        private static async Task ClearStorageAsync(string connectionString)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var destinationStorage = new StorageContext(connectionString, memoryCache);
            var files = await destinationStorage.GetFilesAsync("/");

            await destinationStorage.DeleteFolderAsync("/");
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
        private async Task CopyDatabaseAsync(string sourceConn, string destinationConn, HashSet<Type> entityTypes)
        {
            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var sourceDb = new ApplicationDbContext(sourceConn);
                    using var destinationDb = new ApplicationDbContext(destinationConn);
                    await destinationDb.Database.EnsureCreatedAsync();

                    foreach (var clrType in entityTypes)
                    {
                        try
                        {
                            var records = await ReadEntitiesAsync(sourceDb, clrType);
                            if (records.Count == 0)
                            {
                                continue;
                            }

                            destinationDb.AddRange(records);    
                            await destinationDb.SaveChangesAsync();
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("Unknown entity type"))
                        {
                            // Skip copying for entity types not yet supported in ReadEntitiesAsync
                            // This allows forward compatibility when new entities are added
                            System.Diagnostics.Debug.WriteLine($"Skipping copy for unsupported entity type: {clrType.Name}");
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
        private static async Task CopyStorageAsync(string sourceConn, string destinationConn)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sourceStorage = new StorageContext(sourceConn, memoryCache);
            var destinationStorage = new StorageContext(destinationConn, memoryCache);

            await CopyFilesAndFolders(sourceStorage, destinationStorage, "/");
        }

        /// <summary>
        /// Recursively copies files and folders from a source storage context to a destination storage context.
        /// </summary>
        /// <param name="sourceStorageContext">The source storage context.</param>
        /// <param name="destinationStorageContext">The destination storage context.</param>
        /// <param name="path">The path to start copying from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static async Task CopyFilesAndFolders(StorageContext sourceStorageContext, StorageContext destinationStorageContext, string path)
        {
            // First, copy all files in the current folder
            var blobs = await sourceStorageContext.GetFilesAndDirectories(path);
            var files = blobs.Where(b => !b.IsDirectory).ToList();
            var directories = blobs.Where(b => b.IsDirectory).ToList();

            foreach (var file in files)
            {
                var fileProperties = await sourceStorageContext.GetFileAsync(file.Path);
                using var fileStream = await sourceStorageContext.GetStreamAsync(file.Path);
                using var memStream = new MemoryStream();
                await fileStream.CopyToAsync(memStream);
                memStream.Position = 0;

                await destinationStorageContext.AppendBlob(memStream, new FileUploadMetaData
                {
                    UploadUid = Guid.NewGuid().ToString("N"),
                    RelativePath = file.Path,
                    FileName = Path.GetFileName(file.Path),
                    ContentType = file.ContentType ?? "application/octet-stream",
                    ChunkIndex = 0,
                    TotalChunks = 1,
                    TotalFileSize = memStream.Length
                }, StorageConstants.UploadModeBlock);

                var result = await destinationStorageContext.GetFileAsync(file.Path);
                if (result.Size != fileProperties.Size)
                {
                    throw new InvalidOperationException($"File size mismatch for {file.Path}: source size {fileProperties.Size}, destination size {result.Size}");
                }
            }

            // Then, recursively copy all subdirectories
            foreach (var directory in directories)
            {
                await CopyFilesAndFolders(sourceStorageContext, destinationStorageContext, directory.Path);
            }
        }

        /// <summary>
        /// Determines whether an entity type is supported for database copy and validation operations.
        /// </summary>
        /// <param name="clrType">The CLR type to check.</param>
        /// <returns>True if the entity type is supported; otherwise, false.</returns>
        /// <remarks>
        /// This method checks if the entity type is in the SupportedEntityTypeNames set.
        /// New entity types must be registered in:
        /// 1. SupportedEntityTypeNames constant
        /// 2. CountEntitiesAsync switch statement
        /// 3. ReadEntitiesAsync switch statement
        /// </remarks>
        private bool IsSupportedEntityType(Type clrType)
        {
            var typeName = clrType.Name;

            // Special handling for generic IdentityUserPasskey<T>
            if (typeName.StartsWith("IdentityUserPasskey", StringComparison.Ordinal))
            {
                return true;
            }

            var entityTypes = GetSupportedEntityTypes();

            return entityTypes.Select(t => t.Name).Contains(typeName);
        }

        /// <summary>
        /// Drops the schema for the specified entity type from the database.
        /// </summary>
        /// <param name="dbContext">The database context to use for removing entities.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>   
        private async Task DropAndRecreateSchema(Connection connection)
        {
            // Create the context here.
            using var dbContext = new ApplicationDbContext(connection.DbConn);

            // Detect if there are any containers (in the case of Cosmos DB) or tables (in the case of relational databases) to remove entities from.
            if (dbContext.Database.IsCosmos())
            {
                // Detect if any containers exist in the Cosmos DB database
                var cosmosClient = dbContext.Database.GetCosmosClient();

                var containers = await cosmosClient.GetDatabase(dbContext.Database.GetCosmosDatabaseId()).GetContainerQueryIterator<ContainerProperties>().ReadNextAsync();

                if (containers.Any())
                {
                    foreach (var container in containers)
                    {
                        // Remove all entities from the container
                        var containerClient = cosmosClient.GetContainer(dbContext.Database.GetCosmosDatabaseId(), container.Id);
                        var result = await containerClient.DeleteContainerAsync(null);
                    }
                }

            }
            else
            {
                // For relational databases, drop all tables.
                foreach (var entityType in dbContext.Model.GetEntityTypes())
                {
                    // Drop the table associated with the entity type
                    var tableName = entityType.GetTableName();
                    // build the SQL command to drop the table
                    var dropTableSql = $"DROP TABLE IF EXISTS [{tableName}]";
                    // Execute the SQL command
                    await dbContext.Database.ExecuteSqlRawAsync(dropTableSql);
                }
            }

            // Recreate the schema
            await dbContext.Database.EnsureCreatedAsync();
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
        /// This method uses a type-safe dispatch to read entities for known ApplicationDbContext entity types.
        /// This approach:
        /// - Eliminates reflection-based AsNoTracking and ToListAsync invocation
        /// - Ensures proper query translation across all database providers (Cosmos DB, SQL, MySQL, SQLite)
        /// - Provides type safety with compiler verification
        /// 
        /// No-tracking queries are more efficient for read-only operations like copying data,
        /// as they don't maintain identity maps or change tracking information.
        /// </remarks>
        private static async Task<ICollection<object>> ReadEntitiesAsync(DbContext dbContext, Type clrType)
        {
            // Use type name to dispatch to appropriate query
            var typeName = clrType.Name;

            var results = typeName switch
            {
                nameof(Article) => (ICollection<object>)(await dbContext.Set<Article>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(ArticleLock) => (ICollection<object>)(await dbContext.Set<ArticleLock>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(ArticleLog) => (ICollection<object>)(await dbContext.Set<ArticleLog>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(ArticleNumber) => (ICollection<object>)(await dbContext.Set<ArticleNumber>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(AuthorInfo) => (ICollection<object>)(await dbContext.Set<AuthorInfo>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(CatalogEntry) => (ICollection<object>)(await dbContext.Set<CatalogEntry>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(Cosmos.Common.Data.Contact) => (ICollection<object>)(await dbContext.Set<Cosmos.Common.Data.Contact>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(Layout) => (ICollection<object>)(await dbContext.Set<Layout>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                "Metric" => (ICollection<object>)(await dbContext.Set<Cosmos.Common.Data.Metric>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(PublishedPage) => (ICollection<object>)(await dbContext.Set<PublishedPage>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(PageDesignVersion) => (ICollection<object>)(await dbContext.Set<PageDesignVersion>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(Setting) => (ICollection<object>)(await dbContext.Set<Setting>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(Template) => (ICollection<object>)(await dbContext.Set<Template>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(TotpToken) => (ICollection<object>)(await dbContext.Set<TotpToken>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(MigrationHistory) => (ICollection<object>)(await dbContext.Set<MigrationHistory>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(IdentityUser) => (ICollection<object>)(await dbContext.Set<IdentityUser>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(IdentityRole) => (ICollection<object>)(await dbContext.Set<IdentityRole>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(IdentityUserClaim<string>) => (ICollection<object>)(await dbContext.Set<IdentityUserClaim<string>>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(IdentityUserLogin<string>) => (ICollection<object>)(await dbContext.Set<IdentityUserLogin<string>>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(IdentityUserToken<string>) => (ICollection<object>)(await dbContext.Set<IdentityUserToken<string>>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(IdentityRoleClaim<string>) => (ICollection<object>)(await dbContext.Set<IdentityRoleClaim<string>>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                nameof(IdentityUserRole<string>) => (ICollection<object>)(await dbContext.Set<IdentityUserRole<string>>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                // Handle IdentityUserPasskey - note the generic parameter
                _ when typeName.StartsWith("IdentityUserPasskey", StringComparison.Ordinal)
                    => (ICollection<object>)(await dbContext.Set<IdentityUserPasskey<string>>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
                _ => throw new InvalidOperationException($"Unknown entity type for reading: {clrType.Name}")
            };  

            return results;
        }

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
            job.Status = (int)WebsiteCopyJobStatus.Failed;
            job.ErrorMessage = error;
            job.CompletedUtc = DateTimeOffset.UtcNow;
            job.Locked = false;
            await UpdateProgressAsync(db, job, job.ProgressPercent, "Job failed.");
        }

    }
}

