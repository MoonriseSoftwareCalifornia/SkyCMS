// <copyright file="MultiTenantMediator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Cosmos.DynamicConfig;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Multi-tenant aware mediator decorator that validates user authorization before executing commands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This decorator wraps the base <see cref="Mediator"/> implementation to add multi-tenant security validation.
    /// It intercepts command execution to ensure that users can only perform operations within their authorized tenant.
    /// </para>
    /// <para>
    /// <strong>Security Model:</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item>Validates that the user exists in the system</item>
    ///   <item>Checks that the user's email domain matches the current tenant domain</item>
    ///   <item>Throws <see cref="UnauthorizedAccessException"/> if validation fails</item>
    ///   <item>Skips validation for commands without a UserId property (system commands)</item>
    ///   <item>Skips validation in single-tenant mode (when configurationProvider is null)</item>
    /// </list>
    /// <para>
    /// <strong>Queries:</strong> Query requests pass through without validation because the <see cref="ApplicationDbContext"/>
    /// automatically applies tenant filtering via EF Core query filters.
    /// </para>
    /// <para>
    /// <strong>Future Enhancement:</strong> This implementation uses email domain matching for tenant affiliation.
    /// When the User entity is enhanced with a TenantDomain property, the validation logic should be updated
    /// to use that property instead.
    /// </para>
    /// </remarks>
    public class MultiTenantMediator : IMediator
    {
        private readonly IMediator innerMediator;
        private readonly ApplicationDbContext dbContext;
        private readonly IDynamicConfigurationProvider? configurationProvider;
        private readonly ILogger<MultiTenantMediator> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiTenantMediator"/> class.
        /// </summary>
        /// <param name="innerMediator">The base mediator implementation to wrap.</param>
        /// <param name="dbContext">The database context for user validation.</param>
        /// <param name="configurationProvider">The configuration provider for tenant resolution (null in single-tenant scenarios).</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        public MultiTenantMediator(
            IMediator innerMediator,
            ApplicationDbContext dbContext,
            IDynamicConfigurationProvider? configurationProvider,
            ILogger<MultiTenantMediator> logger)
        {
            this.innerMediator = innerMediator ?? throw new ArgumentNullException(nameof(innerMediator));
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.configurationProvider = configurationProvider;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Sends a command to its registered handler after validating multi-tenant authorization.
        /// </summary>
        /// <typeparam name="TResult">The type of result returned by the command handler.</typeparam>
        /// <param name="command">The command instance to be processed by its handler.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, containing the result from the command handler.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when:
        /// <list type="bullet">
        ///   <item>The user specified in the command does not exist.</item>
        ///   <item>The user's email domain does not match the current tenant domain.</item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        ///   <item>Checks if multi-tenant mode is enabled (configurationProvider is not null)</item>
        ///   <item>Retrieves the current tenant domain from the request context</item>
        ///   <item>Validates that the command's user belongs to the current tenant</item>
        ///   <item>Delegates to the inner mediator to execute the command</item>
        /// </list>
        /// <para>
        /// Commands without a UserId property (such as system-level commands) are not validated and pass through directly.
        /// </para>
        /// </remarks>
        public async Task<TResult> SendAsync<TResult>(
            ICommand<TResult> command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            // Validate tenant affiliation if multi-tenant is configured
            if (configurationProvider != null)
            {
                var tenantDomain = configurationProvider.GetTenantDomainNameFromRequest();

                if (!string.IsNullOrEmpty(tenantDomain))
                {
                    await ValidateUserBelongsToTenant(command, tenantDomain, cancellationToken);
                }
            }

            return await innerMediator.SendAsync(command, cancellationToken);
        }

        /// <summary>
        /// Sends a query to its registered handler for processing.
        /// </summary>
        /// <typeparam name="TResult">The type of result returned by the query handler.</typeparam>
        /// <param name="query">The query instance to be processed by its handler.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, containing the result from the query handler.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is null.</exception>
        /// <remarks>
        /// <para>
        /// Queries do not require explicit tenant validation because the <see cref="ApplicationDbContext"/>
        /// automatically applies tenant filtering via EF Core query filters. This ensures that queries can only
        /// retrieve data from the current tenant's scope.
        /// </para>
        /// </remarks>
        public Task<TResult> QueryAsync<TResult>(
            IQuery<TResult> query,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            // Queries read through DbContext filters - no additional validation needed
            return innerMediator.QueryAsync(query, cancellationToken);
        }

        /// <summary>
        /// Validates that a user belongs to the specified tenant domain.
        /// </summary>
        /// <typeparam name="TResult">The command result type.</typeparam>
        /// <param name="command">The command containing the user identifier.</param>
        /// <param name="tenantDomain">The tenant domain to validate against.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous validation operation.</returns>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the user does not exist or does not belong to the specified tenant.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method uses reflection to locate a UserId property on the command. If the property is not found
        /// or is not of type <see cref="Guid"/>, the validation is skipped (assuming it's a system command that
        /// doesn't require user-level authorization).
        /// </para>
        /// <para>
        /// <strong>Current Implementation:</strong> Uses email domain matching (user.Email ends with @tenantDomain).
        /// </para>
        /// <para>
        /// <strong>Future Enhancement:</strong> Should be updated to check User.TenantDomain property once the
        /// User entity is enhanced with explicit tenant affiliation.
        /// </para>
        /// </remarks>
        private async Task ValidateUserBelongsToTenant<TResult>(
            ICommand<TResult> command,
            string tenantDomain,
            CancellationToken cancellationToken)
        {
            // Use reflection to find UserId property
            var commandType = command.GetType();
            var userIdProperty = commandType.GetProperty("UserId");

            if (userIdProperty == null || userIdProperty.PropertyType != typeof(Guid))
            {
                // Command doesn't have UserId - skip validation (might be system command)
                logger.LogDebug(
                    "Skipping tenant validation for {CommandType} - no UserId property found",
                    commandType.Name);
                return;
            }

            var userId = (Guid)userIdProperty.GetValue(command)!;

            // Validate user exists and belongs to tenant
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == userId.ToString(), cancellationToken);

            if (user == null)
            {
                logger.LogWarning(
                    "User {UserId} not found while executing {CommandType} in tenant {TenantDomain}",
                    userId,
                    commandType.Name,
                    tenantDomain);

                throw new UnauthorizedAccessException($"User {userId} does not exist");
            }

            // Validate user's email domain matches tenant
            // NOTE: This assumes email-based tenant affiliation.
            // Replace with proper User.TenantDomain check once User entity is tenant-aware
            if (string.IsNullOrEmpty(user.Email))
            {
                logger.LogWarning(
                    "User {UserId} has null/empty email - cannot validate tenant affiliation for {CommandType} in {TenantDomain}",
                    userId,
                    commandType.Name,
                    tenantDomain);

                throw new UnauthorizedAccessException(
                    $"User {userId} has no email address and cannot be validated for tenant {tenantDomain}");
            }

            if (!user.Email.EndsWith($"@{tenantDomain}", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "User {UserId} ({Email}) attempted cross-tenant operation in {TenantDomain} via {CommandType}",
                    userId,
                    user.Email,
                    tenantDomain,
                    commandType.Name);

                throw new UnauthorizedAccessException(
                    $"User {userId} is not authorized to perform operations in tenant {tenantDomain}");
            }

            logger.LogDebug(
                "User {UserId} validated for tenant {TenantDomain} executing {CommandType}",
                userId,
                tenantDomain,
                commandType.Name);
        }
    }
}
