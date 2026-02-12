// <copyright file="MsGraphService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/MoonriseSoftwareCalifornia/CosmosCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.MicrosoftGraph
{
    using Azure.Identity;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Graph.Beta;
    using Microsoft.Graph.Beta.Models;

    /// <summary>
    /// This class is used to interact with the Microsoft Graph API. It is used to get the user's profile, the user's app roles, the user's member groups, and the user's groups.
    /// </summary>
    // SEE: https://damienbod.com/2021/09/06/using-azure-security-groups-in-asp-net-core-with-an-azure-b2c-identity-provider/
    public class MsGraphService : IMsGraphService
    {
        private static readonly string[] Scopes = { "https://graph.microsoft.com/.default" };
        private readonly GraphServiceClient graphServiceClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsGraphService"/> class.
        /// </summary>
        /// <param name="graphServiceClient">Graph service client.</param>
        public MsGraphService(GraphServiceClient graphServiceClient)
        {
            this.graphServiceClient = graphServiceClient;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MsGraphService"/> class.
        /// </summary>
        /// <param name="configuration">App configuration.</param>
        public MsGraphService(IConfiguration configuration)
        {
            var entraIdOAuth = configuration.GetSection("MicrosoftOAuth").Get<OAuth>();

            var tenantId = entraIdOAuth?.TenantId ?? configuration.GetValue<string>("AzureAd:TenantId");
            var clientId = entraIdOAuth?.ClientId ?? configuration.GetValue<string>("AzureAd:ClientId");
            var clientSecret = entraIdOAuth?.ClientSecret ?? configuration.GetValue<string>("AzureAd:ClientSecret");

            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            };

            // https://docs.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
            var clientSecretCredential = new ClientSecretCredential(
                tenantId, clientId, clientSecret, options);

            this.graphServiceClient = new GraphServiceClient(clientSecretCredential, Scopes);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MsGraphService"/> class.
        /// </summary>
        /// <param name="clientId">Client ID.</param>
        /// <param name="clientSecret">Client Secret.</param>
        /// <param name="tenantId">Tenant ID.</param>
        public MsGraphService(string clientId, string clientSecret, string tenantId)
        {
            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            };

            // https://docs.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
            var clientSecretCredential = new ClientSecretCredential(
                tenantId, clientId, clientSecret, options);

            this.graphServiceClient = new GraphServiceClient(clientSecretCredential, Scopes);
        }

        /// <summary>
        /// Getst the user's object from the Microsoft Graph API.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public async Task<User?> GetGraphApiUser(string userId)
        {
            return await this.graphServiceClient.Users[userId]
                    .GetAsync(c => c.QueryParameters.Select = new[] { "Identities", "displayName" });
        }

        /// <summary>
        /// Gets the user's object from the Microsoft Graph API by email address.
        /// </summary>
        /// <param name="emailAddress">Email address to search.</param>
        /// <returns>List<User>.</returns>
        public async Task<List<User>?> GetGraphUserByEmailAddress(string emailAddress)
        {
            var response = await this.graphServiceClient.Users
                .GetAsync(a => a.QueryParameters.Filter = $"mail eq '{emailAddress}'");
            return response?.Value;
        }

        /// <summary>
        /// Gets the users from the Microsoft Graph API.
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public async Task<List<User>> GetUsersAsync()
        {
            var users = new List<User>();
            var userCollectionResponse = await this.graphServiceClient.Users.GetAsync(c => c.QueryParameters.Select = new[] { "Identities", "displayName" });

            if (userCollectionResponse != null && userCollectionResponse.Value != null)
            {
                users.AddRange(userCollectionResponse.Value);
            }

            return users;
        }

        /// <summary>
        /// Gets the user's app roles from the Microsoft Graph API.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>User role assignments.</returns>
        public async Task<AppRoleAssignmentCollectionResponse?> GetGraphApiUserAppRoles(string userId)
        {
            return await this.graphServiceClient.Users[userId]
                    .AppRoleAssignments
                    .GetAsync();
        }

        /// <summary>
        /// Gets the user's member groups from the Microsoft Graph API.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public async Task<List<Group>?> GetGraphApiUserMemberGroups(string userId)
        {
            var groups = await this.graphServiceClient.Users[userId].MemberOf.GraphGroup.GetAsync();
            return groups?.Value;
        }

        /// <summary>
        /// Gets the user's groups from the Microsoft Graph API.
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public async Task<List<Group>?> GetGroupsAsync()
        {
            var groups = await this.graphServiceClient.Groups.GetAsync();
            return groups?.Value;
        }

        /// <summary>
        /// Gets the user's profile from the Microsoft Graph API.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public async Task<Profile?> GetUserProfile(string userId)
        {
            var result = await this.graphServiceClient.Users[userId].Profile.GetAsync();
            return result;
        }

        /// <summary>
        /// Gets the group name from the Microsoft Graph API.
        /// </summary>
        /// <param name="groupId">Group ID.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public async Task<string?> GetGroupNameAsync(string groupId)
        {
            var group = await this.graphServiceClient.Groups[groupId].GetAsync();
            return group?.DisplayName;
        }
    }
}
