// <copyright file="PublishArticleValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Publish
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Validates PublishArticleCommand requests.
    /// Ensures article exists and is eligible for publishing.
    /// </summary>
    public class PublishArticleValidator
    {
        private readonly ApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishArticleValidator"/> class.
        /// </summary>
        /// <param name="dbContext">Database context for validation. Optional for basic validation.</param>
        public PublishArticleValidator(ApplicationDbContext dbContext = null)
        {
            this.dbContext = dbContext;
        }

        /// <summary>
        /// Validates a PublishArticleCommand.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <returns>Dictionary of validation errors, empty if no errors.</returns>
        public Dictionary<string, string[]> Validate(PublishArticleCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            // Basic validation: ArticleId cannot be empty
            if (command.ArticleId == Guid.Empty)
            {
                errors[nameof(command.ArticleId)] = new[] { "Article ID is required." };
            }

            return errors;
        }

        /// <summary>
        /// Validates article exists asynchronously.
        /// </summary>
        public async Task<Dictionary<string, string[]>> ValidateAsync(
            PublishArticleCommand command,
            CancellationToken ct = default)
        {
            var errors = this.Validate(command);

            // If basic validation failed, return early
            if (errors.Count > 0)
            {
                return errors;
            }

            // Database validation only if dbContext is available
            if (this.dbContext == null)
            {
                return errors;
            }

            // Check article exists
            var article = await this.dbContext.Articles
                .FirstOrDefaultAsync(a => a.Id == command.ArticleId, ct);

            if (article == null)
            {
                errors[nameof(command.ArticleId)] = new[] { "Article not found." };
                return errors;
            }

            // Check article is not deleted
            if (article.StatusCode == (int)StatusCodeEnum.Deleted)
            {
                errors[nameof(command.ArticleId)] = new[] { "Cannot publish deleted article." };
            }

            return errors;
        }
    }
}
