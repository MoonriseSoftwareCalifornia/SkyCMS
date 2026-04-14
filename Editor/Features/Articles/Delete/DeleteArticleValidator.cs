// <copyright file="DeleteArticleValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Delete
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Validates DeleteArticleCommand requests.
    /// </summary>
    public class DeleteArticleValidator
    {
        private readonly ApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteArticleValidator"/> class.
        /// </summary>
        public DeleteArticleValidator(ApplicationDbContext dbContext = null)
        {
            this.dbContext = dbContext;
        }

        /// <summary>
        /// Validates the delete article command.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string[]> Validate(DeleteArticleCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            if (command.ArticleNumber <= 0)
            {
                errors[nameof(command.ArticleNumber)] = new[] { "Article number must be greater than 0." };
            }

            return errors;
        }

        /// <summary>
        /// Validates article exists and is not root page.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<Dictionary<string, string[]>> ValidateAsync(
            DeleteArticleCommand command,
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

            // Check article exists
            var article = await this.dbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == command.ArticleNumber, ct);

            if (article == null)
            {
                errors[nameof(command.ArticleNumber)] = new[] { "Article not found." };
                return errors;
            }

            // Check if it's the root page (cannot delete)
            if (article.UrlPath != null && article.UrlPath.Equals("root", System.StringComparison.OrdinalIgnoreCase))
            {
                errors[nameof(command.ArticleNumber)] = new[] { "Cannot delete the home page." };
            }

            return errors;
        }
    }
}
