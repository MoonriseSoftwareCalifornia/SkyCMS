// <copyright file="ErrorMessageConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Constants;

/// <summary>
/// Constants for common error messages throughout the application.
/// </summary>
public static class ErrorMessageConstants
{
    /// <summary>
    /// Generic error message for null or empty string parameters.
    /// </summary>
    public const string ParameterCannotBeNullOrEmpty = "Parameter cannot be null or empty.";

    /// <summary>
    /// Generic error message for null parameters.
    /// </summary>
    public const string ParameterCannotBeNull = "Parameter cannot be null.";

    /// <summary>
    /// Error message for invalid configuration.
    /// </summary>
    public const string InvalidConfiguration = "The configuration is invalid or missing required values.";

    /// <summary>
    /// Error message for database connection failure.
    /// </summary>
    public const string DatabaseConnectionFailed = "Failed to connect to the database.";

    /// <summary>
    /// Error message for storage provider not found.
    /// </summary>
    public const string StorageProviderNotFound = "The specified storage provider could not be found.";

    /// <summary>
    /// Error message for tenant not found.
    /// </summary>
    public const string TenantNotFound = "The specified tenant could not be found.";

    /// <summary>
    /// Error message for unauthorized access.
    /// </summary>
    public const string UnauthorizedAccess = "You do not have permission to perform this action.";

    /// <summary>
    /// Error message for resource not found.
    /// </summary>
    public const string ResourceNotFound = "The requested resource could not be found.";

    /// <summary>
    /// Error message for operation timeout.
    /// </summary>
    public const string OperationTimeout = "The operation timed out.";

    /// <summary>
    /// Error message for unsupported operation.
    /// </summary>
    public const string UnsupportedOperation = "This operation is not supported.";
}
