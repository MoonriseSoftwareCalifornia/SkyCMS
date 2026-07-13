namespace Cosmos.MultiTenant.Administrator.Models
{
    public class ConnectionWebSiteViewModel
    {
        public Guid ConnectionId { get; set; } = Guid.Empty;

        public string WebsiteUrl { get; set; } = string.Empty;
    }
}
