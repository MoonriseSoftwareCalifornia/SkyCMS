// <copyright file="EmailSettings.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Email;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Email configuration settings.
/// </summary>
public class EmailSettings
{
    /// <summary>
    /// Gets or sets the email provider (SendGrid, AzureCommunication, SMTP).
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Email provider is required")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SendGrid API key.
    /// </summary>
    public string? SendGridApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Azure Communication Services connection string.
    /// </summary>
    public string? AzureEmailConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the SMTP host.
    /// </summary>
    public string? SmtpHost { get; set; }

    /// <summary>
    /// Gets or sets the SMTP port.
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Gets or sets the SMTP username.
    /// </summary>
    public string? SmtpUsername { get; set; }

    /// <summary>
    /// Gets or sets the SMTP password.
    /// </summary>
    public string? SmtpPassword { get; set; }

    /// <summary>
    /// Gets or sets the sender email address.
    /// </summary>
    public string? SenderEmail { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether email is configured.
    /// </summary>
    public bool IsConfigured { get; set; }
}