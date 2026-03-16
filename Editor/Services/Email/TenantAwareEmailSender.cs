// <copyright file="TenantAwareEmailSender.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Email;

using Azure.Identity;
using Cosmos.Common.Services.Email;
using Cosmos.EmailServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Threading.Tasks;

/// <summary>
/// Tenant-aware email sender that dynamically selects provider based on database settings.
/// </summary>
/// <remarks>
/// This service retrieves email configuration from the database (per tenant) and instantiates
/// the appropriate email provider at runtime. This supports multi-tenant scenarios where
/// different tenants may use different email providers or credentials.
/// </remarks>
public class TenantAwareEmailSender : ICosmosEmailSender
{
    private readonly IEmailConfigurationService configService;
    private readonly ILogger<TenantAwareEmailSender> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly DefaultAzureCredential azureCredential;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantAwareEmailSender"/> class.
    /// </summary>
    /// <param name="configService">Email configuration service.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="loggerFactory">Logger factory for creating provider-specific loggers.</param>
    /// <param name="azureCredential">Azure credential for Azure Communication Services.</param>
    public TenantAwareEmailSender(
        IEmailConfigurationService configService,
        ILogger<TenantAwareEmailSender> logger,
        ILoggerFactory loggerFactory,
        DefaultAzureCredential azureCredential)
    {
        this.configService = configService;
        this.logger = logger;
        this.loggerFactory = loggerFactory;
        this.azureCredential = azureCredential;
        SendResult = new SendResult();
    }

    /// <inheritdoc/>
    public SendResult SendResult { get; private set; }

    /// <inheritdoc/>
    public async Task SendEmailAsync(string emailTo, string subject, string htmlMessage)
    {
        await SendEmailAsync(emailTo, subject, string.Empty, htmlMessage, null);
    }

    /// <inheritdoc/>
    public async Task SendEmailAsync(string emailTo, string subject, string textVersion, string htmlVersion, string? emailFrom = null)
    {
        try
        {
            // Retrieve tenant-specific email settings from database
            var settings = await configService.GetEmailSettingsAsync();

            if (!settings.IsConfigured)
            {
                SendResult = new SendResult
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    Message = "Email service not configured for this tenant"
                };
                logger.LogWarning("Email service not configured. No provider settings found in database or configuration.");
                return;
            }

            // Create the appropriate provider based on settings
            var sender = CreateEmailSender(settings);

            // Determine the from address
            var fromAddress = emailFrom ?? settings.SenderEmail;

            // Send the email
            if (string.IsNullOrEmpty(textVersion))
            {
                await sender.SendEmailAsync(emailTo, subject, htmlVersion, fromAddress);
            }
            else
            {
                await sender.SendEmailAsync(emailTo, subject, textVersion, htmlVersion, fromAddress);
            }

            // Capture the result from the underlying sender
            SendResult = sender.SendResult;

            if (SendResult.IsSuccessStatusCode)
            {
                logger.LogInformation("Email sent successfully to {EmailTo} using {Provider}", emailTo, settings.Provider);
            }
            else
            {
                logger.LogWarning("Email send failed to {EmailTo} using {Provider}: {Message}", emailTo, settings.Provider, SendResult.Message);
            }
        }
        catch (Exception ex)
        {
            SendResult = new SendResult
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Message = $"Failed to send email: {ex.Message}"
            };
            logger.LogError(ex, "Exception occurred while sending email to {EmailTo}", emailTo);
        }
    }

    /// <summary>
    /// Creates the appropriate email sender based on provider settings.
    /// </summary>
    /// <param name="settings">Email settings from database or configuration.</param>
    /// <returns>An instance of ICosmosEmailSender configured for the specified provider.</returns>
    private ICosmosEmailSender CreateEmailSender(EmailSettings settings)
    {
        return settings.Provider switch
        {
            "SendGrid" => CreateSendGridSender(settings),
            "AzureCommunication" => CreateAzureCommunicationSender(settings),
            "SMTP" => CreateSmtpSender(settings),
            _ => CreateNoOpSender()
        };
    }

    /// <summary>
    /// Creates a SendGrid email sender.
    /// </summary>
    private ICosmosEmailSender CreateSendGridSender(EmailSettings settings)
    {
        var options = Options.Create(new SendGridEmailProviderOptions(
            settings.SendGridApiKey,
            settings.SenderEmail,
            sandboxMode: false,
            logSuccesses: false,
            logErrors: true));

        var sendGridLogger = loggerFactory.CreateLogger<SendGridEmailSender>();

        return new SendGridEmailSender(options, sendGridLogger);
    }

    /// <summary>
    /// Creates an Azure Communication Services email sender.
    /// </summary>
    private ICosmosEmailSender CreateAzureCommunicationSender(EmailSettings settings)
    {
        var options = Options.Create(new AzureCommunicationEmailProviderOptions
        {
            ConnectionString = settings.AzureEmailConnectionString,
            DefaultFromEmailAddress = settings.SenderEmail
        });

        var azureLogger = loggerFactory.CreateLogger<AzureCommunicationEmailSender>();

        return new AzureCommunicationEmailSender(options, azureLogger, azureCredential);
    }

    /// <summary>
    /// Creates an SMTP email sender.
    /// </summary>
    private ICosmosEmailSender CreateSmtpSender(EmailSettings settings)
    {
        var options = Options.Create(new SmtpEmailProviderOptions
        {
            Host = settings.SmtpHost,
            Port = settings.SmtpPort,
            UserName = settings.SmtpUsername,
            Password = settings.SmtpPassword,
            DefaultFromEmailAddress = settings.SenderEmail,
            UsesSsl = settings.SmtpPort == 465 // Use SSL for port 465, TLS otherwise
        });

        return new SmtpEmailSender(options);
    }

    /// <summary>
    /// Creates a no-op email sender for when email is not configured.
    /// </summary>
    private ICosmosEmailSender CreateNoOpSender()
    {
        logger.LogWarning("Using NoOp email sender - emails will not be sent");
        return new CosmosNoOpEmailSender();
    }
}