// <copyright file="ImportLayoutCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Import
{
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to import a community layout.
    /// </summary>
    public class ImportLayoutCommand : ICommand<CommandResult<bool>>
    {
        /// <summary>
        /// Gets or sets the community layout ID.
        /// </summary>
        public string CommunityLayoutId { get; set; }
    }
}
