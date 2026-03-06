// <copyright file="LiveEditorHub.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Hubs
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Services;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Sky.Cms.Models;

    /// <summary>
    /// Live editor collaboration hub.
    /// </summary>
    /// [Authorize(Roles = "Reviewers, Administrators, Editors, Authors")]
    public class LiveEditorHub : Hub
    {
        private readonly ILogger<LiveEditorHub> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LiveEditorHub"/> class.
        /// Constructor.
        /// </summary>
        /// <param name="logger">Logger service.</param>
        public LiveEditorHub(ILogger<LiveEditorHub> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Adds an editor to the page group.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task JoinArticleGroup(string articleNumber)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetArticleGroupName(articleNumber));
        }

        /// <summary>
        /// Joins the editing room.
        /// </summary>
        /// <param name="data">Signal data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task Notification(string data)
        {
            try
            {
                var model = JsonConvert.DeserializeObject<EditPostViewModel>(data);
                model.Data = CryptoJsDecryption.Decrypt(model.Data);

                switch (model.Command)
                {
                    case "join":
                        await Groups.AddToGroupAsync(Context.ConnectionId, GetArticleGroupName(model.ArticleNumber));
                        break;
                    case "save":
                    case "SavePageProperties":
                        // Alert others
                        await Clients.OthersInGroup(GetArticleGroupName(model.ArticleNumber)).SendCoreAsync("broadcastMessage", new[] { JsonConvert.SerializeObject(model) });
                        break;
                    default:
                        await Clients.OthersInGroup(GetArticleGroupName(model.ArticleNumber)).SendCoreAsync("broadcastMessage", new[] { data });
                        break;
                }
            }
            catch (Exception e)
            {
                logger.LogError($"{e.Message}", e);
            }
        }

        /// <summary>
        /// Sends a signal to update editors in the group.
        /// </summary>
        /// <param name="editorId">Editor ID.</param>
        /// <param name="data">Html Data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task UpdateEditors(string editorId, string data)
        {
            await Clients.OthersInGroup(editorId).SendCoreAsync("updateEditors", new[] { CryptoJsDecryption.Decrypt(data) });
        }

        private string GetArticleGroupName(int articleNumber)
        {
            return $"Article:{articleNumber}";
        }

        private string GetArticleGroupName(string articleNumber)
        {
            return $"Article:{articleNumber}";
        }
    }
}
