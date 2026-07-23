using Cosmos.DynamicConfig;

namespace Cosmos.MultiTenant.Administrator.Services
{
    /// <summary>
    /// Defines the contract for orchestrating website copy operations from a source connection to a destination connection.
    /// </summary>
    /// <remarks>
    /// The <see cref="IWebsiteCopyOrchestrator"/> interface abstracts the website copy workflow, which includes:
    /// - Job initialization and queuing
    /// - Background processing of copy operations
    /// - Database and storage migration
    /// - Comprehensive validation of copied data
    /// - Connection switching after successful validation
    /// - Job status tracking and retry capabilities
    /// 
    /// Implementing classes are responsible for:
    /// - Managing the asynchronous copy job lifecycle
    /// - Ensuring data integrity through validation
    /// - Preventing concurrent copy operations on the same website
    /// - Maintaining progress tracking and error reporting
    /// - Persisting job state and metadata
    /// 
    /// The orchestrator supports flexible migration scenarios:
    /// - Database-only migration
    /// - Storage-only migration
    /// - Combined database and storage migration
    /// - Dry-run validation without actual data copying
    /// </remarks>
    public interface IWebsiteCopyOrchestrator
    {
        /// <summary>
        /// Starts a new website copy job asynchronously.
        /// </summary>
        /// <param name="job">
        /// The website copy job to start. This should contain:
        /// - SourceConnectionId: The connection to copy from
        /// - DestinationConnectionId (optional) or destination connection strings
        /// - MoveDatabase: Whether to copy the database
        /// - MoveStorage: Whether to copy storage objects
        /// - DryRun: Whether to validate without copying
        /// </param>
        /// <param name="cancellationToken">A cancellation token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the started job with
        /// initialized metadata including status (Queued), creation time, and progress tracking (0%).
        /// </returns>
        /// <remarks>
        /// This method initiates a website copy job and triggers background processing. Key behaviors:
        /// 
        /// Synchronous Actions:
        /// - Persists the job to the database
        /// - Sets initial status to Queued
        /// - Initializes progress to 0%
        /// - Records the creation timestamp
        /// - Returns immediately to the caller
        /// 
        /// Asynchronous Actions (background):
        /// - Runs the actual copy process in a background task
        /// - Performs preflight validation checks
        /// - Copies database and/or storage as configured
        /// - Validates the copy operation
        /// - Updates job status based on completion or failures
        /// 
        /// The job can be monitored using <see cref="GetJobAsync"/> to track progress and status updates.
        /// If the operation fails, it can be retried using <see cref="RetryJobAsync"/>.
        /// </remarks>
        Task<WebsiteCopyJob> StartJobAsync(WebsiteCopyJob job, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the current state of a website copy job by its identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the copy job to retrieve.</param>
        /// <param name="cancellationToken">A cancellation token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the job if found;
        /// otherwise, null. The returned job includes current status, progress percentage, error messages,
        /// and other tracking metadata.
        /// </returns>
        /// <remarks>
        /// This method allows callers to monitor the progress of a copy operation in real-time. The returned
        /// job object contains:
        /// 
        /// Status Information:
        /// - Status: Current state (Queued, Running, Completed, CompletedDryRun, Failed)
        /// - ProgressPercent: Completion percentage (0-100)
        /// - LastMessage: Human-readable status message
        /// - ErrorMessage: Error details if the job failed
        /// 
        /// Timing Information:
        /// - CreatedUtc: When the job was created
        /// - StartedUtc: When processing began (or null if not yet started)
        /// - CompletedUtc: When processing finished (or null if still running)
        /// 
        /// Processing Information:
        /// - AttemptCount: Number of attempts made
        /// - DatabaseCopied: Whether database copy completed
        /// - StorageCopied: Whether storage copy completed
        /// - ValidationCompleted: Whether validation has been performed
        /// - Locked: Whether the job is currently locked (in progress)
        /// 
        /// This method can be called repeatedly to poll job progress without blocking.
        /// </remarks>
        Task<WebsiteCopyJob?> GetJobAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retries a previously failed or incomplete website copy job.
        /// </summary>
        /// <param name="id">The unique identifier of the job to retry.</param>
        /// <param name="cancellationToken">A cancellation token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is true if the retry was successfully
        /// queued; false if the job was not found, is currently running, or cannot be retried.
        /// </returns>
        /// <remarks>
        /// This method enables recovery from transient failures by requeuing a failed job for reprocessing.
        /// 
        /// Success Conditions:
        /// - Job exists in the system
        /// - Job is not currently in Running state
        /// - Job can be safely requeued
        /// 
        /// Upon successful retry:
        /// - Job status is reset to Queued
        /// - Previous error messages are cleared
        /// - A fresh background processing attempt is initiated
        /// - The AttemptCount is incremented (by the background processor)
        /// 
        /// Returns false in the following scenarios:
        /// - Job with the specified ID does not exist
        /// - Job is currently in Running state (prevents concurrent execution)
        /// - Other conditions prevent safe requeuing
        /// 
        /// Note: Completed jobs can be retried, as can jobs with failed or partial completion status.
        /// Only jobs actively running (RunningStatus) cannot be retried to prevent concurrent execution.
        /// </remarks>
        Task<bool> RetryJobAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies the connection switch to the source connection after a copy job completes successfully.
        /// </summary>
        /// <param name="id">The unique identifier of the completed copy job.</param>
        /// <param name="cancellationToken">A cancellation token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is true if the connection switch
        /// was successfully applied; false if the job was not found, incomplete, or has missing connection data.
        /// </returns>
        /// <remarks>
        /// This method performs a critical operation: switching the source website's connection strings to point
        /// to the copied destination resources. This is a manual, explicit operation that requires the job to be
        /// in Completed status.
        /// 
        /// Prerequisites for Success:
        /// - Job must exist in the system
        /// - Job must have Completed status (not Running, Failed, or other states)
        /// - Source connection must exist in the database
        /// - Destination connection strings must be available in the job
        /// 
        /// Connection Update Logic:
        /// - If MoveDatabase is true and a destination database connection is available,
        ///   the source connection's DbConn is updated
        /// - If MoveStorage is true and a destination storage connection is available,
        ///   the source connection's StorageConn is updated
        /// - Both properties can be updated in a single operation if both are configured
        /// 
        /// Returns false in the following scenarios:
        /// - Job with the specified ID does not exist
        /// - Job status is not Completed
        /// - Source connection cannot be found
        /// - Neither database nor storage connection strings are provided for the configured move operations
        /// 
        /// Important Considerations:
        /// - This is a manual operation that should only be executed after validation confirms the copy is successful
        /// - The switch is point-in-time; subsequent connections will use the new destinations
        /// - Consider notifying stakeholders before executing this operation in production
        /// - The operation is not reversible from this interface; maintain backups if needed
        /// </remarks>
        Task<bool> ApplyConnectionSwitchAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
