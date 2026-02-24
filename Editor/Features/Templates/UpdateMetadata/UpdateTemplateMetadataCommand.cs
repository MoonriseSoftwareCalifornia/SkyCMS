// <copyright file="UpdateTemplateMetadataCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.UpdateMetadata
{
    using System;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to update template metadata (title and description).
    /// This command only updates non-content fields and does not affect PageDesignVersions.
    /// </summary>
    public class UpdateTemplateMetadataCommand : ICommand<CommandResult<Template>>
    {
        /// <summary>
        /// Gets or sets the template ID.
        /// </summary>
        public Guid TemplateId { get; set; }

        /// <summary>
        /// Gets or sets the new title for the template.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the new description for the template.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}
