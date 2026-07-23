using System.ComponentModel.DataAnnotations;
using Cosmos.DynamicConfig;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cosmos.MultiTenant.Administrator.Models
{
    /// <summary>
    /// Represents user input and UI state for starting a website copy job.
    /// </summary>
    /// <remarks>
    /// This model is used by the website copy start page to:
    /// <list type="bullet">
    /// <item><description>Select source and destination connections.</description></item>
    /// <item><description>Choose whether to copy database data, storage objects, or both.</description></item>
    /// <item><description>Support either an existing destination connection or ad-hoc destination connection strings.</description></item>
    /// <item><description>Optionally run validation-only copy preflight via dry-run mode.</description></item>
    /// <item><description>Display available connection options and the most recent job status.</description></item>
    /// </list>
    /// </remarks>
    public class WebsiteCopyStartViewModel : IValidatableObject
    {
        /// <summary>
        /// Gets or sets the unique identifier of the source website connection to copy from.
        /// </summary>
        /// <remarks>
        /// This field is required. The selected connection provides the source database and storage connection values.
        /// </remarks>
        [Required]
        [Display(Name = "Source Website")]
        public Guid SourceConnectionId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of an existing destination connection.
        /// </summary>
        /// <remarks>
        /// When <see cref="UseExistingDestination"/> is <see langword="true"/>, this value identifies the managed destination connection.
        /// </remarks>
        [Display(Name = "Destination Connection")]
        public Guid? DestinationConnectionId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether database content should be copied.
        /// </summary>
        /// <remarks>
        /// Defaults to <see langword="true"/> so database data is included unless explicitly disabled.
        /// </remarks>
        [Display(Name = "Copy Database")]
        public bool MoveDatabase { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether storage files/blobs should be copied.
        /// </summary>
        /// <remarks>
        /// Defaults to <see langword="true"/> so storage objects are included unless explicitly disabled.
        /// </remarks>
        [Display(Name = "Copy Storage")]
        public bool MoveStorage { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to use a preconfigured destination connection.
        /// </summary>
        /// <remarks>
        /// If <see langword="true"/>, the job uses <see cref="DestinationConnectionId"/>.
        /// If <see langword="false"/>, the job can use ad-hoc values from
        /// <see cref="DestinationDbConn"/> and <see cref="DestinationStorageConn"/>.
        /// </remarks>
        [Display(Name = "Use Existing Destination Connection")]
        public bool UseExistingDestination { get; set; } = true;

        /// <summary>
        /// Gets or sets the destination database connection string for ad-hoc destination mode.
        /// </summary>
        /// <remarks>
        /// This value is typically used when <see cref="UseExistingDestination"/> is <see langword="false"/>
        /// and database copy is enabled.
        /// </remarks>
        [Display(Name = "Destination Database Connection")]
        public string? DestinationDbConn { get; set; }

        /// <summary>
        /// Gets or sets the destination storage connection string for ad-hoc destination mode.
        /// </summary>
        /// <remarks>
        /// This value is typically used when <see cref="UseExistingDestination"/> is <see langword="false"/>
        /// and storage copy is enabled.
        /// </remarks>
        [Display(Name = "Destination Storage Connection")]
        public string? DestinationStorageConn { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation should run as validation-only.
        /// </summary>
        /// <remarks>
        /// When enabled, the system performs preflight checks and validation flow without persisting copied data as a completed migration.
        /// </remarks>
        [Display(Name = "Dry Run (validate only)")]
        public bool DryRun { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether existing destination data can be removed before copy.
        /// </summary>
        /// <remarks>
        /// When enabled for a non-dry-run job, destination storage is cleared and destination database
        /// schema data is removed before copy starts.
        /// </remarks>
        [Display(Name = "Allow Destination Overwrite")]
        public bool AllowDestinationOverwrite { get; set; }

        /// <summary>
        /// Gets or sets the selectable source connection items for the UI.
        /// </summary>
        public List<SelectListItem> SourceConnections { get; set; } = new();

        /// <summary>
        /// Gets or sets the selectable destination connection items for the UI.
        /// </summary>
        public List<SelectListItem> DestinationConnections { get; set; } = new();

        /// <summary>
        /// Gets or sets the most recent website copy job associated with the current source selection.
        /// </summary>
        /// <remarks>
        /// Used to display last-run status/progress details on the page after submission or refresh.
        /// </remarks>
        public WebsiteCopyJob? LastJob { get; set; }

        /// <summary>
        /// Validates cross-field constraints for the copy request.
        /// </summary>
        /// <param name="validationContext">The validation context for the current model instance.</param>
        /// <returns>
        /// A sequence of validation failures. Empty when the model is valid.
        /// </returns>
        /// <remarks>
        /// Ensures the source and destination are not the same connection when a destination connection ID is provided.
        /// </remarks>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DestinationConnectionId.HasValue && SourceConnectionId == DestinationConnectionId.Value)
            {
                yield return new ValidationResult(
                    "Source and destination connections cannot be the same.",
                    new[] { nameof(DestinationConnectionId) });
            }
        }
    }
}
