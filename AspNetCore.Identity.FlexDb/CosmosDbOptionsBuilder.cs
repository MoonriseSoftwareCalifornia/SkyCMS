using AspNetCore.Identity.FlexDb.Strategies;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AspNetCore.Identity.FlexDb
{
    /// <summary>
    /// Provides automatic database provider configuration based on connection strings.
    /// </summary>
    public class CosmosDbOptionsBuilder : DbContextOptionsBuilder
    {
        private static readonly List<IDatabaseConfigurationStrategy> DefaultStrategies = new List<IDatabaseConfigurationStrategy>
        {
            new CosmosDbConfigurationStrategy(),
            new SqlServerConfigurationStrategy(),
            new MySqlConfigurationStrategy(),
            new SqliteConfigurationStrategy(),
        };

        /// <summary>
        /// Automatically builds <see cref="DbContextOptions{TContext}"/> for the appropriate database provider based on the connection string.
        /// </summary>
        /// <typeparam name="TContext">The DbContext type.</typeparam>
        /// <param name="connectionString">The connection string.</param>
        /// <returns>Configured DbContextOptions.</returns>
        /// <remarks>
        /// <para>This method inspects the provided connection string to determine the appropriate database provider to use. Here are example connection strings:</para>
        /// <para><b>Cosmos DB:</b> AccountEndpoint=https://{Your Cosmos account DNS name}:443/;AccountKey={Your Key};Database={Your database name};</para>
        /// <para><b>SQL Server:</b> Server=tcp:{your_server}.database.windows.net,1433;Initial Catalog={your_database};User ID={your_user};Password={your_password};</para>
        /// <para><b>MySQL:</b> Server={your_server};Port=3306;uid={your_user};pwd={your_password};database={your_database};</para>
        /// <para><b>SQLite (Encrypted):</b> Data Source=/data/localdev.db;Password=yourpassword;</para>
        /// </remarks>
        public static DbContextOptions<TContext> GetDbOptions<TContext>(string connectionString)
            where TContext : DbContext
        {
            return GetDbOptionsBuilder<TContext>(connectionString).Options;
        }

        /// <summary>
        /// Automatically builds <see cref="DbContextOptionsBuilder{TContext}"/> for the appropriate database provider based on the connection string.
        /// </summary>
        /// <typeparam name="TContext">The DbContext type.</typeparam>
        /// <param name="connectionString">The connection string.</param>
        /// <returns>Configured DbContextOptionsBuilder.</returns>
        /// <remarks>
        /// <para>This method inspects the provided connection string to determine the appropriate database provider to use. Here are example connection strings:</para>
        /// <para><b>Cosmos DB:</b> AccountEndpoint=https://{Your Cosmos account DNS name}:443/;AccountKey={Your Key};Database={Your database name};</para>
        /// <para><b>SQL Server:</b> Server=tcp:{your_server}.database.windows.net,1433;Initial Catalog={your_database};User ID={your_user};Password={your_password};</para>
        /// <para><b>MySQL:</b> Server={your_server};Port=3306;uid={your_user};pwd={your_password};database={your_database};</para>
        /// <para><b>SQLite (Encrypted):</b> Data Source=/data/localdev.db;Password=yourpassword;</para>
        /// </remarks>
        public static DbContextOptionsBuilder<TContext> GetDbOptionsBuilder<TContext>(string connectionString)
            where TContext : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<TContext>();
            ConfigureDbOptions(optionsBuilder, connectionString);
            return optionsBuilder;
        }

        /// <summary>
        /// Configures the provided <see cref="DbContextOptionsBuilder"/> for the appropriate database provider based on the connection string.
        /// </summary>
        /// <param name="optionsBuilder">The DbContext options builder.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <exception cref="ArgumentNullException">Thrown when optionsBuilder or connectionString is null.</exception>
        /// <exception cref="ArgumentException">Thrown when no matching database provider is found.</exception>
        /// <remarks>
        /// <para>This method inspects the provided connection string to determine the appropriate database provider to use. Here are example connection strings:</para>
        /// <para><b>Cosmos DB:</b> AccountEndpoint=https://{Your Cosmos account DNS name}:443/;AccountKey={Your Key};Database={Your database name};</para>
        /// <para><b>SQL Server:</b> Server=tcp:{your_server}.database.windows.net,1433;Initial Catalog={your_database};User ID={your_user};Password={your_password};</para>
        /// <para><b>MySQL:</b> Server={your_server};Port=3306;uid={your_user};pwd={your_password};database={your_database};</para>
        /// <para><b>SQLite (Encrypted):</b> Data Source=/data/localdev.db;Password=yourpassword;</para>
        /// </remarks>
        public static void ConfigureDbOptions(DbContextOptionsBuilder optionsBuilder, string connectionString)
        {
            ConfigureDbOptions(optionsBuilder, connectionString, DefaultStrategies);
        }

        /// <summary>
        /// Configures the provided <see cref="DbContextOptionsBuilder"/> using a custom set of strategies.
        /// </summary>
        /// <param name="optionsBuilder">The DbContext options builder.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="strategies">Custom list of configuration strategies.</param>
        /// <exception cref="ArgumentNullException">Thrown when optionsBuilder, connectionString, or strategies is null.</exception>
        /// <exception cref="ArgumentException">Thrown when no matching database provider is found.</exception>
        public static void ConfigureDbOptions(
            DbContextOptionsBuilder optionsBuilder,
            string connectionString,
            IEnumerable<IDatabaseConfigurationStrategy> strategies)
        {
            if (optionsBuilder == null)
            {
                throw new ArgumentNullException(paramName: nameof(optionsBuilder), message: "The optionsBuilder (DbContextOptionsBuilder) is null.");
            }

            // Only validate connectionString if optionsBuilder doesn't already have a provider configured
            if (!optionsBuilder.IsConfigured && string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("(CosmosDbOptionsBuilder): A connection string was not found. Please set your connection string.");
            }

            if (strategies == null)
            {
                throw new ArgumentNullException(nameof(strategies));
            }

            var orderedStrategies = strategies.OrderBy(s => s.Priority).ToList();

            // Find the first strategy that can handle the connection string
            // The method .CanHandle should be implemented by each strategy to determine if it can configure the options based on the connection string format or content
            var strategy = orderedStrategies.FirstOrDefault(s => s.CanHandle(connectionString));

            if (strategy == null)
            {
                var supportedProviders = string.Join(", ", orderedStrategies.Select(s => s.ProviderName));
                throw new ArgumentException(
                    $"The provided connection string does not match any supported database provider. " +
                    $"Supported providers: {supportedProviders}",
                    nameof(connectionString));
            }

            strategy.Configure(optionsBuilder, connectionString);
        }

        /// <summary>
        /// Gets the collection of default database configuration strategies.
        /// </summary>
        /// <returns>A read-only collection of strategies.</returns>
        public static IReadOnlyList<IDatabaseConfigurationStrategy> GetDefaultStrategies()
        {
            return DefaultStrategies.AsReadOnly();
        }
    }
}
