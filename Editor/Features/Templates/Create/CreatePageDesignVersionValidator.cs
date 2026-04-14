// <copyright file="CreatePageDesignVersionValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Create
{
    using System.Collections.Generic;

    /// <summary>
    /// Validator for CreatePageDesignVersionCommand.
    /// </summary>
    public class CreatePageDesignVersionValidator
    {
        /// <summary>
        /// Validates the create page design version command.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <returns>Dictionary of validation errors, empty if valid.</returns>
        public Dictionary<string, string[]> Validate(CreatePageDesignVersionCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            // Currently no validation required per requirements
            // This can be extended later as needed
            return errors;
        }
    }
}