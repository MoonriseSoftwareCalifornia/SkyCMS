// <copyright file="EmailConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Constants;

/// <summary>
/// Constants for email provider configuration and settings.
/// </summary>
public static class EmailConstants
{
    /// <summary>
    /// SendGrid email provider name.
    /// </summary>
    public const string SendGridProvider = "SendGrid";

    /// <summary>
    /// SMTP email provider name.
    /// </summary>
    public const string SmtpProvider = "SMTP";

    /// <summary>
    /// MailChimp service provider name.
    /// </summary>
    public const string MailChimpProvider = "MailChimp";

    /// <summary>
    /// Configuration key for API key.
    /// </summary>
    public const string ApiKeyConfig = "ApiKey";

    /// <summary>
    /// Configuration key for from email address.
    /// </summary>
    public const string FromAddressConfig = "FromAddress";

    /// <summary>
    /// Configuration key for from display name.
    /// </summary>
    public const string FromNameConfig = "FromName";
}
