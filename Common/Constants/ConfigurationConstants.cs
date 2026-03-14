// <copyright file="ConfigurationConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Constants;

/// <summary>
/// Constants for configuration section names and keys used in appsettings.json and user secrets.
/// </summary>
public static class ConfigurationConstants
{
    /// <summary>
    /// Configuration section name for Microsoft OAuth settings.
    /// </summary>
    public const string MicrosoftOAuthSection = "MicrosoftOAuth";

    /// <summary>
    /// Configuration section name for Azure AD settings.
    /// </summary>
    public const string AzureAdSection = "AzureAd";

    /// <summary>
    /// Configuration key for Tenant ID.
    /// </summary>
    public const string TenantIdKey = "TenantId";

    /// <summary>
    /// Configuration key for Client ID (Application ID).
    /// </summary>
    public const string ClientIdKey = "ClientId";

    /// <summary>
    /// Configuration key for Client Secret (Application Secret).
    /// </summary>
    public const string ClientSecretKey = "ClientSecret";

    /// <summary>
    /// Configuration section name for Site Settings.
    /// </summary>
    public const string SiteSettingsSection = "SiteSettings";

    /// <summary>
    /// Configuration section name for Email Settings.
    /// </summary>
    public const string EmailSettingsSection = "EmailSettings";

    /// <summary>
    /// Configuration section name for Storage Settings.
    /// </summary>
    public const string StorageSettingsSection = "StorageSettings";
}
