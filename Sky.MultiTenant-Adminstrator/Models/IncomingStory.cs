using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace Cosmos.MultiTenant.Administrator.Models
{
    public class IncomingStory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ConnectionId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string AuthorEmailAddress { get; set; } = string.Empty;

        public DateTimeOffset Received { get; set; } = DateTimeOffset.UtcNow;

        public List <StoryAttachment> Attachments { get; set; } = new List<StoryAttachment>();
    }
}