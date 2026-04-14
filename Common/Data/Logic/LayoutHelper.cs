// <copyright file="LayoutHelper.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data.Logic
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Helper class for layout-related database operations.
    /// </summary>
    public static class LayoutHelper
    {
        /// <summary>
        /// Gets the current active default layout.
        /// Returns the latest published version of the default layout that is currently active.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <returns>The current default layout, or null if none exists.</returns>
        /// <remarks>
        /// This method finds the default layout by:
        /// 1. Filtering for layouts that have a Published date (Published != null)
        /// 2. Filtering for layouts that are active (Published &lt;= now)
        /// 3. Ordering by version number
        /// 4. Taking the last (highest version) layout
        ///
        /// This ensures we get the most recent published version of the layout.
        /// </remarks>
        public static async Task<Layout> GetCurrentDefaultLayoutAsync(ApplicationDbContext dbContext)
        {
            if (dbContext == null)
            {
                throw new ArgumentNullException(nameof(dbContext));
            }

            var now = DateTimeOffset.UtcNow;

            // Fast path: layout explicitly marked as default and published.
            var layout = await dbContext.Layouts
                .Where(l => l.IsDefault && l.Published != null && l.Published <= now)
                .OrderBy(l => l.Version)
                .LastOrDefaultAsync();

            if (layout != null)
            {
                return layout;
            }

            // Self-healing fallback: find any published layout, mark it as
            // default, persist the fix, and return it.
            layout = await dbContext.Layouts
                .Where(l => l.Published != null && l.Published <= now)
                .OrderBy(l => l.Version)
                .LastOrDefaultAsync();

            if (layout != null)
            {
                layout.IsDefault = true;
                await dbContext.SaveChangesAsync();
            }

            return layout;
        }

        /// <summary>
        /// Checks if any default layout exists in the database.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <returns>True if a default layout exists, false otherwise.</returns>
        /// <remarks>
        /// This method checks for the existence of a published layout by:
        /// 1. Filtering for layouts that have a Published date (Published != null)
        /// 2. Filtering for layouts that are active (Published &lt;= now)
        ///
        /// Useful for setup/initialization scenarios to determine if a published layout exists.
        /// </remarks>
        public static async Task<bool> HasDefaultLayoutAsync(ApplicationDbContext dbContext)
        {
            if (dbContext == null)
            {
                throw new ArgumentNullException(nameof(dbContext));
            }

            var now = DateTimeOffset.UtcNow;

            // Fast path: check for an explicitly-defaulted, published layout.
            var exists = await dbContext.Layouts
                .Where(l => l.IsDefault && l.Published != null && l.Published <= now)
                .CosmosAnyAsync();

            if (exists)
            {
                return true;
            }

            // Self-healing fallback: find any published layout and promote it.
            var layout = await dbContext.Layouts
                .Where(l => l.Published != null && l.Published <= now)
                .OrderBy(l => l.Version)
                .LastOrDefaultAsync();

            if (layout != null)
            {
                layout.IsDefault = true;
                await dbContext.SaveChangesAsync();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a layout by its unique identifier.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="layoutId">The layout ID to find.</param>
        /// <returns>The layout with the specified ID, or null if not found.</returns>
        public static async Task<Layout?> GetLayoutByIdAsync(ApplicationDbContext dbContext, Guid layoutId)
        {
            if (dbContext == null)
            {
                throw new ArgumentNullException(nameof(dbContext));
            }

            if (layoutId == Guid.Empty)
            {
                return null;
            }

            return await dbContext.Layouts.FirstOrDefaultAsync(l => l.Id == layoutId);
        }
    }
}
