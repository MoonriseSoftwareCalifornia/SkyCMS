// <copyright file="TemplateContext.cs" company="Moonrise Software, LLC">
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
    /// Implementation of template context providing infrastructure dependencies.
    /// </summary>
    public class TemplateContext : ITemplateContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateContext"/> class.
        /// </summary>
        /// <param name="database">The database context.</param>
        /// <param name="dynamicConfigProvider">The dynamic configuration provider (may be null in single-tenant scenarios).</param>
        public TemplateContext(
            ApplicationDbContext database,
            IDynamicConfigurationProvider? dynamicConfigProvider)
        {
            Database = database;
            DynamicConfigProvider = dynamicConfigProvider;
        }

        /// <inheritdoc/>
        public ApplicationDbContext Database { get; }

        /// <inheritdoc/>
        public IDynamicConfigurationProvider? DynamicConfigProvider { get; }
    }
}
