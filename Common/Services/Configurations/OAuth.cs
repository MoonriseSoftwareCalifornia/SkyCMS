// <copyright file="OAuth.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Configurations;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Microsoft Entra ID OAuth App Authentication.
/// </summary>
public class OAuth
{
    /// <summary>
    /// Gets the client Id (Application Id) of the app.
    /// </summary>
    [Display(Name = "Client ID")]
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Client Secret (App Secret) of the app.
    /// </summary>
    [Display(Name = "Client Secret")]
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Microsoft Tenant Id.
    /// </summary>
    /// <remarks>For single-tenant apps, this is the tenant id of the app registration.</remarks>
    [Display(Name = "Tenant ID")]
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Callback Domain.
    /// </summary>
    /// <remarks>
    /// If you are using a proxy or firewall, such as Front Door, you may need to set this to the return domain.
    /// </remarks>
    [Display(Name = "Callback Domain")]
    public string CallbackDomain { get; init; } = string.Empty;

    /// <summary>
    /// Indicates if this is configured or not.
    /// </summary>
    /// <returns>True means configuration is present.</returns>
    public virtual bool IsConfigured()
    {
        return ClientId != string.Empty && ClientSecret != string.Empty;
    }
}
