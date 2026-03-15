// <copyright file="PromoteLayoutCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Promote
{
    using Cosmos.Common.Features.Shared;
    using System;

    /// <summary>
    /// Command to promote a layout to a new version.
    /// </summary>
    public class PromoteLayoutCommand : ICommand<CommandResult<int>>
    {
        /// <summary>
        /// Gets or sets the layout ID to promote.
        /// </summary>
        public Guid LayoutId { get; set; }
    }
}
