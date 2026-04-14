// <copyright file="ContactFormRequest.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Models
{
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Contact form submission request.
    /// </summary>
    public class ContactFormRequest
    {
        /// <summary>
        /// Gets or sets the sender's name.
        /// </summary>
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sender's email address.
        /// </summary>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        [Required(ErrorMessage = "Message is required")]
        [StringLength(5000, MinimumLength = 10, ErrorMessage = "Message must be between 10 and 5000 characters")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CAPTCHA response token.
        /// </summary>
        public string? CaptchaToken { get; set; }
    }
}