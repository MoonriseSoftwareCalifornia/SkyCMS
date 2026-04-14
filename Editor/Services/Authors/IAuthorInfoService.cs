// <copyright file="IAuthorInfoService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Authors
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;

    /// <summary>
    /// Service contract for retrieving or creating <see cref="AuthorInfo"/> records
    /// associated with a user account.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations should attempt to locate an existing <see cref="AuthorInfo"/> using the supplied
    /// user ID (typically mapped to <see cref="AuthorInfo.Id"/> as a string).
    /// If no record exists, a new <see cref="AuthorInfo"/> should be created, persisted, and returned.
    /// </para>
    /// <para>
    /// Implementations are expected to be idempotent: multiple concurrent calls with the same
    /// user ID should not create duplicate author records. Any required
    /// concurrency control (e.g., database constraints or distributed locking) is the responsibility
    /// of the implementation.
    /// </para>
    /// </remarks>
    public interface IAuthorInfoService
    {
        /// <summary>
        /// Gets the existing <see cref="AuthorInfo"/> for the specified user, or creates
        /// and persists a new one if none exists.
        /// </summary>
        /// <param name="userId">The unique identifier of the user (maps to <see cref="AuthorInfo.Id"/>).</param>
        /// <returns>
        /// A task that, when completed successfully, yields the existing or newly created <see cref="AuthorInfo"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="userId"/> is <see cref="Guid.Empty"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if creation fails due to a persistence issue.</exception>
        Task<AuthorInfo> GetOrCreateAsync(Guid userId);
    }
}
