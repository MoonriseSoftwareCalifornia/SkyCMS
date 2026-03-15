// <copyright file="TenantResolutionException.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService.Exceptions;

using System;

/// <summary>
/// Exception thrown when tenant-specific storage configuration cannot be resolved in multi-tenant scenarios.
/// </summary>
public class TenantResolutionException : StorageException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TenantResolutionException"/> class.
        /// </summary>
        public TenantResolutionException()
            : base("Cannot resolve tenant storage connection. Ensure HttpContext is available or provide domain explicitly.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantResolutionException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public TenantResolutionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantResolutionException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public TenantResolutionException(string message, Exception innerException)
                    : base(message, innerException)
                {
                }
            }
