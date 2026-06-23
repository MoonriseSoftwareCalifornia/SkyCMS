// <copyright file="EditorContextBuilderLayoutTests.cs" company="Moonrise Software, LLC">
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
/// Unit tests for <see cref="EditorContextBuilder"/> layout context methods.
/// </summary>
[TestClass]
public class EditorContextBuilderLayoutTests
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
    /// Test that BuildLayoutContextAsync throws KeyNotFoundException for non-existent layout.
    /// </summary>
    [TestMethod]
    public async Task BuildLayoutContextAsync_LayoutNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var layoutId = Guid.NewGuid().ToString();
        var emptyLayouts = new List<Layout>();

        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(emptyLayouts));

        // Act + Assert
        try
        {
            await _builder.BuildLayoutContextAsync(layoutId);
            Assert.Fail("Expected KeyNotFoundException was not thrown.");
        }
        catch (KeyNotFoundException)
        {
            // Expected
        }
    }

    /// <summary>
    /// Test that BuildLayoutContextAsync throws ArgumentException for invalid layout ID.
    /// </summary>
    [TestMethod]
    public async Task BuildLayoutContextAsync_InvalidLayoutId_ThrowsArgumentException()
    {
        // Arrange
        var invalidLayoutId = "not-a-guid";

        // Act + Assert
        try
        {
            await _builder.BuildLayoutContextAsync(invalidLayoutId);
            Assert.Fail("Expected ArgumentException was not thrown.");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    /// <summary>
    /// Test that BuildLayoutContextAsync returns proper LayoutEntityContext for valid layout.
    /// </summary>
    [TestMethod]
    public async Task BuildLayoutContextAsync_ValidLayout_ReturnsLayoutEntityContext()
    {
        // Arrange
        var layoutId = Guid.NewGuid();
        var layout = new Layout
        {
            Id = layoutId,
            LayoutNumber = 1,
            Version = 2,
            LayoutName = "Main Layout",
            IsDefault = true,
            Notes = "Main website layout",
            Head = "<link rel=\"stylesheet\" href=\"/css/main.css\">",
            HtmlHeader = "<!--CCMS--START--HEADER--><nav>Header</nav><!--CCMS--END--HEADER-->",
            FooterHtmlContent = "<!--CCMS--START--FOOTER--><footer>Footer</footer><!--CCMS--END--FOOTER-->",
            Published = new DateTimeOffset(2026, 6, 22, 14, 30, 0, TimeSpan.Zero),
        };

        var layouts = new List<Layout> { layout };
        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(layouts));

        // Act
        var result = await _builder.BuildLayoutContextAsync(layoutId.ToString());

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(layoutId.ToString(), result.LayoutId);
        Assert.AreEqual("Main Layout", result.Name);
        Assert.AreEqual("Main website layout", result.Description);
        Assert.AreEqual(2, result.Version);
        Assert.IsTrue(result.IsDefault);
    }

    /// <summary>
    /// Test that BuildLayoutContextAsync extracts CCMS regions correctly.
    /// </summary>
    [TestMethod]
    public async Task BuildLayoutContextAsync_CCMSRegions_ExtractsCorrectly()
    {
        // Arrange
        var layoutId = Guid.NewGuid();
        var layout = new Layout
        {
            Id = layoutId,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Layout with Regions",
            IsDefault = false,
            Head = "<!--CCMS--START--HEAD--><meta>Head</meta><!--CCMS--END--HEAD-->",
            HtmlHeader = "<!--CCMS--START--HEADER--><nav>Header</nav><!--CCMS--END--HEADER-->",
            FooterHtmlContent = "<!--CCMS--START--FOOTER--><footer>Footer</footer><!--CCMS--END--FOOTER-->",
            Published = null,
        };

        var layouts = new List<Layout> { layout };
        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(layouts));

        // Act
        var result = await _builder.BuildLayoutContextAsync(layoutId.ToString());

        // Assert
        Assert.IsNotNull(result.Regions);
        Assert.IsTrue(result.Regions.Count >= 3);

        var headerRegion = result.Regions.Find(r => r.Name == "HEADER");
        Assert.IsNotNull(headerRegion);
        Assert.AreEqual("<!--CCMS--START--HEADER-->", headerRegion.Placeholder);

        var footerRegion = result.Regions.Find(r => r.Name == "FOOTER");
        Assert.IsNotNull(footerRegion);
        Assert.AreEqual("<!--CCMS--START--FOOTER-->", footerRegion.Placeholder);
    }

    /// <summary>
    /// Test that BuildLayoutContextAsync returns default content region when no CCMS markers exist.
    /// </summary>
    [TestMethod]
    public async Task BuildLayoutContextAsync_NoRegionMarkers_ReturnsDefaultRegion()
    {
        // Arrange
        var layoutId = Guid.NewGuid();
        var layout = new Layout
        {
            Id = layoutId,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Simple Layout",
            IsDefault = false,
            Head = "<style>body { color: black; }</style>",
            HtmlHeader = "<header>Simple Header</header>",
            FooterHtmlContent = "<footer>Simple Footer</footer>",
            Published = null,
        };

        var layouts = new List<Layout> { layout };
        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(layouts));

        // Act
        var result = await _builder.BuildLayoutContextAsync(layoutId.ToString());

        // Assert
        Assert.IsNotNull(result.Regions);
        Assert.AreEqual(1, result.Regions.Count);
        Assert.AreEqual("Content", result.Regions[0].Name);
        Assert.AreEqual("<!-- [CONTENT] -->", result.Regions[0].Placeholder);
    }

    /// <summary>
    /// Test that BuildLayoutContextAsync extracts stylesheets from Head.
    /// </summary>
    [TestMethod]
    public async Task BuildLayoutContextAsync_Stylesheets_ExtractsCorrectly()
    {
        // Arrange
        var layoutId = Guid.NewGuid();
        var layout = new Layout
        {
            Id = layoutId,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Layout with Styles",
            IsDefault = false,
            Head = @"<link rel=""stylesheet"" href=""/css/main.css"">
                    <link rel=""stylesheet"" href=""https://example.com/cdn.css"">",
            HtmlHeader = string.Empty,
            FooterHtmlContent = string.Empty,
            Published = null,
        };

        var layouts = new List<Layout> { layout };
        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(layouts));

        // Act
        var result = await _builder.BuildLayoutContextAsync(layoutId.ToString());

        // Assert
        Assert.IsNotNull(result.Stylesheets);
        Assert.IsTrue(result.Stylesheets.Count >= 2);
    }

    /// <summary>
    /// Test that BuildLayoutContextAsync extracts scripts from Head and Footer.
    /// </summary>
    [TestMethod]
    public async Task BuildLayoutContextAsync_Scripts_ExtractsFromHeadAndFooter()
    {
        // Arrange
        var layoutId = Guid.NewGuid();
        var layout = new Layout
        {
            Id = layoutId,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Layout with Scripts",
            IsDefault = false,
            Head = @"<script src=""/js/header-init.js""></script>",
            HtmlHeader = string.Empty,
            FooterHtmlContent = @"<script src=""/js/footer-tracking.js""></script>",
            Published = null,
        };

        var layouts = new List<Layout> { layout };
        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(layouts));

        // Act
        var result = await _builder.BuildLayoutContextAsync(layoutId.ToString());

        // Assert
        Assert.IsNotNull(result.Scripts);
        Assert.IsTrue(result.Scripts.Count >= 2);

        var headScript = result.Scripts.Find(s => s.Location == "head");
        Assert.IsNotNull(headScript);

        var footerScript = result.Scripts.Find(s => s.Location == "body-end");
        Assert.IsNotNull(footerScript);
    }

    /// <summary>
    /// Test that BuildLayoutContextAsync handles null optional fields.
    /// </summary>
    [TestMethod]
    public async Task BuildLayoutContextAsync_NullOptionalFields_HandlesGracefully()
    {
        // Arrange
        var layoutId = Guid.NewGuid();
        var layout = new Layout
        {
            Id = layoutId,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Minimal Layout",
            IsDefault = false,
            Notes = null,
            Head = null,
            HtmlHeader = null,
            FooterHtmlContent = null,
            Published = null,
        };

        var layouts = new List<Layout> { layout };
        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(layouts));

        // Act
        var result = await _builder.BuildLayoutContextAsync(layoutId.ToString());

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNull(result.Description);
        Assert.IsNotNull(result.Regions); // Should have default content region
        Assert.IsNotNull(result.Stylesheets);
        Assert.IsNotNull(result.Scripts);
    }

    /// <summary>
    /// Test that BuildLayoutContextAsync marks draft layouts correctly.
    /// </summary>
    [TestMethod]
    public async Task BuildLayoutContextAsync_UnpublishedLayout_MarksDraft()
    {
        // Arrange
        var layoutId = Guid.NewGuid();
        var layout = new Layout
        {
            Id = layoutId,
            LayoutNumber = 1,
            Version = 3,
            LayoutName = "Draft Layout",
            IsDefault = false,
            Published = null,
            Head = string.Empty,
            HtmlHeader = string.Empty,
            FooterHtmlContent = string.Empty,
        };

        var layouts = new List<Layout> { layout };
        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(layouts));

        // Act
        var result = await _builder.BuildLayoutContextAsync(layoutId.ToString());

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Draft Layout", result.Name);
        Assert.IsFalse(result.IsDefault);
    }

    /// <summary>
    /// Test that BuildLayoutContextAsync truncates large layout markup.
    /// </summary>
    [TestMethod]
    public async Task BuildLayoutContextAsync_LargeMarkup_Truncates()
    {
        // Arrange
        var layoutId = Guid.NewGuid();
        var largeMarkup = new string('x', 60_000);
        var layout = new Layout
        {
            Id = layoutId,
            LayoutNumber = 1,
            Version = 1,
            LayoutName = "Large Layout",
            IsDefault = false,
            Head = largeMarkup,
            HtmlHeader = string.Empty,
            FooterHtmlContent = string.Empty,
            Published = new DateTimeOffset(2026, 6, 22, 14, 30, 0, TimeSpan.Zero),
        };

        var layouts = new List<Layout> { layout };
        _mockDbContext
            .Setup(db => db.Layouts)
            .Returns(MockDbSet(layouts));

        // Act
        var result = await _builder.BuildLayoutContextAsync(layoutId.ToString());

        // Assert
        Assert.IsNotNull(result.LayoutMarkup);
        Assert.IsTrue(result.LayoutMarkup.Contains("... (truncated)"));
    }

    /// <summary>
    /// Creates a mock DbSet for testing with async support.
    /// </summary>
    private static DbSet<Layout> MockDbSet(List<Layout> layouts)
    {
        var queryable = layouts.AsQueryable();
        var mockSet = new Mock<DbSet<Layout>>();
        mockSet.As<IAsyncEnumerable<Layout>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<Layout>(queryable.GetEnumerator()));
        mockSet.As<IQueryable<Layout>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<Layout>(queryable.Provider));
        mockSet.As<IQueryable<Layout>>()
            .Setup(m => m.Expression)
            .Returns(queryable.Expression);
        mockSet.As<IQueryable<Layout>>()
            .Setup(m => m.ElementType)
            .Returns(queryable.ElementType);
        mockSet.As<IQueryable<Layout>>()
            .Setup(m => m.GetEnumerator())
            .Returns(queryable.GetEnumerator());
        return mockSet.Object;
    }
}
