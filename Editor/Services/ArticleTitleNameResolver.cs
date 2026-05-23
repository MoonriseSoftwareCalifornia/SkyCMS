// <copyright file="ArticleTitleNameResolver.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using SkyCMS.Drivers.ElFinder.Adapters;

    /// <summary>
    /// elFinder name resolver that substitutes canonical entity IDs with friendly titles
    /// for paths under <c>/pub/articles/{articleNumber}/</c> and <c>/pub/templates/{guid}/</c>.
    /// All other paths are returned unchanged.
    /// </summary>
    public sealed class ArticleTitleNameResolver : IElFinderNameResolver
    {
        private readonly IPublicFileEntryTitleResolver titleResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleTitleNameResolver"/> class.
        /// </summary>
        /// <param name="titleResolver">Article/template title resolver.</param>
        public ArticleTitleNameResolver(IPublicFileEntryTitleResolver titleResolver)
        {
            this.titleResolver = titleResolver;
        }

        /// <inheritdoc />
        public async Task<string> ResolveNameAsync(string fullPath, string rawName, CancellationToken cancellationToken = default)
        {
            var normalized = PublicFileEntryHelper.NormalizePath(fullPath);
            var segments = normalized.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
            {
                return rawName;
            }

            var scope = segments[0];
            var kind = segments[1];
            var idSegment = segments[2];

            if (!string.Equals(idSegment, rawName, System.StringComparison.OrdinalIgnoreCase))
            {
                return rawName;
            }

            if (string.Equals(scope, "pub", System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(kind, "articles", System.StringComparison.OrdinalIgnoreCase)
                && int.TryParse(idSegment, out var articleNumber))
            {
                var titles = await this.titleResolver.GetArticleTitlesByNumberAsync(new[] { articleNumber });
                if (titles.TryGetValue(articleNumber, out var articleTitle) && !string.IsNullOrWhiteSpace(articleTitle))
                {
                    return articleTitle;
                }
            }

            if (string.Equals(scope, "pub", System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(kind, "templates", System.StringComparison.OrdinalIgnoreCase)
                && System.Guid.TryParse(idSegment, out var templateId))
            {
                var titles = await this.titleResolver.GetTemplateTitlesByIdAsync(new[] { templateId });
                if (titles.TryGetValue(templateId, out var templateTitle) && !string.IsNullOrWhiteSpace(templateTitle))
                {
                    return templateTitle;
                }
            }

            return rawName;
        }
    }
}
