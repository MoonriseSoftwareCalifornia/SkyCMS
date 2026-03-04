// <copyright file="SensitiveFieldHelper.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System;
    using System.Collections.Generic;
    /// <summary>
    /// Helper utility for masking and managing sensitive field display in setup wizard.
    /// Provides methods to mask passwords, API keys, and connection strings in the UI.
    /// </summary>
    public static class SensitiveFieldHelper
    {
        /// <summary>
        /// List of properties that contain sensitive data requiring masking/reveal functionality.
        /// </summary>
        private static readonly HashSet<string> SensitiveProperties = new ()
        {
            "AdminPassword",
            "SmtpPassword",
            "SendGridApiKey",
            "AzureEmailConnectionString",
            "DatabaseConnectionString",
            "StorageConnectionString",
            "CloudFrontSecretAccessKey",
            "CloudflareApiToken",
            "SucuriApiSecret",
            "CloudFrontAccessKeyId"
        };

        /// <summary>
        /// Determines if a property should be masked in the UI.
        /// </summary>
        /// <param name="propertyName">The property name to check.</param>
        /// <returns>True if the property contains sensitive data; otherwise, false.</returns>
        public static bool IsSensitiveProperty(string propertyName)
        {
            return SensitiveProperties.Contains(propertyName);
        }

        /// <summary>
        /// Returns a masked version of a sensitive value for display.
        /// Passwords show asterisks, connection strings/keys show first 10 and last 10 chars with middle masked.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="value">The actual value to mask.</param>
        /// <returns>A masked representation of the value.</returns>
        public static string MaskSensitiveValue(string propertyName, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "(empty)";
            }

            // Passwords: Show only asterisks
            if (propertyName == "AdminPassword" || propertyName == "SmtpPassword")
            {
                return new string('?', Math.Min(value.Length, 20));
            }

            // Connection strings and API keys: Show first 10 + "..." + last 10
            if (value.Length <= 20)
            {
                return new string('?', value.Length);
            }

            var first10 = value.Substring(0, 10);
            var last10 = value.Substring(value.Length - 10);
            return $"{first10}...{new string('?', 10)}...{last10}";
        }

        /// <summary>
        /// Generates a unique HTML ID for a reveal button for a sensitive field.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <returns>A unique HTML ID for the reveal button.</returns>
        public static string GetRevealButtonId(string propertyName)
        {
            return $"reveal-{propertyName.ToLowerInvariant()}";
        }

        /// <summary>
        /// Generates a unique HTML ID for a copy button for a sensitive field.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <returns>A unique HTML ID for the copy button.</returns>
        public static string GetCopyButtonId(string propertyName)
        {
            return $"copy-{propertyName.ToLowerInvariant()}";
        }

        /// <summary>
        /// Generates a unique HTML ID for the input field that holds the actual value.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <returns>A unique HTML ID for the input field.</returns>
        public static string GetInputFieldId(string propertyName)
        {
            return $"field-{propertyName.ToLowerInvariant()}";
        }

        /// <summary>
        /// Gets the display mask HTML for a sensitive value in Razor Pages.
        /// Shows masked value initially, with reveal/copy buttons.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="actualValue">The actual value (never displayed until revealed).</param>
        /// <param name="isMasked">Whether to show the masked value initially (default: true).</param>
        /// <returns>HTML string for the masked display.</returns>
        public static string GetMaskedFieldHtml(string propertyName, string actualValue, bool isMasked = true)
        {
            if (!IsSensitiveProperty(propertyName))
            {
                return actualValue; // Not sensitive, return as-is
            }

            var maskedValue = MaskSensitiveValue(propertyName, actualValue);
            var revealBtnId = GetRevealButtonId(propertyName);
            var copyBtnId = GetCopyButtonId(propertyName);
            var inputId = GetInputFieldId(propertyName);
            var displayClass = isMasked ? "display-masked" : "display-revealed";

            return $@"
<div class=""sensitive-field-wrapper"">
    <input type=""hidden"" id=""{inputId}"" value=""{System.Net.WebUtility.HtmlEncode(actualValue)}"" />
    <span id=""field-display-{propertyName.ToLowerInvariant()}"" class=""sensitive-field-display {displayClass}"">
        {System.Net.WebUtility.HtmlEncode(maskedValue)}
    </span>
    <button type=""button"" id=""{revealBtnId}"" class=""btn btn-sm btn-outline-secondary"" title=""Reveal value"">
        <i class=""fas fa-eye""></i>
    </button>
    <button type=""button"" id=""{copyBtnId}"" class=""btn btn-sm btn-outline-secondary"" title=""Copy to clipboard"">
        <i class=""fas fa-copy""></i>
    </button>
</div>";
        }
    }
}
