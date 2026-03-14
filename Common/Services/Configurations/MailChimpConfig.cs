// <copyright file="MailChimpConfig.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Configurations;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// MailChimp configuration.
/// </summary>
public class MailChimpConfig
{
    /// <summary>
    /// Gets or sets the MailChimp API key.
    /// </summary>
    /// <remarks>Get an <see href="https://us21.admin.mailchimp.com/account/api/">API Key from MailChimp</see>.</remarks>
    [Display(Name = "MailChimp API Key")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "MailChimp API Key is required")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    ///  Gets or sets the name of the list that contacts are added to.
    /// </summary>
    [Display(Name = "Email list name")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Contact list name is required")]
    public string ContactListName { get; set; } = string.Empty;
}
