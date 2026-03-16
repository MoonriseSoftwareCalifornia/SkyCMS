// <copyright file="SubClaimUserIdProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Infrastructure.SignalR
{
    using Microsoft.AspNetCore.SignalR;
    using System.Linq;

    /// <summary>
    /// Maps SignalR connections to users based on the "sub" claim.
    /// </summary>
    public class SubClaimUserIdProvider : IUserIdProvider
    {
        /// <summary>
        /// Gets the user identifier from the connection's principal.
        /// </summary>
        /// <param name="connection">The hub connection.</param>
        /// <returns>The user identifier from the "sub" claim.</returns>
        public string GetUserId(HubConnectionContext connection)
        {
            return connection.User?.Claims
                .FirstOrDefault(c => c.Type == "sub")?.Value;
        }
    }
}