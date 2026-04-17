using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AspNetCore.Identity.CosmosDb.Contracts;
using AspNetCore.Identity.CosmosDb.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Identity.FlexDb
{
    /// <summary>
    /// A cross-provider-aware user store that fixes <see cref="CosmosUserStore{TUserEntity, TRoleEntity, TKey}"/>
    /// <c>FindByIdAsync</c> for relational providers (SQL Server, MySQL, SQLite).
    /// </summary>
    /// <remarks>
    /// The base <see cref="CosmosUserStore{TUserEntity, TRoleEntity, TKey}.FindByIdAsync"/> uses
    /// <c>CosmosQueryableExtensions.WithPartitionKey</c>, which is a no-op for relational providers
    /// and results in an unfiltered <c>SingleOrDefaultAsync()</c> with no WHERE clause on Id.
    /// This class re-implements <see cref="IUserStore{TUserEntity}.FindByIdAsync"/> with a standard
    /// EF Core query that works across all providers.
    /// </remarks>
    public class FlexDbUserStore<TUserEntity, TRoleEntity, TKey>
        : CosmosUserStore<TUserEntity, TRoleEntity, TKey>, IUserStore<TUserEntity>, IUserLoginStore<TUserEntity>
        where TUserEntity : IdentityUser<TKey>, new()
        where TRoleEntity : IdentityRole<TKey>, new()
        where TKey : IEquatable<TKey>
    {
        public FlexDbUserStore(IRepository repo)
            : base(repo)
        {
        }

        /// <summary>
        /// Provider-aware FindByEmailAsync that normalizes the input email
        /// to uppercase before querying, ensuring case-insensitive lookup
        /// works across all providers (including case-sensitive ones like SQLite).
        /// </summary>
        public new async Task<TUserEntity?> FindByEmailAsync(
            string normalizedEmail, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(normalizedEmail))
            {
                return default;
            }

            var upperEmail = normalizedEmail.ToUpperInvariant();

            return await _repo.Table<TUserEntity>()
                .FirstOrDefaultAsync(
                    u => u.NormalizedEmail == upperEmail,
                    cancellationToken);
        }

        /// <summary>
        /// Provider-aware FindByNameAsync that normalizes the input name
        /// to uppercase before querying, ensuring case-insensitive lookup
        /// works across all providers (including case-sensitive ones like SQLite).
        /// </summary>
        public new async Task<TUserEntity?> FindByNameAsync(
            string normalizedUserName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(normalizedUserName))
            {
                return default;
            }

            var upperName = normalizedUserName.ToUpperInvariant();

            return await _repo.Table<TUserEntity>()
                .FirstOrDefaultAsync(
                    u => u.NormalizedUserName == upperName,
                    cancellationToken);
        }

        /// <summary>
        /// Provider-aware CreateAsync that checks for duplicate email
        /// before delegating to the base class.
        /// </summary>
        public new async Task<IdentityResult> CreateAsync(
            TUserEntity user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (!string.IsNullOrEmpty(user.NormalizedEmail))
            {
                var existing = await _repo.Table<TUserEntity>()
                    .FirstOrDefaultAsync(
                        u => u.NormalizedEmail == user.NormalizedEmail,
                        cancellationToken);

                if (existing != null)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = "DuplicateEmail",
                        Description = $"Email '{user.Email}' is already taken."
                    });
                }
            }

            return await base.CreateAsync(user, cancellationToken);
        }

        /// <summary>
        /// Provider-aware FindByIdAsync that uses a standard EF Core query
        /// instead of the Cosmos-specific WithPartitionKey approach.
        /// </summary>
        public new async Task<TUserEntity?> FindByIdAsync(
            string userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (userId == null)
            {
                throw new ArgumentNullException(nameof(userId));
            }

            var typedId = (TKey)TypeDescriptor.GetConverter(typeof(TKey))
                .ConvertFromInvariantString(userId)!;

            return await _repo.Table<TUserEntity>()
                .SingleOrDefaultAsync(u => u.Id.Equals(typedId), cancellationToken);
        }

        /// <summary>
        /// Provider-aware FindByLoginAsync that avoids the Cosmos-specific
        /// WithPartitionKey call in the base class.
        /// </summary>
        public new async Task<TUserEntity?> FindByLoginAsync(
            string loginProvider, string providerKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var userLogin = await _repo.Table<IdentityUserLogin<TKey>>()
                .FirstOrDefaultAsync(
                    l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey,
                    cancellationToken);

            if (userLogin == null)
            {
                return default;
            }

            return await _repo.Table<TUserEntity>()
                .SingleOrDefaultAsync(u => u.Id.Equals(userLogin.UserId), cancellationToken);
        }

        /// <summary>
        /// Provider-aware UpdateAsync that validates the user argument
        /// before delegating to the base class.
        /// </summary>
        public new async Task<IdentityResult> UpdateAsync(
            TUserEntity user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return await base.UpdateAsync(user, cancellationToken);
        }
    }
}
