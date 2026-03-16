// <copyright file="AzureConnectionStringComponents.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService;

/// <summary>
/// Contains parsed components of an Azure Blob Storage connection string.
/// </summary>
public class AzureConnectionStringComponents
{
    /// <summary>
    /// Gets or sets the storage account name.
    /// </summary>
    required public string AccountName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the connection uses Azure AD access tokens.
    /// </summary>
    public bool UsesAccessToken { get; set; }

    /// <summary>
    /// Gets or sets the full connection string.
    /// </summary>
    required public string FullConnectionString { get; set; }
}
