using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace Sky.Editor.Data
{
    /// <summary>
    /// Design-time factory for ApplicationDbContext.
    /// Automatically selects the correct database provider based on environment variables.
    /// </summary>
    /// <remarks>
    /// Set MIGRATION_CONNECTION_STRING for the connection string.
    /// Set MIGRATION_PROVIDER to: SqlServer, MySql, or Sqlite (defaults to SqlServer).
    /// </remarks>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        /// <summary>
        /// Creates a new instance of ApplicationDbContext for design-time operations.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>A configured ApplicationDbContext instance.</returns>
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var provider = Environment.GetEnvironmentVariable("MIGRATION_PROVIDER") ?? "SqlServer";
            var connectionString = Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Default connection strings for each provider
                connectionString = provider.ToLowerInvariant() switch
                {
                    "mysql" => "Server=localhost;Port=3306;Database=skycms_migrations;User=root;Password=yourpassword;",
                    "sqlite" => "Data Source=skycms_migrations.db",
                    _ => "Server=(localdb)\\mssqllocaldb;Database=SkyCMS_Migrations;Trusted_Connection=True;MultipleActiveResultSets=true"
                };
            }

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            switch (provider.ToLowerInvariant())
            {
                case "sqlserver":
                    optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(typeof(ApplicationDbContextFactory).Assembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
                    });
                    break;

                case "mysql":
                    var serverVersion = new MySqlServerVersion(new Version(8, 0, 21));
                    optionsBuilder.UseMySql(connectionString, serverVersion, mySqlOptions =>
                    {
                        mySqlOptions.MigrationsAssembly(typeof(ApplicationDbContextFactory).Assembly.FullName);
                        mySqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                    });
                    break;

                case "sqlite":
                    optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
                    {
                        sqliteOptions.MigrationsAssembly(typeof(ApplicationDbContextFactory).Assembly.FullName);
                        sqliteOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported MIGRATION_PROVIDER: {provider}. Use SqlServer, MySql, or Sqlite.");
            }

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}