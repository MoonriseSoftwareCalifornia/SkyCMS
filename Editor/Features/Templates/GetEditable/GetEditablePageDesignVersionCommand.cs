// <copyright file="GetEditablePageDesignVersionCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.GetEditable
{
    using Cosmos.Common.Features.Shared;
    using System;

    /// <summary>
    /// Command to get the latest editable page design version for a template.
    /// Creates a new editable version from the published template when needed.
    /// </summary>
    public sealed class GetEditablePageDesignVersionCommand : ICommand<CommandResult<GetEditablePageDesignVersionResult>>
    {
        /// <summary>
        /// Gets or sets the template ID.
        /// </summary>
        public Guid TemplateId { get; init; }
    }
}
