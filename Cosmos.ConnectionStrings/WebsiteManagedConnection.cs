using System.ComponentModel.DataAnnotations;

namespace Cosmos.DynamicConfig
{
    /// <summary>
    /// Connection metadata for website copy workflows.
    /// </summary>
    public class WebsiteManagedConnection : Connection
    {
        /// <summary>
        /// Gets or sets the website connection this managed connection is currently associated with.
        /// </summary>
        public Guid? WebsiteConnectionId { get; set; }

        /// <summary>
        /// Gets or sets when content was last copied to this destination.
        /// </summary>
        public DateTimeOffset? LastCopiedUtc { get; set; }

        /// <summary>
        /// Gets or sets when this destination was last validated.
        /// </summary>
        public DateTimeOffset? LastValidatedUtc { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this destination is known to be empty.
        /// </summary>
        public bool IsKnownEmpty { get; set; } = true;

        [MaxLength(1024)]
        public string? LastValidationSummary { get; set; }
    }
}
