// <copyright file="TitleChangedEventHandler.cs" company="Moonrise Software, LLC">
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
    /// Logs title changes. Could also enqueue slug update tasks or invalidate caches.
    /// </summary>
    public sealed class TitleChangedEventHandler : IDomainEventHandler<TitleChangedEvent>
    {
        /// <summary>
        /// Logger instance for diagnostic output.
        /// </summary>
        private readonly ILogger<TitleChangedEventHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TitleChangedEventHandler"/> class.
        /// </summary>
        /// <param name="logger">The logger used to record title change events.</param>
        public TitleChangedEventHandler(ILogger<TitleChangedEventHandler> logger) =>
            this.logger = logger;

        /// <summary>
        /// Handles a <see cref="TitleChangedEvent"/> (non-cancellable overload).
        /// </summary>
        /// <param name="event">The domain event instance containing title change data.</param>
        /// <returns>A completed task.</returns>
        /// <remarks>
        /// This overload delegates to the cancellable version with <see cref="CancellationToken.None"/>.
        /// </remarks>
        public Task HandleAsync(TitleChangedEvent @event) =>
            HandleAsync(@event, CancellationToken.None);

        /// <summary>
        /// Handles a <see cref="TitleChangedEvent"/>, logging the old and new titles.
        /// </summary>
        /// <param name="event">The domain event instance containing title change data.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete. (Ignored here because the work is synchronous.)</param>
        /// <returns>A completed task.</returns>
        /// <remarks>
        /// Extend this method to:
        /// <list type="bullet">
        ///   <item>Enqueue background work to regenerate or update slugs.</item>
        ///   <item>Invalidate cached navigation or search indexes.</item>
        ///   <item>Publish integration events to external systems.</item>
        /// </list>
        /// </remarks>
        public Task HandleAsync(TitleChangedEvent @event, CancellationToken cancellationToken)
        {
            logger.LogInformation(
                "Title changed: ArticleNumber={ArticleNumber} '{Old}' -> '{New}'",
                @event.ArticleNumber, @event.OldTitle, @event.NewTitle);
            return Task.CompletedTask;
        }
    }
}
