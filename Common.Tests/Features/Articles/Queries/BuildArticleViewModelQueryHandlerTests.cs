// <copyright file="BuildArticleViewModelQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Queries
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="BuildArticleViewModelQueryHandler"/>.
    /// Validates delegation to <see cref="IArticleViewModelBuilder"/>.
    /// </summary>
    [TestClass]
    public class BuildArticleViewModelQueryHandlerTests : CommonTestsBase
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
        public void Constructor_WithValidDependencies_ShouldSucceed()
        {
            var mockBuilder = new Mock<IArticleViewModelBuilder>();

            var handler = new BuildArticleViewModelQueryHandler(mockBuilder.Object);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public void Constructor_WithNullViewModelBuilder_ShouldThrowArgumentNullException()
        {
            try
            {
                _ = new BuildArticleViewModelQueryHandler(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("viewModelBuilder", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithNullQuery_ShouldThrowArgumentNullException()
        {
            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            var handler = new BuildArticleViewModelQueryHandler(mockBuilder.Object);

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
        public async Task HandleAsync_WithDefaultParameters_ShouldCallBuilderWithDefaults()
        {
            var article = TestDataBuilder.CreateArticle();
            var expected = new ArticleViewModel { Title = "Built" };

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            mockBuilder
                .Setup(b => b.BuildFromArticleAsync(article, "en", true))
                .ReturnsAsync(expected);

            var handler = new BuildArticleViewModelQueryHandler(mockBuilder.Object);
            var query = new BuildArticleViewModelQuery(article);

            var result = await handler.HandleAsync(query);

            Assert.AreEqual(expected, result);
            mockBuilder.Verify(b => b.BuildFromArticleAsync(article, "en", true), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithExplicitParameters_ShouldPassAllToBuilder()
        {
            var article = TestDataBuilder.CreateArticle();
            var expected = new ArticleViewModel { Title = "Built Explicit" };

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            mockBuilder
                .Setup(b => b.BuildFromArticleAsync(article, "fr-FR", false))
                .ReturnsAsync(expected);

            var handler = new BuildArticleViewModelQueryHandler(mockBuilder.Object);
            var query = new BuildArticleViewModelQuery(article, "fr-FR", false);

            var result = await handler.HandleAsync(query);

            Assert.AreEqual(expected, result);
            mockBuilder.Verify(b => b.BuildFromArticleAsync(article, "fr-FR", false), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_ShouldReturnBuilderResult()
        {
            var article = TestDataBuilder.CreateArticle();
            var expected = new ArticleViewModel
            {
                Title = "Delegated Result",
                ArticleNumber = 1234,
                LanguageCode = "es-ES"
            };

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            mockBuilder
                .Setup(b => b.BuildFromArticleAsync(It.IsAny<Article>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(expected);

            var handler = new BuildArticleViewModelQueryHandler(mockBuilder.Object);
            var query = new BuildArticleViewModelQuery(article, "es-ES", true);

            var result = await handler.HandleAsync(query);

            Assert.AreEqual("Delegated Result", result.Title);
            Assert.AreEqual(1234, result.ArticleNumber);
            Assert.AreEqual("es-ES", result.LanguageCode);
        }
    }
}
