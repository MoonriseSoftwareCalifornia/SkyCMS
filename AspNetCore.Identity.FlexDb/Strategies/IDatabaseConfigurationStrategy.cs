// <copyright file="IDatabaseConfigurationStrategy.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace AspNetCore.Identity.FlexDb.Strategies
{
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Defines a strategy pattern interface for database provider-specific configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface enables automatic database provider detection and configuration based on connection strings.
    /// Each implementation represents a specific database provider (Cosmos DB, SQL Server, MySQL, SQLite, etc.)
    /// and knows how to identify and configure Entity Framework Core for that provider.
    /// </para>
    /// <para>
    /// <b>Design Pattern:</b> Strategy Pattern - allows different database configuration algorithms to be selected at runtime.
    /// </para>
    /// <para>
    /// <b>Thread Safety:</b> Implementations should be stateless and thread-safe as they may be called concurrently.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Example implementation for SQL Server:</para>
    /// <code>
    /// public class SqlServerConfigurationStrategy : IDatabaseConfigurationStrategy
    /// {
    ///     public string ProviderName => "SQL Server";
    ///     public int Priority => 20;
    ///     
    ///     public bool CanHandle(string connectionString)
    ///     {
    ///         return connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
    ///                connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase);
    ///     }
    ///     
    ///     public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    ///     {
    ///         optionsBuilder.UseSqlServer(connectionString);
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="CosmosDbOptionsBuilder"/>
    public interface IDatabaseConfigurationStrategy
    {
        /// <summary>
        /// Gets the human-readable provider name identifier.
        /// </summary>
        /// <value>
        /// A descriptive name for the database provider (e.g., "Cosmos DB", "SQL Server", "MySQL", "SQLite").
        /// </value>
        /// <remarks>
        /// <para>
        /// This name is used for:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Logging and diagnostics to identify which provider was selected</description></item>
        /// <item><description>Error messages to help users understand which providers are supported</description></item>
        /// <item><description>Developer tooling and debugging</description></item>
        /// </list>
        /// <para>
        /// <b>Implementation Guidelines:</b> Use consistent, user-friendly names that match official provider documentation.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// public string ProviderName => "Microsoft.EntityFrameworkCore.Cosmos" for Azure Cosmos DB;
        /// public string ProviderName => "Microsoft.EntityFrameworkCore.SqlServer" for SQL Server;
        /// public string ProviderName => "MySql.EntityFrameworkCore" for MySQL;
        /// public string ProviderName => "Microsoft.EntityFrameworkCore.Sqlite" for SQLite;
        /// </code>
        /// </example>
        string ProviderName { get; }

        /// <summary>
        /// Gets the priority order for provider detection (lower values = higher priority).
        /// </summary>
        /// <value>
        /// An integer representing the detection priority. Lower numbers are evaluated first.
        /// Recommended range: 1-100.
        /// </value>
        /// <remarks>
        /// <para>
        /// The priority system prevents ambiguous detection when multiple strategies could potentially
        /// match the same connection string pattern. Strategies are evaluated in ascending priority order.
        /// </para>
        /// <para>
        /// <b>Recommended Priority Values:</b>
        /// </para>
        /// <list type="table">
        /// <listheader>
        /// <term>Priority</term>
        /// <description>Provider Type</description>
        /// </listheader>
        /// <item>
        /// <term>1-10</term>
        /// <description>Cloud-specific providers with unique identifiers (e.g., Cosmos DB with "AccountEndpoint=")</description>
        /// </item>
        /// <item>
        /// <term>11-30</term>
        /// <description>Database providers with distinctive connection string patterns</description>
        /// </item>
        /// <item>
        /// <term>31-50</term>
        /// <description>Generic SQL providers (may have overlapping patterns)</description>
        /// </item>
        /// <item>
        /// <term>51-100</term>
        /// <description>Fallback or less specific matchers</description>
        /// </item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Cosmos DB has high priority due to unique "AccountEndpoint" pattern
        /// public int Priority => 10;
        /// 
        /// // SQL Server has medium priority
        /// public int Priority => 20;
        /// 
        /// // SQLite has lower priority due to generic "Data Source" pattern
        /// public int Priority => 40;
        /// </code>
        /// </example>
        int Priority { get; }

        /// <summary>
        /// Determines if this strategy can handle the provided connection string.
        /// </summary>
        /// <param name="connectionString">The connection string to evaluate. Must not be null or empty.</param>
        /// <returns>
        /// <c>true</c> if this strategy can configure the database provider for the connection string;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method performs connection string pattern matching to determine if the strategy is appropriate.
        /// It should be fast, stateless, and not attempt to validate the connection or connect to the database.
        /// </para>
        /// <para>
        /// <b>Implementation Guidelines:</b>
        /// </para>
        /// <list type="bullet">
        /// <item><description>Use case-insensitive string comparisons for robustness</description></item>
        /// <item><description>Look for provider-specific keywords or patterns</description></item>
        /// <item><description>Return <c>false</c> quickly if the pattern doesn't match</description></item>
        /// <item><description>Don't throw exceptions - return <c>false</c> instead</description></item>
        /// <item><description>Avoid expensive operations or network calls</description></item>
        /// </list>
        /// <para>
        /// <b>Thread Safety:</b> This method must be thread-safe as it may be called concurrently.
        /// </para>
        /// </remarks>
        /// <example>
        /// <para>Examples of pattern matching for different providers:</para>
        /// <code>
        /// // Cosmos DB: Look for unique "AccountEndpoint" keyword
        /// public bool CanHandle(string connectionString)
        /// {
        ///     return connectionString.Contains("AccountEndpoint=", StringComparison.OrdinalIgnoreCase);
        /// }
        /// 
        /// // MySQL: Look for "server=" or "uid=" patterns
        /// public bool CanHandle(string connectionString)
        /// {
        ///     return connectionString.Contains("server=", StringComparison.OrdinalIgnoreCase) &amp;&amp;
        ///            (connectionString.Contains("uid=", StringComparison.OrdinalIgnoreCase) ||
        ///             connectionString.Contains("user id=", StringComparison.OrdinalIgnoreCase));
        /// }
        /// 
        /// // SQLite: Look for file-based pattern
        /// public bool CanHandle(string connectionString)
        /// {
        ///     return connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &amp;&amp;
        ///            connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase);
        /// }
        /// </code>
        /// </example>
        /// <exception cref="System.ArgumentNullException">
        /// Should not be thrown - implementations should return <c>false</c> for invalid input instead.
        /// </exception>
        bool CanHandle(string connectionString);

        /// <summary>
        /// Configures the DbContext options builder for the specific database provider.
        /// </summary>
        /// <param name="optionsBuilder">
        /// The Entity Framework Core options builder to configure. Must not be null.
        /// </param>
        /// <param name="connectionString">
        /// The validated connection string for the database provider. Must not be null or empty.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method is called after <see cref="CanHandle"/> returns <c>true</c> and configures
        /// the Entity Framework Core DbContext with provider-specific settings.
        /// </para>
        /// <para>
        /// <b>Implementation Guidelines:</b>
        /// </para>
        /// <list type="bullet">
        /// <item><description>Call the appropriate <c>Use[Provider]</c> extension method on the options builder</description></item>
        /// <item><description>Apply provider-specific optimizations and configurations</description></item>
        /// <item><description>Handle provider-specific connection string parsing if needed</description></item>
        /// <item><description>Configure retry policies, timeouts, or other resilience patterns</description></item>
        /// <item><description>Set up logging or diagnostics as appropriate</description></item>
        /// </list>
        /// <para>
        /// <b>Error Handling:</b> Let Entity Framework Core exceptions propagate naturally.
        /// Don't catch and suppress configuration errors.
        /// </para>
        /// <para>
        /// <b>Thread Safety:</b> This method must be thread-safe as the options builder may be
        /// used concurrently during service registration.
        /// </para>
        /// </remarks>
        /// <example>
        /// <para>Examples of configuring different providers:</para>
        /// <code>
        /// // Cosmos DB with custom options
        /// public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
        /// {
        ///     var parts = connectionString.Split(';');
        ///     var endpoint = parts.First(p => p.StartsWith("AccountEndpoint=")).Split('=')[1];
        ///     var key = parts.First(p => p.StartsWith("AccountKey=")).Split('=')[1];
        ///     var database = parts.First(p => p.StartsWith("Database=")).Split('=')[1];
        ///     
        ///     optionsBuilder.UseCosmos(endpoint, key, database);
        /// }
        /// 
        /// // SQL Server with retry policy
        /// public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
        /// {
        ///     optionsBuilder.UseSqlServer(connectionString, options =>
        ///     {
        ///         options.EnableRetryOnFailure(
        ///             maxRetryCount: 3,
        ///             maxRetryDelay: TimeSpan.FromSeconds(5),
        ///             errorNumbersToAdd: null);
        ///     });
        /// }
        /// 
        /// // MySQL with connection pooling
        /// public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
        /// {
        ///     optionsBuilder.UseMySQL(connectionString);
        /// }
        /// 
        /// // SQLite with encryption support
        /// public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
        /// {
        ///     optionsBuilder.UseSqlite(connectionString);
        /// }
        /// </code>
        /// </example>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="optionsBuilder"/> or <paramref name="connectionString"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="connectionString"/> is invalid or cannot be parsed.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when the provider cannot be configured (e.g., missing required NuGet packages).
        /// </exception>
        /// <seealso cref="DbContextOptionsBuilder"/>
        /// <seealso cref="CanHandle"/>
        void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString);
    }
}
