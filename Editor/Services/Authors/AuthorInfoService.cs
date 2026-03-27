// <copyright file="AuthorInfoService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Authors
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Services.Caching;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides operations for retrieving or creating <see cref="AuthorInfo"/> records
    /// associated with an identity user. Results are cached to reduce database load.
    /// </summary>
    public class AuthorInfoService : IAuthorInfoService
    {
        /// <summary>
        /// Database context used to query and persist author and identity records.
        /// </summary>
        private readonly ApplicationDbContext db;

        /// <summary>
        /// Cache used to store recently accessed <see cref="AuthorInfo"/> instances.
        /// </summary>
        private readonly ICacheService<AuthorInfo> cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorInfoService"/> class.
        /// </summary>
        /// <param name="db">The <see cref="ApplicationDbContext"/> used for persistence.</param>
        /// <param name="cache">The cache service used to cache results.</param>
        public AuthorInfoService(ApplicationDbContext db, ICacheService<AuthorInfo> cache)
        {
            this.db = db;
            this.cache = cache;
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
        public async Task<AuthorInfo> GetOrCreateAsync(Guid userId)
        {
            var key = userId.ToString();
            if (cache.TryGet(key, out var cached) && cached != null)
            {
                return cached;
            }

            var existing = await db.AuthorInfos.FirstOrDefaultAsync(a => a.Id == key);
            if (existing == null)
            {
                var identity = await db.Users.FirstOrDefaultAsync(u => u.Id == key);
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

                db.AuthorInfos.Add(existing);
                await db.SaveChangesAsync();
            }

            cache.Set(key, existing, TimeSpan.FromMinutes(10));
            return existing;
        }
    }
}
