// <copyright file="CommonTestsBase.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Infrastructure
{
    using Cosmos.Common.Data;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Base class for Cosmos.Common tests providing shared infrastructure for parallel test execution.
    /// Manages a pooled set of ApplicationDbContext instances for efficient, isolated testing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This base class provides two strategies for obtaining test database contexts:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <term>Pooled Context (GetPooledContext)</term>
    /// <description>
    /// Fast, pre-created contexts from a pool. Use for most tests. Thread-safe with round-robin distribution.
    /// Each context has its own isolated in-memory database.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Isolated Context (GetIsolatedContext)</term>
    /// <description>
    /// Creates a brand-new context not from the pool. Use for tests that need guaranteed isolation
    /// or modify context configuration. Slightly slower due to creation overhead.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// The pool is initialized once per test class and supports a minimum of 6 parallel workers.
    /// </para>
    /// </remarks>
    public abstract class CommonTestsBase
    {
        /// <summary>
        /// Shared pool of database contexts for this test class.
        /// Initialized once in ClassInitialize, disposed in ClassCleanup.
        /// </summary>
        protected static TestDbContextPool? ContextPool;

        /// <summary>
        /// Random number generator for test data. Thread-safe via ThreadLocal.
        /// </summary>
        protected static readonly ThreadLocal<Random> Random = new(() => new Random(Guid.NewGuid().GetHashCode()));

        /// <summary>
        /// Initializes the context pool for the test class.
        /// Must be called from derived class's [ClassInitialize] method.
        /// </summary>
        /// <param name="context">Test context provided by MSTest.</param>
        /// <param name="poolSize">Number of contexts in the pool. Default is 10.</param>
        protected static void InitializeContextPool(TestContext context, int poolSize = 10)
        {
            ContextPool = new TestDbContextPool(poolSize);
        }

        /// <summary>
        /// Disposes the context pool for the test class.
        /// Must be called from derived class's [ClassCleanup] method.
        /// </summary>
        protected static void CleanupContextPool()
        {
            ContextPool?.Dispose();
            ContextPool = null;
        }

        /// <summary>
        /// Gets a context from the pool for use in tests.
        /// Thread-safe and supports parallel execution.
        /// </summary>
        /// <returns>ApplicationDbContext from the pool.</returns>
        /// <exception cref="InvalidOperationException">If context pool is not initialized.</exception>
        protected static ApplicationDbContext GetPooledContext()
        {
            if (ContextPool == null)
            {
                throw new InvalidOperationException(
                    "Context pool not initialized. Ensure your test class calls InitializeContextPool in [ClassInitialize].");
            }

            return ContextPool.GetContext();
        }

        /// <summary>
        /// Creates a new isolated context not from the pool.
        /// Use for tests requiring complete isolation or context configuration changes.
        /// </summary>
        /// <returns>New ApplicationDbContext with unique in-memory database.</returns>
        protected static ApplicationDbContext GetIsolatedContext()
        {
            return TestDbContextPool.CreateIsolatedContext();
        }

        /// <summary>
        /// Gets a random integer between min (inclusive) and max (exclusive).
        /// Thread-safe for parallel tests.
        /// </summary>
        /// <param name="min">Minimum value (inclusive).</param>
        /// <param name="max">Maximum value (exclusive).</param>
        /// <returns>Random integer.</returns>
        protected static int GetRandomInt(int min = 0, int max = int.MaxValue)
        {
            return Random.Value!.Next(min, max);
        }

        /// <summary>
        /// Gets a random boolean value.
        /// Thread-safe for parallel tests.
        /// </summary>
        /// <returns>Random boolean.</returns>
        protected static bool GetRandomBool()
        {
            return Random.Value!.Next(2) == 0;
        }

        /// <summary>
        /// Gets a random DateTime within the past year.
        /// Thread-safe for parallel tests.
        /// </summary>
        /// <returns>Random DateTime in UTC.</returns>
        protected static DateTime GetRandomPastDateTime()
        {
            var daysAgo = Random.Value!.Next(1, 365);
            return DateTime.UtcNow.AddDays(-daysAgo);
        }

        /// <summary>
        /// Gets a random future DateTime within the next year.
        /// Thread-safe for parallel tests.
        /// </summary>
        /// <returns>Random future DateTime in UTC.</returns>
        protected static DateTime GetRandomFutureDateTime()
        {
            var daysAhead = Random.Value!.Next(1, 365);
            return DateTime.UtcNow.AddDays(daysAhead);
        }
    }
}
