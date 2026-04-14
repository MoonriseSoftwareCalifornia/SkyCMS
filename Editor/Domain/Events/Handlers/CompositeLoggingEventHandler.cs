// <copyright file="CompositeLoggingEventHandler.cs" company="Moonrise Software, LLC">
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
    /// Generic, no-op style domain event handler that logs a trace-level entry whenever
    /// a domain event of type <typeparamref name="TEvent"/> is dispatched.
    /// </summary>
    /// <remarks>
    /// Intended primarily for diagnostics and observability. Registering this as an open generic
    /// (e.g. services.AddTransient(typeof(IDomainEventHandler), typeof(CompositeLoggingEventHandler)))
    /// enables basic logging for all events without creating dedicated handlers.
    /// This handler does not modify state and completes synchronously.
    /// </remarks>
    /// <typeparam name="TEvent">Concrete domain event type being observed.</typeparam>
    public sealed class CompositeLoggingEventHandler<TEvent> : IDomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
        /// <summary>
        /// Logger used to emit trace information for received domain events.
        /// </summary>
        private readonly ILogger<CompositeLoggingEventHandler<TEvent>> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeLoggingEventHandler{TEvent}"/> class.
        /// </summary>
        /// <param name="logger">The logger instance injected from DI.</param>
        public CompositeLoggingEventHandler(ILogger<CompositeLoggingEventHandler<TEvent>> logger) =>
            this.logger = logger;

        /// <summary>
        /// Handles the specified event by delegating to the cancellation-aware overload
        /// with <see cref="CancellationToken.None"/>.
        /// </summary>
        /// <param name="event">The domain event instance.</param>
        /// <returns>A completed task.</returns>
        public Task HandleAsync(TEvent @event) =>
            HandleAsync(@event, CancellationToken.None);

        /// <summary>
        /// Logs a trace entry describing the received domain event.
        /// </summary>
        /// <param name="event">The domain event instance.</param>
        /// <param name="cancellationToken">Cancellation token (ignored; operation is instantaneous).</param>
        /// <returns>A completed task.</returns>
        /// <remarks>
        /// This method performs no asynchronous or cancellable work; the token is accepted only
        /// to satisfy the interface contract and future extensibility.
        /// </remarks>
        public Task HandleAsync(TEvent @event, CancellationToken cancellationToken)
        {
            logger.LogTrace("Domain event received: {EventType} at {Time}", typeof(TEvent).Name, @event.OccurredOn);
            return Task.CompletedTask;
        }
    }
}
