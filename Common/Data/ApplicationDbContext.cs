// <copyright file="ApplicationDbContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using AspNetCore.Identity.FlexDb;
    using Cosmos.Common.Data.SQlite;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Azure.Cosmos;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Database Context for Sky CMS.
    /// Includes identity, content (articles, pages, templates, layouts),
    /// operational metadata (metrics, logs) and now multi-blog support via <see cref="BlobService"/>.
    /// </summary>
    public class ApplicationDbContext : CosmosIdentityDbContext<IdentityUser, IdentityRole, string>, IDataProtectionKeyContext, IApplicationDbContext
    {
        private readonly IDynamicConfigurationProvider? _configurationProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
        /// </summary>
        /// <param name="options">Database context options.</param>
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class with tenant configuration.
        /// </summary>
        /// <param name="options">Database context options.</param>
        /// <param name="configurationProvider">Dynamic configuration provider for tenant resolution.</param>
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IDynamicConfigurationProvider? configurationProvider)
            : base(options, true)
        {
            _configurationProvider = configurationProvider;
            CurrentTenantDomain = _configurationProvider?.GetTenantDomainNameFromRequest();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class with connection string.
        /// Automatically determines if connection string is for Cosmos DB, MySQL or SQL Server.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public ApplicationDbContext(string connectionString)
            : base(CosmosDbOptionsBuilder.GetDbOptions<ApplicationDbContext>(connectionString), true)
        {
        }

        /// <summary>
        /// Gets the current tenant domain captured at DbContext creation time.
        /// </summary>
        /// <remarks>
        /// This property is used by EF Core query filters to automatically filter entities by tenant.
        /// The value is captured once when the DbContext is instantiated for better performance
        /// and improved compatibility across all database providers (Cosmos DB, SQL Server, MySQL, SQLite).
        /// Null indicates no tenant filtering (e.g., in testing or non-web contexts).
        /// </remarks>
        public string? CurrentTenantDomain { get; private set; }

        /// <summary>
        /// Gets or sets catalog of Articles (flattened listing metadata + permissions).
        /// </summary>
        public DbSet<CatalogEntry> ArticleCatalog { get; set; }

        /// <summary>
        /// Gets or sets article locks (edit session coordination).
        /// </summary>
        public DbSet<ArticleLock> ArticleLocks { get; set; }

        /// <summary>
        /// Gets or sets article activity logs (audit trail).
        /// </summary>
        public DbSet<ArticleLog> ArticleLogs { get; set; }

        /// <summary>
        /// Gets or sets article number sequence records.
        /// </summary>
        public DbSet<ArticleNumber> ArticleNumbers { get; set; }

        /// <summary>
        /// Gets or sets versioned article entities (draft + historical).
        /// </summary>
        public DbSet<Article> Articles { get; set; }

        /// <summary>
        /// Gets or sets public author/editor profile info.
        /// </summary>
        public DbSet<AuthorInfo> AuthorInfos { get; set; }

        /// <summary>
        /// Gets or sets the contacts list.
        /// </summary>
        public DbSet<Contact> Contacts { get; set; }

        /// <summary>
        /// Gets or sets website layouts (chrome containers).
        /// </summary>
        public DbSet<Layout> Layouts { get; set; }

        /// <summary>
        /// Gets or sets metrics for the site.
        /// </summary>
        public DbSet<Metric> Metrics { get; set; }

        /// <summary>
        /// Gets or sets published page snapshots (one active per ArticleNumber).
        /// </summary>
        public DbSet<PublishedPage> Pages { get; set; }

        /// <summary>
        /// Gets or sets page design versions (drafts and historical).
        /// </summary>
        public DbSet<PageDesignVersion> PageDesignVersions { get; set; }

        /// <summary>
        /// Gets or sets site settings (key/value configuration).
        /// </summary>
        public DbSet<Setting> Settings { get; set; }

        /// <summary>
        /// Gets or sets web page templates (starter content).
        /// </summary>
        public DbSet<Template> Templates { get; set; }

        /// <summary>
        /// Gets or sets the TOTP tokens for users.
        /// </summary>
        public DbSet<TotpToken> TotpTokens { get; set; } = null!;

        /// <summary>
        /// Gets or sets data protection keys.
        /// </summary>
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

        /// <summary>
        /// Gets or sets migration history tracking.
        /// </summary>
        public DbSet<MigrationHistory> MigrationHistory { get; set; } = null!;

        /// <summary>
        /// Ensure database exists and returns status.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        /// <returns>Success or not.</returns>
        public static DbStatus EnsureDatabaseExists(string connectionString)
        {
            using var dbContext = new ApplicationDbContext(connectionString);

            if (dbContext.Database.IsCosmos())
            {
                var databaseName = connectionString.Split(';').FirstOrDefault(s => s.StartsWith("Database=", StringComparison.InvariantCultureIgnoreCase))?.Split('=')[1];
                var cosmosClient = dbContext.Database.GetCosmosClient();
                var exists = DoesCosmosDatabaseExist(cosmosClient, databaseName).Result;

                if (exists == false)
                {
                    var task = dbContext.Database.EnsureCreatedAsync();
                    task.Wait();

                    if (task.IsFaulted)
                    {
                        return DbStatus.CreationFailed;
                    }
                }

                var userCount = dbContext.Users.Select(s => s.Id).ToListAsync().Result;
                if (userCount.Count == 0)
                {
                    return DbStatus.ExistsWithNoUsers;
                }

                return DbStatus.ExistsWithUsers;
            }

            var result = dbContext.Database.EnsureCreatedAsync().Result;

            if (result)
            {
                var userCount = dbContext.Users.CountAsync().Result;
                if (userCount == 0)
                {
                    return DbStatus.ExistsWithNoUsers;
                }

                return DbStatus.ExistsWithUsers;
            }

            return DbStatus.CreationFailed;
        }

        /// <summary>
        /// Ensure database exists and returns status (Cosmos DB specific path).
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="setup">Whether to set up the database if it doesn't exist.</param>
        /// <param name="databaseName">The name of the database.</param>
        /// <returns>Returns the database status.</returns>
        public static DbStatus EnsureDatabaseExists(ApplicationDbContext dbContext, bool setup, string databaseName)
        {
            var cosmosClient = dbContext.Database.GetCosmosClient();

            DbStatus dbStatus = DbStatus.DoesNotExist;

            try
            {
                DatabaseResponse response = cosmosClient.GetDatabase(databaseName).ReadAsync().Result;
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // Check required containers.
                    var identityContainerResult = cosmosClient.GetContainer(databaseName, "Identity").ReadContainerAsync().Result;
                    var articleContainerResult = cosmosClient.GetContainer(databaseName, "Articles").ReadContainerAsync().Result;

                    if (identityContainerResult.StatusCode == System.Net.HttpStatusCode.OK &&
                        articleContainerResult.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var query = identityContainerResult.Container.GetItemLinqQueryable<IdentityUser>(allowSynchronousQueryExecution: true);
                        var count = query.Count();
                        dbStatus = count > 0 ? DbStatus.ExistsWithUsers : DbStatus.ExistsWithNoUsers;
                    }
                    else
                    {
                        dbStatus = DbStatus.ExistsWithMissingContainers;
                    }
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                dbStatus = DbStatus.DoesNotExist;
            }

            if (setup && (dbStatus == DbStatus.DoesNotExist || dbStatus == DbStatus.ExistsWithMissingContainers))
            {
                var task = dbContext.Database.EnsureCreatedAsync();
                task.Wait();
                if (task.IsCompletedSuccessfully)
                {
                    dbStatus = DbStatus.ExistsWithNoUsers;
                }
                else if (task.IsFaulted)
                {
                    throw task.Exception;
                }
                else
                {
                    throw new Exception("EnsureCreatedAsync() failed.");
                }
            }

            return dbStatus;
        }

        /// <summary>
        /// Returns true if the context can connect to the configured database.
        /// </summary>
        /// <returns>True if configured.</returns>
        public async Task<bool> IsConfigured()
        {
            return await this.Database.CanConnectAsync();
        }

        /// <summary>
        /// Configure provider-specific options (e.g., suppress Cosmos sync warnings).
        /// </summary>
        /// <param name="optionsBuilder">The options builder.</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var isCosmosDb = optionsBuilder.IsConfigured &&
                             optionsBuilder.Options.Extensions.Any(e => e is Microsoft.EntityFrameworkCore.Cosmos.Infrastructure.Internal.CosmosOptionsExtension);

            if (isCosmosDb)
            {
                optionsBuilder.ConfigureWarnings(w => w.Ignore(CosmosEventId.SyncNotSupported));
            }
            else
            {
                // For relational databases, suppress pending model changes warning
                // This is necessary because we have provider-specific migrations in the same assembly
                optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            }

            base.OnConfiguring(optionsBuilder);
        }

        /// <summary>
        /// Model creation and container / index configuration.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (this.Database.IsSqlite())
            {
                SQLiteUtils.OnModelCreating(modelBuilder);
                base.OnModelCreating(modelBuilder);
                return;
            }

            if (this.Database.IsCosmos())
            {
                // DEFAULT CONTAINER ENTITIES
                modelBuilder.HasDefaultContainer("CosmosCms");

                modelBuilder.Entity<Contact>()
                    .ToContainer("CosmosCms")
                    .HasPartitionKey(k => k.Id)
                    .HasKey(k => k.Id);

                modelBuilder.Entity<TotpToken>()
                    .ToContainer("CosmosCms")
                    .HasPartitionKey(k => k.Id)
                    .HasKey(k => k.Id);

                modelBuilder.Entity<ArticleNumber>()
                    .ToContainer("ArticleNumber")
                    .HasPartitionKey(k => k.Id)
                    .HasKey(k => k.Id);

                modelBuilder.Entity<Article>()
                    .Property(e => e.ArticleNumber)
                    .HasConversion<string>();

                modelBuilder.Entity<Article>()
                    .ToContainer("Articles")
                    .HasPartitionKey(a => a.ArticleNumber)
                    .HasKey(article => article.Id);

                modelBuilder.Entity<ArticleLock>()
                    .ToContainer("ArticleLocks")
                    .HasPartitionKey(a => a.Id)
                    .HasKey(article => article.Id);

                modelBuilder.Entity<ArticleLog>()
                    .ToContainer("ArticleLogs")
                    .HasPartitionKey(k => k.Id)
                    .HasKey(log => log.Id);

                modelBuilder.Entity<CatalogEntry>().OwnsMany(o => o.ArticlePermissions);

                modelBuilder.Entity<CatalogEntry>()
                    .Property(e => e.ArticleNumber)
                    .HasConversion<string>();

                modelBuilder.Entity<CatalogEntry>()
                    .ToContainer("ArticleCatalog")
                    .HasPartitionKey(k => k.ArticleNumber)
                    .HasKey(log => log.ArticleNumber);

                modelBuilder.Entity<Layout>()
                    .ToContainer("Layouts")
                    .HasPartitionKey(a => a.Id)
                    .HasKey(layout => layout.Id);

                modelBuilder.Entity<PublishedPage>()
                    .ToContainer("Pages")
                    .HasPartitionKey(a => a.UrlPath)
                    .HasKey(article => article.Id);

                modelBuilder.Entity<Setting>()
                    .ToContainer("Settings")
                    .HasPartitionKey(a => a.Id)
                    .HasKey(article => article.Id);

                modelBuilder.Entity<Template>()
                    .ToContainer("Templates")
                    .HasPartitionKey(k => k.Id)
                    .HasKey(node => node.Id);

                modelBuilder.Entity<PageDesignVersion>()
                    .ToContainer("PageDesignVersions")
                    .HasPartitionKey(k => k.Id)
                    .HasKey(node => node.Id);

                modelBuilder.Entity<AuthorInfo>()
                    .ToContainer("AuthorInfo")
                    .HasPartitionKey(k => k.Id)
                    .HasKey(k => k.Id);

                modelBuilder.Entity<Metric>()
                    .ToContainer("Metrics")
                    .HasPartitionKey(k => k.Id)
                    .HasKey(k => k.Id);

                modelBuilder.Entity<DataProtectionKey>()
                    .ToContainer("DataProtection")
                    .HasPartitionKey(k => k.Id)
                    .HasKey(k => k.Id);

                modelBuilder.Entity<MigrationHistory>()
                    .ToContainer("MigrationHistory")
                    .HasPartitionKey(k => k.Id)
                    .HasKey(k => k.Id);
            }
            else
            {
                // Relational Database Indexes (SQL Server, MySQL, SQLite)
                // Layout versioning indexes for efficient querying by family and version
                modelBuilder.Entity<Layout>()
                    .HasIndex(l => l.LayoutNumber)
                    .HasDatabaseName("IX_Layout_LayoutNumber");

                modelBuilder.Entity<Layout>()
                    .HasIndex(l => new { l.LayoutNumber, l.Version })
                    .HasDatabaseName("IX_Layout_LayoutNumber_Version");

                modelBuilder.Entity<Layout>()
                    .HasIndex(l => new { l.LayoutNumber, l.IsDefault, l.Published })
                    .HasDatabaseName("IX_Layout_LayoutNumber_IsDefault_Published");

                // Template indexes for layout family lookups
                modelBuilder.Entity<Template>()
                    .HasIndex(t => t.LayoutNumber)
                    .HasDatabaseName("IX_Template_LayoutNumber");

                modelBuilder.Entity<Template>()
                    .HasIndex(t => new { t.LayoutNumber, t.LayoutId })
                    .HasDatabaseName("IX_Template_LayoutNumber_LayoutId");

                // Migration history indexes
                modelBuilder.Entity<MigrationHistory>()
                    .HasIndex(m => m.MigrationId)
                    .HasDatabaseName("IX_MigrationHistory_MigrationId");

                modelBuilder.Entity<MigrationHistory>()
                    .HasIndex(m => m.Provider)
                    .HasDatabaseName("IX_MigrationHistory_Provider");

                // MySQL-specific prefix length constraints
                if (Database.IsMySql())
                {
                    modelBuilder.Entity<PublishedPage>()
                        .HasIndex(p => p.UrlPath)
                        .HasAnnotation("MySql:IndexPrefixLength", new[] { 768 }); // 768 * 4 = 3072 bytes max

                    modelBuilder.Entity<CatalogEntry>()
                        .HasIndex(p => p.UrlPath)
                        .HasAnnotation("MySql:IndexPrefixLength", new[] { 768 }); // 768 * 4 = 3072 bytes max
                }

                // All SQL providers: ETag concurrency
                modelBuilder.Entity<Article>().Property(e => e.RowVersion).IsETagConcurrency();
                modelBuilder.Entity<CatalogEntry>().Property(e => e.RowVersion).IsETagConcurrency();
                modelBuilder.Entity<PublishedPage>().Property(e => e.RowVersion).IsETagConcurrency();
                modelBuilder.Entity<PageDesignVersion>().Property(e => e.RowVersion).IsETagConcurrency();

            }

            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// Determine if a Cosmos DB database exists.
        /// </summary>
        private static async Task<bool> DoesCosmosDatabaseExist(CosmosClient client, string databaseId)
        {
            QueryDefinition query = new QueryDefinition(
                "select * from c where c.id = @databaseId")
                    .WithParameter("@databaseId", databaseId);

            FeedIterator<dynamic> resultSet = client.GetDatabaseQueryIterator<dynamic>(query);

            while (resultSet.HasMoreResults)
            {
                FeedResponse<dynamic> response = await resultSet.ReadNextAsync();
                return response.Count > 0;
            }

            return false;
        }
    }
}
