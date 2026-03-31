// <copyright file="SiteSettings.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Services.Configurations;

using System.ComponentModel.DataAnnotations;
using Cosmos.Cms.Common.Constants;

/// <summary>
///     Startup.ConfigureServices method captures the site customization options found in "secrets" in this
///     object.
/// </summary>
public class SiteSettings
{
    /// <summary>
    ///     Gets or sets allowed file type extensions.
    /// </summary>
    [Display(Name = "File types")]
    [Required]
    public string AllowedFileTypes { get; set; } = FileTypeConstants.DefaultAllowedFileTypes;

    /// <summary>
    ///     Gets or sets a value indicating whether allows a website to go into setup mode. For use only on fresh sites.
    /// </summary>
    [Display(Name = "Allow setup")]
    public bool AllowSetup { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether allows local accounts (default = true).
    /// </summary>
    /// <remarks>
    /// If disabled then assumes the use of Microsoft, Google or other supported OAuth provider.
    /// </remarks>
    public bool AllowLocalAccounts { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether publisher requires authentication.
    /// </summary>
    public bool CosmosRequiresAuthentication { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether the editor supports multiple websites.
    /// </summary>
    public bool MultiTenantEditor { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating where users are redirected when accessing the main site in a multi-tenant setup.
    /// </summary>
    public string MultiTenantRedirectUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Microsoft Application Id for OAuth authentication.
    /// </summary>
    public string? MicrosoftAppId { get; set; }
}
