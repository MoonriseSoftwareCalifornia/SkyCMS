using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AspNetCore.Identity.CosmosDb.Contracts;
using AspNetCore.Identity.CosmosDb.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
        : CosmosUserStore<TUserEntity, TRoleEntity, TKey>,
            IUserStore<TUserEntity>,
            IUserLoginStore<TUserEntity>,
            IUserEmailStore<TUserEntity>,
            IUserPhoneNumberStore<TUserEntity>,
            IUserLockoutStore<TUserEntity>
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
                .AsNoTracking()
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
                .AsNoTracking()
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
                    .AsNoTracking()
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
                .AsNoTracking()
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
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey,
                    cancellationToken);

            if (userLogin == null)
            {
                return default;
            }

            return await _repo.Table<TUserEntity>()
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id.Equals(userLogin.UserId), cancellationToken);
        }

        async Task IUserStore<TUserEntity>.SetUserNameAsync(TUserEntity user, string userName, CancellationToken cancellationToken)
        {
            await SetUserNameAsync(user, userName, cancellationToken);
        }

        async Task IUserStore<TUserEntity>.SetNormalizedUserNameAsync(TUserEntity user, string normalizedName, CancellationToken cancellationToken)
        {
            await SetNormalizedUserNameAsync(user, normalizedName, cancellationToken);
        }

        async Task IUserEmailStore<TUserEntity>.SetNormalizedEmailAsync(TUserEntity user, string normalizedEmail, CancellationToken cancellationToken)
        {
            await SetNormalizedEmailAsync(user, normalizedEmail, cancellationToken);
        }

        async Task IUserPhoneNumberStore<TUserEntity>.SetPhoneNumberConfirmedAsync(TUserEntity user, bool confirmed, CancellationToken cancellationToken)
        {
            await SetPhoneNumberConfirmedAsync(user, confirmed, cancellationToken);
        }

        async Task IUserLockoutStore<TUserEntity>.SetLockoutEndDateAsync(TUserEntity user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            await SetLockoutEndDateAsync(user, lockoutEnd, cancellationToken);
        }

        Task<DateTimeOffset?> IUserLockoutStore<TUserEntity>.GetLockoutEndDateAsync(TUserEntity user, CancellationToken cancellationToken)
        {
            return GetLockoutEndDateAsync(user, cancellationToken);
        }

        Task<int> IUserLockoutStore<TUserEntity>.IncrementAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
        {
            return IncrementAccessFailedCountAsync(user, cancellationToken);
        }

        Task IUserLockoutStore<TUserEntity>.ResetAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
        {
            return ResetAccessFailedCountAsync(user, cancellationToken);
        }

        public new async Task SetUserNameAsync(TUserEntity user, string userName, CancellationToken cancellationToken = default)
        {
            await base.SetUserNameAsync(user, userName, cancellationToken);
            await PersistIfExistingAsync(user, cancellationToken);
        }

        public new async Task SetNormalizedUserNameAsync(TUserEntity user, string normalizedName, CancellationToken cancellationToken = default)
        {
            await base.SetNormalizedUserNameAsync(user, normalizedName, cancellationToken);
            await PersistIfExistingAsync(user, cancellationToken);
        }

        public new async Task SetNormalizedEmailAsync(TUserEntity user, string normalizedEmail, CancellationToken cancellationToken = default)
        {
            await base.SetNormalizedEmailAsync(user, normalizedEmail, cancellationToken);
            await PersistIfExistingAsync(user, cancellationToken);
        }

        public new async Task SetPhoneNumberConfirmedAsync(TUserEntity user, bool confirmed, CancellationToken cancellationToken = default)
        {
            await base.SetPhoneNumberConfirmedAsync(user, confirmed, cancellationToken);
            await PersistIfExistingAsync(user, cancellationToken);
        }

        public new Task SetLockoutEndDateAsync(TUserEntity user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.LockoutEnd = lockoutEnd;
            return Task.CompletedTask;
        }

        public new Task<DateTimeOffset?> GetLockoutEndDateAsync(TUserEntity user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.LockoutEnd);
        }

        public new Task<int> IncrementAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.AccessFailedCount++;
            return Task.FromResult(user.AccessFailedCount);
        }

        public new Task ResetAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.AccessFailedCount = 0;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Provider-aware UpdateAsync that persists mutations even when entities are detached.
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

            var users = _repo.Table<TUserEntity>();
            var dbContext = GetDbContext(users);

            var localEntries = dbContext.ChangeTracker
                .Entries<TUserEntity>()
                .Where(e => e.Entity.Id.Equals(user.Id))
                .ToList();

            foreach (var entry in localEntries)
            {
                entry.State = EntityState.Detached;
            }

            var tracked = await users.FirstOrDefaultAsync(u => u.Id.Equals(user.Id), cancellationToken);
            if (tracked == null)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "NotFound",
                    Description = "User does not exist."
                });
            }

            dbContext.Entry(tracked).CurrentValues.SetValues(user);

            var newConcurrencyStamp = Guid.NewGuid().ToString();
            tracked.ConcurrencyStamp = newConcurrencyStamp;
            user.ConcurrencyStamp = newConcurrencyStamp;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return IdentityResult.Success;
            }
            catch (DbUpdateConcurrencyException)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "ConcurrencyFailure",
                    Description = "Optimistic concurrency failure, object has been modified."
                });
            }
        }

        private async Task PersistIfExistingAsync(TUserEntity user, CancellationToken cancellationToken)
        {
            var result = await UpdateAsync(user, cancellationToken);
            if (!result.Succeeded)
            {
                var isNotFound = result.Errors.Any(e => e.Code == "NotFound");
                if (isNotFound)
                {
                    return;
                }

                var message = string.Join(", ", result.Errors.Select(e => $"[{e.Code}] {e.Description}"));
                throw new InvalidOperationException($"Unable to persist user changes. {message}");
            }
        }

        private static DbContext GetDbContext(IQueryable<TUserEntity> queryable)
        {
            if (queryable is IInfrastructure<IServiceProvider> infrastructure
                && infrastructure.Instance.GetService(typeof(ICurrentDbContext)) is ICurrentDbContext currentDbContext)
            {
                return currentDbContext.Context;
            }

            throw new InvalidOperationException("Unable to resolve DbContext from repository queryable.");
        }
    }
}
