// <copyright file="PublishArticleCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Editor.Features.Articles.Publish
{
    using System;
    using System.Collections.Generic;
    using Cosmos.Common.Features.Shared;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Command to publish an article version, updating its published timestamp and triggering CDN purge.
    /// </summary>
    public class PublishArticleCommand : ICommand<CommandResult<PublishArticleCommandResult>>
    {
        /// <summary>
        /// Gets or sets the article ID (row GUID) to publish.
        /// </summary>
        public Guid ArticleId { get; set; }

        /// <summary>
        /// Gets or sets the optional publish timestamp. If null, current UTC time is used.
        /// </summary>
        public DateTimeOffset? PublishTime { get; set; }
    }

    /// <summary>
    /// Result returned from PublishArticleCommand containing CDN purge information.
    /// </summary>
    public class PublishArticleCommandResult
    {
        /// <summary>
        /// Gets or sets the list of CDN purge results (empty if no CDN configured).
        /// </summary>
        public List<CdnResult> CdnResults { get; set; } = new List<CdnResult>();
    }
}
