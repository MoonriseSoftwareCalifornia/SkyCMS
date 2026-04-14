// <copyright file="IPublishingProgressReporter.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Publishing
{
    using System.Threading.Tasks;

    /// <summary>
    /// Reports progress updates for publishing operations.
    /// </summary>
    public interface IPublishingProgressReporter
    {
        /// <summary>
        /// Reports progress for the current user's publishing operation.
        /// </summary>
        /// <param name="currentPage">Current page number being processed.</param>
        /// <param name="totalPages">Total number of pages to process.</param>
        /// <param name="message">Status message.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task ReportProgressAsync(int currentPage, int totalPages, string message);
    }
}