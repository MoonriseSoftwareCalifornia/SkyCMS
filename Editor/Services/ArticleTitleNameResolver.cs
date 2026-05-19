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
    /// elFinder name resolver that substitutes the article number with the article's title
    /// for any path rooted under <c>/pub/articles/{articleNumber}/</c>.
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
            // Only apply to paths under /pub/articles with a numeric third segment.
            if (!PublicFileEntryHelper.TryGetArticleNumberFromPath(fullPath, out var articleNumber))
            {
                return rawName;
            }

            // The display name is only overridden for the article folder itself
            // (i.e. the third path segment). Sub-folders and files keep their raw names.
            var normalized = PublicFileEntryHelper.NormalizePath(fullPath);
            var segments = normalized.Split('/', System.StringSplitOptions.RemoveEmptyEntries);

            // segments[0]=pub  segments[1]=articles  segments[2]=<articleNumber>
            // Only rename when rawName is the article-number segment.
            if (segments.Length < 3 || !string.Equals(segments[2], rawName, System.StringComparison.OrdinalIgnoreCase))
            {
                return rawName;
            }

            var titles = await this.titleResolver.GetArticleTitlesByNumberAsync(new[] { articleNumber });
            if (titles.TryGetValue(articleNumber, out var title) && !string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return rawName;
        }
    }
}
