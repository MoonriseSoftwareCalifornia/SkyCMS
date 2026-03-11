// <copyright file="RedirectCreatedEventHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Domain.Events.Handlers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Domain event handler that reacts to <see cref="RedirectCreatedEvent"/> occurrences.
    /// Currently its responsibility is to log the creation of a redirect so
    /// that operational or audit tooling can observe slug mapping changes.
    /// </summary>
    /// <remarks>
    /// This handler is intentionally lightweight and non-blocking. It performs only structured logging
    /// and returns a completed task. If future needs require additional side-effects
    /// (e.g., cache invalidation, search index updates, metrics emission), they can be added here
    /// provided they remain idempotent.
    /// Thread-safety: The handler is stateless (aside from the injected logger) and is safe for concurrent use.
    /// </remarks>
    public sealed class RedirectCreatedEventHandler : IDomainEventHandler<RedirectCreatedEvent>
    {
        private readonly ILogger<RedirectCreatedEventHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedirectCreatedEventHandler"/> class.
        /// </summary>
        /// <param name="logger">Typed logger used to emit structured diagnostics for redirect creation events.</param>
        public RedirectCreatedEventHandler(ILogger<RedirectCreatedEventHandler> logger) =>
            this.logger = logger;

        /// <summary>
        /// Handles the redirect creation event (no cancellation token overload).
        /// </summary>
        /// <param name="event">The redirect creation domain event instance.</param>
        /// <returns>A completed <see cref="Task"/> since the operation is synchronous.</returns>
        /// <remarks>
        /// This overload delegates to the cancellation-aware version supplying <see cref="CancellationToken.None"/>.
        /// </remarks>
        public Task HandleAsync(RedirectCreatedEvent @event) =>
            HandleAsync(@event, CancellationToken.None);

        /// <summary>
        /// Handles the redirect creation event by logging the mapping from source to destination slug.
        /// </summary>
        /// <param name="event">The redirect creation domain event instance.</param>
        /// <param name="cancellationToken">
        /// A cancellation token. Currently not observed because the operation is trivial and synchronous;
        /// supplied for interface compliance and future extensibility.
        /// </param>
        /// <returns>A completed <see cref="Task"/> since the handler performs only a logging side-effect.</returns>
        /// <remarks>
        /// The log entry includes structured properties (From, To) to facilitate filtering and analysis.
        /// </remarks>
        public Task HandleAsync(RedirectCreatedEvent @event, CancellationToken cancellationToken)
        {
            logger.LogInformation("Redirect created: {From} -> {To}", @event.FromSlug, @event.ToSlug);
            return Task.CompletedTask;
        }
    }
}
