// <copyright file="GetEditablePageDesignVersionResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.GetEditable
{
    using Cosmos.Common.Data;

    /// <summary>
    /// Result for editable page design version retrieval.
    /// </summary>
    public sealed class GetEditablePageDesignVersionResult
    {
        /// <summary>
        /// Gets or sets the template.
        /// </summary>
        public Template Template { get; set; }

        /// <summary>
        /// Gets or sets the editable page design version.
        /// </summary>
        public PageDesignVersion EditableVersion { get; set; }
    }
}
