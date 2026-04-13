// <copyright file="PublishPageDesignVersionCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Publishing
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using System;

    /// <summary>
    /// Command to publish a page design version.
    /// </summary>
    /// <remarks>
    /// Publishing a page design version:
    /// <list type="bullet">
    ///   <item>Sets the Published timestamp on the PageDesignVersion.</item>
    ///   <item>Updates the Template entity with the published content.</item>
    ///   <item>Creates new article versions for all articles using this template.</item>
    ///   <item>Republishes articles that were previously published.</item>
    /// </list>
    /// </remarks>
    public sealed class PublishPageDesignVersionCommand : ICommand<CommandResult<Template>>
    {
        /// <summary>
        /// Gets the page design version ID to publish.
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        /// Gets the user ID performing the publish operation.
        /// </summary>
        public Guid UserId { get; init; }
    }
}