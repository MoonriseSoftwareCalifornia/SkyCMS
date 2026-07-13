namespace Cosmos.MultiTenant.Administrator.Models
{
    public class StoryDeskConfig
    {
        public string TenantId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Mailbox { get; set; } = string.Empty;
    }
}
