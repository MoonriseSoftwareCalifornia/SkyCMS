// <copyright file="PublishingProgressReporter.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Publishing
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.SignalR;
    using Sky.Editor.Hubs;

    /// <summary>
    /// SignalR-based implementation of publishing progress reporting.
    /// </summary>
    public class PublishingProgressReporter : IPublishingProgressReporter
    {
        private readonly IHubContext<PublishingProgressHub> hubContext;
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishingProgressReporter"/> class.
        /// </summary>
        /// <param name="hubContext">SignalR hub context.</param>
        /// <param name="httpContextAccessor">HTTP context accessor to identify the current user.</param>
        public PublishingProgressReporter(
            IHubContext<PublishingProgressHub> hubContext,
            IHttpContextAccessor httpContextAccessor)
        {
            this.hubContext = hubContext;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public async Task ReportProgressAsync(int currentPage, int totalPages, string message)
        {
            var userId = httpContextAccessor.HttpContext?.User?.Claims
                .FirstOrDefault(c => c.Type == "sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return; // No user context, skip progress reporting
            }

            var progressPercentage = totalPages > 0 
                ? (int)Math.Round((double)currentPage / totalPages * 100) 
                : 0;

            await hubContext.Clients.User(userId).SendAsync("ReceiveProgress", new
            {
                progressPercentage,
                currentPage,
                totalPages,
                message,
                timestamp = DateTimeOffset.UtcNow
            });
        }
    }
}