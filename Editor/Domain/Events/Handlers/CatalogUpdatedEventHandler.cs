// <copyright file="CatalogUpdatedEventHandler.cs" company="Moonrise Software, LLC">
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

    /*
        Pseudocode (documentation update only):
        - Enhance XML summary for handler class: purpose, typical extension points.
        - Add XML doc for constructor: describe dependency.
        - Add XML doc for HandleAsync overloads:
            - Single-param overload delegates to full overload with CancellationToken.None.
            - Two-param overload logs debug message and returns completed task.
        - Clarify cancellation token usage (currently ignored).
        - No behavioral changes.
    */

    /// <summary>
    /// Domain event handler that reacts to <see cref="CatalogUpdatedEvent"/> occurrences.
    /// Currently its responsibility is to emit a structured debug log entry indicating which
    /// catalog (identified by <c>ArticleNumber</c>) was updated. This keeps an auditable trail
    /// in logs and provides a single place to later:
    /// <list type="bullet">
    ///   <item>Warm or invalidate in-memory / distributed caches.</item>
    ///   <item>Publish integration events to external systems (e.g., message bus).</item>
    ///   <item>Trigger search index refreshes or denormalized view updates.</item>
    /// </list>
    /// The class is intentionally minimal and side-effect free beyond logging so that higher
    /// level orchestration / composition can evolve without breaking existing consumers.
    /// </summary>
    public sealed class CatalogUpdatedEventHandler : IDomainEventHandler<CatalogUpdatedEvent>
    {
        private readonly ILogger<CatalogUpdatedEventHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CatalogUpdatedEventHandler"/> class.
        /// </summary>
        /// <param name="logger">Typed logger used to record diagnostic information when catalog updates occur.</param>
        public CatalogUpdatedEventHandler(ILogger<CatalogUpdatedEventHandler> logger) =>
            this.logger = logger;

        /// <summary>
        /// Handles the <see cref="CatalogUpdatedEvent"/> by delegating to the cancellation-enabled overload
        /// with <see cref="CancellationToken.None"/>. Prefer calling the overload accepting a token when
        /// invoked from infrastructure that can propagate cancellation.
        /// </summary>
        /// <param name="event">The catalog update event instance.</param>
        /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task HandleAsync(CatalogUpdatedEvent @event) =>
            HandleAsync(@event, CancellationToken.None);

        /// <summary>
        /// Handles the <see cref="CatalogUpdatedEvent"/> and records a debug-level log entry containing
        /// the associated <c>ArticleNumber</c>. Designed as an extension point for future cross-cutting
        /// activities (cache invalidation, search indexing, downstream notifications).
        /// </summary>
        /// <param name="event">The catalog update event instance.</param>
        /// <param name="cancellationToken">
        /// A cancellation token. Currently not observed because the operation is instantaneous,
        /// but retained to satisfy the contract and to allow future async work to respect cancellation.
        /// </param>
        /// <returns>A completed <see cref="Task"/> since the current implementation is synchronous.</returns>
        /// <remarks>
        /// Logging occurs at Debug level to avoid noise in higher log levels while still enabling
        /// detailed diagnostics when needed.
        /// </remarks>
        public Task HandleAsync(CatalogUpdatedEvent @event, CancellationToken cancellationToken)
        {
            logger.LogDebug("Catalog updated for ArticleNumber={ArticleNumber}", @event.ArticleNumber);
            return Task.CompletedTask;
        }
    }
}
