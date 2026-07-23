using Cosmos.BlobService;
using Cosmos.DynamicConfig;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.MultiTenant.Administrator.Models
{
    /// <summary>
    /// View model for the connection settings.
    /// </summary>
    public class ConnectionViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionViewModel"/> class.
        /// </summary>
        public ConnectionViewModel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionViewModel"/> class with the specified connection.
        /// </summary>
        /// <param name="connection"></param>
        public ConnectionViewModel(Connection connection)
        {
            Id = connection.Id;
            DomainNames = connection.DomainNames == null ? string.Empty : string.Join(", ", connection.DomainNames);
            DbConn = connection.DbConn;
            StorageConn = connection.StorageConn;
            Customer = connection.Customer;
            ResourceGroup = connection.ResourceGroup;
            PublisherMode = connection.PublisherMode;
            WebsiteUrl = connection.WebsiteUrl;
            OwnerEmail = connection.OwnerEmail;
        }

        /// <summary>
        /// Gets or sets the unique identifier of the connection.
        /// </summary>
        [Key]
        [Display(Name = "ID")]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the database provider name.
        /// </summary>
        public string DatabaseProvider { 
            get
            {
                if (DbConn == null || DbConn == string.Empty)
                {
                    return string.Empty;
                }
                return AspNetCore.Identity.FlexDb.Utilities.InferDatabaseProviderShortName(DbConn);
            }
        }

        /// <summary>
        /// Gets or sets the blob storage provider name.
        /// </summary>
        public string BlobStorageProvider
        {
            get
            {
                if (StorageConn == null || StorageConn == string.Empty)
                {
                    return string.Empty;
                }
                var provider = ConnectionStringParser.DetermineProvider(StorageConn);
                return ConnectionStringParser.GetProviderName(provider);
            }
        }

        /// <summary>
        /// Gets or sets the editor domain name of the connection.
        /// </summary>
        [Display(Name = "Editor Domain Names")]
        public string? DomainNames { get; set; } = null!;

        /// <summary>
        /// Gets or sets the database connection string.
        /// </summary>
        [Display(Name = "Database Connection String")]
        public string? DbConn { get; set; } = null!;

        /// <summary>
        /// Gets or sets the storage connection string.
        /// </summary>
        [Display(Name = "Storage Connection String")]
        public string? StorageConn { get; set; } = null!;

        /// <summary>
        /// Gets or sets the customer name.
        /// </summary>
        [Display(Name = "Customer  or Connection Name")]
        [Required(AllowEmptyStrings = false)]
        public string? Customer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resrouce group where the customer's resources are kept.
        /// </summary>
        [Display(Name = "Customer Resource Group")]
        public string? ResourceGroup { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the publisher mode.
        /// </summary>
        [AllowedValues("Static", "Decoupled", "Headless", "Hybrid", "Static-dynamic", "")]
        public string? PublisherMode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the website URL.
        /// </summary>
        [Url]
        [Display(Name = "Website URL")]
        public string? WebsiteUrl { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Owner Email Address")]
        public string? OwnerEmail { get; set; } = string.Empty;

        /// <summary>
        /// Converts the view model to a <see cref="Connection"/> object.
        /// </summary>
        /// <returns></returns>
        internal Connection ToConnection()
        {
            return new Connection
            {
                Id = Id,
                DomainNames = GetDomainNames(DomainNames),
                DbConn = DbConn ?? string.Empty,
                StorageConn = StorageConn ?? string.Empty,
                Customer = Customer ?? string.Empty,
                ResourceGroup = ResourceGroup ?? string.Empty,
                PublisherMode = PublisherMode ?? string.Empty,
                WebsiteUrl = WebsiteUrl ?? string.Empty,
                OwnerEmail = OwnerEmail  // Preserve null/empty instead of invalid default
            };
        }

        private static string[] GetDomainNames(string? domainNames)
        {
            return domainNames == null || domainNames == string.Empty ? new string[0] : domainNames.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()).ToArray();
        }

        //private static string GetDatabaseName(string connectionString)
        //{
        //    return connectionString
        //        .Split(';', StringSplitOptions.RemoveEmptyEntries)
        //        .FirstOrDefault(s => s.StartsWith("Database=", StringComparison.InvariantCultureIgnoreCase))
        //        ?.Split('=', 2)[1]
        //        ?? "cosmoscms";
        //}
    }
}
