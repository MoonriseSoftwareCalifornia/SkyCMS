// <copyright file="GetTemplateQueryResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Get
{
    using Cosmos.Common.Data;
    using System.Collections.Generic;

    /// <summary>
    /// Result data transfer object for template retrieval queries.
    /// Contains the template entity and optional page design versions.
    /// </summary>
    public class GetTemplateQueryResult
    {
        /// <summary>
        /// Gets or sets the retrieved template entity.
        /// </summary>
        public Template Template { get; set; }

        /// <summary>
        /// Gets or sets the page design versions (empty if not requested or none found).
        /// </summary>
        public IEnumerable<PageDesignVersion> Versions { get; set; } = new List<PageDesignVersion>();
    }
}
