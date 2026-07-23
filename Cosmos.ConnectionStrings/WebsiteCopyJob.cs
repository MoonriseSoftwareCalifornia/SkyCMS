using System.ComponentModel.DataAnnotations;

namespace Cosmos.DynamicConfig
{
    /// <summary>
    /// Represents the lifecycle states of a website copy job.
    /// </summary>
    /// <remarks>
    /// The status transitions follow this general flow:
    /// - Queued → Running → Completed (successful copy)
    /// - Queued → Running → CompletedDryRun (dry-run validation only)
    /// - Queued → Running → Failed (any stage failure)
    /// - Failed/Completed → Queued (after retry)
    /// - Any status → Cancelled (user cancellation)
    /// </remarks>
    public enum WebsiteCopyJobStatus
    {
        /// <summary>
        /// Job has been created but not yet started for processing.
        /// </summary>
        /// <remarks>
        /// This is the initial status assigned when a job is created via StartJobAsync.
        /// The job remains queued until background processing begins.
        /// </remarks>
        Queued,

        /// <summary>
        /// Job is currently being processed (copy, validation, or both).
        /// </summary>
        /// <remarks>
        /// This status indicates the orchestrator is actively executing the copy workflow.
        /// Only one job per source website can be in this state at a time due to locking mechanisms.
        /// </remarks>
        Running,

        /// <summary>
        /// Copy operation completed successfully with all data copied and validated.
        /// </summary>
        /// <remarks>
        /// This indicates that:
        /// - All requested copy operations (database and/or storage) completed
        /// - Validation confirmed the copied data matches the source
        /// - The job is ready for connection switching via ApplyConnectionSwitchAsync
        /// - Further retries will re-copy over any existing destination data
        /// </remarks>
        Completed,

        /// <summary>
        /// Dry-run validation completed successfully without copying actual data.
        /// </summary>
        /// <remarks>
        /// This status is used when DryRun is true. It indicates:
        /// - Preflight validation checks passed
        /// - No data was actually copied to the destination
        /// - Connection strings and destination availability were verified
        /// - This status is useful for pre-flight checks before committing to actual copy
        /// - ApplyConnectionSwitchAsync will fail for dry-run completions
        /// </remarks>
        CompletedDryRun,

        /// <summary>
        /// Job failed at some stage of processing.
        /// </summary>
        /// <remarks>
        /// Failure can occur at various stages:
        /// - Preflight validation (missing connections, destination not empty)
        /// - Database copy (schema issues, constraint violations)
        /// - Storage copy (file access, quota exceeded)
        /// - Post-copy validation (record count mismatch, file count mismatch)
        /// - Any stage exception (connection timeout, database lock)
        /// 
        /// The ErrorMessage property contains details about the failure.
        /// Jobs in this state can be retried using RetryJobAsync.
        /// </remarks>
        Failed,

        /// <summary>
        /// Job was cancelled by user request.
        /// </summary>
        /// <remarks>
        /// This status indicates the job was explicitly cancelled.
        /// Current implementation does not support mid-job cancellation;
        /// this status is reserved for future cancellation support.
        /// </remarks>
        Cancelled
    }

    /// <summary>
    /// Audit and job tracking record for website copy operations between connections.
    /// </summary>
    /// <remarks>
    /// The WebsiteCopyJob class represents a complete record of a website migration operation,
    /// including what to copy (database, storage, both), where to copy from/to, and tracking
    /// the entire lifecycle of the operation from queuing through completion or failure.
    /// 
    /// Key workflow stages tracked by this record:
    /// 1. Initialization: Job is created with source and destination connection info
    /// 2. Validation: Preflight checks ensure destination is empty and accessible
    /// 3. Copy: Database entities and storage files are transferred
    /// 4. Verification: Copied data is validated against source
    /// 5. Completion: Job reaches terminal state (Completed, Failed, or CompletedDryRun)
    /// 6. Switch: Connection strings are optionally updated to point to destination
    /// 
    /// This record serves dual purposes:
    /// - Audit trail: Maintains history of all copy operations for compliance
    /// - Progress tracking: Enables real-time progress monitoring via GetJobAsync
    /// </remarks>
    public class WebsiteCopyJob
    {
        /// <summary>
        /// Unique identifier for this copy job.
        /// </summary>
        /// <remarks>
        /// Auto-generated GUID used throughout the system to track, retrieve, and manage this job.
        /// </remarks>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The identifier of the source connection to copy from.
        /// </summary>
        /// <remarks>
        /// This required field identifies which website/connection will be copied.
        /// The source connection's database and storage (if configured) will be read during copy operations.
        /// Locking is applied per source connection to prevent concurrent copies of the same website.
        /// </remarks>
        public Guid SourceConnectionId { get; set; }

        /// <summary>
        /// Optional identifier of an existing destination connection to copy to.
        /// </summary>
        /// <remarks>
        /// If provided, this references a pre-configured Connection entity that will receive the copied data.
        /// If null, the copy operation uses DestinationDbConn and DestinationStorageConn strings instead.
        /// 
        /// Resolution priority:
        /// - If DestinationConnectionId is set, uses the referenced connection
        /// - Otherwise, uses DestinationDbConn and DestinationStorageConn as temporary connection info
        /// 
        /// This allows two usage patterns:
        /// - Managed: Copy to a known, pre-configured connection
        /// - Ad-hoc: Copy to temporary destinations via raw connection strings
        /// </remarks>
        public Guid? DestinationConnectionId { get; set; }

        /// <summary>
        /// Whether to copy the source database to the destination.
        /// </summary>
        /// <remarks>
        /// When true, the orchestrator will:
        /// - Create an ApplicationDbContext for both source and destination using provided connection strings
        /// - Discover all entity types in the model (excluding owned types)
        /// - Read all entities from source database
        /// - Write entities to destination database, ordered by foreign key count to maintain referential integrity
        /// - Validate that record counts match between source and destination
        /// 
        /// When false, no database copy occurs regardless of whether DestinationDbConn is provided.
        /// 
        /// The destination database MUST be empty before copy begins (strict validation).
        /// If the database copy fails, the job status becomes Failed and can be retried.
        /// 
        /// Typical use cases:
        /// - true: Full website migration to new database
        /// - false: Storage-only migration (static files to new storage account)
        /// </remarks>
        public bool CopyDatabase { get; set; }

        /// <summary>
        /// Whether to copy source storage objects (files, blobs) to the destination.
        /// </summary>
        /// <remarks>
        /// When true, the orchestrator will:
        /// - Initialize storage contexts for source and destination
        /// - List all files from the source root directory
        /// - Stream each file into memory and copy to destination with preserved metadata
        /// - Determine MIME type from file extension
        /// - Update job progress in real-time during the copy (65-80% range)
        /// - Validate that file counts match between source and destination
        /// 
        /// When false, no storage copy occurs regardless of whether DestinationStorageConn is provided.
        /// 
        /// The destination storage MUST be empty before copy begins (strict validation).
        /// Files are copied one at a time with full content buffered in memory.
        /// If any file copy fails, the job status becomes Failed and can be retried.
        /// 
        /// Typical use cases:
        /// - true: Full website migration including uploaded files
        /// - false: Database-only migration (moving records without files)
        /// </remarks>
        public bool CopyStorage { get; set; }

        /// <summary>
        /// Whether to perform a dry-run validation without actually copying data.
        /// </summary>
        /// <remarks>
        /// When true, the orchestrator will:
        /// - Execute all preflight validation checks (connections valid, destination empty)
        /// - Verify source and destination connectivity
        /// - Update job status to CompletedDryRun without copying any data
        /// - Skip actual database copy phase
        /// - Skip actual storage copy phase
        /// - Skip post-copy validation
        /// 
        /// When false, proceeds with normal copy operations.
        /// 
        /// Dry-run is useful for:
        /// - Verifying configuration before committing to actual migration
        /// - Testing connectivity and permissions without data risk
        /// - Pre-flight checks that ensure destination is ready
        /// - Estimating what will be copied (progress shows structure but no actual copy)
        /// 
        /// Note: ApplyConnectionSwitchAsync will fail for dry-run completions, as no data was actually copied.
        /// Jobs with CompletedDryRun status cannot be switched but can be retried as normal jobs.
        /// </remarks>
        public bool DryRun { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether existing destination data may be overwritten.
        /// </summary>
        /// <remarks>
        /// When true, the orchestrator will clear destination data before copying:
        /// - Destination storage files/blobs are deleted
        /// - Destination database schema data is removed (Cosmos containers deleted; relational tables dropped)
        ///
        /// Default is false to preserve safety and prevent accidental data loss.
        /// </remarks>
        public bool AllowDestinationOverwrite { get; set; }

        /// <summary>
        /// Whether to automatically update the source connection's strings upon successful completion.
        /// </summary>
        /// <remarks>
        /// This property is reserved for future use to enable automatic connection switching.
        /// 
        /// Current behavior: This property is checked but the actual switch is always manual via
        /// ApplyConnectionSwitchAsync to ensure deliberate, controlled connection updates in production.
        /// 
        /// Future behavior (when implemented):
        /// - If true and job completes successfully, connection strings are automatically updated
        /// - If false, ApplyConnectionSwitchAsync must be called manually to switch connections
        /// </remarks>
        public bool UpdateConnectionOnSuccess { get; set; }

        /// <summary>
        /// Destination database connection string for ad-hoc copy operations.
        /// </summary>
        /// <remarks>
        /// Used when DestinationConnectionId is not provided. Contains the connection string
        /// for the destination database where data will be copied.
        /// 
        /// Only used during copy if:
        /// - DestinationConnectionId is null, AND
        /// - MoveDatabase is true, AND
        /// - This value is not null/empty
        /// 
        /// Typical format: "Server=...; Database=...; User Id=...; Password=..."
        /// 
        /// This field enables temporary, ad-hoc migrations without requiring pre-configured connections.
        /// For managed migrations, use DestinationConnectionId instead for better security (secrets in config).
        /// </remarks>
        public string? DestinationDbConn { get; set; }

        /// <summary>
        /// Destination storage connection string for ad-hoc copy operations.
        /// </summary>
        /// <remarks>
        /// Used when DestinationConnectionId is not provided. Contains the connection string
        /// for the destination storage where files will be copied.
        /// 
        /// Only used during copy if:
        /// - DestinationConnectionId is null, AND
        /// - MoveStorage is true, AND
        /// - This value is not null/empty
        /// 
        /// Typical format (Azure): "BlobEndpoint=https://...; SharedAccessSignature=..."
        /// 
        /// This field enables temporary, ad-hoc migrations without requiring pre-configured connections.
        /// For managed migrations, use DestinationConnectionId instead for better security.
        /// </remarks>
        public string? DestinationStorageConn { get; set; }

        /// <summary>
        /// User email or identifier of the person who initiated this copy job.
        /// </summary>
        /// <remarks>
        /// Captured from the current user context when the job is created via StartJobAsync.
        /// Used for audit trail, tracking who requested the migration, and potentially notifying
        /// the user when the operation completes.
        /// 
        /// Maximum length: 320 characters (standard email length).
        /// 
        /// Populated from claims context if available; null if not captured.
        /// </remarks>
        [MaxLength(320)]
        public string? StartedBy { get; set; }

        /// <summary>
        /// The UTC timestamp when this job record was created.
        /// </summary>
        /// <remarks>
        /// Automatically set to the current UTC time when the job is created.
        /// Used for audit trail and to determine how long a job has been in the system.
        /// </remarks>
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// The UTC timestamp when processing of this job began (when Running status was set).
        /// </summary>
        /// <remarks>
        /// Null until the background processor begins executing ProcessJobAsync.
        /// Once set, remains unchanged even if the job is retried (preserves first attempt time).
        /// Null for jobs that have never entered Running status (still Queued).
        /// </remarks>
        public DateTimeOffset? StartedUtc { get; set; }

        /// <summary>
        /// The UTC timestamp when the job reached a terminal status (Completed, Failed, or CompletedDryRun).
        /// </summary>
        /// <remarks>
        /// Null while the job is Queued or Running.
        /// Set when the job transitions to terminal status.
        /// Used to calculate total time-to-completion for the migration.
        /// 
        /// Can be used to determine:
        /// - How long the migration took: CompletedUtc - StartedUtc
        /// - How long since completion: DateTimeOffset.UtcNow - CompletedUtc
        /// </remarks>
        public DateTimeOffset? CompletedUtc { get; set; }

        /// <summary>
        /// The current lifecycle status of this job.
        /// </summary>
        /// <remarks>
        /// Begins as Queued, transitions through Running, and ends in a terminal state
        /// (Completed, CompletedDryRun, Failed, or Cancelled).
        /// 
        /// Status values indicate:
        /// - Queued: Waiting to be processed
        /// - Running: Actively copying or validating
        /// - Completed: Copy and validation succeeded; ready for connection switch
        /// - CompletedDryRun: Validation succeeded; no data was copied
        /// - Failed: Copy or validation failed; eligible for retry
        /// - Cancelled: Job was explicitly cancelled
        /// 
        /// Use GetJobAsync to poll the current status.
        /// </remarks>
        public WebsiteCopyJobStatus Status { get; set; } = WebsiteCopyJobStatus.Queued;

        /// <summary>
        /// Error message if the job failed, describing why the operation could not complete.
        /// </summary>
        /// <remarks>
        /// Null for Queued and Running jobs.
        /// Populated when Status becomes Failed.
        /// Cleared when the job is retried via RetryJobAsync.
        /// 
        /// Contains diagnostics such as:
        /// - "Destination database must be empty. Entity AspNetUser has 5 record(s)."
        /// - "Source connection not found."
        /// - "Another copy is already in progress for this website."
        /// - "Validation failed for AspNetRole: source count 3, destination count 2."
        /// - Database timeout, file access, or other operation-specific errors
        /// 
        /// Maximum length: 2048 characters.
        /// </remarks>
        [MaxLength(2048)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Human-readable status message describing the current operation phase or result.
        /// </summary>
        /// <remarks>
        /// Updated frequently during processing to indicate which stage is executing.
        /// Used for UI display to show users what is happening without exposing technical details.
        /// 
        /// Example messages:
        /// - "Queued" (initial state)
        /// - "Running preflight checks..." (5% progress)
        /// - "Preflight checks completed." (20% progress)
        /// - "Copying database..." (35% progress)
        /// - "Copying storage objects..." (65% progress)
        /// - "Validating copied data..." (85% progress)
        /// - "Copy completed successfully. Manual switch available." (100% progress)
        /// - "Job failed." (on failure)
        /// - "Dry-run completed successfully." (on dry-run completion)
        /// 
        /// Maximum length: 1024 characters.
        /// </remarks>
        [MaxLength(1024)]
        public string? LastMessage { get; set; }

        /// <summary>
        /// Percentage of job completion (0-100).
        /// </summary>
        /// <remarks>
        /// Updated in real-time as the job progresses through stages:
        /// - 0%: Queued or just started
        /// - 5%: Preflight checks running
        /// - 20%: Preflight checks completed
        /// - 35%: Database copy in progress (if MoveDatabase)
        /// - 65%: Storage copy in progress (if MoveStorage)
        /// - 85%: Validation in progress
        /// - 100%: Completed or failed
        /// 
        /// Note: Progress during storage copy is calculated dynamically
        /// (65% + (completed_files / total_files) * 15).
        /// 
        /// Consumers can use this for progress bars and UI indicators.
        /// </remarks>
        public int ProgressPercent { get; set; }

        /// <summary>
        /// Number of times this job has been attempted (including retries).
        /// </summary>
        /// <remarks>
        /// Incremented each time the job enters Running status.
        /// Starts at 0 and increments before the first attempt begins.
        /// 
        /// Tracked to enforce MaxAttempts limit and provide diagnostics.
        /// Example: If AttemptCount reaches MaxAttempts without success, retry is no longer allowed.
        /// 
        /// Used for:
        /// - Debugging recurring failures
        /// - Audit trail showing how many attempts were made
        /// - Determining if a job should be given up on (exhaust MaxAttempts)
        /// </remarks>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Maximum number of times the job is allowed to be attempted.
        /// </summary>
        /// <remarks>
        /// Defaults to 3, meaning the job can be retried up to 2 times (for 3 total attempts).
        /// 
        /// Used to prevent infinite retry loops on permanently failing operations.
        /// When AttemptCount reaches MaxAttempts without success, RetryJobAsync may be blocked
        /// (implementation detail; currently always allows retry).
        /// 
        /// Can be customized per job at creation time if different retry behavior is needed.
        /// </remarks>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Whether the database copy phase completed (all entities successfully copied).
        /// </summary>
        /// <remarks>
        /// Null/false until MoveDatabase copy phase completes.
        /// Set to true after database copy finishes without throwing exceptions.
        /// 
        /// Used to:
        /// - Resume jobs that partially completed (skip already-copied phases)
        /// - Report what aspects of the copy succeeded
        /// - Support pause/resume scenarios (though not currently implemented)
        /// 
        /// Set to true: Database copy succeeded
        /// Set to false/null: Database copy not yet completed or not required (MoveDatabase=false)
        /// </remarks>
        public bool DatabaseCopied { get; set; }

        /// <summary>
        /// Whether the storage copy phase completed (all files successfully copied).
        /// </summary>
        /// <remarks>
        /// Null/false until MoveStorage copy phase completes.
        /// Set to true after storage copy finishes without throwing exceptions.
        /// 
        /// Used to:
        /// - Resume jobs that partially completed (skip already-copied phases)
        /// - Report what aspects of the copy succeeded
        /// - Support pause/resume scenarios (though not currently implemented)
        /// 
        /// Set to true: Storage copy succeeded
        /// Set to false/null: Storage copy not yet completed or not required (MoveStorage=false)
        /// </remarks>
        public bool StorageCopied { get; set; }

        /// <summary>
        /// Whether the post-copy validation phase completed and passed.
        /// </summary>
        /// <remarks>
        /// Set to true when validation confirms:
        /// - Database: All entity types have matching record counts between source and destination
        /// - Storage: File counts match between source and destination
        /// - DryRun: All preflight checks passed (no actual data copied)
        /// 
        /// False/null: Validation not yet completed or failed.
        /// 
        /// Once set to true, the job is eligible for connection switching via ApplyConnectionSwitchAsync.
        /// Indicates the copy operation achieved its objective and destination data is trustworthy.
        /// </remarks>
        public bool ValidationCompleted { get; set; }

        /// <summary>
        /// Whether this job is currently locked (being processed or has an active semaphore).
        /// </summary>
        /// <remarks>
        /// Set to true when the job transitions to Running status.
        /// Set to false when the job reaches terminal status (Completed, Failed, CompletedDryRun).
        /// 
        /// The lock is enforced via a ConcurrentDictionary of SemaphoreSlim objects keyed by job ID.
        /// Only one ProcessJobAsync can hold the lock for a given job ID at a time.
        /// Prevents concurrent processing of the same job.
        /// 
        /// Used to:
        /// - Prevent duplicate processing if the same job is queued multiple times
        /// - Serialize phases of a single job
        /// - Provide status indication to consumers
        /// </remarks>
        public bool Locked { get; set; }
    }
}

