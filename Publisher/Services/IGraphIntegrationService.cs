// <copyright file="IGraphIntegrationService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Cosmos.Common.Models;

namespace Cosmos.Publisher.Services
{
    /// <summary>
    /// Service for integrating with Microsoft Graph API.
    /// Handles user group membership verification and authorization checks.
    /// </summary>
    public interface IGraphIntegrationService
    {
        /// <summary>
        /// Checks if a user belongs to any of the specified groups.
        /// </summary>
        /// <param name="emailAddress">The user's email address.</param>
        /// <param name="requiredGroups">Array of group names to check membership against.</param>
        /// <returns>True if the user is a member of at least one of the required groups; otherwise false.</returns>
        Task<bool> IsUserInGroupsAsync(string emailAddress, string[] requiredGroups);

        /// <summary>
        /// Gets the groups that a user belongs to.
        /// </summary>
        /// <param name="emailAddress">The user's email address.</param>
        /// <returns>List of groups the user belongs to, or empty list if Graph service is unavailable.</returns>
        Task<List<GroupInfo>> GetUserGroupsAsync(string emailAddress);

        /// <summary>
        /// Gets a value indicating whether the Graph service is available/configured.
        /// </summary>
        bool IsAvailable { get; }
    }

    /// <summary>
    /// Represents a group in Microsoft Graph.
    /// </summary>
    public class GroupInfo
    {
        /// <summary>
        /// Gets or sets the display name of the group.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the ID of the group.
        /// </summary>
        public string Id { get; set; }
    }
}
