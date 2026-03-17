// <copyright file="GraphIntegrationService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Cosmos.MicrosoftGraph;
using Cosmos.Publisher.Services;
using Microsoft.Extensions.Logging;

namespace Cosmos.Publisher.Services
{
    /// <summary>
    /// Service for integrating with Microsoft Graph API.
    /// Provides capabilities for checking user group membership and authorization.
    /// </summary>
    public class GraphIntegrationService : IGraphIntegrationService
    {
        private readonly MsGraphService msGraphService;
        private readonly ILogger<GraphIntegrationService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphIntegrationService"/> class.
        /// </summary>
        /// <param name="msGraphService">The Microsoft Graph service for API calls.</param>
        /// <param name="logger">Logger instance.</param>
        public GraphIntegrationService(MsGraphService msGraphService, ILogger<GraphIntegrationService> logger)
        {
            this.msGraphService = msGraphService ?? throw new ArgumentNullException(nameof(msGraphService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public bool IsAvailable => this.msGraphService != null;

        /// <inheritdoc/>
        public async Task<bool> IsUserInGroupsAsync(string emailAddress, string[] requiredGroups)
        {
            if (string.IsNullOrWhiteSpace(emailAddress))
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            if (requiredGroups == null || requiredGroups.Length == 0)
            {
                return true;
            }

            if (!this.IsAvailable)
            {
                this.logger.LogWarning("Graph service is not available; cannot check group membership for {Email}", emailAddress);
                return false;
            }

            try
            {
                var userGroups = await this.GetUserGroupsAsync(emailAddress);
                var userGroupNames = userGroups.Select(g => g.DisplayName).ToList();
                return requiredGroups.Any(rg => userGroupNames.Contains(rg));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error checking group membership for {Email}", emailAddress);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<GroupInfo>> GetUserGroupsAsync(string emailAddress)
        {
            if (string.IsNullOrWhiteSpace(emailAddress))
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            if (!this.IsAvailable)
            {
                this.logger.LogWarning("Graph service is not available; cannot retrieve groups for {Email}", emailAddress);
                return new List<GroupInfo>();
            }

            try
            {
                var graphUsers = await this.msGraphService.GetGraphUserByEmailAddress(emailAddress);
                if (graphUsers == null || !graphUsers.Any())
                {
                    this.logger.LogWarning("User not found in Graph API: {Email}", emailAddress);
                    return new List<GroupInfo>();
                }

                var userId = graphUsers.FirstOrDefault()?.Id;
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return new List<GroupInfo>();
                }

                var graphGroups = await this.msGraphService.GetGraphApiUserMemberGroups(userId);
                if (graphGroups == null)
                {
                    return new List<GroupInfo>();
                }

                return graphGroups
                    .Select(g => new GroupInfo { DisplayName = g.DisplayName, Id = g.Id })
                    .ToList();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error retrieving groups for user {Email}", emailAddress);
                return new List<GroupInfo>();
            }
        }
    }
}
