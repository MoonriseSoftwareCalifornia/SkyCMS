using Cosmos.DynamicConfig;

namespace Cosmos.MultiTenant.Administrator.Models
{
    public class WebsiteCopyDetailsViewModel
    {
        public required WebsiteCopyJob Job { get; set; }

        public Connection? SourceConnection { get; set; }

        public Connection? DestinationConnection { get; set; }
    }
}
