// <copyright file="CreatePageDesignVersionCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Create
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using System;

    /// <summary>
    /// Command to create a new page design version.
    /// </summary>
    public sealed class CreatePageDesignVersionCommand : ICommand<CommandResult<PageDesignVersion>>
    {
        /// <summary>
        /// Gets the template ID this version belongs to.
        /// </summary>
        public Guid TemplateId { get; init; }

        /// <summary>
        /// Gets the layout ID.
        /// </summary>
        public Guid? LayoutId { get; init; }

        /// <summary>
        /// Gets the community layout ID.
        /// </summary>
        public string CommunityLayoutId { get; init; }

        /// <summary>
        /// Gets the title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets the HTML content.
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Gets the page type.
        /// </summary>
        public string PageType { get; init; } = string.Empty;
    }
}