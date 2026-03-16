// <copyright file="ArticlePublishedEventHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Domain.Events.Handlers
{
    using Microsoft.Extensions.Logging;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Domain event handler for <see cref="ArticlePublishedEvent"/>.
    /// </summary>
    /// <remarks>
    /// Current implementation performs a lightweight side effect (structured log entry) and
    /// therefore completes synchronously (returns <see cref="Task.CompletedTask" />).
    /// This makes the handler naturally idempotent and safe to invoke multiple times
    /// (e.g., in replay or retry scenarios) because it only emits a log line.
    /// <para>
    /// Extend this handler if additional post�publish processes are required
    /// (e.g., cache invalidation, search indexing, web hook dispatch, etc.).
    /// For more complex or multi-step operations prefer making those operations
    /// resilient (idempotent) and consider offloading to background processing (queue)
    /// to keep domain event handling fast and predictable.
    /// </para>
    /// </remarks>
    public sealed class ArticlePublishedEventHandler : IDomainEventHandler<ArticlePublishedEvent>
    {
        /// <summary>
        /// Logger used to record publication diagnostics.
        /// </summary>
        private readonly ILogger<ArticlePublishedEventHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArticlePublishedEventHandler"/> class.
        /// </summary>
        /// <param name="logger">Application logger instance.</param>
        public ArticlePublishedEventHandler(ILogger<ArticlePublishedEventHandler> logger) =>
            this.logger = logger;

        /// <summary>
        /// Handles the <see cref="ArticlePublishedEvent"/> without an external cancellation token.
        /// </summary>
        /// <param name="event">The published article event instance.</param>
        /// <returns>A completed task.</returns>
        /// <remarks>
        /// Delegates to the overload that accepts a <see cref="CancellationToken"/> passing
        /// <see cref="CancellationToken.None"/> to preserve a single implementation point.
        /// </remarks>
        public Task HandleAsync(ArticlePublishedEvent @event) =>
            HandleAsync(@event, CancellationToken.None);

        /// <summary>
        /// Handles the <see cref="ArticlePublishedEvent"/> producing a structured log entry.
        /// </summary>
        /// <param name="event">The published article event instance.</param>
        /// <param name="cancellationToken">
        /// A token signaling cancellation. Currently not used because the operation
        /// is instantaneous; included for future extensibility.
        /// </param>
        /// <returns>A completed task.</returns>
        /// <remarks>
        /// No exception handling wrapper is added here so that failures (if any future
        /// logic is introduced) surface to the dispatcher for consistent unit-of-work
        /// handling or retry strategy.
        /// </remarks>
        public Task HandleAsync(ArticlePublishedEvent @event, CancellationToken cancellationToken)
        {
            logger.LogInformation(
                "Article published: ArticleNumber={ArticleNumber} Id={Id}",
                @event.ArticleNumber,
                @event.ArticleId);

            return Task.CompletedTask;
        }
    }
}
