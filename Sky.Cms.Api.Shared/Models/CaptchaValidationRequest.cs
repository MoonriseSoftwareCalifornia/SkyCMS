// <copyright file="CaptchaValidationRequest.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Models
{
    /// <summary>
    /// CAPTCHA validation request.
    /// </summary>
    public class CaptchaValidationRequest
    {
        /// <summary>
        /// Gets or sets the CAPTCHA response token from the client.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's IP address.
        /// </summary>
        public string RemoteIp { get; set; } = string.Empty;
    }
}