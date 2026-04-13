// <copyright file="ILayoutFamilyService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layout
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Service for managing layout families and their versions.
    /// </summary>
    public interface ILayoutFamilyService
    {
        /// <summary>
        /// Gets all layout versions belonging to the specified layout family.
        /// </summary>
        /// <param name="layoutNumber">The layout number identifying the family.</param>
        /// <returns>A list of all layout versions in the family.</returns>
        Task<List<Cosmos.Common.Data.Layout>> GetLayoutFamilyAsync(int layoutNumber);

        /// <summary>
        /// Gets the latest (highest version) layout for the given layout family.
        /// </summary>
        /// <param name="layoutNumber">The layout number identifying the family.</param>
        /// <returns>The latest layout version, or <c>null</c> if none exists.</returns>
        Task<Cosmos.Common.Data.Layout?> GetLatestVersionAsync(int layoutNumber);

        /// <summary>
        /// Gets the currently published layout version for the given layout family.
        /// </summary>
        /// <param name="layoutNumber">The layout number identifying the family.</param>
        /// <returns>The published layout, or <c>null</c> if none is published.</returns>
        Task<Cosmos.Common.Data.Layout?> GetPublishedVersionAsync(int layoutNumber);

        /// <summary>
        /// Gets all distinct layout numbers present in the database.
        /// </summary>
        /// <returns>A list of layout numbers.</returns>
        Task<List<int>> GetAllLayoutNumbersAsync();

        /// <summary>
        /// Gets summary information about a layout family, including all versions.
        /// </summary>
        /// <param name="layoutNumber">The layout number identifying the family.</param>
        /// <returns>A <see cref="LayoutFamilyInfo"/> instance, or <c>null</c> if no layouts exist for the given number.</returns>
        Task<LayoutFamilyInfo?> GetFamilyInfoAsync(int layoutNumber);

        /// <summary>
        /// Creates a new layout version based on the latest version in the given family.
        /// </summary>
        /// <param name="layoutNumber">The layout number identifying the family.</param>
        /// <param name="userId">The optional ID of the user creating the new version.</param>
        /// <returns>The newly created layout version.</returns>
        Task<Cosmos.Common.Data.Layout> CreateNewVersionAsync(int layoutNumber, string? userId = null);

        /// <summary>
        /// Publishes the specified layout version, making it the active version for its family.
        /// </summary>
        /// <param name="layoutId">The unique identifier of the layout version to publish.</param>
        /// <returns><c>true</c> if publishing succeeded; <c>false</c> if the layout was not found.</returns>
        Task<bool> PublishVersionAsync(Guid layoutId);

        /// <summary>
        /// Deletes the specified layout version and any associated templates.
        /// </summary>
        /// <param name="layoutId">The unique identifier of the layout version to delete.</param>
        /// <returns><c>true</c> if deletion succeeded; <c>false</c> if the layout was not found or is currently published.</returns>
        Task<bool> DeleteVersionAsync(Guid layoutId);

        /// <summary>
        /// Gets all layout versions grouped by their layout family.
        /// </summary>
        /// <returns>A list of <see cref="LayoutFamilyGroup"/> objects representing each family.</returns>
        Task<List<LayoutFamilyGroup>> GetLayoutsGroupedByFamilyAsync();
    }
}