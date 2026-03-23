// <copyright file="LayoutQueryExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Extensions
{
    using Cosmos.Common.Data;
    using System.Linq;

    /// <summary>
    /// LINQ extension methods for querying Layout entities with LayoutNumber support.
    /// </summary>
    public static class LayoutQueryExtensions
    {
        /// <summary>
        /// Filters layouts by LayoutNumber (family).
        /// </summary>
        public static IQueryable<Layout> ByLayoutNumber(this IQueryable<Layout> query, int layoutNumber)
        {
            return query.Where(l => l.LayoutNumber == layoutNumber);
        }

        /// <summary>
        /// Gets the latest version of each layout family.
        /// </summary>
        public static IQueryable<Layout> LatestVersions(this IQueryable<Layout> query)
        {
            return query
                .GroupBy(l => l.LayoutNumber)
                .Select(g => g.OrderByDescending(l => l.Version).First());
        }

        /// <summary>
        /// Gets only published (default) layouts.
        /// </summary>
        public static IQueryable<Layout> Published(this IQueryable<Layout> query)
        {
            return query.Where(l => l.IsDefault && l.Published.HasValue);
        }

        /// <summary>
        /// Gets only draft (unpublished) layouts.
        /// </summary>
        public static IQueryable<Layout> Drafts(this IQueryable<Layout> query)
        {
            return query.Where(l => !l.IsDefault || !l.Published.HasValue);
        }

        /// <summary>
        /// Orders layouts by family (LayoutNumber) and then by version (descending).
        /// </summary>
        public static IQueryable<Layout> OrderByFamilyAndVersion(this IQueryable<Layout> query)
        {
            return query.OrderBy(l => l.LayoutNumber).ThenByDescending(l => l.Version);
        }

        /// <summary>
        /// Orders layouts by most recently modified first.
        /// </summary>
        public static IQueryable<Layout> OrderByNewest(this IQueryable<Layout> query)
        {
            return query.OrderByDescending(l => l.LastModified ?? System.DateTimeOffset.MinValue);
        }

        /// <summary>
        /// Filters to layouts that have been assigned a LayoutNumber (excludes unmigrated layouts).
        /// </summary>
        public static IQueryable<Layout> WithLayoutNumber(this IQueryable<Layout> query)
        {
            return query.Where(l => l.LayoutNumber > 0);
        }
    }
}