// <copyright file="VsCodeAuthViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Models
{
    /// <summary>
    /// View model for the VS Code browser authentication pages.
    /// </summary>
    public class VsCodeAuthViewModel
    {
        /// <summary>
        /// Gets or sets the one-time sign-in code displayed to the user as a manual fallback.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the correlation state for this auth attempt.
        /// </summary>
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the <c>vscode://</c> callback URI that the browser will redirect to automatically.
        /// </summary>
        public string VsCodeCallbackUri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the website title derived from the root article.
        /// </summary>
        public string WebsiteTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the public-facing website URL from site settings.
        /// </summary>
        public string PublicUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable error message shown on the failure page.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
