// <copyright file="ConnectionStringProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace Cosmos.DynamicConfig
{
    public class DynamicConfigDbContext : DbContext
    {
        public DynamicConfigDbContext(DbContextOptions<DynamicConfigDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the connections entity.
        /// </summary>
        public DbSet<Connection> Connections { get; set; } = null!;

        /// <summary>
        /// Gets or sets the metrics entity.
        /// </summary>
        public DbSet<Metric> Metrics { get; set; } = null!;

        /// <summary>
        ///  Handles the on model creating event.
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var provider = Database.ProviderName;
            if (provider != null && provider.Contains("Cosmos", StringComparison.OrdinalIgnoreCase))
            {
                modelBuilder.Entity<Connection>().ToContainer("config");
                modelBuilder.Entity<Metric>().ToContainer("Metrics");
            }
            else
            {
                modelBuilder.Entity<Connection>();
                modelBuilder.Entity<Metric>();
            }
            base.OnModelCreating(modelBuilder);
        }
    }
}
