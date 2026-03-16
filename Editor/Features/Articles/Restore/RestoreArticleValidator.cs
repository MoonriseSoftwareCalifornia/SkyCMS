// <copyright file="RestoreArticleValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Restore
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Validates RestoreArticleCommand requests.
    /// </summary>
    public class RestoreArticleValidator
    {
        private readonly ApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestoreArticleValidator"/> class.
        /// </summary>
        public RestoreArticleValidator(ApplicationDbContext dbContext = null)
        {
            this.dbContext = dbContext;
        }

        /// <summary>
        /// Validates the restore article command.
        /// </summary>
        public Dictionary<string, string[]> Validate(RestoreArticleCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            if (command.ArticleNumber <= 0)
            {
                errors[nameof(command.ArticleNumber)] = new[] { "Article number must be greater than 0." };
            }

            return errors;
        }

        /// <summary>
        /// Validates article exists in deleted state.
        /// </summary>
        public async Task<Dictionary<string, string[]>> ValidateAsync(
            RestoreArticleCommand command,
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

            // Check if it's in deleted state (can only restore deleted articles)
            if (article.StatusCode != (int)StatusCodeEnum.Deleted)
            {
                errors[nameof(command.ArticleNumber)] = new[] { "Only deleted articles can be restored." };
            }

            return errors;
        }
    }
}
