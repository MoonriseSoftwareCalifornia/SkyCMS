// <copyright file="SaveArticleValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Save
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Validates the SaveArticleCommand.
    /// </summary>
    public class SaveArticleValidator
    {
        /// <summary>
        /// Validates the command and returns a dictionary of errors (empty if valid).
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <returns>Dictionary of field errors.</returns>
        public Dictionary<string, string[]> Validate(SaveArticleCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            // ArticleNumber validation
            if (command.ArticleNumber <= 0)
            {
                errors[nameof(command.ArticleNumber)] = new[] { "Article number must be greater than zero." };
            }

            // Title validation
            if (string.IsNullOrWhiteSpace(command.Title))
            {
                errors[nameof(command.Title)] = new[] { "Title is required." };
            }
            else if (command.Title.Length > 254)
            {
                errors[nameof(command.Title)] = new[] { "Title must not exceed 254 characters." };
            }

            // UserId validation
            if (command.UserId == Guid.Empty)
            {
                errors[nameof(command.UserId)] = new[] { "UserId is required." };
            }

            // Category validation (optional, but length check if present)
            if (!string.IsNullOrEmpty(command.Category) && command.Category.Length > 64)
            {
                errors[nameof(command.Category)] = new[] { "Category must not exceed 64 characters." };
            }

            // Introduction validation (optional, but length check if present)
            if (!string.IsNullOrEmpty(command.Introduction) && command.Introduction.Length > 512)
            {
                errors[nameof(command.Introduction)] = new[] { "Introduction must not exceed 512 characters." };
            }

            return errors;
        }
    }
}
