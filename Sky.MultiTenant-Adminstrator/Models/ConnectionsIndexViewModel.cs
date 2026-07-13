using Cosmos.DynamicConfig;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.MultiTenant.Administrator.Models
{
    public class ConnectionsIndexViewModel : Connection
    {
        public ConnectionsIndexViewModel() { }

        public ConnectionsIndexViewModel(Connection connection)
        {
            this.Id = connection.Id;
            this.DomainNames = connection.DomainNames;
            this.StorageConn = connection.StorageConn;
            this.DbConn = connection.DbConn;
            this.DbName = GetDatabaseName(connection.DbConn);
            this.PublisherMode = connection.PublisherMode;
            this.Customer = connection.Customer;
            this.ResourceGroup = connection.ResourceGroup;
            this.WebsiteUrl = connection.WebsiteUrl;
            this.OwnerEmail = connection.OwnerEmail;
        }

        /// <summary>
        /// Indicates if the database connection is OK.
        /// </summary>
        [Display(Name = "Database Name")]
        public string DbName { get; set; } = "cosmoscms";

        [Display(Name = "Database")]
        public bool DatabaseStatus { get; set; } = false;

        /// <summary>
        /// Indicates if the storage connection is OK.
        /// </summary>
        [Display(Name = "Storage")]
        public bool StorageStatus { get; set; } = false;

        private static string GetDatabaseName(string connectionString)
        {
            return connectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(s => s.StartsWith("Database=", StringComparison.InvariantCultureIgnoreCase))
                ?.Split('=', 2)[1]
                ?? "cosmoscms";
        }
    }
}
