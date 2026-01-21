// <copyright file="ILayoutMigrationService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layout
{
    using System.Threading.Tasks;

    /// <summary>
    /// Service for migrating existing layouts to use LayoutNumber versioning.
    /// </summary>
    public interface ILayoutMigrationService
    {
        /// <summary>
        /// Determines whether the database needs layout number migration.
        /// </summary>
        /// <returns>True if any layouts have LayoutNumber = 0, indicating migration is needed.</returns>
        Task<bool> NeedsMigrationAsync();

        /// <summary>
        /// Migrates existing layouts to assign LayoutNumber values based on CommunityLayoutId.
        /// </summary>
        /// <remarks>
        /// This method groups layouts by their CommunityLayoutId to identify version families,
        /// then assigns sequential LayoutNumber values (1, 2, 3...) to each family.
        /// All versions within an active family will have IsDefault = true.
        /// </remarks>
        /// <returns>The number of layouts migrated.</returns>
        Task<int> MigrateLayoutNumbersAsync();

        /// <summary>
        /// Migrates template LayoutNumber values based on their current LayoutId.
        /// </summary>
        /// <remarks>
        /// Updates templates that have LayoutNumber = 0 by looking up the LayoutNumber
        /// from their associated Layout via LayoutId.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task MigrateTemplateLayoutNumbersAsync();
    }
}