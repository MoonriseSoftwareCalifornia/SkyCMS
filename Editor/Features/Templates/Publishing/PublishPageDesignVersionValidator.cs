// <copyright file="PublishPageDesignVersionValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Publishing
{
    using System.Collections.Generic;

    /// <summary>
    /// Validator for PublishPageDesignVersionCommand.
    /// </summary>
    public class PublishPageDesignVersionValidator
    {
        /// <summary>
        /// Validates the publish page design version command.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <returns>Dictionary of validation errors, empty if valid.</returns>
        public Dictionary<string, string[]> Validate(PublishPageDesignVersionCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            // Currently no validation required per requirements
            // This can be extended later as needed
            return errors;
        }
    }
}