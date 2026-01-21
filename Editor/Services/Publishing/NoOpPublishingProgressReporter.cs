// <copyright file="NoOpPublishingProgressReporter.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Publishing
{
    using System.Threading.Tasks;

    /// <summary>
    /// No-op implementation of <see cref="IPublishingProgressReporter"/> for background jobs.
    /// </summary>
    /// <remarks>
    /// Used in Hangfire background jobs where there is no HTTP context or connected user
    /// to send progress updates to.
    /// </remarks>
    public class NoOpPublishingProgressReporter : IPublishingProgressReporter
    {
        /// <inheritdoc/>
        public Task ReportProgressAsync(int currentPage, int totalPages, string message)
        {
            // No-op: Background jobs don't have a user to report progress to
            return Task.CompletedTask;
        }
    }
}