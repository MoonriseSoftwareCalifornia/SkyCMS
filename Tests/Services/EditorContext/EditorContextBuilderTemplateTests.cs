// <copyright file="EditorContextBuilderTemplateTests.cs" company="Moonrise Software, LLC">
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
/// Unit tests for <see cref="EditorContextBuilder"/> template context methods.
/// </summary>
[TestClass]
public class EditorContextBuilderTemplateTests
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
    /// Test that BuildTemplateContextAsync throws ArgumentException for invalid template ID.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_InvalidTemplateId_ThrowsArgumentException()
    {
        // Arrange
        var invalidTemplateId = "not-a-guid";

        // Act + Assert
        try
        {
            await _builder.BuildTemplateContextAsync(invalidTemplateId);
            Assert.Fail("Expected ArgumentException was not thrown.");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync throws KeyNotFoundException for non-existent template.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_TemplateNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();
        var emptyTemplates = new List<Template>();

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(emptyTemplates));

        // Act + Assert
        try
        {
            await _builder.BuildTemplateContextAsync(templateId);
            Assert.Fail("Expected KeyNotFoundException was not thrown.");
        }
        catch (KeyNotFoundException)
        {
            // Expected
        }
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync throws KeyNotFoundException when no design version exists.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_NoDesignVersion_ThrowsKeyNotFoundException()
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var templateId = templateGuid.ToString();
        
        var template = new Template
        {
            Id = templateGuid,
            Title = "Test Template",
        };

        var emptyVersions = new List<PageDesignVersion>();

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(emptyVersions));

        // Act + Assert
        try
        {
            await _builder.BuildTemplateContextAsync(templateId);
            Assert.Fail("Expected KeyNotFoundException was not thrown.");
        }
        catch (KeyNotFoundException)
        {
            // Expected
        }
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync returns proper TemplateEntityContext for valid template.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_ValidTemplate_ReturnsTemplateEntityContext()
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var templateId = templateGuid.ToString();

        var template = new Template
        {
            Id = templateGuid,
            Title = "Home Template",
            Description = "Main landing page template",
            PageType = "home",
        };

        var version = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 1,
            Title = "Home Template v1",
            Description = "Version 1 of home template",
            Content = @"<div data-ccms-ceid='hero'>Hero Section</div>
                       <div data-ccms-ceid='content'>Main Content</div>",
            PageType = "home",
            Published = new DateTimeOffset(2026, 6, 22, 14, 30, 0, TimeSpan.Zero),
        };

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { version }));

        // Act
        var result = await _builder.BuildTemplateContextAsync(templateId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(templateId, result.TemplateId);
        Assert.AreEqual("Home Template v1", result.Name);
        Assert.AreEqual("Version 1 of home template", result.Description);
        Assert.AreEqual(1, result.Version);
        Assert.AreEqual("wrapper", result.CompositionType);
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync extracts data-ccms-ceid fields correctly.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_DataCcmsCeidMarkers_ExtractsCorrectly()
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var templateId = templateGuid.ToString();

        var template = new Template
        {
            Id = templateGuid,
            Title = "Multi-Section Template",
            PageType = "content",
        };

        var version = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 1,
            Title = "Multi-Section Template",
            Content = @"<header data-ccms-ceid='header'>Header</header>
                       <div data-ccms-ceid='intro'>Introduction</div>
                       <div data-ccms-ceid='body'>Body Content</div>
                       <aside data-ccms-ceid='sidebar'>Sidebar</aside>
                       <footer data-ccms-ceid='footer'>Footer</footer>",
            PageType = "content",
            Published = null,
        };

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { version }));

        // Act
        var result = await _builder.BuildTemplateContextAsync(templateId);

        // Assert
        Assert.IsNotNull(result.ExpectedFields);
        Assert.AreEqual(5, result.ExpectedFields.Count);

        var headerField = result.ExpectedFields.Find(f => f.FieldName == "header");
        Assert.IsNotNull(headerField);
        Assert.AreEqual("html", headerField.DataType);
        Assert.IsTrue(headerField.Required);

        var bodyField = result.ExpectedFields.Find(f => f.FieldName == "body");
        Assert.IsNotNull(bodyField);
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync returns default field when no markers exist.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_NoMarkers_ReturnsDefaultField()
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var templateId = templateGuid.ToString();

        var template = new Template
        {
            Id = templateGuid,
            Title = "Simple Template",
            PageType = "content",
        };

        var version = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 1,
            Title = "Simple Template",
            Content = "<div>Static content with no markers</div>",
            PageType = "content",
            Published = null,
        };

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { version }));

        // Act
        var result = await _builder.BuildTemplateContextAsync(templateId);

        // Assert
        Assert.IsNotNull(result.ExpectedFields);
        Assert.AreEqual(1, result.ExpectedFields.Count);
        Assert.AreEqual("content", result.ExpectedFields[0].FieldName);
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync handles duplicate field names.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_DuplicateMarkers_DeduplicatesFields()
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var templateId = templateGuid.ToString();

        var template = new Template
        {
            Id = templateGuid,
            Title = "Template with Duplicates",
            PageType = "content",
        };

        var version = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 1,
            Title = "Template with Duplicates",
            Content = @"<div data-ccms-ceid='content'>First</div>
                       <div data-ccms-ceid='content'>Second</div>",
            PageType = "content",
            Published = null,
        };

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { version }));

        // Act
        var result = await _builder.BuildTemplateContextAsync(templateId);

        // Assert - should only have one content field despite two markers
        Assert.IsNotNull(result.ExpectedFields);
        Assert.AreEqual(1, result.ExpectedFields.Count);
        Assert.AreEqual("content", result.ExpectedFields[0].FieldName);
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync handles null content gracefully.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_NullContent_HandlesGracefully()
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var templateId = templateGuid.ToString();

        var template = new Template
        {
            Id = templateGuid,
            Title = "Template with Null Content",
            PageType = "content",
        };

        var version = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 1,
            Title = "Template with Null Content",
            Content = null,
            PageType = "content",
            Published = null,
        };

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { version }));

        // Act
        var result = await _builder.BuildTemplateContextAsync(templateId);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.ExpectedFields);
        Assert.AreEqual(1, result.ExpectedFields.Count); // Default field
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync correctly determines composition type from PageType.
    /// </summary>
    [TestMethod]
    [DataRow("home", "wrapper")]
    [DataRow("content", "wrapper")]
    [DataRow("sidebar", "partial")]
    [DataRow("widget", "partial")]
    [DataRow("card", "partial")]
    [DataRow("custom", "custom")]
    public async Task BuildTemplateContextAsync_PageType_CorrectCompositionType(string pageType, string expectedComposition)
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var templateId = templateGuid.ToString();

        var template = new Template
        {
            Id = templateGuid,
            Title = "Test Template",
            PageType = pageType,
        };

        var version = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 1,
            Title = "Test Template",
            Content = "<div>Content</div>",
            PageType = pageType,
            Published = null,
        };

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { version }));

        // Act
        var result = await _builder.BuildTemplateContextAsync(templateId);

        // Assert
        Assert.AreEqual(expectedComposition, result.CompositionType);
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync marks draft templates correctly.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_UnpublishedVersion_MarksDraft()
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var templateId = templateGuid.ToString();

        var template = new Template
        {
            Id = templateGuid,
            Title = "Draft Template",
            PageType = "content",
        };

        var version = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 2,
            Title = "Draft Template v2",
            Content = "<div>Draft content</div>",
            PageType = "content",
            Published = null, // Not published = draft
        };

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { version }));

        // Act
        var result = await _builder.BuildTemplateContextAsync(templateId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Version);
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync includes rendering rules.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_RenderingRules_IncludedInContext()
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var templateId = templateGuid.ToString();

        var template = new Template
        {
            Id = templateGuid,
            Title = "Secure Template",
            PageType = "content",
        };

        var version = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 1,
            Title = "Secure Template",
            Content = "<div data-ccms-ceid='content'>Content</div>",
            PageType = "content",
            Published = null,
        };

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { version }));

        // Act
        var result = await _builder.BuildTemplateContextAsync(templateId);

        // Assert
        Assert.IsNotNull(result.RenderingRules);
        Assert.IsTrue(result.RenderingRules.PreserveArticleContent);
        Assert.IsFalse(result.RenderingRules.AllowCustomScripts);
        Assert.IsNotNull(result.RenderingRules.AllowedHtmlElements);
        Assert.IsTrue(result.RenderingRules.AllowedHtmlElements.Contains("div"));
    }

    /// <summary>
    /// Test that BuildTemplateContextAsync truncates large template markup.
    /// </summary>
    [TestMethod]
    public async Task BuildTemplateContextAsync_LargeMarkup_Truncates()
    {
        // Arrange
        var templateGuid = Guid.NewGuid();
        var templateId = templateGuid.ToString();
        var largeContent = new string('x', 60_000);

        var template = new Template
        {
            Id = templateGuid,
            Title = "Large Template",
            PageType = "content",
        };

        var version = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateGuid,
            Version = 1,
            Title = "Large Template",
            Content = largeContent,
            PageType = "content",
            Published = null,
        };

        _mockDbContext
            .Setup(db => db.Templates)
            .Returns(MockDbSet(new List<Template> { template }));

        _mockDbContext
            .Setup(db => db.PageDesignVersions)
            .Returns(MockDbSet(new List<PageDesignVersion> { version }));

        // Act
        var result = await _builder.BuildTemplateContextAsync(templateId);

        // Assert
        Assert.IsNotNull(result.TemplateMarkup);
        Assert.IsTrue(result.TemplateMarkup.Contains("... (truncated)"));
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
