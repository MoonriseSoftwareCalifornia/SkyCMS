// <copyright file="PublishLayoutCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Publish
{
    using System;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to publish a layout as the default layout.
    /// </summary>
    public class PublishLayoutCommand : ICommand<CommandResult<bool>>
    {
        /// <summary>
        /// Gets or sets the layout ID to publish.
        /// </summary>
        public Guid LayoutId { get; set; }
    }
}
