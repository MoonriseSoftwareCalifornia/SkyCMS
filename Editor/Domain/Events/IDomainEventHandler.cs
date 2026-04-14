// <copyright file="IDomainEventHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Domain.Events
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a handler for a specific domain event type.
    /// </summary>
    /// <typeparam name="TEvent">Concrete domain event type.</typeparam>
    public interface IDomainEventHandler<in TEvent>
        where TEvent : IDomainEvent
    {
        /// <summary>
        /// Handles the specified domain event.
        /// </summary>
        /// <param name="event">Event instance.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task HandleAsync(TEvent @event);

        /// <summary>
        /// Handles the specified domain event with cancellation support.
        /// Implementers may ignore the token if not needed.
        /// </summary>
        /// <param name="event">Event instance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// The dispatcher will invoke either this overload (if present) or the single-parameter version.
        /// </remarks>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task HandleAsync(TEvent @event, CancellationToken cancellationToken) =>
            HandleAsync(@event);
    }
}
