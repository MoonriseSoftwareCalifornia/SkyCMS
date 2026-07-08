// <copyright file="DataProtectionDbContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.DataProtection
{
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Database context for data protection keys.
    /// </summary>
    public class DataProtectionDbContext : DbContext, IDataProtectionKeyContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataProtectionDbContext"/> class.
        /// </summary>
        /// <param name="options">DB context options.</param>
        public DataProtectionDbContext(DbContextOptions<DataProtectionDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the data protection keys.
        /// </summary>
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

        /// <summary>
        /// Configures the model for data protection keys.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "modelBuilder is provided by framework")]
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure DataProtectionKey for Cosmos DB with proper partitioning
            modelBuilder.Entity<DataProtectionKey>()
                .ToContainer("DataProtectionKeys")
                .HasPartitionKey(k => k.Id);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AssignDataProtectionKeyIds();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            AssignDataProtectionKeyIds();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void AssignDataProtectionKeyIds()
        {
            foreach (var entry in ChangeTracker.Entries<DataProtectionKey>()
                     .Where(e => e.State == EntityState.Added && e.Entity.Id == 0))
            {
                entry.Entity.Id = RandomNumberGenerator.GetInt32(1, int.MaxValue);
            }
        }
    }
}
