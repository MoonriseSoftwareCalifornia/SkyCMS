// <copyright file="ITitleChangeContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Titles
{
    using Cosmos.Common.Data;
    using Sky.Editor.Domain.Events;
    using Sky.Editor.Infrastructure.Time;

    /// <summary>
    /// Provides access to infrastructure dependencies required for title change operations.
    /// </summary>
    /// <remarks>
    /// This composite groups infrastructure services used by title change coordination:
    /// database context for persisting changes, clock for timestamps, and event dispatcher for notifications.
    /// </remarks>
    public interface ITitleChangeContext
    {
        /// <summary>
        /// Gets the database context for article and redirect persistence.
        /// </summary>
        ApplicationDbContext Database { get; }

        /// <summary>
        /// Gets the clock abstraction for obtaining testable timestamps.
        /// </summary>
        IClock Clock { get; }

        /// <summary>
        /// Gets the domain event dispatcher for publishing title change events.
        /// </summary>
        IDomainEventDispatcher Dispatcher { get; }
    }
}
