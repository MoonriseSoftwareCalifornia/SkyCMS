// <copyright file="IApplicationDbContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction over the Sky CMS application database context.
    /// </summary>
    /// <remarks>
    /// This interface exists to decouple consumers from the concrete
    /// <see cref="ApplicationDbContext"/> implementation and to improve
    /// testability by enabling mocking or substitution in unit tests.
    /// It exposes the main <see cref="DbSet{TEntity}"/> collections used
    /// throughout the editor and publisher pipelines, as well as helper
    /// methods for database provisioning and configuration checks.
    /// </remarks>
    public interface IApplicationDbContext
    {
        /// <summary>
        /// Gets or sets the catalog of article metadata (flattened listing plus permissions).
        /// </summary>
        DbSet<CatalogEntry> ArticleCatalog { get; set; }

        /// <summary>
        /// Gets or sets the collection of article lock records used for edit-session coordination.
        /// </summary>
        DbSet<ArticleLock> ArticleLocks { get; set; }

        /// <summary>
        /// Gets or sets the article activity log entries (audit trail).
        /// </summary>
        DbSet<ArticleLog> ArticleLogs { get; set; }

        /// <summary>
        /// Gets or sets the article number sequence records that back logical article numbering.
        /// </summary>
        DbSet<ArticleNumber> ArticleNumbers { get; set; }

        /// <summary>
        /// Gets or sets the versioned article entities (draft, historical, and active).
        /// </summary>
        DbSet<Article> Articles { get; set; }

        /// <summary>
        /// Gets or sets public author/editor profile information.
        /// </summary>
        DbSet<AuthorInfo> AuthorInfos { get; set; }

        /// <summary>
        /// Gets or sets the contact form submissions and related contact entities.
        /// </summary>
        DbSet<Contact> Contacts { get; set; }

        /// <summary>
        /// Gets or sets the data protection key store used by
        /// <see cref="IDataProtectionKeyContext"/>.
        /// </summary>
        DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        /// <summary>
        /// Gets or sets the website layout definitions (chrome containers).
        /// </summary>
        DbSet<Layout> Layouts { get; set; }

        /// <summary>
        /// Gets or sets the site metrics and telemetry records.
        /// </summary>
        DbSet<Metric> Metrics { get; set; }

        /// <summary>
        /// Gets or sets the published page snapshots (one active per article number).
        /// </summary>
        DbSet<PublishedPage> Pages { get; set; }

        /// <summary>
        /// Gets or sets the key/value configuration settings table.
        /// </summary>
        DbSet<Setting> Settings { get; set; }

        /// <summary>
        /// Gets or sets the page and article templates (starter content).
        /// </summary>
        DbSet<Template> Templates { get; set; }

        /// <summary>
        /// Gets or sets the time-based one-time password (TOTP) tokens for users.
        /// </summary>
        DbSet<TotpToken> TotpTokens { get; set; }

        /// <summary>
        /// Gets or sets the page design versions (draft and historical design iterations).
        /// </summary>
        DbSet<PageDesignVersion> PageDesignVersions { get; set; }

        /// <summary>
        /// Gets or sets the migration history tracking records.
        /// </summary>
        DbSet<MigrationHistory> MigrationHistory { get; set; }

        /// <summary>
        /// Gets or sets the identity users collection.
        /// </summary>
        DbSet<IdentityUser> Users { get; set; }

        /// <summary>
        /// Gets or sets the identity roles collection.
        /// </summary>
        DbSet<IdentityRole> Roles { get; set; }

        /// <summary>
        /// Gets or sets the user-role associations collection.
        /// </summary>
        DbSet<IdentityUserRole<string>> UserRoles { get; set; }

        /// <summary>
        /// Ensures that the backing database for the supplied context exists and,
        /// optionally, performs setup tasks for Cosmos DB providers.
        /// </summary>
        /// <param name="dbContext">
        /// The <see cref="ApplicationDbContext"/> instance to use for provisioning.
        /// </param>
        /// <param name="setup">
        /// When <see langword="true"/>, creates the database and required containers
        /// if they do not already exist.
        /// </param>
        /// <param name="databaseName">
        /// The logical database name to validate or create (Cosmos DB specific).
        /// </param>
        /// <returns>
        /// A <see cref="DbStatus"/> value describing the resulting database state.
        /// </returns>
        static abstract DbStatus EnsureDatabaseExists(ApplicationDbContext dbContext, bool setup, string databaseName);

        /// <summary>
        /// Ensures that a database exists for the supplied connection string,
        /// automatically detecting the provider (Cosmos DB, MySQL, SQL Server, etc.).
        /// </summary>
        /// <param name="connectionString">
        /// The provider-specific connection string used to connect to the database.
        /// </param>
        /// <returns>
        /// A <see cref="DbStatus"/> value describing whether the database exists
        /// and whether any users are provisioned.
        /// </returns>
        static abstract DbStatus EnsureDatabaseExists(string connectionString);

        /// <summary>
        /// Determines whether the underlying database is reachable using the
        /// current configuration.
        /// </summary>
        /// <returns>
        /// A task that resolves to <see langword="true"/> if the context can
        /// connect to the configured database; otherwise <see langword="false"/>.
        /// </returns>
        Task<bool> IsConfigured();
    }
}