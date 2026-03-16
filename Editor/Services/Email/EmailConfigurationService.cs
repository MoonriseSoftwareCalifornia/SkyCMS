// <copyright file="EmailConfigurationService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Email;

using Cosmos.Common.Data;
using Cosmos.Common.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Service for retrieving email configuration from environment variables or database.
/// </summary>
public class EmailConfigurationService : IEmailConfigurationService
{
    private readonly IConfiguration configuration;
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<EmailConfigurationService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailConfigurationService"/> class.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="logger">Logger.</param>
    public EmailConfigurationService(
        IConfiguration configuration,
        ApplicationDbContext dbContext,
        ILogger<EmailConfigurationService> logger)
    {
        this.configuration = configuration;
        this.dbContext = dbContext;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmailSettings> GetEmailSettingsAsync()
    {
        var settings = new EmailSettings();

        try
        {
            // Try environment variables first (highest priority)
            settings.SendGridApiKey = configuration["CosmosSendGridApiKey"];
            settings.AzureEmailConnectionString = configuration.GetConnectionString("AzureCommunicationConnection");
            settings.SmtpHost = configuration["SmtpEmailProviderOptions:Host"]
                ?? configuration["SmtpEmailProviderOptions__Host"];
            settings.SmtpPort = int.TryParse(
                configuration["SmtpEmailProviderOptions:Port"]
                ?? configuration["SmtpEmailProviderOptions__Port"],
                out var port) ? port : 587;
            settings.SmtpUsername = configuration["SmtpEmailProviderOptions:UserName"]
                ?? configuration["SmtpEmailProviderOptions__UserName"];
            settings.SmtpPassword = configuration["SmtpEmailProviderOptions:Password"]
                ?? configuration["SmtpEmailProviderOptions__Password"];
            settings.SenderEmail = configuration["AdminEmail"];

            // If not found in environment, try database settings
            if (string.IsNullOrEmpty(settings.SendGridApiKey) &&
                string.IsNullOrEmpty(settings.AzureEmailConnectionString) &&
                string.IsNullOrEmpty(settings.SmtpHost))
            {
                logger.LogInformation("Email settings not found in environment variables, checking database");

                var dbSettings = await dbContext.Settings
                    .Where(s => s.Group == "EMAIL")
                    .ToListAsync();

                foreach (var setting in dbSettings)
                {
                    switch (setting.Name)
                    {
                        case "SendGridApiKey":
                            settings.SendGridApiKey = setting.Value;
                            break;
                        case "AzureEmailConnectionString":
                            settings.AzureEmailConnectionString = setting.Value;
                            break;
                        case "SmtpHost":
                            settings.SmtpHost = setting.Value;
                            break;
                        case "SmtpPort":
                            settings.SmtpPort = int.TryParse(setting.Value, out var dbPort) ? dbPort : 587;
                            break;
                        case "SmtpUsername":
                            settings.SmtpUsername = setting.Value;
                            break;
                        case "SmtpPassword":
                            settings.SmtpPassword = setting.Value;
                            break;
                        case "AdminEmail":
                            settings.SenderEmail = setting.Value;
                            break;
                    }
                }
            }

            // Determine provider
            settings.Provider = DetermineProvider(settings);
            settings.IsConfigured = !string.IsNullOrEmpty(settings.Provider);

            return settings;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load email settings");
            return settings; // Return empty settings rather than throw
        }
    }

    /// <summary>
    /// Determines the email provider based on available settings.
    /// </summary>
    private string DetermineProvider(EmailSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.SendGridApiKey))
        {
            return "SendGrid";
        }

        if (!string.IsNullOrEmpty(settings.AzureEmailConnectionString))
        {
            return "AzureCommunication";
        }

        if (!string.IsNullOrEmpty(settings.SmtpHost))
        {
            return "SMTP";
        }

        return string.Empty;
    }
}