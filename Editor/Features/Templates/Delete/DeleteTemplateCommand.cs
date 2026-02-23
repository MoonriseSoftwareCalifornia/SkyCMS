// <copyright file="DeleteTemplateCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Delete
{
    using System;
    using Sky.Editor.Features.Shared;

    /// <summary>
    /// Command to delete a template and its associated page design versions.
    /// </summary>
    public class DeleteTemplateCommand : ICommand<CommandResult<bool>>
    {
        /// <summary>
        /// Gets or sets the ID of the template to delete.
        /// </summary>
        public Guid TemplateId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user performing the deletion.
        /// </summary>
        public Guid UserId { get; set; }
    }
}
