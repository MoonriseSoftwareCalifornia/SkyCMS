// <copyright file="LayoutVersioningService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layouts
{
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Html;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Shared service for creating layout versions and importing community templates.
    /// </summary>
    public class LayoutVersioningService : ILayoutVersioningService
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IArticleHtmlService htmlService;
        private readonly ILogger<LayoutVersioningService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutVersioningService"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="htmlService">HTML service.</param>
        /// <param name="logger">Logger.</param>
        public LayoutVersioningService(
            ApplicationDbContext dbContext,
            IArticleHtmlService htmlService,
            ILogger<LayoutVersioningService> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.htmlService = htmlService ?? throw new ArgumentNullException(nameof(htmlService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Layout> CreateNewVersionAsync(Layout layout, CancellationToken cancellationToken = default)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var newLayout = new Layout
            {
                CommunityLayoutId = layout.CommunityLayoutId,
                LayoutName = layout.LayoutName,
                Notes = layout.Notes,
                Head = layout.Head,
                HtmlHeader = layout.HtmlHeader,
                BodyHtmlAttributes = layout.BodyHtmlAttributes,
                FooterHtmlContent = layout.FooterHtmlContent,
                IsDefault = false,
                LayoutNumber = layout.LayoutNumber,
                Version = (await dbContext.Layouts.Where(l => l.LayoutNumber == layout.LayoutNumber).CountAsync(cancellationToken)) + 1,
                LastModified = DateTimeOffset.UtcNow,
                Published = null,
                Id = Guid.NewGuid()
            };

            dbContext.Layouts.Add(newLayout);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Created new version of layout family LayoutNumber={LayoutNumber}, Version={Version}",
                newLayout.LayoutNumber,
                newLayout.Version);

            return newLayout;
        }

        /// <inheritdoc/>
        public async Task ImportCommunityTemplatesAsync(
            IEnumerable<Template> communityPages,
            Guid layoutId,
            int layoutNumber,
            CancellationToken cancellationToken = default)
        {
            if (communityPages == null)
            {
                throw new ArgumentNullException(nameof(communityPages));
            }

            foreach (var page in communityPages)
            {
                var template = new Template
                {
                    CommunityLayoutId = page.CommunityLayoutId,
                    Content = htmlService.EnsureEditableMarkers(page.Content),
                    Description = page.Description,
                    LayoutId = layoutId,
                    LayoutNumber = layoutNumber,
                    Title = page.Title,
                    PageType = page.PageType,
                    Id = Guid.NewGuid()
                };

                dbContext.Templates.Add(template);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
