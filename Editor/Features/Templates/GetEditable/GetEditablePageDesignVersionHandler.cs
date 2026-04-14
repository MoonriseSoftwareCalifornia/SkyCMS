// <copyright file="GetEditablePageDesignVersionHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.GetEditable
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Html;

    /// <summary>
    /// Resolves the editable template version for editing operations.
    /// </summary>
    public class GetEditablePageDesignVersionHandler : ICommandHandler<GetEditablePageDesignVersionCommand, CommandResult<GetEditablePageDesignVersionResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IArticleHtmlService htmlService;
        private readonly IClock clock;
        private readonly ILogger<GetEditablePageDesignVersionHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetEditablePageDesignVersionHandler"/> class.
        /// </summary>
        public GetEditablePageDesignVersionHandler(
            ApplicationDbContext dbContext,
            IArticleHtmlService htmlService,
            IClock clock,
            ILogger<GetEditablePageDesignVersionHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.htmlService = htmlService ?? throw new ArgumentNullException(nameof(htmlService));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<CommandResult<GetEditablePageDesignVersionResult>> HandleAsync(
            GetEditablePageDesignVersionCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return CommandResult<GetEditablePageDesignVersionResult>.Failure("Command cannot be null.");
            }

            if (command.TemplateId == Guid.Empty)
            {
                return CommandResult<GetEditablePageDesignVersionResult>.Failure("Template ID cannot be empty.");
            }

            var template = await dbContext.Templates
                .FirstOrDefaultAsync(t => t.Id == command.TemplateId, cancellationToken);

            if (template == null)
            {
                return CommandResult<GetEditablePageDesignVersionResult>.Failure($"Template {command.TemplateId} was not found.");
            }

            var editableVersion = await dbContext.PageDesignVersions
                .Where(v => v.TemplateId == command.TemplateId && v.Published == null)
                .OrderByDescending(v => v.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (editableVersion == null)
            {
                // Will return 0 if no records exist, so this will be the first version.
                var latestVersionNumber = await dbContext.PageDesignVersions
                    .Where(v => v.TemplateId == command.TemplateId)
                    .OrderByDescending(v => v.Version)
                    .Select(v => v.Version)
                    .FirstOrDefaultAsync(cancellationToken);

                editableVersion = new PageDesignVersion
                {
                    Id = Guid.NewGuid(),
                    TemplateId = template.Id,
                    LayoutId = template.LayoutId,
                    CommunityLayoutId = template.CommunityLayoutId,
                    Version = latestVersionNumber + 1,
                    Title = template.Title,
                    Description = template.Description,
                    Content = htmlService.EnsureEditableMarkers(template.Content),
                    PageType = template.PageType,
                    Published = null,
                    Modified = clock.UtcNow
                };

                dbContext.PageDesignVersions.Add(editableVersion);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Created editable page design version {Version} for template {TemplateId}",
                    editableVersion.Version,
                    template.Id);
            }

            return CommandResult<GetEditablePageDesignVersionResult>.Success(new GetEditablePageDesignVersionResult
            {
                Template = template,
                EditableVersion = editableVersion
            });
        }
    }
}
