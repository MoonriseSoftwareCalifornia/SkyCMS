// <copyright file="ImportLayoutValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Import
{
    using System.Collections.Generic;

    /// <summary>
    /// Validates <see cref="ImportLayoutCommand"/> requests.
    /// </summary>
    public class ImportLayoutValidator
    {
        /// <summary>
        /// Validates command values.
        /// </summary>
        /// <param name="command">Command to validate.</param>
        /// <returns>Validation errors keyed by field name.</returns>
        public Dictionary<string, string[]> Validate(ImportLayoutCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            if (command == null)
            {
                errors[nameof(ImportLayoutCommand)] = new[] { "Command cannot be null." };
                return errors;
            }

            if (string.IsNullOrWhiteSpace(command.CommunityLayoutId))
            {
                errors[nameof(command.CommunityLayoutId)] = new[] { "Layout ID is required" };
            }

            return errors;
        }
    }
}
