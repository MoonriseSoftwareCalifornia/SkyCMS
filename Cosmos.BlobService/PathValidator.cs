// <copyright file="PathValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Validates storage paths to prevent path traversal attacks, reserved names, and invalid structures.
    /// </summary>
    /// <remarks>
    /// This validator operates on normalized paths and enforces defense-in-depth security by:
    /// - Detecting path traversal attempts (parent directory references)
    /// - Rejecting Windows reserved names (CON, PRN, AUX, NUL, etc.)
    /// - Validating path structure and content
    /// - Catching mutations that would break normalization invariants
    /// </remarks>
    public sealed class PathValidator : IPathValidator
    {
        /// <summary>
        /// Windows reserved device names that are not allowed as filenames or directory names.
        /// See: https://docs.microsoft.com/en-us/windows/win32/fileio/naming-a-file
        /// </summary>
        private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// Pattern to detect path traversal attempts (e.g., `..`, `../`, `..\\`).
        /// </summary>
        private static readonly Regex TraversalPattern = new Regex(
            @"(^|/|\\)\.\.(/|\\|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="PathValidator"/> class.
        /// </summary>
        public PathValidator()
        {
        }

        /// <inheritdoc cref="IPathValidator.ValidatePath(string)"/>
        public PathValidationResult ValidatePath(string path)
        {
            // Check for null or empty (empty is acceptable for root, but normalized root should not reach here)
            if (path == null)
            {
                return PathValidationResult.Failure("Path cannot be null.");
            }

            // Empty paths are treated as root and are valid
            if (path == string.Empty)
            {
                return PathValidationResult.Success();
            }

            // Check for null bytes (common attack vector)
            if (path.Contains('\0'))
            {
                return PathValidationResult.Failure("Path contains null bytes.");
            }

            // Check for control characters (except tab, which may be valid in some contexts)
            foreach (var ch in path)
            {
                if (char.IsControl(ch) && ch != '\t')
                {
                    return PathValidationResult.Failure($"Path contains invalid control character: U+{(int)ch:X4}");
                }
            }

            // Check for path traversal attempts
            if (TraversalPattern.IsMatch(path))
            {
                return PathValidationResult.Failure("Path contains traversal attempt (..).");
            }

            // Check for single dot (current directory reference)
            if (path == "." || path.StartsWith("./") || path.StartsWith(".\\") || path.EndsWith("/.") || path.EndsWith("\\."))
            {
                return PathValidationResult.Failure("Path contains current directory reference (.).");
            }

            // Split path and validate each segment
            var segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                // Check for reserved names in any segment
                if (WindowsReservedNames.Contains(segment))
                {
                    return PathValidationResult.Failure($"Path segment '{segment}' is a reserved name.");
                }

                // Check for dot-only segments
                if (segment == "." || segment == "..")
                {
                    return PathValidationResult.Failure($"Path segment '{segment}' is invalid.");
                }

                // Check for null bytes in segments
                if (segment.Contains('\0'))
                {
                    return PathValidationResult.Failure($"Path segment contains null bytes.");
                }

                // Check for excessive length (common on some filesystems)
                // Most filesystems have 255-byte limits for individual segments
                if (segment.Length > 255)
                {
                    return PathValidationResult.Failure($"Path segment '{segment}' exceeds 255 characters ({segment.Length}).");
                }
            }

            // Check for excessive path depth (security and performance consideration)
            // 64 segments is a reasonable limit for most applications
            if (segments.Length > 64)
            {
                return PathValidationResult.Failure($"Path depth exceeds limit (64 segments max, got {segments.Length}).");
            }

            return PathValidationResult.Success();
        }

        /// <inheritdoc cref="IPathValidator.ValidateFilename(string)"/>
        public PathValidationResult ValidateFilename(string filename)
        {
            // Check for null
            if (filename == null)
            {
                return PathValidationResult.Failure("Filename cannot be null.");
            }

            // Check for empty or whitespace
            if (string.IsNullOrWhiteSpace(filename))
            {
                return PathValidationResult.Failure("Filename cannot be empty or contain only whitespace.");
            }

            // Check for null bytes
            if (filename.Contains('\0'))
            {
                return PathValidationResult.Failure("Filename contains null bytes.");
            }

            // Check for control characters
            foreach (var ch in filename)
            {
                if (char.IsControl(ch) && ch != '\t')
                {
                    return PathValidationResult.Failure($"Filename contains invalid control character: U+{(int)ch:X4}");
                }
            }

            // Check for path separators in filename (should be filename only, not a path)
            if (filename.Contains('/') || filename.Contains('\\'))
            {
                return PathValidationResult.Failure("Filename contains path separators. Use ValidatePath for full paths.");
            }

            // Check for reserved names
            if (WindowsReservedNames.Contains(filename))
            {
                return PathValidationResult.Failure($"Filename '{filename}' is a reserved name.");
            }

            // Check for dot-only names
            if (filename == "." || filename == "..")
            {
                return PathValidationResult.Failure($"Filename '{filename}' is invalid.");
            }

            // Check for excessive length
            if (filename.Length > 255)
            {
                return PathValidationResult.Failure($"Filename exceeds 255 characters ({filename.Length}).");
            }

            return PathValidationResult.Success();
        }
    }
}
