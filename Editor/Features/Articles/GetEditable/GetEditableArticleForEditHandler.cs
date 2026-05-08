// <copyright file="GetEditableArticleForEditHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.GetEditable
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Sky.Editor.Features.Articles.CreateVersion;

    /// <summary>
    /// Resolves the editable article version for editing operations.
    /// Creates a new draft when the latest version is already published.
    /// </summary>
    public class GetEditableArticleForEditHandler : ICommandHandler<GetEditableArticleForEditCommand, CommandResult<GetEditableArticleForEditResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IMediator mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetEditableArticleForEditHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="mediator">Mediator service.</param>
        public GetEditableArticleForEditHandler(ApplicationDbContext dbContext, IMediator mediator)
        {
            this.dbContext = dbContext;
            this.mediator = mediator;
        }

        /// <inheritdoc/>
        public async Task<CommandResult<GetEditableArticleForEditResult>> HandleAsync(
            GetEditableArticleForEditCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return CommandResult<GetEditableArticleForEditResult>.Failure("Command cannot be null.");
            }

            if (command.ArticleNumber <= 0)
            {
                return CommandResult<GetEditableArticleForEditResult>.Failure("Article number must be greater than zero.");
            }

            var family = await dbContext.Articles
                .Where(a => a.ArticleNumber == command.ArticleNumber)
                .ToListAsync(cancellationToken);

            var latest = family
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefault();

            if (latest == null)
            {
                return CommandResult<GetEditableArticleForEditResult>.Failure($"Article {command.ArticleNumber} was not found.");
            }

            if (!latest.Published.HasValue)
            {
                return CommandResult<GetEditableArticleForEditResult>.Success(new GetEditableArticleForEditResult
                {
                    Article = latest,
                    CreatedNewDraft = false,
                });
            }

            var versionResult = await mediator.SendAsync(new CreateArticleVersionCommand
            {
                ArticleNumber = latest.ArticleNumber,
            });

            if (!versionResult.IsSuccess)
            {
                return versionResult.Errors != null
                    ? CommandResult<GetEditableArticleForEditResult>.Failure(versionResult.Errors)
                    : CommandResult<GetEditableArticleForEditResult>.Failure(
                        versionResult.ErrorMessage ?? "Failed to create editable article version.");
            }

            var refreshedFamily = await dbContext.Articles
                .Where(a => a.ArticleNumber == command.ArticleNumber)
                .ToListAsync(cancellationToken);

            var editable = refreshedFamily
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefault();

            if (editable == null)
            {
                return CommandResult<GetEditableArticleForEditResult>.Failure("Failed to load editable article version after version creation.");
            }

            return CommandResult<GetEditableArticleForEditResult>.Success(new GetEditableArticleForEditResult
            {
                Article = editable,
                CreatedNewDraft = true,
            });
        }
    }
}
