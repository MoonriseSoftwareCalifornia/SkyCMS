// <copyright file="ITemplateContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Templates
{
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;

    /// <summary>
    /// Provides access to infrastructure dependencies required for template operations.
    /// </summary>
    /// <remarks>
    /// This composite groups infrastructure services used by template management:
    /// database context for template persistence and dynamic configuration for tenant resolution.
    /// </remarks>
    public interface ITemplateContext
    {
        /// <summary>
        /// Gets the database context for template persistence.
        /// </summary>
        ApplicationDbContext Database { get; }

        /// <summary>
        /// Gets the dynamic configuration provider for tenant resolution.
        /// </summary>
        /// <remarks>
        /// Used in multi-tenant scenarios to determine which tenant's templates to load.
        /// May be null in single-tenant deployments.
        /// </remarks>
        IDynamicConfigurationProvider? DynamicConfigProvider { get; }
    }
}
