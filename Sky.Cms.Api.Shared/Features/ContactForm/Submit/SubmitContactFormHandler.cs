// <copyright file="SubmitContactFormHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Features.ContactForm.Submit;

using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services.Email;
using Cosmos.EmailServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sky.Cms.Api.Shared.Models;

/// <summary>
/// Handler for submitting contact forms.
/// </summary>
public class SubmitContactFormHandler : ICommandHandler<SubmitContactFormCommand, CommandResult<ContactFormResponse>>
{
    private readonly ICosmosEmailSender emailSender;
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<SubmitContactFormHandler> logger;
    private readonly IEmailConfigurationService emailConfigService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitContactFormHandler"/> class.
    /// </summary>
    /// <param name="emailSender">Email sender service.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="emailConfigService">Email configuration service for fallback admin email.</param>
    public SubmitContactFormHandler(
        ICosmosEmailSender emailSender,
        ApplicationDbContext dbContext,
        ILogger<SubmitContactFormHandler> logger,
        IEmailConfigurationService emailConfigService)
    {
        this.emailSender = emailSender;
        this.dbContext = dbContext;
        this.logger = logger;
        this.emailConfigService = emailConfigService;
    }

    /// <inheritdoc/>
    public async Task<CommandResult<ContactFormResponse>> HandleAsync(
        SubmitContactFormCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Load tenant-specific configuration from Settings table
            var configSettings = await dbContext.Settings
                .Where(s => s.Group == "ContactApi")
                .ToListAsync(cancellationToken);

            var emailSettings = await emailConfigService.GetEmailSettingsAsync();
            if (emailSettings == null || string.IsNullOrEmpty(emailSettings.SenderEmail))
            {
                logger.LogError("Email settings not configured");
                return CommandResult<ContactFormResponse>.Failure("System configuration error");
            }

            var adminEmail = emailSettings.SenderEmail;

            var request = command.Request;
            var remoteIp = command.RemoteIpAddress;

            logger.LogInformation(
                "Processing contact form submission from {Email} (IP: {RemoteIp})",
                request.Email,
                remoteIp);

            // Prepare email content
            var subject = $"Contact Form Submission from {request.Name}";
            var textBody = $@"
Contact Form Submission

Name: {request.Name}
Email: {request.Email}
IP Address: {remoteIp}

Message:
{request.Message}

---
Submitted: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
";

            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #f4f4f4; padding: 10px; border-bottom: 2px solid #007bff; }}
        .content {{ padding: 20px; background-color: #fff; }}
        .field {{ margin-bottom: 15px; }}
        .label {{ font-weight: bold; color: #555; }}
        .value {{ margin-top: 5px; }}
        .message {{ background-color: #f9f9f9; padding: 15px; border-left: 4px solid #007bff; }}
        .footer {{ margin-top: 20px; padding-top: 10px; border-top: 1px solid #ddd; font-size: 12px; color: #888; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Contact Form Submission</h2>
        </div>
        <div class='content'>
            <div class='field'>
                <div class='label'>Name:</div>
                <div class='value'>{request.Name}</div>
            </div>
            <div class='field'>
                <div class='label'>Email:</div>
                <div class='value'><a href='mailto:{request.Email}'>{request.Email}</a></div>
            </div>
            <div class='field'>
                <div class='label'>IP Address:</div>
                <div class='value'>{remoteIp}</div>
            </div>
            <div class='field'>
                <div class='label'>Message:</div>
                <div class='message'>{System.Web.HttpUtility.HtmlEncode(request.Message)}</div>
            </div>
        </div>
        <div class='footer'>
            Submitted: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
        </div>
    </div>
</body>
</html>
";

            // Send email using tenant-aware email sender
            await emailSender.SendEmailAsync(
                adminEmail,
                subject,
                textBody,
                htmlBody);

            if (!emailSender.SendResult.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Failed to send contact form email. Status: {StatusCode}, Message: {Message}",
                    emailSender.SendResult.StatusCode,
                    emailSender.SendResult.Message);

                return CommandResult<ContactFormResponse>.Failure(
                    "Failed to send your message. Please try again later.");
            }

            logger.LogInformation(
                "Contact form email sent successfully to {AdminEmail} from {Email}",
                adminEmail,
                request.Email);

            var response = new ContactFormResponse
            {
                Success = true,
                Message = "Thank you for your message. We'll get back to you soon!"
            };

            return CommandResult<ContactFormResponse>.Success(response);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error processing contact form submission from {Email} (IP: {RemoteIp})",
                command.Request.Email,
                command.RemoteIpAddress);

            return CommandResult<ContactFormResponse>.Failure(
                "An unexpected error occurred. Please try again later.");
        }
    }
}