using System.ComponentModel.DataAnnotations;

namespace Cosmos.MultiTenant.Administrator.Data
{
    public class WebsiteAuthor
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ConnectionId { get; set; }

        public required string WebsiteUrl { get; set; }

        public required string EmailAddress { get; set; }

        public required string Path { get; set; }

        public Guid? TemplateId { get; set; }

        public required string TemplateName { get; set; }
    }
}