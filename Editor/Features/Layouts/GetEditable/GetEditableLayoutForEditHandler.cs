// <copyright file="GetEditableLayoutForEditHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.GetEditable
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Resolves the editable layout version for editing operations.
    /// Creates a new draft when the latest version is already published.
    /// </summary>
    public class GetEditableLayoutForEditHandler : ICommandHandler<GetEditableLayoutForEditCommand, CommandResult<GetEditableLayoutForEditResult>>
    {
        private readonly ApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetEditableLayoutForEditHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        public GetEditableLayoutForEditHandler(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        /// <inheritdoc/>
        public async Task<CommandResult<GetEditableLayoutForEditResult>> HandleAsync(
            GetEditableLayoutForEditCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return CommandResult<GetEditableLayoutForEditResult>.Failure("Command cannot be null.");
            }

            if (command.LayoutNumber <= 0)
            {
                return CommandResult<GetEditableLayoutForEditResult>.Failure("Layout number must be greater than zero.");
            }

            var family = await dbContext.Layouts
                .Where(l => l.LayoutNumber == command.LayoutNumber)
                .ToListAsync(cancellationToken);

            var latest = family
                .OrderByDescending(l => l.Version ?? 0)
                .FirstOrDefault();

            if (latest == null)
            {
                return CommandResult<GetEditableLayoutForEditResult>.Failure($"Layout {command.LayoutNumber} was not found.");
            }

            if (!latest.Published.HasValue)
            {
                return CommandResult<GetEditableLayoutForEditResult>.Success(new GetEditableLayoutForEditResult
                {
                    Layout = latest,
                    CreatedNewDraft = false,
                });
            }

            var versionCount = family.Count;

            var newLayout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutNumber = latest.LayoutNumber,
                Version = versionCount + 1,
                LayoutName = latest.LayoutName,
                Notes = latest.Notes,
                Head = latest.Head,
                HtmlHeader = latest.HtmlHeader,
                BodyHtmlAttributes = latest.BodyHtmlAttributes,
                FooterHtmlContent = latest.FooterHtmlContent,
                IsDefault = false,
                Published = null,
                CommunityLayoutId = latest.CommunityLayoutId,
                LastModified = DateTimeOffset.UtcNow,
            };

            dbContext.Layouts.Add(newLayout);
            await dbContext.SaveChangesAsync(cancellationToken);

            return CommandResult<GetEditableLayoutForEditResult>.Success(new GetEditableLayoutForEditResult
            {
                Layout = newLayout,
                CreatedNewDraft = true,
            });
        }
    }
}
