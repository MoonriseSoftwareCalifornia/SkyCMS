// <copyright file="CheckDefaultLayoutExistsQueryHandlerTests.cs" company="Moonrise Software, LLC">
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
    /// Tests for <see cref="CheckDefaultLayoutExistsQueryHandler"/>.
    /// Validates default layout existence checking with optional caching.
    /// </summary>
    [TestClass]
    public class CheckDefaultLayoutExistsQueryHandlerTests : CommonTestsBase
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
        public void Constructor_WithValidDependencies_ShouldSucceed()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var handler = new CheckDefaultLayoutExistsQueryHandler(context, memoryCache);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithNullQuery_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var handler = new CheckDefaultLayoutExistsQueryHandler(context);

            try
            {
                await handler.HandleAsync(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected exception - test passes
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithNoLayouts_ShouldReturnFalse()
        {
            using var context = GetIsolatedContext();
            var handler = new CheckDefaultLayoutExistsQueryHandler(context);
            var query = new CheckDefaultLayoutExistsQuery();

            var result = await handler.HandleAsync(query);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithDefaultLayoutPublished_ShouldReturnTrue()
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

            var handler = new CheckDefaultLayoutExistsQueryHandler(context);
            var query = new CheckDefaultLayoutExistsQuery();

            var result = await handler.HandleAsync(query);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithUnpublishedDefaultLayout_ShouldReturnFalse()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Default Layout",
                IsDefault = true,
                Published = DateTimeOffset.UtcNow.AddDays(1) // Future date
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var handler = new CheckDefaultLayoutExistsQueryHandler(context);
            var query = new CheckDefaultLayoutExistsQuery();

            var result = await handler.HandleAsync(query);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithPublishedLayoutButIsDefaultFalse_ShouldSelfHealAndReturnTrue()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Published Layout",
                IsDefault = false,
                Published = DateTimeOffset.UtcNow.AddDays(-1)
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var handler = new CheckDefaultLayoutExistsQueryHandler(context);
            var query = new CheckDefaultLayoutExistsQuery();

            var result = await handler.HandleAsync(query);

            Assert.IsTrue(result);

            // Verify self-healing: IsDefault should now be true in the database
            var healed = await context.Layouts.FindAsync(layout.Id);
            Assert.IsTrue(healed.IsDefault, "Self-healing should set IsDefault to true");
        }

        [TestMethod]
        public async Task HandleAsync_WithUnpublishedLayout_ShouldReturnFalse()
        {
            using var context = GetIsolatedContext();
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Draft Layout",
                IsDefault = false,
                Published = null
            };
            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            var handler = new CheckDefaultLayoutExistsQueryHandler(context);
            var query = new CheckDefaultLayoutExistsQuery();

            var result = await handler.HandleAsync(query);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithMultipleDefaultLayouts_ShouldReturnTrue()
        {
            using var context = GetIsolatedContext();
            var layout1 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Default Layout 1",
                IsDefault = true,
                Published = DateTimeOffset.UtcNow.AddDays(-5)
            };
            var layout2 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Default Layout 2",
                IsDefault = true,
                Published = DateTimeOffset.UtcNow.AddDays(-2)
            };
            context.Layouts.Add(layout1);
            context.Layouts.Add(layout2);
            await context.SaveChangesAsync();

            var handler = new CheckDefaultLayoutExistsQueryHandler(context);
            var query = new CheckDefaultLayoutExistsQuery();

            var result = await handler.HandleAsync(query);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithCaching_ShouldCacheResult()
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

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new CheckDefaultLayoutExistsQueryHandler(context, memoryCache);
            var query = new CheckDefaultLayoutExistsQuery 
            { 
                CacheDuration = TimeSpan.FromMinutes(5)
            };

            // First call - cache miss
            var result1 = await handler.HandleAsync(query);

            // Remove the layout
            context.Layouts.Remove(layout);
            await context.SaveChangesAsync();

            // Second call - should return cached result (true)
            var result2 = await handler.HandleAsync(query);

            Assert.IsTrue(result1);
            Assert.IsTrue(result2); // Still true from cache
        }

        [TestMethod]
        public async Task HandleAsync_WithoutCaching_ShouldAlwaysFetchFresh()
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

            var handler = new CheckDefaultLayoutExistsQueryHandler(context);
            var query = new CheckDefaultLayoutExistsQuery(); // No cache duration

            // First call
            var result1 = await handler.HandleAsync(query);

            // Remove the layout
            context.Layouts.Remove(layout);
            await context.SaveChangesAsync();

            // Second call - should fetch fresh data
            var result2 = await handler.HandleAsync(query);

            Assert.IsTrue(result1);
            Assert.IsFalse(result2); // Fresh data, layout removed
        }
    }
}
