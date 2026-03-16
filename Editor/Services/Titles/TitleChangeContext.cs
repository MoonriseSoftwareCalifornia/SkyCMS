// <copyright file="TitleChangeContext.cs" company="Moonrise Software, LLC">
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
    /// Implementation of title change context providing infrastructure dependencies.
    /// </summary>
    public class TitleChangeContext : ITitleChangeContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TitleChangeContext"/> class.
        /// </summary>
        /// <param name="database">The database context.</param>
        /// <param name="clock">The clock abstraction.</param>
        /// <param name="dispatcher">The domain event dispatcher.</param>
        public TitleChangeContext(
            ApplicationDbContext database,
            IClock clock,
            IDomainEventDispatcher dispatcher)
        {
            Database = database;
            Clock = clock;
            Dispatcher = dispatcher;
        }

        /// <inheritdoc/>
        public ApplicationDbContext Database { get; }

        /// <inheritdoc/>
        public IClock Clock { get; }

        /// <inheritdoc/>
        public IDomainEventDispatcher Dispatcher { get; }
    }
}
