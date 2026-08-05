using System.Security.Claims;
using Cosmos.DynamicConfig;
using Cosmos.MultiTenant.Administrator.Models;
using Cosmos.MultiTenant.Administrator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cosmos.MultiTenant.Administrator.Controllers
{
    /// <summary>
    /// Handles website copy and migration operations for multi-tenant administration.
    /// Provides functionality to copy website configurations, databases, and storage content
    /// from a source connection to a destination.
    /// </summary>
    /// <remarks>
    /// This controller enforces strict validation including destination-empty checks and
    /// requires proper Microsoft Graph API scopes for all operations. Connection switches
    /// remain manual-only after validation to ensure data integrity.
    /// </remarks>
    public class WebsiteCopyController : Controller
    {
        private readonly DynamicConfigDbContext configDb;
        private readonly IWebsiteCopyOrchestrator orchestrator;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebsiteCopyController"/> class.
        /// </summary>
        /// <param name="configDb">The dynamic configuration database context.</param>
        /// <param name="orchestrator">The website copy orchestrator service.</param>
        public WebsiteCopyController(DynamicConfigDbContext configDb, IWebsiteCopyOrchestrator orchestrator)
        {
            this.configDb = configDb;
            this.orchestrator = orchestrator;
        }

        /// <summary>
        /// Displays the website copy start page with available source and destination connections.
        /// </summary>
        /// <returns>A view containing the website copy form with source and destination options.</returns>
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Index()
        {
            var model = await BuildStartViewModelAsync();
            return View(model);
        }

        /// <summary>
        /// Initiates a website copy job with the specified configuration.
        /// </summary>
        /// <param name="model">The website copy start parameters including source, destination, and copy options.</param>
        /// <returns>
        /// If validation fails, returns the Index view with validation errors.
        /// If validation succeeds, redirects to the Details view for the created job.
        /// </returns>
        /// <remarks>
        /// Validates that at least one of database or storage copy is requested, ensures a destination
        /// is specified (either existing or via connection strings), and records the user who initiated
        /// the job via their email or username claim. The job is created with automatic destination
        /// overwrite behavior as configured and dry-run mode as requested.
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Start(WebsiteCopyStartViewModel model)
        {
            if (!model.MoveDatabase && !model.MoveStorage)
            {
                ModelState.AddModelError(string.Empty, "Select database, storage, or both.");
            }

            if (model.UseExistingDestination && !model.DestinationConnectionId.HasValue)
            {
                ModelState.AddModelError(nameof(model.DestinationConnectionId), "Select a destination connection.");
            }

            if (!model.UseExistingDestination)
            {
                if (model.MoveDatabase && string.IsNullOrWhiteSpace(model.DestinationDbConn))
                {
                    ModelState.AddModelError(nameof(model.DestinationDbConn), "Destination database connection is required.");
                }

                if (model.MoveStorage && string.IsNullOrWhiteSpace(model.DestinationStorageConn))
                {
                    ModelState.AddModelError(nameof(model.DestinationStorageConn), "Destination storage connection is required.");
                }
            }

            if (!ModelState.IsValid)
            {
                var rebuilt = await BuildStartViewModelAsync(model);
                return View("Index", rebuilt);
            }

            var destination = model.UseExistingDestination
                ? await configDb.Connections.FirstOrDefaultAsync(x => x.Id == model.DestinationConnectionId)
                : null;

            if (!destination.AllowSetup)
            {
                ModelState.AddModelError(string.Empty, "Destination connection is not allowed for overwrite.");
                var rebuilt = await BuildStartViewModelAsync(model);
                return View("Index", rebuilt);
            }

            var job = new WebsiteCopyJob
            {
                SourceConnectionId = model.SourceConnectionId,
                DestinationConnectionId = destination?.Id,
                DestinationDbConn = destination?.DbConn ?? model.DestinationDbConn,
                DestinationStorageConn = destination?.StorageConn ?? model.DestinationStorageConn,
                CopyDatabase = model.MoveDatabase,
                CopyStorage = model.MoveStorage,
                DryRun = model.DryRun,
                AllowDestinationOverwrite = model.AllowDestinationOverwrite,
                UpdateConnectionOnSuccess = false,
                StartedBy = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("preferred_username") ?? User.Identity?.Name
            };

            var created = await orchestrator.StartJobAsync(job);
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }

        /// <summary>
        /// Displays detailed information about a specific website copy job.
        /// </summary>
        /// <param name="id">The unique identifier of the website copy job.</param>
        /// <returns>
        /// The Details view with job information and associated source/destination connections
        /// if the job exists; otherwise returns a Not Found result.
        /// </returns>
        /// <remarks>
        /// Shows current job status, progress percentage, and retrieved source/destination
        /// connection information for user visibility and manual switch application.
        /// </remarks>
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Details(Guid id)
        {
            var job = await orchestrator.GetJobAsync(id);
            if (job == null)
            {
                return NotFound();
            }

            var model = new WebsiteCopyDetailsViewModel
            {
                Job = job,
                SourceConnection = await configDb.Connections.FirstOrDefaultAsync(x => x.Id == job.SourceConnectionId),
                DestinationConnection = job.DestinationConnectionId.HasValue
                    ? await configDb.Connections.FirstOrDefaultAsync(x => x.Id == job.DestinationConnectionId.Value)
                    : null
            };

            return View(model);
        }

        /// <summary>
        /// Retries a previously failed website copy job.
        /// </summary>
        /// <param name="id">The unique identifier of the website copy job to retry.</param>
        /// <returns>Redirects back to the Details view for the retried job.</returns>
        /// <remarks>
        /// Attempts to retry the job up to the configured maximum attempt count.
        /// The job status and progress will be updated via the orchestrator service.
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Retry(Guid id)
        {
            await orchestrator.RetryJobAsync(id);
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Applies the connection switch for a completed website copy job.
        /// </summary>
        /// <param name="id">The unique identifier of the website copy job.</param>
        /// <returns>Redirects back to the Details view for the job.</returns>
        /// <remarks>
        /// This operation is manual-only and should only be performed after the job
        /// has completed successfully. It switches the website connection to use the
        /// destination resources (database and/or storage) as configured in the job.
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> ApplySwitch(Guid id)
        {
            await orchestrator.ApplyConnectionSwitchAsync(id);
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Retrieves the current progress information for a website copy job.
        /// </summary>
        /// <param name="id">The unique identifier of the website copy job.</param>
        /// <returns>
        /// A JSON object containing the job status, progress percentage, last message,
        /// error message (if any), attempt count, and maximum attempts if the job exists;
        /// otherwise returns a Not Found result.
        /// </returns>
        /// <remarks>
        /// This endpoint is typically called via AJAX polling on the Details page
        /// to update the UI with real-time job progress information.
        /// </remarks>
        [HttpGet]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Progress(Guid id)
        {
            var job = await orchestrator.GetJobAsync(id);
            if (job == null)
            {
                return NotFound();
            }

            return Json(new
            {
                job.Status,
                job.ProgressPercent,
                job.LastMessage,
                job.ErrorMessage,
                job.AttemptCount,
                job.MaxAttempts
            });
        }

        /// <summary>
        /// Displays a list of all website copy jobs with their current status and action options.
        /// </summary>
        /// <returns>A view containing a table of all copy jobs sorted by most recent first.</returns>
        /// <remarks>
        /// Shows all jobs regardless of status, including Queued, Running, Completed, Failed, and other states.
        /// Jobs are sorted by creation date (most recent first) for easy tracking and management.
        /// Provides inline delete buttons for completed and failed jobs.
        /// </remarks>
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Jobs()
        {
            var jobs = await configDb.WebsiteCopyJobs
                .OrderByDescending(x => x.CreatedUtc)
                .ToListAsync();

            return View(jobs);
        }

        /// <summary>
        /// Displays a confirmation page before deleting a website copy job.
        /// </summary>
        /// <param name="id">The unique identifier of the job to delete.</param>
        /// <returns>
        /// The confirmation view with job details if the job exists; otherwise returns a Not Found result.
        /// </returns>
        /// <remarks>
        /// This view allows the user to confirm deletion of a job. The deletion is permanent and cannot be undone.
        /// Only completed and failed jobs can be deleted; jobs in other states must be retried or resolved first.
        /// </remarks>
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> DeleteJob(Guid id)
        {
            var job = await configDb.WebsiteCopyJobs.FirstOrDefaultAsync(x => x.Id == id);
            if (job == null)
            {
                return NotFound();
            }

            return View(job);
        }

        /// <summary>
        /// Permanently deletes a website copy job from the system.
        /// </summary>
        /// <param name="id">The unique identifier of the job to delete.</param>
        /// <returns>Redirects back to the Jobs list after deletion.</returns>
        /// <remarks>
        /// This method permanently removes the job record from the database.
        /// Once deleted, the job cannot be recovered. This is useful for cleaning up old,
        /// failed, or completed jobs that are no longer needed for reference.
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> ConfirmDeleteJob(Guid id)
        {
            var job = await configDb.WebsiteCopyJobs.FirstOrDefaultAsync(x => x.Id == id);
            if (job != null)
            {
                configDb.WebsiteCopyJobs.Remove(job);
                await configDb.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Jobs));
        }

        /// <summary>
        /// Builds the website copy start view model with current connections and job history.
        /// </summary>
        /// <param name="current">
        /// Optional existing model to populate. If null, a new instance is created.
        /// </param>
        /// <returns>
        /// A populated WebsiteCopyStartViewModel containing lists of available source
        /// and destination connections (ordered by website URL) and the most recent job.
        /// </returns>
        /// <remarks>
        /// This helper method retrieves all connections from the configuration database
        /// and formats them as select list items showing owner email and website URL.
        /// It also retrieves the most recent copy job for display purposes.
        /// </remarks>
        private async Task<WebsiteCopyStartViewModel> BuildStartViewModelAsync(WebsiteCopyStartViewModel? current = null)
        {
            current ??= new WebsiteCopyStartViewModel();
            var connections = await configDb.Connections.OrderBy(x => x.WebsiteUrl).ToListAsync();

            current.SourceConnections = connections
                .Select(x => new SelectListItem($"{x.Customer} - {x.WebsiteUrl}", x.Id.ToString()))
                .ToList();

            current.DestinationConnections = connections
                .Select(x => new SelectListItem($"{x.Customer} - {x.WebsiteUrl}", x.Id.ToString()))
                .ToList();

            current.LastJob = await configDb.WebsiteCopyJobs
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync();

            return current;
        }
    }
}
