// <copyright file="EditorContextBuilderArticleTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Tests.Services.EditorContext;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Cms.Api.Shared.Services.EditorContext;

/// <summary>
/// Unit tests for <see cref="EditorContextBuilder"/> article context methods.
/// </summary>
[TestClass]
public class EditorContextBuilderArticleTests
{
    private Mock<ApplicationDbContext> _mockDbContext;
    private Mock<IKnowledgeContextProvider> _mockKnowledgeProvider;
    private EditorContextBuilder _builder;

    /// <summary>
    /// Test initialization.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _mockDbContext = new Mock<ApplicationDbContext>();
        _mockKnowledgeProvider = new Mock<IKnowledgeContextProvider>();
        _builder = new EditorContextBuilder(_mockDbContext.Object, _mockKnowledgeProvider.Object);
    }

    /// <summary>
    /// Test that BuildArticleContextAsync throws KeyNotFoundException for non-existent article.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(KeyNotFoundException))]
    public async Task BuildArticleContextAsync_ArticleNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var articleNumber = 999;
        var emptyArticles = new List<Article>();

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(emptyArticles));

        // Act
        await _builder.BuildArticleContextAsync(articleNumber);

        // Assert - exception thrown by test attribute
    }

    /// <summary>
    /// Test that BuildArticleContextAsync returns proper ArticleEntityContext for valid article.
    /// </summary>
    [TestMethod]
    public async Task BuildArticleContextAsync_ValidArticle_ReturnsArticleEntityContext()
    {
        // Arrange
        var articleNumber = 42;
        var article = new Article
        {
            ArticleNumber = articleNumber,
            VersionNumber = 3,
            Title = "Test Article",
            UrlPath = "test-article",
            Content = "<h1>Test</h1><p>Content here</p>",
            HeaderJavaScript = "console.log('header');",
            FooterJavaScript = "console.log('footer');",
            StatusCode = 3, // Published
            Updated = new DateTimeOffset(2026, 6, 22, 14, 30, 0, TimeSpan.Zero),
            BannerImage = "https://example.com/banner.jpg",
            Category = "Technology",
            ArticleType = 1, // Article
        };

        var articles = new List<Article> { article };
        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(articles));

        // Act
        var result = await _builder.BuildArticleContextAsync(articleNumber);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(articleNumber, result.ArticleNumber);
        Assert.AreEqual("Test Article", result.Title);
        Assert.AreEqual("test-article", result.UrlPath);
        Assert.AreEqual("published", result.Status);
        Assert.AreEqual(3, result.Version);
        Assert.IsNotNull(result.BannerImage);
        Assert.AreEqual("https://example.com/banner.jpg", result.BannerImage.Url);
    }

    /// <summary>
    /// Test that BuildArticleContextAsync truncates large content fields.
    /// </summary>
    [TestMethod]
    public async Task BuildArticleContextAsync_LargeContent_Truncates()
    {
        // Arrange
        var articleNumber = 42;
        var largeContent = new string('x', 60_000); // Larger than 50KB limit

        var article = new Article
        {
            ArticleNumber = articleNumber,
            VersionNumber = 1,
            Title = "Large Article",
            UrlPath = "large-article",
            Content = largeContent,
            StatusCode = 3,
            Updated = DateTimeOffset.UtcNow,
        };

        var articles = new List<Article> { article };
        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(articles));

        // Act
        var result = await _builder.BuildArticleContextAsync(articleNumber);

        // Assert
        Assert.IsNotNull(result.Content);
        Assert.IsTrue(result.Content.Length < largeContent.Length);
        Assert.IsTrue(result.Content.Contains("... (truncated)"));
    }

    /// <summary>
    /// Test that BuildArticleContextAsync handles articles with null optional fields.
    /// </summary>
    [TestMethod]
    public async Task BuildArticleContextAsync_NullOptionalFields_HandlesGracefully()
    {
        // Arrange
        var articleNumber = 42;
        var article = new Article
        {
            ArticleNumber = articleNumber,
            VersionNumber = 1,
            Title = "Minimal Article",
            UrlPath = "minimal",
            Content = "Some content",
            StatusCode = 1, // Draft
            Updated = DateTimeOffset.UtcNow,
            HeaderJavaScript = null,
            FooterJavaScript = null,
            BannerImage = null,
            TemplateId = null,
            ArticleType = null,
        };

        var articles = new List<Article> { article };
        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(articles));

        // Act
        var result = await _builder.BuildArticleContextAsync(articleNumber);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNull(result.HeaderJavaScript);
        Assert.IsNull(result.FooterJavaScript);
        Assert.IsNull(result.BannerImage);
        Assert.IsNull(result.TemplateId);
        Assert.IsNull(result.ArticleType);
        Assert.AreEqual("draft", result.Status);
    }

    /// <summary>
    /// Test that BuildArticleContextAsync returns latest version when multiple versions exist.
    /// </summary>
    [TestMethod]
    public async Task BuildArticleContextAsync_MultipleVersions_ReturnsLatest()
    {
        // Arrange
        var articleNumber = 42;
        var articles = new List<Article>
        {
            new()
            {
                ArticleNumber = articleNumber,
                VersionNumber = 1,
                Title = "Version 1",
                UrlPath = "article-v1",
                Content = "Old content",
                StatusCode = 3,
                Updated = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new()
            {
                ArticleNumber = articleNumber,
                VersionNumber = 3,
                Title = "Version 3 (Latest)",
                UrlPath = "article-v3",
                Content = "New content",
                StatusCode = 1,
                Updated = new DateTimeOffset(2026, 6, 22, 14, 30, 0, TimeSpan.Zero),
            },
            new()
            {
                ArticleNumber = articleNumber,
                VersionNumber = 2,
                Title = "Version 2",
                UrlPath = "article-v2",
                Content = "Middle content",
                StatusCode = 3,
                Updated = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero),
            },
        };

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(articles));

        // Act
        var result = await _builder.BuildArticleContextAsync(articleNumber);

        // Assert
        Assert.AreEqual("Version 3 (Latest)", result.Title);
        Assert.AreEqual("article-v3", result.UrlPath);
        Assert.AreEqual(3, result.Version);
        Assert.AreEqual("draft", result.Status);
    }

    /// <summary>
    /// Test that BuildArticleContextAsync correctly maps status codes.
    /// </summary>
    [TestMethod]
    [DataRow(1, "draft")]
    [DataRow(2, "review")]
    [DataRow(3, "published")]
    [DataRow(4, "archived")]
    [DataRow(5, "redirect")]
    [DataRow(99, "unknown")]
    public async Task BuildArticleContextAsync_StatusCodeMapping_Correct(int statusCode, string expectedStatus)
    {
        // Arrange
        var articleNumber = 42;
        var article = new Article
        {
            ArticleNumber = articleNumber,
            VersionNumber = 1,
            Title = "Status Test",
            UrlPath = "status-test",
            Content = "Test",
            StatusCode = statusCode,
            Updated = DateTimeOffset.UtcNow,
        };

        var articles = new List<Article> { article };
        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(articles));

        // Act
        var result = await _builder.BuildArticleContextAsync(articleNumber);

        // Assert
        Assert.AreEqual(expectedStatus, result.Status);
    }

    /// <summary>
    /// Test that timestamp is correctly formatted as ISO 8601.
    /// </summary>
    [TestMethod]
    public async Task BuildArticleContextAsync_Timestamp_FormattedAsISO8601()
    {
        // Arrange
        var articleNumber = 42;
        var utcTimestamp = new DateTimeOffset(2026, 6, 22, 14, 30, 45, 500, TimeSpan.Zero);
        var article = new Article
        {
            ArticleNumber = articleNumber,
            VersionNumber = 1,
            Title = "Timestamp Test",
            UrlPath = "timestamp-test",
            Content = "Test",
            StatusCode = 3,
            Updated = utcTimestamp,
        };

        var articles = new List<Article> { article };
        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(articles));

        // Act
        var result = await _builder.BuildArticleContextAsync(articleNumber);

        // Assert
        Assert.AreEqual(utcTimestamp.ToString("O"), result.LastModified);
        Assert.IsTrue(result.LastModified.Contains("2026-06-22"));
        Assert.IsTrue(result.LastModified.Contains("14:30:45"));
    }

    /// <summary>
    /// Test that published date is null when article is not published.
    /// </summary>
    [TestMethod]
    public async Task BuildArticleContextAsync_UnpublishedArticle_PublishedDateNull()
    {
        // Arrange
        var articleNumber = 42;
        var article = new Article
        {
            ArticleNumber = articleNumber,
            VersionNumber = 1,
            Title = "Unpublished",
            UrlPath = "unpublished",
            Content = "Test",
            StatusCode = 1, // Draft
            Updated = DateTimeOffset.UtcNow,
            Published = null,
        };

        var articles = new List<Article> { article };
        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(articles));

        // Act
        var result = await _builder.BuildArticleContextAsync(articleNumber);

        // Assert
        Assert.IsNull(result.PublishedDate);
    }

    /// <summary>
    /// Creates a mock IQueryable DbSet for testing.
    /// </summary>
    private static IQueryable<Article> MockDbSet(List<Article> articles)
    {
        return articles.AsQueryable();
    }
}
