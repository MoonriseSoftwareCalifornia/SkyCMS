// <copyright file="CloneArticleValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Clone
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Validates <see cref="CloneArticleCommand"/> instances.
    /// </summary>
    public class CloneArticleValidator
    {
        /// <summary>
        /// Validates a clone article command.
        /// </summary>
        /// <param name="command">Command to validate.</param>
        /// <returns>Dictionary of validation errors, empty if valid.</returns>
        public Dictionary<string, string[]> Validate(CloneArticleCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            if (command.SourceArticleId == Guid.Empty)
            {
                errors["SourceArticleId"] = new[] { "Source article ID is required." };
            }

            if (string.IsNullOrWhiteSpace(command.NewTitle))
            {
                errors["NewTitle"] = new[] { "New title is required." };
            }

            if (command.UserId == Guid.Empty)
            {
                errors["UserId"] = new[] { "User ID is required." };
            }

            return errors;
        }
    }
}