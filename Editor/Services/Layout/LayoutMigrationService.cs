// <copyright file="LayoutMigrationService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layout
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Service for migrating existing layouts to use LayoutNumber versioning.
    /// </summary>
    /// <remarks>
    /// This service handles the one-time migration from the old LayoutId-based relationship
    /// to the new LayoutNumber-based versioning system. It groups layouts by CommunityLayoutId
    /// to identify version families and assigns persistent LayoutNumber identifiers.
    /// </remarks>
    public class LayoutMigrationService : ILayoutMigrationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<LayoutMigrationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutMigrationService"/> class.
        /// </summary>
        /// <param name="dbContext">The database context for accessing layout and template data.</param>
        /// <param name="logger">The logger for diagnostic and error information.</param>
        public LayoutMigrationService(
            ApplicationDbContext dbContext,
            ILogger<LayoutMigrationService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Determines whether the database needs layout number migration.
        /// </summary>
        /// <returns>True if any layouts have LayoutNumber = 0, indicating migration is needed.</returns>
        /// <remarks>
        /// This method checks if any Layout entities have LayoutNumber = 0, which is the
        /// default value assigned to new entities and indicates they haven't been migrated yet.
        /// </remarks>
        public async Task<bool> NeedsMigrationAsync()
        {
            try
            {
                var needsMigration = await _dbContext.Layouts
                    .AnyAsync(l => l.LayoutNumber == 0);

                if (needsMigration)
                {
                    _logger.LogInformation(
                        "Layout migration required: Found layouts with LayoutNumber = 0");
                }
                else
                {
                    _logger.LogDebug("Layout migration not needed: All layouts have assigned LayoutNumbers");
                }

                return needsMigration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if layout migration is needed");
                throw;
            }
        }

        /// <summary>
        /// Migrates existing layouts to assign LayoutNumber values based on CommunityLayoutId.
        /// </summary>
        /// <remarks>
        /// This method groups layouts by their CommunityLayoutId to identify version families,
        /// then assigns sequential LayoutNumber values (1, 2, 3...) to each family.
        /// All versions within an active family will have IsDefault = true.
        /// Uses a database transaction to ensure atomic updates.
        /// </remarks>
        /// <returns>The number of layouts migrated.</returns>
        public async Task<int> MigrateLayoutNumbersAsync()
        {
            IDbContextTransaction transaction = null;
            
            try
            {
                _logger.LogInformation("Starting layout number migration");

                // Load all layouts that need migration
                var layouts = await _dbContext.Layouts
                    .Where(l => l.LayoutNumber == 0)
                    .ToListAsync();

                if (layouts.Count == 0)
                {
                    _logger.LogInformation("No layouts require migration");
                    return 0;
                }

                _logger.LogInformation("Found {Count} layouts to migrate", layouts.Count);

                // Start transaction for atomic updates
                transaction = await _dbContext.Database.BeginTransactionAsync();

                // Group layouts by CommunityLayoutId to identify version families
                // If CommunityLayoutId is null, use the layout's Id as the family identifier
                var families = layouts
                    .GroupBy(l => l.CommunityLayoutId ?? l.Id.ToString())
                    .OrderBy(g => g.Min(l => l.LastModified ?? DateTimeOffset.MinValue))
                    .ToList();

                _logger.LogInformation("Identified {FamilyCount} layout families", families.Count);

                int layoutNumber = 1;
                int updatedCount = 0;

                foreach (var family in families)
                {
                    // Determine if this family is currently active (has any IsDefault = true)
                    var isActiveFamily = family.Any(l => l.IsDefault);

                    _logger.LogDebug(
                        "Processing layout family '{FamilyId}' with {VersionCount} versions (Active: {IsActive})",
                        family.Key,
                        family.Count(),
                        isActiveFamily);

                    // Assign the same LayoutNumber to all versions in this family
                    foreach (var layout in family.OrderBy(l => l.Version ?? 0))
                    {
                        layout.LayoutNumber = layoutNumber;
                        
                        // All versions in an active family get IsDefault = true
                        // All versions in an inactive family get IsDefault = false
                        layout.IsDefault = isActiveFamily;

                        updatedCount++;

                        _logger.LogTrace(
                            "Updated Layout Id={LayoutId}, Version={Version} -> LayoutNumber={LayoutNumber}, IsDefault={IsDefault}",
                            layout.Id,
                            layout.Version,
                            layout.LayoutNumber,
                            layout.IsDefault);
                    }

                    layoutNumber++;
                }

                // Save all changes within transaction
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Layout migration completed successfully: {UpdatedCount} layouts migrated into {FamilyCount} families",
                    updatedCount,
                    families.Count);

                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during layout number migration");

                // Rollback transaction on error
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("Layout migration transaction rolled back");
                }

                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        /// <summary>
        /// Migrates template LayoutNumber values based on their current LayoutId.
        /// </summary>
        /// <remarks>
        /// Updates templates that have LayoutNumber = 0 by looking up the LayoutNumber
        /// from their associated Layout via LayoutId. Templates without a LayoutId
        /// are skipped and logged as warnings.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task MigrateTemplateLayoutNumbersAsync()
        {
            try
            {
                _logger.LogInformation("Starting template LayoutNumber migration");

                // Load templates that need migration
                var templates = await _dbContext.Templates
                    .Where(t => t.LayoutNumber == 0)
                    .ToListAsync();

                if (templates.Count == 0)
                {
                    _logger.LogInformation("No templates require LayoutNumber migration");
                    return;
                }

                _logger.LogInformation("Found {Count} templates to migrate", templates.Count);

                int updatedCount = 0;
                int skippedCount = 0;

                foreach (var template in templates)
                {
                    if (!template.LayoutId.HasValue)
                    {
                        _logger.LogWarning(
                            "Template Id={TemplateId} ('{Title}') has no LayoutId, cannot migrate LayoutNumber",
                            template.Id,
                            template.Title);
                        skippedCount++;
                        continue;
                    }

                    // Look up the Layout to get its LayoutNumber
                    var layout = await _dbContext.Layouts
                        .FirstOrDefaultAsync(l => l.Id == template.LayoutId.Value);

                    if (layout == null)
                    {
                        _logger.LogWarning(
                            "Template Id={TemplateId} ('{Title}') references non-existent Layout Id={LayoutId}",
                            template.Id,
                            template.Title,
                            template.LayoutId.Value);
                        skippedCount++;
                        continue;
                    }

                    if (layout.LayoutNumber == 0)
                    {
                        _logger.LogWarning(
                            "Template Id={TemplateId} references Layout Id={LayoutId} which has LayoutNumber=0. Run layout migration first.",
                            template.Id,
                            layout.Id);
                        skippedCount++;
                        continue;
                    }

                    // Copy LayoutNumber from Layout to Template
                    template.LayoutNumber = layout.LayoutNumber;
                    updatedCount++;

                    _logger.LogTrace(
                        "Updated Template Id={TemplateId} ('{Title}') -> LayoutNumber={LayoutNumber} from Layout Id={LayoutId}",
                        template.Id,
                        template.Title,
                        template.LayoutNumber,
                        layout.Id);
                }

                // Save changes if any templates were updated
                if (updatedCount > 0)
                {
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation(
                        "Template migration completed: {UpdatedCount} templates updated, {SkippedCount} skipped",
                        updatedCount,
                        skippedCount);
                }
                else
                {
                    _logger.LogWarning(
                        "No templates could be migrated. {SkippedCount} templates skipped due to missing or invalid Layout references",
                        skippedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during template LayoutNumber migration");
                throw;
            }
        }
    }
}