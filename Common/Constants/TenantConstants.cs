// <copyright file="TenantConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Constants;

/// <summary>
/// Constants related to multi-tenant configuration and behavior.
/// </summary>
public static class TenantConstants
{
    /// <summary>
    /// Single tenant mode - one website instance per deployment.
    /// </summary>
    public const string SingleTenant = "SingleTenant";

    /// <summary>
    /// Multi-tenant mode - multiple websites per deployment.
    /// </summary>
    public const string MultiTenant = "MultiTenant";

    /// <summary>
    /// Default blob public URL path.
    /// </summary>
    public const string DefaultBlobPublicUrl = "/";
}
