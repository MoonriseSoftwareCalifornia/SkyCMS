// <copyright file="GetTemplateQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Get
{
    using System;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Query to retrieve a template by ID with optional inclusion of page design versions.
    /// Compatible with all supported database providers: Azure Cosmos DB, SQL Server, MySQL, and SQLite.
    /// </summary>
    /// <remarks>
    /// This query uses standard EF Core LINQ patterns to ensure compatibility across all database providers.
    /// No provider-specific extensions or syntax are used.
    /// </remarks>
    public sealed class GetTemplateQuery : IQuery<CommandResult<GetTemplateQueryResult>>
    {
        /// <summary>
        /// Gets or sets the template ID to retrieve.
        /// </summary>
        public Guid TemplateId { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether to include page design versions.
        /// </summary>
        public bool IncludeVersions { get; init; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to include only the latest version when IncludeVersions is true.
        /// </summary>
        public bool LatestVersionOnly { get; init; } = false;
    }
}
