// <copyright file="MigrationSummary.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Migrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Summary of migration execution results.
    /// </summary>
    public class MigrationSummary
    {
        /// <summary>
        /// Gets or sets a value indicating whether migrations were executed successfully.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the total number of tenants processed (multi-tenant mode only).
        /// </summary>
        public int TotalProcessed { get; set; }

        /// <summary>
        /// Gets or sets the number of successful migrations.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the number of failed migrations.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the number of skipped migrations (e.g., missing connection strings).
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// Gets or sets the error message if migration failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets detailed error information if migration failed.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets individual tenant migration results (multi-tenant mode only).
        /// </summary>
        public List<TenantMigrationResult> TenantResults { get; set; } = new List<TenantMigrationResult>();

        /// <summary>
        /// Logs the migration summary to the console.
        /// </summary>
        public void LogResults()
        {
            if (IsSuccess)
            {
                if (TenantResults.Any())
                {
                    // Multi-tenant summary
                    System.Console.WriteLine($"✅ Multi-tenant migration summary: {SuccessCount} succeeded, {FailureCount} failed, {SkippedCount} skipped");
                }
                else
                {
                    // Single-tenant summary
                    System.Console.WriteLine("✅ Custom migrations completed successfully");
                }
            }
            else
            {
                System.Console.WriteLine($"⚠️ WARNING: Migration failed: {ErrorMessage}");
                if (Exception != null)
                {
                    System.Console.WriteLine($"   {Exception.StackTrace}");
                }
            }
        }
    }

    /// <summary>
    /// Result of migration for a single tenant.
    /// </summary>
    public class TenantMigrationResult
    {
        /// <summary>
        /// Gets or sets the tenant domain name.
        /// </summary>
        public string DomainName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the migration was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the number of layouts migrated (if applicable).
        /// </summary>
        public int LayoutsMigrated { get; set; }

        /// <summary>
        /// Gets or sets the error message if migration failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tenant was skipped.
        /// </summary>
        public bool WasSkipped { get; set; }
    }
}
