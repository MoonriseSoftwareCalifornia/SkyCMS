// <copyright file="GetLayoutByIdQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Layouts.Queries
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Layouts.Queries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="GetLayoutByIdQueryHandler"/>.
    /// Validates layout retrieval by ID with optional caching.
    /// </summary>
    [TestClass]
    public class GetLayoutByIdQueryHandlerTests : CommonTestsBase
    {
        /// <summary>
        /// Initializes the shared test infrastructure for this test class.
        /// </summary>
        /// <param name="context">Test context provided by MSTest.</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            ContextPool = new TestDbContextPool();
        }

        /// <summary>
        /// Cleans up the shared test infrastructure after all tests complete.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            ContextPool?.Dispose();
        }

        [TestMethod]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            try
            {
                var handler = new GetLayoutByIdQueryHandler(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public void Constructor_WithValidDbContext_ShouldSucceed()
        {
            using var context = GetIsolatedContext();

            var handler = new GetLayoutByIdQueryHandler(context);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithNullQuery_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var handler = new GetLayoutByIdQueryHandler(context);

            try
            {
                await handler.HandleAsync(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("query", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithEmptyGuid_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var handler = new GetLayoutByIdQueryHandler(context);

            var result = await handler.HandleAsync(new GetLayoutByIdQuery(Guid.Empty));

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentId_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var handler = new GetLayoutByIdQueryHandler(context);

            var result = await handler.HandleAsync(new GetLayoutByIdQuery(Guid.NewGuid()));

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithValidId_ShouldReturnLayout()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = false
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var handler = new GetLayoutByIdQueryHandler(context);

            var result = await handler.HandleAsync(new GetLayoutByIdQuery(layout.Id));

            Assert.IsNotNull(result);
            Assert.AreEqual(layout.Id, result.Id);
            Assert.AreEqual(layout.LayoutName, result.LayoutName);
        }

        [TestMethod]
        public async Task HandleAsync_WithCacheDuration_ShouldCacheResult()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Cached Layout",
                IsDefault = false
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetLayoutByIdQueryHandler(context, memoryCache);
            var query = new GetLayoutByIdQuery(layout.Id)
            {
                CacheDuration = TimeSpan.FromMinutes(10)
            };

            var result1 = await handler.HandleAsync(query);
            var result2 = await handler.HandleAsync(query);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(result1.Id, result2.Id);
        }

        [TestMethod]
        public async Task HandleAsync_WithCacheDuration_ShouldCacheNullResults()
        {
            using var context = GetIsolatedContext();
            var nonExistentId = Guid.NewGuid();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetLayoutByIdQueryHandler(context, memoryCache);
            var query = new GetLayoutByIdQuery(nonExistentId)
            {
                CacheDuration = TimeSpan.FromMinutes(10)
            };

            var result1 = await handler.HandleAsync(query);
            var result2 = await handler.HandleAsync(query);

            Assert.IsNull(result1);
            Assert.IsNull(result2);
        }

        [TestMethod]
        public async Task HandleAsync_WithoutCacheDuration_ShouldNotCache()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Uncached Layout",
                IsDefault = false
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetLayoutByIdQueryHandler(context, memoryCache);
            var query = new GetLayoutByIdQuery(layout.Id);

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithNullMemoryCache_ShouldStillWork()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "No Cache Layout",
                IsDefault = false
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var handler = new GetLayoutByIdQueryHandler(context, null);
            var query = new GetLayoutByIdQuery(layout.Id)
            {
                CacheDuration = TimeSpan.FromMinutes(10)
            };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(layout.Id, result.Id);
        }
    }
}
