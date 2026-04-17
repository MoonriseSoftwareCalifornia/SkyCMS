// <copyright file="FlexDbRoleStore.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using AspNetCore.Identity.CosmosDb.Contracts;
using AspNetCore.Identity.CosmosDb.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Identity.FlexDb
{
    /// <summary>
    /// A cross-provider-aware role store that extends <see cref="CosmosRoleStore{TRoleEntity, TKey}"/>
    /// with duplicate role name detection in <see cref="CreateAsync"/>.
    /// </summary>
    /// <remarks>
    /// The base <see cref="CosmosRoleStore{TRoleEntity, TKey}.CreateAsync"/> does not check for
    /// existing roles with the same <c>NormalizedName</c>, which allows duplicates on providers
    /// that lack a unique index (e.g., Cosmos DB). This class adds that check.
    /// </remarks>
    public class FlexDbRoleStore<TRoleEntity, TKey>
        : CosmosRoleStore<TRoleEntity, TKey>
        where TRoleEntity : IdentityRole<TKey>, new()
        where TKey : IEquatable<TKey>
    {
        public FlexDbRoleStore(IRepository repo)
            : base(repo)
        {
        }

        /// <summary>
        /// Creates a new role after verifying no role with the same normalized name exists.
        /// </summary>
        public new async Task<IdentityResult> CreateAsync(
            TRoleEntity role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (role == null)
            {
                throw new ArgumentNullException(nameof(role));
            }

            if (!string.IsNullOrEmpty(role.NormalizedName))
            {
                var existing = await _repo.Table<TRoleEntity>()
                    .FirstOrDefaultAsync(
                        r => r.NormalizedName == role.NormalizedName,
                        cancellationToken);

                if (existing != null)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = "DuplicateRoleName",
                        Description = $"Role name '{role.Name}' is already taken."
                    });
                }
            }

            return await base.CreateAsync(role, cancellationToken);
        }

        /// <summary>
        /// Provider-aware FindByIdAsync that uses a standard EF Core query
        /// instead of the Cosmos-specific WithPartitionKey approach.
        /// </summary>
        public new async Task<TRoleEntity?> FindByIdAsync(
            string roleId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (roleId == null)
            {
                throw new ArgumentNullException(nameof(roleId));
            }

            var typedId = (TKey)TypeDescriptor.GetConverter(typeof(TKey))
                .ConvertFromInvariantString(roleId)!;

            return await _repo.Table<TRoleEntity>()
                .SingleOrDefaultAsync(r => r.Id.Equals(typedId), cancellationToken);
        }

        /// <summary>
        /// Finds a role by its name, normalizing the input to ensure case-insensitive matching
        /// across all database providers (including those with case-sensitive collations like MySQL).
        /// </summary>
        public new async Task<TRoleEntity> FindByNameAsync(
            string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(normalizedRoleName))
            {
                return null;
            }

            var upperName = normalizedRoleName.ToUpperInvariant();
            return await _repo.Table<TRoleEntity>()
                .FirstOrDefaultAsync(
                    r => r.NormalizedName == upperName,
                    cancellationToken);
        }
    }
}
