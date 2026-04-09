// <copyright file="CreateLayoutHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Create
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handles creating a new layout.
    /// </summary>
    public class CreateLayoutHandler : ICommandHandler<CreateLayoutCommand, CommandResult<Guid>>
    {
        private const string NewLayoutPrefix = "New Layout";
        private const string NewLayoutNotes = "New layout created. Please customize using code editor.";

        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<CreateLayoutHandler> logger;
        private readonly CreateLayoutValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateLayoutHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="logger">Logger.</param>
        public CreateLayoutHandler(
            ApplicationDbContext dbContext,
            ILogger<CreateLayoutHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            validator = new CreateLayoutValidator();
        }

        /// <inheritdoc/>
        public async Task<CommandResult<Guid>> HandleAsync(
            CreateLayoutCommand command,
            CancellationToken cancellationToken = default)
        {
            var validationErrors = validator.Validate(command);
            if (validationErrors.Any())
            {
                return CommandResult<Guid>.Failure(validationErrors);
            }

            try
            {
                var layoutCount = await dbContext.Layouts.CountAsync(cancellationToken);

                var maxLayoutNumber = await dbContext.Layouts
                    .Where(l => l.LayoutNumber > 0)
                    .MaxAsync(l => (int?)l.LayoutNumber, cancellationToken) ?? 0;

                var layout = new Layout
                {
                    IsDefault = layoutCount == 0,
                    Published = layoutCount == 0 ? DateTimeOffset.UtcNow : (DateTimeOffset?)null,
                    LayoutName = $"{NewLayoutPrefix} {layoutCount}",
                    Notes = NewLayoutNotes,
                    LayoutNumber = maxLayoutNumber + 1,
                    Version = 1
                };

                dbContext.Layouts.Add(layout);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Created new layout {LayoutId} with name '{LayoutName}', LayoutNumber={LayoutNumber}",
                    layout.Id,
                    layout.LayoutName,
                    layout.LayoutNumber);

                return CommandResult<Guid>.Success(layout.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating new layout");
                return CommandResult<Guid>.Failure("An error occurred while creating the layout");
            }
        }
    }
}
