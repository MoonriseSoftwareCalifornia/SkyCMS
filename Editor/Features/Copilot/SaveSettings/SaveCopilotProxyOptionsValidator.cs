// <copyright file="SaveCopilotProxyOptionsValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Copilot.SaveSettings
{
    using System.Collections.Generic;

    /// <summary>
    /// Validator for <see cref="SaveCopilotProxyOptionsCommand"/>.
    /// </summary>
    public class SaveCopilotProxyOptionsValidator
    {
        /// <summary>
        /// Validates the save command.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <returns>Dictionary of validation errors, empty if valid.</returns>
        public Dictionary<string, string[]> Validate(SaveCopilotProxyOptionsCommand command)
        {
            var errors = new Dictionary<string, string[]>();

            if (command == null)
            {
                errors[nameof(command)] = new[] { "Command is required." };
                return errors;
            }

            if (command.Options == null)
            {
                errors[nameof(command.Options)] = new[] { "Copilot options are required." };
                return errors;
            }

            var options = command.Options;

            if (options.Enabled && string.IsNullOrWhiteSpace(options.Endpoint))
            {
                errors[nameof(options.Endpoint)] = new[] { "Endpoint is required when Copilot is enabled." };
            }

            if (options.Enabled && string.IsNullOrWhiteSpace(options.AccessToken))
            {
                errors[nameof(options.AccessToken)] = new[] { "Access token is required when Copilot is enabled." };
            }

            if (options.TimeoutMs < 1000 || options.TimeoutMs > 60000)
            {
                errors[nameof(options.TimeoutMs)] = new[] { "Timeout must be between 1000 and 60000 milliseconds." };
            }

            if (options.Temperature < 0 || options.Temperature > 2)
            {
                errors[nameof(options.Temperature)] = new[] { "Temperature must be between 0 and 2." };
            }

            if (options.MaxTokens < 16 || options.MaxTokens > 1024)
            {
                errors[nameof(options.MaxTokens)] = new[] { "Max tokens must be between 16 and 1024." };
            }

            if (!string.IsNullOrWhiteSpace(options.EmbeddingModel) && options.EmbeddingModel.Length > 128)
            {
                errors[nameof(options.EmbeddingModel)] = new[] { "Embedding model must be 128 characters or fewer." };
            }

            return errors;
        }
    }
}
