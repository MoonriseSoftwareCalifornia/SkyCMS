// <copyright file="PromoteLayoutValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Promote
{
    using System.Collections.Generic;

    /// <summary>
    /// Validates <see cref="PromoteLayoutCommand"/> requests.
    /// </summary>
    public class PromoteLayoutValidator
    {
        /// <summary>
        /// Validates command values.
        /// </summary>
        /// <param name="command">Command to validate.</param>
        /// <returns>Validation errors keyed by field name.</returns>
        public Dictionary<string, string[]> Validate(PromoteLayoutCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            if (command == null)
            {
                errors[nameof(PromoteLayoutCommand)] = new[] { "Command cannot be null." };
                return errors;
            }

            if (command.LayoutId == System.Guid.Empty)
            {
                errors[nameof(command.LayoutId)] = new[] { "Invalid layout ID" };
            }

            return errors;
        }
    }
}
