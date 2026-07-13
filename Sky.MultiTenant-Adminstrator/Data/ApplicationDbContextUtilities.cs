using Cosmos.Common.Data;
using Cosmos.DynamicConfig;
using Microsoft.EntityFrameworkCore;

namespace Cosmos.MultiTenant.Administrator.Data
{
    /// <summary>
    /// Utilities for the ApplicationDbContext class.
    /// </summary>
    internal static class ApplicationDbContextUtilities
    {
        /// <summary>
        /// Get an ApplicationDbContext from a connection.
        /// </summary>
        /// <param name="connection">Website connection.</param>
        /// <returns></returns>
        internal static ApplicationDbContext GetApplicationDbContext(Connection connection)
        {
            DbContextOptionsBuilder<ApplicationDbContext> dbContextOptionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            dbContextOptionsBuilder.UseCosmos(connection.DbConn, GetDatabaseName(connection.DbConn));
            return new ApplicationDbContext(dbContextOptionsBuilder.Options);
        }

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
