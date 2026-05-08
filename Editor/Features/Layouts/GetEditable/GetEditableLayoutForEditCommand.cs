// <copyright file="GetEditableLayoutForEditCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.GetEditable
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to get the editable version of a layout for editing operations.
    /// If the latest version is published, a new draft version is created.
    /// </summary>
    public class GetEditableLayoutForEditCommand : ICommand<CommandResult<GetEditableLayoutForEditResult>>
    {
        /// <summary>
        /// Gets or sets the layout number to retrieve editable version for.
        /// </summary>
        public int LayoutNumber { get; set; }
    }

    /// <summary>
    /// Result containing the editable layout and creation status.
    /// </summary>
    public class GetEditableLayoutForEditResult
    {
        /// <summary>
        /// Gets or sets the layout entity that is ready for editing.
        /// </summary>
        public Layout Layout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a new draft version was created.
        /// </summary>
        public bool CreatedNewDraft { get; set; }
    }
}
