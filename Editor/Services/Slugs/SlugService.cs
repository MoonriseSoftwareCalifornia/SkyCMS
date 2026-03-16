// <copyright file="SlugService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Slugs
{
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// URL-safe slug implementation for Razor Pages route segments.
    /// Produces only ASCII letters/digits and separator characters; removes diacritics.
    /// </summary>
    public sealed class SlugService : ISlugService
    {
        // Choose your separator to match your style/SEO: '-' (common) or '_' (your original).
        private const char Separator = '-';

        /// <inheritdoc cref="ISlugService.Normalize(string, string)"/>
        public string Normalize(string input, string blogKey = "")
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            // 1) Normalize to decomposed form and lowercase
            var normalized = input.Trim().Normalize(NormalizationForm.FormD);

            // 2) Build string by removing diacritics and normalizing characters
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);

                // Skip combining diacritical marks (the accent marks, not the base character)
                if (category == UnicodeCategory.NonSpacingMark ||
                    category == UnicodeCategory.SpacingCombiningMark ||
                    category == UnicodeCategory.EnclosingMark)
                {
                    continue;
                }

                // Convert to lowercase after diacritic removal
                var lowerCh = char.ToLowerInvariant(ch);

                // Keep ASCII letters, digits, and forward slashes
                if ((lowerCh >= 'a' && lowerCh <= 'z') || (lowerCh >= '0' && lowerCh <= '9') || lowerCh == '/')
                {
                    sb.Append(lowerCh);
                }
                else
                {
                    // Replace everything else (spaces, punctuation, etc.) with separator
                    sb.Append(Separator);
                }
            }

            // 3) Guard against reserved dot-segments BEFORE collapsing
            if (input.Trim() is "." or "..")
            {
                // Return the converted form without collapsing
                return sb.ToString();
            }

            // 4) Collapse consecutive separators into single separator
            var collapsed = Regex.Replace(sb.ToString(), $"{Regex.Escape(Separator.ToString())}{{2,}}", Separator.ToString());

            // 5) Trim separators and other unsafe characters from ends
            var slug = collapsed.Trim(Separator, '/', '.', '_', '~');

            // 6) Prepend blogKey if provided
            if (!string.IsNullOrWhiteSpace(blogKey))
            {
                return $"{blogKey}/{slug}";
            }

            return slug;
        }
    }
}
