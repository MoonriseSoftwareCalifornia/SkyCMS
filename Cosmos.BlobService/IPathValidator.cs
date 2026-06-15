// <copyright file="IPathValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService
{
    /// <summary>
    /// Defines validation rules for storage paths to prevent security vulnerabilities and inconsistencies.
    /// </summary>
    /// <remarks>
    /// Path validation operates on normalized paths and checks for:
    /// - Path traversal attacks (e.g., `../../../etc/passwd`)
    /// - Reserved or dangerous filenames (e.g., `CON`, `PRN`, `.`, `..`)
    /// - Invalid path structures (empty segments, invalid characters in specific contexts)
    /// Validation is performed at the StorageContext entry points to ensure that only safe,
    /// well-formed paths reach the underlying storage drivers. This defense-in-depth approach
    /// complements normalization by ensuring consistency and security.
    /// </remarks>
    public interface IPathValidator
    {
        /// <summary>
        /// Validates that a normalized path does not contain traversal attempts or reserved names.
        /// </summary>
        /// <param name="path">The normalized path to validate (e.g., "folder/file.txt", not "/folder/file.txt/").</param>
        /// <returns>
        /// A <see cref="PathValidationResult"/> indicating success or failure.
        /// If validation fails, the result includes a detailed error message explaining the issue.
        /// </returns>
        /// <remarks>
        /// This method performs the following checks:
        /// - Detects path traversal attempts (`..` segments, relative path patterns)
        /// - Rejects reserved/dangerous names (`.`, `..`, Windows reserved names)
        /// - Validates path structure (no leading/trailing slashes expected on normalized input)
        /// - Ensures no null bytes or other control characters
        /// A normalized path is expected to be:
        /// - Without leading or trailing slashes
        /// - With forward slashes as separators
        /// - With consecutive slashes already collapsed
        /// This method should be called for every path operation at the StorageContext level.
        /// </remarks>
        /// <example>
        /// <code>
        /// var result = validator.ValidatePath("articles/2024/post.md");
        /// if (!result.IsValid)
        /// {
        ///     throw new StorageException($"Invalid path: {result.ErrorMessage}");
        /// }
        /// </code>
        /// </example>
        PathValidationResult ValidatePath(string path);

        /// <summary>
        /// Validates a filename to ensure it does not contain reserved or dangerous names.
        /// </summary>
        /// <param name="filename">The filename (just the name, without path separators).</param>
        /// <returns>
        /// A <see cref="PathValidationResult"/> indicating success or failure.
        /// </returns>
        /// <remarks>
        /// This method checks for:
        /// - Reserved names like `CON`, `PRN`, `AUX`, `NUL` (Windows device names)
        /// - Special dot names (`.`, `..`)
        /// - Null bytes or control characters
        /// - Names that are empty or only whitespace
        /// Note: This method validates the filename component only, not the full path.
        /// For full path validation, use <see cref="ValidatePath(string)"/> instead.
        /// </remarks>
        /// <example>
        /// <code>
        /// var result = validator.ValidateFilename("document.pdf");
        /// var badResult = validator.ValidateFilename("CON");  // Windows reserved name
        /// </code>
        /// </example>
        PathValidationResult ValidateFilename(string filename);
    }

    /// <summary>
    /// Represents the result of a path validation operation.
    /// </summary>
    public sealed class PathValidationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PathValidationResult"/> class with a success result.
        /// </summary>
        public PathValidationResult()
            : this(isValid: true, errorMessage: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathValidationResult"/> class with a validation error.
        /// </summary>
        /// <param name="errorMessage">A detailed message describing why validation failed.</param>
        public PathValidationResult(string errorMessage)
            : this(isValid: false, errorMessage: errorMessage)
        {
        }

        private PathValidationResult(bool isValid, string errorMessage)
        {
            this.IsValid = isValid;
            this.ErrorMessage = errorMessage ?? string.Empty;
        }

        /// <summary>
        /// Gets a value indicating whether the path passed validation.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the error message if validation failed, or an empty string if validation succeeded.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Creates a success validation result.
        /// </summary>
        /// <returns>A <see cref="PathValidationResult"/> representing a successful validation.</returns>
        public static PathValidationResult Success() => new PathValidationResult();

        /// <summary>
        /// Creates a failure validation result with the specified error message.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A <see cref="PathValidationResult"/> representing a failed validation.</returns>
        public static PathValidationResult Failure(string errorMessage) => new PathValidationResult(errorMessage);
    }
}
