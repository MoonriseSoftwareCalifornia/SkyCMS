// <copyright file="CosmosLinqExtensionsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Data
{
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="CosmosLinqExtensions"/>.
    /// </summary>
    [TestClass]
    public class CosmosLinqExtensionsTests : CommonTestsBase
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            ContextPool = new TestDbContextPool();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ContextPool?.Dispose();
        }

        [TestMethod]
        public async Task CosmosAnyAsync_WithMatchingRows_ShouldReturnTrue()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var query = context.Articles.Where(a => a.Id == article.Id);
            var result = await query.CosmosAnyAsync();

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task CosmosAnyAsync_WithNoRows_ShouldReturnFalse()
        {
            using var context = GetIsolatedContext();

            var query = context.Articles.Where(a => a.ArticleNumber == -999);
            var result = await query.CosmosAnyAsync();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task CosmosAnyAsync_WithNullQuery_ShouldReturnFalse()
        {
            IQueryable<Article>? query = null;

            var result = await query!.CosmosAnyAsync();

            Assert.IsFalse(result);
        }
    }
}
