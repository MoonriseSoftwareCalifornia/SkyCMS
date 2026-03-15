// <copyright file="NoOpEmailService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Email
{
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// No-op email service used during setup wizard before email is configured.
    /// </summary>
    public class NoOpEmailService : IEmailSender
    {
        private readonly ILogger<NoOpEmailService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoOpEmailService"/> class.
        /// </summary>
        /// <param name="logger">Logger.</param>
        public NoOpEmailService(ILogger<NoOpEmailService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task<bool> SendEmailAsync(string to, string subject, string htmlMessage, string textMessage = null)
        {
            logger.LogWarning("Email service not configured (setup mode). Email to {To} not sent: {Subject}", to, subject);
            return Task.FromResult(true); // Return success to avoid blocking setup
        }

        /// <inheritdoc/>
        public Task<bool> SendEmailAsync(string from, string to, string subject, string htmlMessage, string textMessage = null)
        {
            logger.LogWarning("Email service not configured (setup mode). Email from {From} to {To} not sent: {Subject}", from, to, subject);
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // This method is part of the IEmailSender interface
            logger.LogWarning("Email service not configured (setup mode). Email to {Email} not sent: {Subject}", email, subject);
            return Task.CompletedTask;
        }
    }
}