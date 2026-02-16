using System;
using System.Threading;

namespace AspNetCore.Identity.FlexDb
{
    /// <summary>
    /// Utility methods for the FlexDb library.
    /// </summary>
    public static class Utilities
    {
        /// <summary>
        /// Generates a random integer. This is used to create unique database names for testing purposes.
        /// </summary>
        /// <returns></returns>
        internal static int GenerateRandomInt()
        {
            Thread.Sleep(20); // Ensure that the seed changes.
            var rand = new Random();
            return rand.Next(1, int.MaxValue);
        }

        /// <summary>
        /// Gets the name of the database provider based on the connection string.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static string InferDatabaseProvider(string connectionString)
        {
            var strategies = CosmosDbOptionsBuilder.GetDefaultStrategies();

            foreach (var strategy in strategies)
            {
                if (strategy.CanHandle(connectionString))
                {
                    return strategy.ProviderName;
                }
            }

            return "Un-supported.";
        }

        /// <summary>
        /// Gets the short name of the database provider based on the connection string.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static string InferDatabaseProviderShortName(string connectionString)
        {
            var strategies = CosmosDbOptionsBuilder.GetDefaultStrategies();

            foreach (var strategy in strategies)
            {
                if (strategy.CanHandle(connectionString))
                {
                    if (strategy.ProviderName.Contains("Cosmos"))
                    { return "Cosmos"; }
                    else if (strategy.ProviderName.Contains("SqlServer"))
                    { return "SQL Server"; }
                    else if (strategy.ProviderName.Contains("MySql"))
                    { return "MySQL"; }
                    else if (strategy.ProviderName.Contains("PostgreSql"))
                    { return "PostgreSQL"; }
                }
            }

            return "Un-supported";
        }
    }
}
