// <copyright file="IMsGraphService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/MoonriseSoftwareCalifornia/CosmosCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.MicrosoftGraph
{
    using Microsoft.Graph.Beta.Models;

    /// <summary>
    /// IMsGraphService Interface.
    /// </summary>
    public interface IMsGraphService
    {
        /// <summary>
        /// Gets the Graph API User.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>User.</returns>
        Task<User?> GetGraphApiUser(string userId);

        /// <summary>
        /// Gets the user app roles.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>AppRoleAssignmentCollectionResponse.</returns>
        Task<AppRoleAssignmentCollectionResponse?> GetGraphApiUserAppRoles(string userId);

        /// <summary>
        /// Gets the group membership for a user.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>List of Groups.</returns>
        Task<List<Group>?> GetGraphApiUserMemberGroups(string userId);

        /// <summary>
        /// Get a group name.
        /// </summary>
        /// <param name="groupId">Group ID.</param>
        /// <returns>String.</returns>
        Task<string?> GetGroupNameAsync(string groupId);

        /// <summary>
        /// Gets the user groups.
        /// </summary>
        /// <returns>List of groups.</returns>
        Task<List<Group>?> GetGroupsAsync();

        /// <summary>
        /// Gets a user's profile.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>User profile.</returns>
        Task<Profile?> GetUserProfile(string userId);

        /// <summary>
        /// Gets a users by email address.
        /// </summary>
        /// <param name="emailAddress">Email address to search.</param>
        /// <returns>List of users (hopefully only one).</returns>
        Task<List<User>?> GetGraphUserByEmailAddress(string emailAddress);

        /// <summary>
        /// Gets a list of users.
        /// </summary>
        /// <returns>List of users.</returns>
        Task<List<User>> GetUsersAsync();
    }
}