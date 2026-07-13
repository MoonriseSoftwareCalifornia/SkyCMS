using Cosmos.MultiTenant.Administrator.Data;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.MultiTenant.Administrator.Models
{
    public class WebsiteAuthorsViewModel
    {
        /// <summary>
        /// Constructor to initialize a new instance of the StoryAuthorsAndWebsitesViewModel class.
        /// </summary>
        public WebsiteAuthorsViewModel()
        {
            Id = Guid.NewGuid();
        }

        /// <summary>
        /// Initializes a new instance of the StoryAuthorsAndWebsitesViewModel class with the specified entity and connections.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="connections"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public WebsiteAuthorsViewModel(WebsiteAuthor entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "StoryAuthorsAndWebsites cannot be null");
            }

            Id = entity.Id;
            ConnectionId = entity.ConnectionId;

            // Fix for CS0029: Convert List<string> to a single string (e.g., comma-separated values)
            EmailAddress = entity.EmailAddress;

            // Fix for CS0029: Convert List<Path> to a single string (e.g., JSON or delimited format)
            Path = entity.Path; // Adjust serialization as needed

            WebsiteUrl = entity.WebsiteUrl;
            TemplateName = entity.TemplateName;
            TemplateId = entity.TemplateId ?? Guid.Empty; // Handle nullable Guid
        }

        /// <summary>
        /// Gets or sets the unique identifier for the story author and website entry.
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the connection identifier associated with the entity.
        /// </summary>
        [Required]
        public Guid ConnectionId { get; set; }

        [Display(Name = "Website URL")]
        [DataType(DataType.Url)]
        [Required(AllowEmptyStrings = false)]
        public string WebsiteUrl { get; set; } = string.Empty;

        [Display(Name = "Page Template")]
        [Required]
        public Guid TemplateId { get; set; }

        [Display(Name = "Page Template")]
        [Required(AllowEmptyStrings = false)]
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the email address associated with the story author.
        /// </summary>
        [Display(Name = "Author Email")]
        [DataType(DataType.EmailAddress)]
        [Required(AllowEmptyStrings = false)]
        public string EmailAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the paths associated with the story author and website.
        /// </summary>
        [Display(Name = "Publishing Path")]
        [Required(AllowEmptyStrings = false)]
        public string Path { get; set; } = string.Empty;


    }
}
