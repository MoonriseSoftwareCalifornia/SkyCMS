// <copyright file="ContactFormResponse.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Models
{
    /// <summary>
    /// Response for contact form submission.
    /// </summary>
    public class ContactFormResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the submission was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the response message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets any error details.
        /// </summary>
        public string Error { get; set; } = string.Empty;
    }
}