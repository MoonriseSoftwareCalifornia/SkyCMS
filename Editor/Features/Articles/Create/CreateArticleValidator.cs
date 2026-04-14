// <copyright file="CreateArticleValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Create
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Validates the CreateArticleCommand.
    /// </summary>
    public class CreateArticleValidator
    {
        public Dictionary<string, string[]> Validate(CreateArticleCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(command.Title))
            {
                errors[nameof(command.Title)] = new[] { "Title is required." };
            }
            else if (command.Title.Length > 254)
            {
                errors[nameof(command.Title)] = new[] { "Title must not exceed 254 characters." };
            }

            if (command.UserId == Guid.Empty)
            {
                errors[nameof(command.UserId)] = new[] { "UserId is required." };
            }

            if (command.BlogKey.Length > 128)
            {
                errors[nameof(command.BlogKey)] = new[] { "BlogKey must not exceed 128 characters." };
            }

            return errors;
        }
    }
}
