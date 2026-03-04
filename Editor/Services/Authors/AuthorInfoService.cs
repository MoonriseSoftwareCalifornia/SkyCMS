// <copyright file="AuthorInfoService.cs" company="Moonrise Software, LLC">
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
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;

    /// <summary>
    /// Provides operations for retrieving or creating <see cref="AuthorInfo"/> records
    /// associated with an identity user. Results are cached in an <see cref="IMemoryCache"/>
    /// to reduce database load.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service maps a supplied <see cref="Guid"/> to the string-typed <see cref="AuthorInfo.Id"/>.
    /// It attempts to return an existing <see cref="AuthorInfo"/> from an in-memory cache keyed by the
    /// user's GUID string. If no cached or persisted record exists, the service will attempt to
    /// locate the corresponding identity user in <c>ApplicationDbContext.Users</c> and create
    /// a new <see cref="AuthorInfo"/> initialized from the identity (using <c>UserName</c> or <c>Email</c>
    /// for the displayed author name).
    /// </para>
    /// <para>
    /// Caching duration is 10 minutes. Implementations should be idempotent; however, concurrent
    /// callers may require database-level constraints to avoid duplicate records in race conditions.
    /// </para>
    /// </remarks>
    public class AuthorInfoService : IAuthorInfoService
    {
        /// <summary>
        /// Database context used to query and persist author and identity records.
        /// </summary>
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// In-memory cache used to store recently accessed <see cref="AuthorInfo"/> instances.
        /// </summary>
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorInfoService"/> class.
        /// </summary>
        /// <param name="db">The <see cref="ApplicationDbContext"/> used for persistence.</param>
        /// <param name="cache">The <see cref="IMemoryCache"/> used to cache results.</param>
        public AuthorInfoService(ApplicationDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        /// <summary>
        /// Gets an existing <see cref="AuthorInfo"/> for the specified <paramref name="userId"/>,
        /// or creates and persists a new one when none exists.
        /// </summary>
        /// <param name="userId">The unique identifier of the user (maps to <see cref="AuthorInfo.Id"/> as a string).</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the existing
        /// or newly created <see cref="AuthorInfo"/>, or <c>null</c> when the identity user cannot be found.
        /// </returns>
        /// <remarks>
        /// Behavior details:
        /// - The method derives a string cache key from <paramref name="userId"/> via <c>userId.ToString()</c>.
        /// - If a cached <see cref="AuthorInfo"/> exists, it is returned immediately.
        /// - Otherwise, the method queries <see cref="ApplicationDbContext.AuthorInfos"/> for a persisted record.
        /// - If no persisted record exists, the method queries <see cref="ApplicationDbContext"/> to obtain
        ///   an identity record. If the identity is found, a new <see cref="AuthorInfo"/> is created with:
        ///     - <see cref="AuthorInfo.Id"/> = userId string
        ///     - <see cref="AuthorInfo.AuthorName"/> = identity.UserName ?? identity.Email ?? userId string
        ///     - <see cref="AuthorInfo.AuthorDescription"/> = empty string
        ///   The new record is added to the context and saved.
        /// - The resulting <see cref="AuthorInfo"/> is cached for 10 minutes and returned.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the provided <paramref name="userId"/> is <see langword="null"/> (not applicable for Guid parameter but included for completeness).</exception>
        /// <exception cref="InvalidOperationException">Thrown when persisting a newly created author record fails.</exception>
        public async Task<AuthorInfo> GetOrCreateAsync(Guid userId)
        {
            var key = userId.ToString();
            if (_cache.TryGetValue(key, out AuthorInfo cached))
            {
                return cached;
            }

            var existing = await _db.AuthorInfos.FirstOrDefaultAsync(a => a.Id == key);
            if (existing == null)
            {
                var identity = await _db.Users.FirstOrDefaultAsync(u => u.Id == key);
                if (identity == null)
                {
                    return null;
                }

                existing = new AuthorInfo
                {
                    Id = key,
                    AuthorName = identity.UserName ?? identity.Email ?? key,
                    AuthorDescription = string.Empty,
                    EmailAddress = identity.Email,
                    InstagramUrl = string.Empty,
                    TwitterHandle = string.Empty,
                    Website = string.Empty
                };

                _db.AuthorInfos.Add(existing);
                await _db.SaveChangesAsync();
            }

            _cache.Set(key, existing, TimeSpan.FromMinutes(10));
            return existing;
        }
    }
}
