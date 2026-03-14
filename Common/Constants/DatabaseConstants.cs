// <copyright file="DatabaseConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Constants;

/// <summary>
/// Constants for database configuration and connection strings.
/// </summary>
public static class DatabaseConstants
{
    /// <summary>
    /// Configuration section name for connection strings.
    /// </summary>
    public const string ConnectionStringsSection = "ConnectionStrings";

    /// <summary>
    /// Default connection string name for the application database.
    /// </summary>
    public const string ApplicationDbConnection = "ApplicationDbConnection";

    /// <summary>
    /// Legacy default connection string name.
    /// </summary>
    public const string DefaultConnection = "DefaultConnection";

    /// <summary>
    /// CosmosDB database container name for identity data.
    /// </summary>
    public const string CosmosIdentityContainer = "CosmosIdentity";
}
