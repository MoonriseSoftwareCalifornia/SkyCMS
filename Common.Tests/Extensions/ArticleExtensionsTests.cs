// <copyright file="ArticleExtensionsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Extensions
{
    using Cosmos.Cms.Common;
    using Cosmos.Cms.Common.Extensions;
    using Cosmos.Cms.Common.Models;
    using Cosmos.Common.Data;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Text.Json;

    /// <summary>
    /// Tests for ArticleExtensions.
    /// Demonstrates using pooled contexts and test data builders for parallel-safe testing.
    /// </summary>
    [TestClass]
    public class ArticleExtensionsTests : CommonTestsBase
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            InitializeContextPool(context);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CleanupContextPool();
        }

        [TestMethod]
        public void IsSpaArticle_WithSpaArticleType_ReturnsTrue()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.SpaApp);

            // Act
            var result = article.IsSpaArticle();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsSpaArticle_WithNonSpaArticleType_ReturnsFalse()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.General);

            // Act
            var result = article.IsSpaArticle();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsSpaArticle_WithBlogPostArticleType_ReturnsFalse()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.BlogPost);

            // Act
            var result = article.IsSpaArticle();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void GetSpaMetadata_WithValidSpaArticle_ReturnsMetadata()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.SpaApp);
            var metadata = new SpaMetadata
            {
                DeploymentKeyHash = "test-hash",
                DeploymentKeyRotatedAt = DateTimeOffset.UtcNow
            };
            article.Content = JsonSerializer.Serialize(metadata);

            // Act
            var result = article.GetSpaMetadata();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("test-hash", result.DeploymentKeyHash);
        }

        [TestMethod]
        public void GetSpaMetadata_WithNullContent_ReturnsEmptyMetadata()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.SpaApp);
            article.Content = null;

            // Act
            var result = article.GetSpaMetadata();

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void GetSpaMetadata_WithNonSpaArticle_ThrowsInvalidOperationException()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.General);

            // Act & Assert
            try
            {
                article.GetSpaMetadata();
                Assert.Fail("Expected InvalidOperationException was not thrown.");
            }
            catch (InvalidOperationException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        public void GetSpaMetadata_WithInvalidJson_ThrowsJsonException()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.SpaApp);
            article.Content = "{ invalid json }";

            // Act & Assert
            try
            {
                article.GetSpaMetadata();
                Assert.Fail("Expected JsonException was not thrown.");
            }
            catch (JsonException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        public void SetSpaMetadata_WithValidArticle_SerializesMetadata()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.SpaApp);
            var metadata = new SpaMetadata
            {
                DeploymentKeyHash = "my-deployment-hash",
                DeploymentKeyRotatedAt = DateTimeOffset.UtcNow
            };

            // Act
            article.SetSpaMetadata(metadata);

            // Assert
            Assert.IsNotNull(article.Content);
            Assert.IsTrue(article.Content.Contains("my-deployment-hash"));

            // Verify it can be deserialized back
            var deserializedMetadata = JsonSerializer.Deserialize<SpaMetadata>(article.Content);
            Assert.IsNotNull(deserializedMetadata);
            Assert.AreEqual("my-deployment-hash", deserializedMetadata.DeploymentKeyHash);
        }

        [TestMethod]
        public void SetSpaMetadata_WithNonSpaArticle_ThrowsInvalidOperationException()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.General);
            var metadata = new SpaMetadata();

            // Act & Assert
            try
            {
                article.SetSpaMetadata(metadata);
                Assert.Fail("Expected InvalidOperationException was not thrown.");
            }
            catch (InvalidOperationException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        public void TryGetSpaMetadata_WithValidSpaArticle_ReturnsTrue()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.SpaApp);
            var originalMetadata = new SpaMetadata { DeploymentKeyHash = "test-hash" };
            article.Content = JsonSerializer.Serialize(originalMetadata);

            // Act
            var success = article.TryGetSpaMetadata(out var metadata);

            // Assert
            Assert.IsTrue(success);
            Assert.IsNotNull(metadata);
            Assert.AreEqual("test-hash", metadata.DeploymentKeyHash);
        }

        [TestMethod]
        public void TryGetSpaMetadata_WithNonSpaArticle_ReturnsFalse()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.General);

            // Act
            var success = article.TryGetSpaMetadata(out var metadata);

            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(metadata);
        }

        [TestMethod]
        public void TryGetSpaMetadata_WithInvalidJson_ReturnsFalse()
        {
            // Arrange
            var article = TestDataBuilder.CreateArticle(articleType: ArticleType.SpaApp);
            article.Content = "{ invalid }";

            // Act
            var success = article.TryGetSpaMetadata(out var metadata);

            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(metadata);
        }
    }
}
