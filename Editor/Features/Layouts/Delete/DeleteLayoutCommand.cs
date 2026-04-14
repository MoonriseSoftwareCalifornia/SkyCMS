// <copyright file="DeleteLayoutCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Delete
{
    using System;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to delete a non-default layout.
    /// </summary>
    public class DeleteLayoutCommand : ICommand<CommandResult<bool>>
    {
        /// <summary>
        /// Gets or sets the layout ID.
        /// </summary>
        public Guid LayoutId { get; set; }
    }
}
