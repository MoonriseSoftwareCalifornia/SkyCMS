// <copyright file="GetDefaultLayoutQueryHandlerTests.cs" company="Moonrise Software, LLC">
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
    /// Tests for <see cref="GetDefaultLayoutQueryHandler"/>.
    /// Validates default layout retrieval with caching and published date filtering.
    /// </summary>
    [TestClass]
    public class GetDefaultLayoutQueryHandlerTests : CommonTestsBase
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
                var handler = new GetDefaultLayoutQueryHandler(null!);
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

            var handler = new GetDefaultLayoutQueryHandler(context);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public void Constructor_WithNullMemoryCache_ShouldSucceed()
        {
            using var context = GetIsolatedContext();

            var handler = new GetDefaultLayoutQueryHandler(context, null);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithNoDefaultLayout_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var handler = new GetDefaultLayoutQueryHandler(context);

            var result = await handler.HandleAsync(new GetDefaultLayoutQuery());

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithDefaultLayout_ShouldReturnLayout()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Default Layout",
                IsDefault = true,
                Published = DateTimeOffset.UtcNow.AddDays(-1)
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var handler = new GetDefaultLayoutQueryHandler(context);

            var result = await handler.HandleAsync(new GetDefaultLayoutQuery());

            Assert.IsNotNull(result);
            Assert.AreEqual(layout.Id, result.Id);
            Assert.AreEqual(layout.LayoutName, result.LayoutName);
        }

        [TestMethod]
        public async Task HandleAsync_WithFuturePublishedLayout_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Future Layout",
                IsDefault = true,
                Published = DateTimeOffset.UtcNow.AddDays(7)
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var handler = new GetDefaultLayoutQueryHandler(context);

            var result = await handler.HandleAsync(new GetDefaultLayoutQuery());

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithMultipleDefaultLayouts_ShouldReturnMostRecentPublished()
        {
            using var context = GetIsolatedContext();
            var oldLayout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Old Layout",
                IsDefault = true,
                Published = DateTimeOffset.UtcNow.AddDays(-10)
            };
            context.Layouts.Add(oldLayout);

            var newLayout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "New Layout",
                IsDefault = true,
                Published = DateTimeOffset.UtcNow.AddDays(-1)
            };
            context.Layouts.Add(newLayout);
            await context.SaveChangesAsync();

            var handler = new GetDefaultLayoutQueryHandler(context);

            var result = await handler.HandleAsync(new GetDefaultLayoutQuery());

            Assert.IsNotNull(result);
            Assert.AreEqual(newLayout.Id, result.Id);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonDefaultLayouts_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Non-Default Layout",
                IsDefault = false,
                Published = DateTimeOffset.UtcNow.AddDays(-1)
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var handler = new GetDefaultLayoutQueryHandler(context);

            var result = await handler.HandleAsync(new GetDefaultLayoutQuery());

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithCacheDuration_ShouldCacheResult()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Cached Layout",
                IsDefault = true,
                Published = DateTimeOffset.UtcNow.AddDays(-1)
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetDefaultLayoutQueryHandler(context, memoryCache);
            var query = new GetDefaultLayoutQuery { CacheDuration = TimeSpan.FromMinutes(10) };

            var result1 = await handler.HandleAsync(query);
            var result2 = await handler.HandleAsync(query);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(result1.Id, result2.Id);
        }

        [TestMethod]
        public async Task HandleAsync_WithoutCacheDuration_ShouldNotCache()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Uncached Layout",
                IsDefault = true,
                Published = DateTimeOffset.UtcNow.AddDays(-1)
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetDefaultLayoutQueryHandler(context, memoryCache);
            var query = new GetDefaultLayoutQuery();

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
                IsDefault = true,
                Published = DateTimeOffset.UtcNow.AddDays(-1)
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var handler = new GetDefaultLayoutQueryHandler(context, null);
            var query = new GetDefaultLayoutQuery { CacheDuration = TimeSpan.FromMinutes(10) };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(layout.Id, result.Id);
        }
    }
}
