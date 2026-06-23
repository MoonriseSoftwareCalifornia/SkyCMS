// <copyright file="EditorContextBuilderRenderingTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.EditorContext;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Cms.Api.Shared.Services.EditorContext;

/// <summary>
/// Unit tests for <see cref="EditorContextBuilder"/> rendering context methods.
/// </summary>
[TestClass]
public class EditorContextBuilderRenderingTests
{
    private TestApplicationDbContext _mockDbContext;
    private Mock<IKnowledgeContextProvider> _mockKnowledgeProvider;
    private EditorContextBuilder _builder;

    /// <summary>
    /// Test initialization.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _mockDbContext = new TestApplicationDbContext();
        _mockKnowledgeProvider = new Mock<IKnowledgeContextProvider>();
        _builder = new EditorContextBuilder(_mockDbContext, _mockKnowledgeProvider.Object);
    }

    /// <summary>
    /// Test that BuildRenderingContextAsync throws KeyNotFoundException for non-existent article.
    /// </summary>
    [TestMethod]
    public async Task BuildRenderingContextAsync_ArticleNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var articleNumber = 9999;
        var emptyArticles = new List<Article>();

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(emptyArticles));

        // Act + Assert
        try
        {
            await _builder.BuildRenderingContextAsync(articleNumber);
            Assert.Fail("Expected KeyNotFoundException was not thrown.");
        }
        catch (KeyNotFoundException)
        {
            // Expected
        }
    }

    /// <summary>
    /// Test that BuildRenderingContextAsync returns context for article without template.
    /// </summary>
    [TestMethod]
    public async Task BuildRenderingContextAsync_ArticleWithoutTemplate_ReturnsDirectLayoutFlow()
    {
        // Arrange
        var layoutGuid = Guid.NewGuid();
        var article = new Article
        {
            ArticleNumber = 1,
            VersionNumber = 1,
            Title = "Test Article",
            UrlPath = "/test",
            Content = "<p>Article content</p>",
            TemplateId = null, // No template
        };

        var layout = new Layout
        {
            Id = layoutGuid,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Main Layout",
            HtmlHeader = "<!--CCMS--START--HEADER--><nav>Header</nav><!--CCMS--END--HEADER-->",
            Head = string.Empty,
            FooterHtmlContent = "<!--CCMS--START--FOOTER--><footer>Footer</footer><!--CCMS--END--FOOTER-->",
        };

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(new List<Article> { article }));

        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(new List<Layout> { layout }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion>()));

        // Act
        var result = await _builder.BuildRenderingContextAsync(1);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.RenderingFlow.Contains("Article #1"));
        Assert.IsTrue(result.RenderingFlow.Contains("Layout (direct)"));
        Assert.AreEqual("content", result.ContentInsertion.Field);
        Assert.AreEqual("layout-region", result.ContentInsertion.Destination);
        Assert.IsNotNull(result.Placeholders);
        Assert.IsTrue(result.Placeholders.Count >= 2); // At least header and footer regions
    }

    /// <summary>
    /// Test that BuildRenderingContextAsync returns context for article with template.
    /// </summary>
    [TestMethod]
    public async Task BuildRenderingContextAsync_ArticleWithTemplate_ReturnsTemplateToLayoutFlow()
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var layoutGuid = Guid.NewGuid();

        var article = new Article
        {
            ArticleNumber = 2,
            VersionNumber = 1,
            Title = "Blog Post",
            UrlPath = "/blog/post",
            Content = "<p>Blog content</p>",
            TemplateId = templateGuid,
        };

        var template = new Template
        {
            Id = templateGuid,
            Title = "Blog Template",
            PageType = "content",
        };

        var templateVersion = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 1,
            Title = "Blog Template v1",
            Content = @"<div data-ccms-ceid='intro'>Introduction</div>
                       <div data-ccms-ceid='body'>Body Content</div>",
            PageType = "content",
            Published = new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero),
        };

        var layout = new Layout
        {
            Id = layoutGuid,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Main Layout",
            HtmlHeader = "<!--CCMS--START--HEADER--><nav>Header</nav><!--CCMS--END--HEADER-->",
            Head = string.Empty,
            FooterHtmlContent = "<!--CCMS--START--FOOTER--><footer>Footer</footer><!--CCMS--END--FOOTER-->",
        };

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(new List<Article> { article }));

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { templateVersion }));

        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(new List<Layout> { layout }));

        // Act
        var result = await _builder.BuildRenderingContextAsync(2);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.RenderingFlow.Contains("Article #2"));
        Assert.IsTrue(result.RenderingFlow.Contains("Template 'Blog Template'"));
        Assert.IsTrue(result.RenderingFlow.Contains("Layout"));
        Assert.AreEqual("template-region", result.ContentInsertion.Destination);
        Assert.IsNotNull(result.Placeholders);
        Assert.IsTrue(result.Placeholders.Count >= 4); // Template fields + layout regions
    }

    /// <summary>
    /// Test that BuildRenderingContextAsync extracts placeholder mappings correctly.
    /// </summary>
    [TestMethod]
    public async Task BuildRenderingContextAsync_PlaceholderMappings_ExtractsCorrectly()
    {
        // Arrange
        var layoutGuid = Guid.NewGuid();

        var article = new Article
        {
            ArticleNumber = 3,
            VersionNumber = 1,
            Title = "Test",
            UrlPath = "/test",
            Content = "Content",
            TemplateId = null,
        };

        var layout = new Layout
        {
            Id = layoutGuid,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Layout",
            HtmlHeader = "<!--CCMS--START--HEADER--><nav>H</nav><!--CCMS--END--HEADER-->",
            Head = string.Empty,
            FooterHtmlContent = "<!--CCMS--START--FOOTER--><footer>F</footer><!--CCMS--END--FOOTER-->",
        };

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(new List<Article> { article }));

        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(new List<Layout> { layout }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion>()));

        // Act
        var result = await _builder.BuildRenderingContextAsync(3);

        // Assert
        Assert.IsNotNull(result.Placeholders);
        var headerPlaceholder = result.Placeholders.Find(p => p.Field == "HEADER");
        Assert.IsNotNull(headerPlaceholder);
        Assert.AreEqual("layout", headerPlaceholder.Source);
        Assert.IsTrue(headerPlaceholder.Required);

        var footerPlaceholder = result.Placeholders.Find(p => p.Field == "FOOTER");
        Assert.IsNotNull(footerPlaceholder);
        Assert.AreEqual("layout", footerPlaceholder.Source);
    }

    /// <summary>
    /// Test that BuildRenderingContextAsync includes article's own scripts.
    /// </summary>
    [TestMethod]
    public async Task BuildRenderingContextAsync_ArticleScripts_IncludedInScriptOrder()
    {
        // Arrange
        var layoutGuid = Guid.NewGuid();

        var article = new Article
        {
            ArticleNumber = 4,
            VersionNumber = 1,
            Title = "Scripted Article",
            UrlPath = "/scripted",
            Content = "Content",
            TemplateId = null,
            HeaderJavaScript = "console.log('header');",
            FooterJavaScript = "console.log('footer');",
        };

        var layout = new Layout
        {
            Id = layoutGuid,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Layout",
            HtmlHeader = string.Empty,
            Head = "<script src='/js/main.js'></script>",
            FooterHtmlContent = string.Empty,
        };

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(new List<Article> { article }));

        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(new List<Layout> { layout }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion>()));

        // Act
        var result = await _builder.BuildRenderingContextAsync(4);

        // Assert
        Assert.IsNotNull(result.ScriptLoadingOrder);
        Assert.IsTrue(result.ScriptLoadingOrder.Count >= 3); // Layout script + header + footer

        var headerScript = result.ScriptLoadingOrder.Find(s => s.Source == "article-header");
        Assert.IsNotNull(headerScript);
        Assert.AreEqual("head", headerScript.Location);

        var footerScript = result.ScriptLoadingOrder.Find(s => s.Source == "article-footer");
        Assert.IsNotNull(footerScript);
        Assert.AreEqual("body-end", footerScript.Location);
    }

    /// <summary>
    /// Test that BuildRenderingContextAsync handles missing template reference.
    /// </summary>
    [TestMethod]
    public async Task BuildRenderingContextAsync_MissingTemplate_IncludesNoteAndFlow()
    {
        // Arrange
        var layoutGuid = Guid.NewGuid();
        var missingTemplateId = Guid.NewGuid();

        var article = new Article
        {
            ArticleNumber = 5,
            VersionNumber = 1,
            Title = "Article with Missing Template",
            UrlPath = "/missing",
            Content = "Content",
            TemplateId = missingTemplateId,
        };

        var layout = new Layout
        {
            Id = layoutGuid,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Layout",
            HtmlHeader = string.Empty,
            Head = string.Empty,
            FooterHtmlContent = string.Empty,
        };

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(new List<Article> { article }));

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template>())); // Template not found

        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(new List<Layout> { layout }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion>()));

        // Act
        var result = await _builder.BuildRenderingContextAsync(5);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.RenderingFlow.Contains("Missing Template"));
        Assert.IsNotNull(result.Notes);
        Assert.IsTrue(result.Notes.Any(n => n.Contains("non-existent")));
    }

    /// <summary>
    /// Test that BuildRenderingContextAsync includes rendering notes.
    /// </summary>
    [TestMethod]
    public async Task BuildRenderingContextAsync_RenderingNotes_IncludesLayoutAndTemplateInfo()
    {
        // Arrange
        var layoutGuid = Guid.NewGuid();
        var templateGuid = Guid.NewGuid();

        var article = new Article
        {
            ArticleNumber = 6,
            VersionNumber = 1,
            Title = "Article",
            UrlPath = "/test",
            Content = "Content",
            TemplateId = templateGuid,
        };

        var template = new Template
        {
            Id = templateGuid,
            Title = "Home Template",
            PageType = "home",
        };

        var templateVersion = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 2,
            Title = "Home Template v2",
            Content = "<div data-ccms-ceid='hero'>Hero</div>",
            PageType = "home",
            Published = null, // Draft version
        };

        var layout = new Layout
        {
            Id = layoutGuid,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Corporate Layout",
            HtmlHeader = string.Empty,
            Head = string.Empty,
            FooterHtmlContent = string.Empty,
        };

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(new List<Article> { article }));

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { templateVersion }));

        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(new List<Layout> { layout }));

        // Act
        var result = await _builder.BuildRenderingContextAsync(6);

        // Assert
        Assert.IsNotNull(result.Notes);
        Assert.IsTrue(result.Notes.Any(n => n.Contains("Home Template")));
        Assert.IsTrue(result.Notes.Any(n => n.Contains("v2")));
        Assert.IsTrue(result.Notes.Any(n => n.Contains("Corporate Layout")));
    }

    /// <summary>
    /// Test that BuildRenderingContextAsync handles null layout gracefully.
    /// </summary>
    [TestMethod]
    public async Task BuildRenderingContextAsync_NullLayout_HandlesGracefully()
    {
        // Arrange
        var article = new Article
        {
            ArticleNumber = 7,
            VersionNumber = 1,
            Title = "Article without Layout",
            UrlPath = "/test",
            Content = "Content",
            TemplateId = null,
        };

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(new List<Article> { article }));

        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(new List<Layout>()));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion>()));

        // Act
        var result = await _builder.BuildRenderingContextAsync(7);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.RenderingFlow.Contains("Article #7"));
        Assert.IsNotNull(result.ContentInsertion);
    }

    /// <summary>
    /// Test that BuildRenderingContextAsync returns different composition types.
    /// </summary>
    [TestMethod]
    public async Task BuildRenderingContextAsync_MultipleArticles_TracesIndependentFlows()
    {
        // Arrange - Create two articles with different configurations
        var layoutGuid1 = Guid.NewGuid();
        var layoutGuid2 = Guid.NewGuid();

        var article1 = new Article
        {
            ArticleNumber = 10,
            VersionNumber = 1,
            Title = "Article 1",
            UrlPath = "/article1",
            Content = "Content",
            TemplateId = null,
        };

        var article2 = new Article
        {
            ArticleNumber = 11,
            VersionNumber = 1,
            Title = "Article 2",
            UrlPath = "/article2",
            Content = "Content",
            TemplateId = null,
        };

        var layout1 = new Layout
        {
            Id = layoutGuid1,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Layout A",
            HtmlHeader = string.Empty,
            Head = string.Empty,
            FooterHtmlContent = string.Empty,
        };

        var layout2 = new Layout
        {
            Id = layoutGuid2,
            LayoutNumber = 2,
            Version = 1,
            LayoutName = "Layout B",
            HtmlHeader = string.Empty,
            Head = string.Empty,
            FooterHtmlContent = string.Empty,
        };

        _mockDbContext
            .Setup(db => db.Articles)
            .Returns(MockDbSet(new List<Article> { article1, article2 }));

        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(new List<Layout> { layout1, layout2 }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion>()));

        // Act
        var result1 = await _builder.BuildRenderingContextAsync(10);
        var result2 = await _builder.BuildRenderingContextAsync(11);

        // Assert
        Assert.IsNotNull(result1);
        Assert.IsNotNull(result2);
        Assert.IsTrue(result1.RenderingFlow.Contains("Article #10"));
        Assert.IsTrue(result2.RenderingFlow.Contains("Article #11"));
        Assert.IsFalse(result1.RenderingFlow.Equals(result2.RenderingFlow)); // Different flows
    }

    /// <summary>
    /// Creates a mock DbSet for testing with async support.
    /// </summary>
    private static DbSet<T> MockDbSet<T>(List<T> items) where T : class
    {
        var queryable = items.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();
        mockSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));
        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Expression)
            .Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>()
            .Setup(m => m.ElementType)
            .Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>()
            .Setup(m => m.GetEnumerator())
            .Returns(queryable.GetEnumerator());
        return mockSet.Object;
    }
}
