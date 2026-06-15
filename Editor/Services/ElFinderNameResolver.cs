// <copyright file="ElFinderNameResolver.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.DynamicConfig;
    using SkyCMS.Drivers.ElFinder.Adapters;

    /// <summary>
    /// elFinder name resolver that substitutes canonical entity IDs with friendly titles
    /// for paths under <c>/pub/articles/{articleNumber}/</c> and <c>/pub/templates/{guid}/</c>.
    /// All other paths are returned unchanged.
    /// </summary>
    public sealed class ElFinderNameResolver : IElFinderNameResolver
    {
        private readonly IFileEntryTitleService titleResolver;
        private readonly IDynamicConfigurationProvider configProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ElFinderNameResolver"/> class.
        /// </summary>
        /// <param name="titleResolver">Article/template title resolver.</param>>>
        /// <param name="configProvider">Dynamic configuration provider for tenant settings.</param>
        public ElFinderNameResolver(IFileEntryTitleService titleResolver, IDynamicConfigurationProvider configProvider)
        {
            this.titleResolver = titleResolver;
            this.configProvider = configProvider;
        }

        /// <inheritdoc />
        public async Task<string> ResolveNameAsync(string fullPath, string rawName, CancellationToken cancellationToken = default)
        {
            var normalized = FileEntryPathHelper.NormalizePath(fullPath);

            // Only resolve when the raw name is the ID segment (i.e. the caller hasn't already
            // substituted a friendly name).  We check this by requiring that the last qualifying
            // segment in the canonical path equals rawName.
            var segments = normalized.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3 || !string.Equals(segments[2], rawName, System.StringComparison.OrdinalIgnoreCase))
            {
                return rawName;
            }

            if (FileEntryPathHelper.TryGetArticleNumberFromPath(normalized, out var articleNumber))
            {
                var tenantDomain = this.configProvider.GetTenantDomainNameFromRequest();
                var titles = await this.titleResolver.GetArticleTitlesByNumberAsync(new int[] { articleNumber }, tenantDomain);
                if (titles.TryGetValue(articleNumber, out var articleTitle) && !string.IsNullOrWhiteSpace(articleTitle))
                {
                    return articleTitle;
                }
            }

            if (FileEntryPathHelper.TryGetTemplateId(normalized, out var templateId))
            {
                var titles = await this.titleResolver.GetTemplateTitlesByIdAsync(new System.Guid[] { templateId });
                if (titles.TryGetValue(templateId, out var templateTitle) && !string.IsNullOrWhiteSpace(templateTitle))
                {
                    return templateTitle;
                }
            }

            return rawName;
        }
    }
}
