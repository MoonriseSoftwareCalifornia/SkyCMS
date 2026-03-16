// <copyright file="TestDbContextPool.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Infrastructure
{
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Provides a thread-safe pool of pre-configured ApplicationDbContext instances
    /// for parallel test execution. Each context uses an isolated in-memory database.
    /// </summary>
    /// <remarks>
    /// This pool is designed to support parallel test execution with a minimum of 6 workers.
    /// The pool creates 10 isolated contexts to handle concurrent access patterns.
    /// Each context has its own in-memory database to ensure complete data isolation.
    /// </remarks>
    public sealed class TestDbContextPool : IDisposable
    {
        private readonly List<ApplicationDbContext> _contexts;
        private readonly object _lockObject = new();
        private int _currentIndex;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestDbContextPool"/> class.
        /// Creates a pool of 10 isolated ApplicationDbContext instances.
        /// </summary>
        /// <param name="poolSize">Number of contexts to create in the pool. Default is 10 for 6+ parallel workers.</param>
        public TestDbContextPool(int poolSize = 10)
        {
            _contexts = new List<ApplicationDbContext>(poolSize);

            for (int i = 0; i < poolSize; i++)
            {
                var context = CreateContext($"TestDb_Pool_{i}_{Guid.NewGuid()}");
                _contexts.Add(context);
            }
        }

        /// <summary>
        /// Gets the total number of contexts in the pool.
        /// </summary>
        public int PoolSize => _contexts.Count;

        /// <summary>
        /// Gets the next available context from the pool using round-robin distribution.
        /// Thread-safe for parallel test execution.
        /// </summary>
        /// <returns>An ApplicationDbContext from the pool.</returns>
        public ApplicationDbContext GetContext()
        {
            lock (_lockObject)
            {
                var context = _contexts[_currentIndex % _contexts.Count];
                _currentIndex++;
                return context;
            }
        }

        /// <summary>
        /// Creates an isolated ApplicationDbContext with its own in-memory database.
        /// </summary>
        /// <param name="databaseName">Unique name for the in-memory database.</param>
        /// <returns>Configured ApplicationDbContext.</returns>
        private static ApplicationDbContext CreateContext(string databaseName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new ApplicationDbContext(options);
        }

        /// <summary>
        /// Creates a new isolated context not from the pool.
        /// Use this for tests that require complete isolation or modify context configuration.
        /// </summary>
        /// <returns>New ApplicationDbContext with unique in-memory database.</returns>
        public static ApplicationDbContext CreateIsolatedContext()
        {
            return CreateContext($"TestDb_Isolated_{Guid.NewGuid()}");
        }

        /// <summary>
        /// Disposes all contexts in the pool.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var context in _contexts)
            {
                context.Dispose();
            }

            _contexts.Clear();
            _disposed = true;
        }
    }
}
