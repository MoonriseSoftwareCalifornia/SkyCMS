using AspNetCore.Identity.FlexDb.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace AspNetCore.Identity.FlexDb.Stores
{
    /// <summary>
    /// Cosmos DB Role Store
    /// </summary>
    /// <typeparam name="TRoleEntity"></typeparam>
    public class CosmosRoleStore<TUserRoleEntity, TRoleEntity, TKey> : IdentityStoreBase, IRoleStore<TRoleEntity>,
        IQueryableRoleStore<TRoleEntity>,
        IRoleClaimStore<TRoleEntity>
        where TRoleEntity : IdentityRole<TKey>, new()
        where TKey : IEquatable<TKey>

    {
        private readonly IRepository _repo;
        private readonly ILookupNormalizer _normalizer;
        private bool _disposed;

        /// <summary>
        /// Throws if this class has been disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// Role query
        /// </summary>
        public IQueryable<TRoleEntity> Roles
        {
            get { return (IQueryable<TRoleEntity>)_repo.Roles; }
        }

        /// <summary>
        /// UserRoles query
        /// </summary>
        public IQueryable<IdentityUserRole<TKey>> UserRoles
        {
            get { return (IQueryable<IdentityUserRole<TKey>>)_repo.UserRoles; }
        }

        /// <summary>
        /// UserRoles query
        /// </summary>
        public IQueryable<IdentityRoleClaim<TKey>> RoleClaims
        {
            get { return (IQueryable<IdentityRoleClaim<TKey>>)_repo.RoleClaims; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="normalizer">Identity normalizer for role lookup normalization.</param>
        public CosmosRoleStore(IRepository repo, ILookupNormalizer normalizer)
            : base(repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        }


        // <inheritdoc />
        public async Task<IdentityResult> CreateAsync(TRoleEntity role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (role == null)
                throw new ArgumentNullException(nameof(role));

            try
            {
                // Check for duplicate NormalizedName
                if (!string.IsNullOrEmpty(role.NormalizedName))
                {
                    var existingRole = await _repo.Table<TRoleEntity>()
                        .FirstOrDefaultAsync(_ => _.NormalizedName == role.NormalizedName, cancellationToken: cancellationToken);

                    if (existingRole != null)
                    {
                        return Fail("DuplicateRoleName", "Role with this name already exists.");
                    }
                }

                _repo.Add(role);
                await _repo.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return ProcessExceptions(ex);
            }

            return IdentityResult.Success;
        }

        // <inheritdoc />
        public async Task<IdentityResult> DeleteAsync(TRoleEntity role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (role == null)
            {
                throw new ArgumentNullException(nameof(role));
            }

            try
            {
                var userRoles = await UserRoles.Where(w => w.RoleId.Equals(role.Id)).ToListAsync(cancellationToken);
                foreach (var userRole in userRoles)
                {
                    _repo.Delete(userRole);
                }

                var roleClaims = await RoleClaims.Where(w => w.RoleId.Equals(role.Id)).ToListAsync(cancellationToken);
                foreach (var roleClaim in roleClaims)
                {
                    _repo.Delete(roleClaim);
                }

                _repo.Delete(role);
                await _repo.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return ProcessExceptions(ex);
            }

            return IdentityResult.Success;
        }

        // <inheritdoc />
        public async Task<TRoleEntity?> FindByIdAsync(string roleId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(roleId))
                throw new ArgumentNullException(nameof(roleId));

            TKey roleKey;

            if (typeof(TKey) == typeof(string))
            {
                roleKey = (TKey)(object)roleId;
            }
            else if (typeof(TKey) == typeof(Guid))
            {
                roleKey = (TKey)(object)Guid.Parse(roleId);
            }
            else
            {
                roleKey = (TKey)Convert.ChangeType(roleId, typeof(TKey), CultureInfo.InvariantCulture);
            }

            var role = await _repo.Table<TRoleEntity>()
                .SingleOrDefaultAsync(_ => _.Id.Equals(roleKey), cancellationToken: cancellationToken);

            return role;
        }

        // <inheritdoc />
        public async Task<TRoleEntity?> FindByNameAsync(string normalizedName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(normalizedName))
                throw new ArgumentNullException(nameof(normalizedName));

            // Normalize the input to ensure case-insensitive comparison
            var normalizedSearchName = _normalizer.NormalizeName(normalizedName) ?? normalizedName;

            var role = await _repo.Table<TRoleEntity>()
                .SingleOrDefaultAsync(_ => _.NormalizedName == normalizedSearchName, cancellationToken: cancellationToken);

            return role;
        }

        // <inheritdoc />
        public Task<string?> GetNormalizedRoleNameAsync(TRoleEntity role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (role == null)
            {
                throw new ArgumentNullException(nameof(role));
            }

            return Task.FromResult(role.NormalizedName);
        }

        // <inheritdoc />
        public Task<string> GetRoleIdAsync(TRoleEntity role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (role == null)
            {
                throw new ArgumentNullException(nameof(role));
            }

            return Task.FromResult(role.Id.ToString()!);
        }

        // <inheritdoc />
        public Task<string?> GetRoleNameAsync(TRoleEntity role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (role == null)
            {
                throw new ArgumentNullException(nameof(role));
            }

            return Task.FromResult(role.Name);
        }

        // <inheritdoc />
        public Task SetNormalizedRoleNameAsync(TRoleEntity role, string? normalizedName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (role == null)
            {
                throw new ArgumentNullException(nameof(role));
            }

            SetRoleProperty(role, normalizedName, (u, m) => u.NormalizedName = normalizedName, cancellationToken);

            return Task.CompletedTask;
        }

        // <inheritdoc />
        public Task SetRoleNameAsync(TRoleEntity role, string? roleName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (role == null)
                throw new ArgumentNullException(nameof(role));


            SetRoleProperty(role, roleName, (u, m) => u.Name = roleName, cancellationToken);

            return Task.CompletedTask;
        }

        // <inheritdoc />
        public async Task<IdentityResult> UpdateAsync(TRoleEntity role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(role);

            role.ConcurrencyStamp = Guid.NewGuid().ToString();

            try
            {
                _repo.Update(role);
                await _repo.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return ProcessExceptions(ex);
            }

            return IdentityResult.Success;
        }

        private void SetRoleProperty<T>(TRoleEntity role, T value, Action<TRoleEntity, T> setter,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (role == null) throw new ArgumentNullException(nameof(role));
            if (object.Equals(value, default(T))) throw new ArgumentNullException(nameof(value));

            setter(role, value);
        }

        // <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
        }

        #region Methods that implement IRoleClaimStore<TRoleEntity>

        // <inheritdoc />
        public async Task<IList<Claim>> GetClaimsAsync(TRoleEntity role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));

            var claims = await _repo.Table<IdentityRoleClaim<TKey>>().Where(c => c.RoleId.Equals(role.Id))
                .ToListAsync(cancellationToken);

            return claims.Select(c => c.ToClaim()).ToList();
        }

        // <inheritdoc />
        public async Task AddClaimAsync(TRoleEntity role, Claim claim, CancellationToken cancellationToken = default)
        {
            // Since the IdentityRoleClaim requires an integer ID, we need to get the last ID used and increment by one.
            // This means that if this fails, because of a concurrency issue, we need to retry.
            await Retry.DoAsync(() => InternalAddClaimAsync(role, claim, cancellationToken), TimeSpan.FromSeconds(1), cancellationToken: cancellationToken);
        }

        private async Task InternalAddClaimAsync(TRoleEntity role, Claim claim, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));
            if (claim == null)
                throw new ArgumentNullException(nameof(claim));


            if (ProviderNames.IsCosmos(_repo.ProviderName))
            {
                var identityRoleClaim = new IdentityRoleClaim<TKey>()
                {
                    ClaimType = claim.Type,
                    ClaimValue = claim.Value,
                    RoleId = role.Id,
                    Id = Utilities.GenerateRandomInt()
                };
                _repo.Add(identityRoleClaim);
            }
            else
            {
                var identityRoleClaim = new IdentityRoleClaim<TKey>()
                {
                    ClaimType = claim.Type,
                    ClaimValue = claim.Value,
                    RoleId = role.Id
                };
                _repo.Add(identityRoleClaim);
            }

            await _repo.SaveChangesAsync(cancellationToken);
        }

        // <inheritdoc />
        public async Task RemoveClaimAsync(TRoleEntity role, Claim claim, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));
            if (claim == null)
                throw new ArgumentNullException(nameof(claim));

            var doomed = await _repo.Table<IdentityRoleClaim<TKey>>()
                .FirstOrDefaultAsync(c => c.RoleId.Equals(role.Id) &&
                                          c.ClaimValue == claim.Value && c.ClaimType == claim.Type, cancellationToken);

            if (doomed != null)
            {
                _repo.Delete(doomed);
                await _repo.SaveChangesAsync(cancellationToken);
            }
        }

        #endregion
    }
}
