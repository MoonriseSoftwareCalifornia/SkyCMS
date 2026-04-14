// <copyright file="IReservedPaths.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System.Collections.Generic;
using System.Threading.Tasks;
using Sky.Cms.Models;

namespace Sky.Editor.Services.ReservedPaths
{
    /// <summary>
    /// Manages reserved paths that cannot be used for articles, pages, or other content.
    /// </summary>
    public interface IReservedPaths
    {
        /// <summary>
        /// Gets the reserved paths.
        /// </summary>
        /// <returns>The reserved paths.</returns>
        Task<List<ReservedPath>> GetReservedPaths();

        /// <summary>
        /// Adds or updates the specified reserved path in the system.
        /// </summary>
        /// <param name="path">The path of the item to add. This must be a valid, non-null, and non-empty string.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task Upsert(ReservedPath path);

        /// <summary>
        /// Removes the item with the specified path from the system.
        /// </summary>
        /// <param name="path">The path of the item to remove. This must be a valid, non-null, and non-empty string.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task Remove(string path);

        /// <summary>
        /// Checks if the specified path is reserved.
        /// </summary>
        /// <param name="path">The path to check. This must be a valid, non-null, and non-empty string.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value indicating whether the path is reserved.</returns>
        Task<bool> IsReserved(string path);
    }
}
