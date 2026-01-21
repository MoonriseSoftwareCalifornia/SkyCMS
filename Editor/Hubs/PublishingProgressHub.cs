// <copyright file="PublishingProgressHub.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Editor.Hubs
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.SignalR;

    /// <summary>
    /// SignalR hub for broadcasting static page generation progress to connected clients.
    /// </summary>
    [Authorize(Roles = "Editors,Administrators")]
    public class PublishingProgressHub : Hub
    {
        /// <summary>
        /// Called when a client connects to the hub.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        /// <param name="exception">Exception that caused the disconnect, if any.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public override async Task OnDisconnectedAsync(System.Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}