// <copyright file="SavePageDesignVersionCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Save
{
    using System;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to save (update) an existing page design version.
    /// </summary>
    public sealed class SavePageDesignVersionCommand : ICommand<CommandResult<PageDesignVersion>>
    {
        /// <summary>
        /// Gets or sets the page design version ID.
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        /// Gets or sets the layout ID.
        /// </summary>
        public Guid? LayoutId { get; init; }

        /// <summary>
        /// Gets or sets the community layout ID.
        /// </summary>
        public string CommunityLayoutId { get; init; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTML content.
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the page type.
        /// </summary>
        public string PageType { get; init; } = string.Empty;
    }
}