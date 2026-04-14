// <copyright file="DeleteTemplateHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Delete
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Handler for deleting a template and its associated page design versions.
    /// </summary>
    public class DeleteTemplateHandler : ICommandHandler<DeleteTemplateCommand, CommandResult<bool>>
    {
        private readonly ApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteTemplateHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        public DeleteTemplateHandler(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Handles the delete template command.
        /// </summary>
        /// <param name="command">Delete template command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result indicating success or failure.</returns>
        public async Task<CommandResult<bool>> HandleAsync(DeleteTemplateCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (command.TemplateId == Guid.Empty)
            {
                return CommandResult<bool>.Failure("Invalid template ID.");
            }

            // 1. Check if template exists
            var template = await dbContext.Templates.FindAsync(new object[] { command.TemplateId }, cancellationToken);
            if (template == null)
            {
                return CommandResult<bool>.Failure("Template not found.");
            }

            // 2. Check if any articles/pages are using this template
            var articlesUsingTemplate = await dbContext.ArticleCatalog
                .Where(c => c.TemplateId == command.TemplateId)
                .CountAsync(cancellationToken);

            if (articlesUsingTemplate > 0)
            {
                return CommandResult<bool>.Failure(
                    $"Cannot delete template '{template.Title}': " +
                    $"{articlesUsingTemplate} page(s) are currently using it. " +
                    $"Please reassign or delete those pages first.");
            }

            // 3. Delete PageDesignVersions (cascade)
            var versions = await dbContext.PageDesignVersions
                .Where(v => v.TemplateId == command.TemplateId)
                .ToListAsync(cancellationToken);

            dbContext.PageDesignVersions.RemoveRange(versions);

            // 4. Delete Template
            dbContext.Templates.Remove(template);

            await dbContext.SaveChangesAsync(cancellationToken);

            return CommandResult<bool>.Success(true);
        }
    }
}
