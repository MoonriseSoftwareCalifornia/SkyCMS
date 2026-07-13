using System;
using System.Security.Cryptography;

namespace AspNetCore.Identity.FlexDb
{
    internal static class ProviderNames
    {
        internal const string Cosmos = "Microsoft.EntityFrameworkCore.Cosmos";
        internal const string SqlServer = "Microsoft.EntityFrameworkCore.SqlServer";
        internal const string Sqlite = "Microsoft.EntityFrameworkCore.Sqlite";
        internal const string PomeloMySql = "Pomelo.EntityFrameworkCore.MySql";
        internal const string OracleMySql = "MySql.EntityFrameworkCore";

        internal static bool IsCosmos(string? providerName)
            => string.Equals(providerName, Cosmos, StringComparison.OrdinalIgnoreCase);

        internal static bool IsMySql(string? providerName)
            => !string.IsNullOrWhiteSpace(providerName)
               && (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(providerName, PomeloMySql, StringComparison.OrdinalIgnoreCase));
    }

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
            return RandomNumberGenerator.GetInt32(1, int.MaxValue);
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
                    if (ProviderNames.IsCosmos(strategy.ProviderName))
                    {
                        return "Cosmos";
                    }

                    if (string.Equals(strategy.ProviderName, ProviderNames.SqlServer, StringComparison.OrdinalIgnoreCase))
                    {
                        return "SQL Server";
                    }

                    if (ProviderNames.IsMySql(strategy.ProviderName))
                    {
                        return "MySQL";
                    }

                    if (string.Equals(strategy.ProviderName, ProviderNames.Sqlite, StringComparison.OrdinalIgnoreCase))
                    {
                        return "SQLite";
                    }
                }
            }

            return "Un-supported";
        }

    }
}
