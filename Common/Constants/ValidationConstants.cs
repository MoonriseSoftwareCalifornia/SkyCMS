// <copyright file="ValidationConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Constants;

/// <summary>
/// Constants for validation messages and patterns.
/// </summary>
public static class ValidationConstants
{
    /// <summary>
    /// Email validation error message.
    /// </summary>
    public const string InvalidEmailMessage = "Please enter a valid email address.";

    /// <summary>
    /// Required field validation message.
    /// </summary>
    public const string RequiredFieldMessage = "This field is required.";

    /// <summary>
    /// Password mismatch validation message.
    /// </summary>
    public const string PasswordMismatchMessage = "Passwords do not match.";

    /// <summary>
    /// Invalid URL validation message.
    /// </summary>
    public const string InvalidUrlMessage = "Please enter a valid URL.";

    /// <summary>
    /// File size exceeded validation message.
    /// </summary>
    public const string FileSizeExceededMessage = "File size exceeds the maximum allowed limit.";

    /// <summary>
    /// Invalid file type validation message.
    /// </summary>
    public const string InvalidFileTypeMessage = "File type is not allowed.";

    /// <summary>
    /// Email address regex pattern for validation.
    /// </summary>
    public const string EmailRegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    /// <summary>
    /// URL regex pattern for validation.
    /// </summary>
    public const string UrlRegexPattern = @"^(https?:\/\/)?([\da-z\.-]+)\.([a-z\.]{2,6})([\/\w \.-]*)*\/?$";

    /// <summary>
    /// Slug validation regex pattern (lowercase alphanumeric with hyphens).
    /// </summary>
    public const string SlugRegexPattern = @"^[a-z0-9]+(?:-[a-z0-9]+)*$";

    /// <summary>
    /// Maximum allowed file size in bytes (default: 10MB).
    /// </summary>
    public const int MaxFileSizeBytes = 10 * 1024 * 1024;
}
