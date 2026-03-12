// <copyright file="TrashArticleValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Editor.Features.Articles.Trash
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Validates requests to permanently trash an article.
    /// </summary>
    public class TrashArticleValidator
    {
        private readonly ApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrashArticleValidator"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        public TrashArticleValidator(ApplicationDbContext dbContext = null)
        {
            this.dbContext = dbContext;
        }

        /// <summary>
        /// Performs basic synchronous validation.
        /// </summary>
        /// <param name="command">Trash command.</param>
        /// <returns>Validation errors.</returns>
        public Dictionary<string, string[]> Validate(TrashArticleCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            if (command?.ArticleNumber <= 0)
            {
                errors[nameof(TrashArticleCommand.ArticleNumber)] = new[] { "Article number must be greater than 0." };
            }

            return errors;
        }

        /// <summary>
        /// Validates article existence and deleted-state preconditions.
        /// </summary>
        /// <param name="command">Trash command.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Validation errors.</returns>
        public async Task<Dictionary<string, string[]>> ValidateAsync(
            TrashArticleCommand command,
            CancellationToken ct = default)
        {
            var errors = this.Validate(command);
            if (errors.Count > 0)
            {
                return errors;
            }

            if (this.dbContext == null)
            {
                return errors;
            }

            var articles = await this.dbContext.Articles
                .Where(a => a.ArticleNumber == command.ArticleNumber)
                .ToListAsync(ct);

            if (articles.Count == 0)
            {
                errors[nameof(TrashArticleCommand.ArticleNumber)] = new[] { "Article not found." };
                return errors;
            }

            if (articles.Any(a => a.UrlPath != null && a.UrlPath.Equals("root", System.StringComparison.OrdinalIgnoreCase)))
            {
                errors[nameof(TrashArticleCommand.ArticleNumber)] = new[] { "Cannot permanently trash the home page." };
                return errors;
            }

            if (articles.Any(a => a.StatusCode != (int)StatusCodeEnum.Deleted))
            {
                errors[nameof(TrashArticleCommand.ArticleNumber)] = new[] { "Article must be in deleted state before permanent trash." };
            }

            return errors;
        }
    }
}
