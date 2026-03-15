namespace Cosmos.Editor.Services
{
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Identity;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for multi-database management utilities.
    /// </summary>
    public interface IMultiDatabaseManagementUtilities
    {
        /// <summary>
        /// Gets a value indicating whether the application is configured for multi-tenancy.
        /// </summary>
        bool IsMultiTenant { get; }

        /// <summary>
        /// Retrieves domains associated with an email address across all configured databases.
        /// </summary>
        /// <param name="emailAddress">Email address to search.</param>
        /// <returns>List of domains where email address was found.</returns>
        Task<List<string>> GetDomainsByEmailAddress(string emailAddress);

        /// <summary>
        /// Updates the identity user across all Cosmos DB connections where the user exists.
        /// </summary>
        /// <param name="identityUser">Identity user entity.</param>
        /// <returns>Task.</returns>
        Task UpdateIdentityUser(IdentityUser identityUser);

        /// <summary>
        /// Retrieves all connections from the dynamic configuration database.
        /// </summary>
        /// <returns>Gets the complete list of connections.</returns>
        Task<List<Connection>> GetConnections();

        /// <summary>
        /// Ensures that all databases are configured and created if they do not exist.
        /// </summary>
        /// <returns>Task.</returns>
        Task EnsureDatabasesAreConfigured();
    }
}
