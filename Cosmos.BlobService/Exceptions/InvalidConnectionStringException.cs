// <copyright file="InvalidConnectionStringException.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService.Exceptions;

using System;

/// <summary>
/// Exception thrown when a storage connection string is invalid or cannot be parsed.
/// </summary>
public class InvalidConnectionStringException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidConnectionStringException"/> class.
    /// </summary>
    public InvalidConnectionStringException()
        : base("The storage connection string is invalid.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidConnectionStringException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InvalidConnectionStringException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidConnectionStringException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InvalidConnectionStringException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets or sets the provider type that was being parsed (if known).
    /// </summary>
    public CloudStorageProvider? AttemptedProvider { get; set; }
}
