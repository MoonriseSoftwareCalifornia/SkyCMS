// <copyright file="TemplateServiceCoverageTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services.Templates
{
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Additional tests for TemplateService to increase code coverage.
    /// Tests template CRUD operations, default templates, and layout associations.
    /// </summary>
    [TestClass]
    public class TemplateServiceCoverageTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
        }

        #region Default Template Tests

        /// <summary>
        /// Tests that default templates are created on initialization.
        /// </summary>
        [TestMethod]
        public async Task EnsureDefaultTemplatesExist_CreatesRequiredTemplates()
        {
            // Act
            await TemplateService.EnsureDefaultTemplatesExistAsync();

            // Assert
            var templates = await Db.Templates.ToListAsync();
            Assert.IsTrue(templates.Count > 0, "Default templates should be created");

            // Verify critical templates exist
            var blogPostTemplate = templates.FirstOrDefault(t => t.PageType == "blog-post");
            var blogStreamTemplate = templates.FirstOrDefault(t => t.PageType == "blog-stream");

            Assert.IsNotNull(blogPostTemplate, "blog-post template should exist");
            Assert.IsNotNull(blogStreamTemplate, "blog-stream template should exist");
        }

        /// <summary>
        /// Tests that GetTemplateByKeyAsync returns correct template.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateByKey_ValidKey_ReturnsTemplate()
        {
            // Arrange
            var key = "blog-post";

            // Act
            var template = await TemplateService.GetTemplateByKeyAsync(key);

            // Assert
            Assert.IsNotNull(template, "Template should be returned for valid key");
            Assert.IsFalse(string.IsNullOrEmpty(template.Content), "Template should have content");
        }

        /// <summary>
        /// Tests that GetTemplateByKeyAsync returns null for invalid keys.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateByKey_InvalidKey_ReturnsNull()
        {
            // Arrange
            var invalidKey = "non-existent-template-" + Guid.NewGuid();

            // Act
            var template = await TemplateService.GetTemplateByKeyAsync(invalidKey);

            // Assert
            Assert.IsNull(template, "Should return null for non-existent template keys");
        }

        #endregion

        #region Template CRUD Tests

        /// <summary>
        /// Tests creating a new template.
        /// </summary>
        [TestMethod]
        public async Task CreateTemplate_NewTemplate_SavesSuccessfully()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                PageType = "custom-page-type",
                Content = "<div>Custom Template Content</div>",
                LayoutId = layout.Id,
                Description = "Test custom template"
            };

            // Act
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Assert
            var savedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNotNull(savedTemplate);
            Assert.AreEqual("custom-page-type", savedTemplate.PageType);
            Assert.AreEqual("<div>Custom Template Content</div>", savedTemplate.Content);
        }

        /// <summary>
        /// Tests updating an existing template.
        /// </summary>
        [TestMethod]
        public async Task UpdateTemplate_ExistingTemplate_UpdatesSuccessfully()
        {
            // Arrange
            var template = await Db.Templates.FirstAsync();
            var originalContent = template.Content;
            var newContent = "<div>Updated Content</div>";

            // Act
            template.Content = newContent;
            await Db.SaveChangesAsync();

            // Assert
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.AreEqual(newContent, updatedTemplate.Content);
            Assert.AreNotEqual(originalContent, updatedTemplate.Content);
        }

        /// <summary>
        /// Tests deleting a template.
        /// </summary>
        [TestMethod]
        public async Task DeleteTemplate_ExistingTemplate_RemovesSuccessfully()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                PageType = "deletable-template",
                Content = "<div>To be deleted</div>",
                LayoutId = layout.Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            Db.Templates.Remove(template);
            await Db.SaveChangesAsync();

            // Assert
            var deletedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNull(deletedTemplate, "Template should be deleted");
        }

        #endregion

        #region Template Listing Tests

        /// <summary>
        /// Tests getting all templates.
        /// </summary>
        [TestMethod]
        public async Task GetAllTemplates_ReturnsAllTemplates()
        {
            // Arrange
            await TemplateService.EnsureDefaultTemplatesExistAsync();

            // Act
            var templates = await Db.Templates.ToListAsync();

            // Assert
            Assert.IsTrue(templates.Count > 0, "Should return at least default templates");
        }

        /// <summary>
        /// Tests filtering templates by PageType.
        /// </summary>
        [TestMethod]
        public async Task GetTemplatesByPageType_ReturnsMatchingTemplates()
        {
            // Arrange
            var pageType = "blog-post";

            // Act
            var templates = await Db.Templates
                .Where(t => t.PageType == pageType)
                .ToListAsync();

            // Assert
            Assert.IsTrue(templates.Count > 0, "Should find blog-post templates");
            Assert.IsTrue(templates.All(t => t.PageType == pageType), "All returned templates should match page type");
        }

        #endregion

        #region Layout Association Tests

        /// <summary>
        /// Tests that templates are correctly associated with layouts.
        /// </summary>
        [TestMethod]
        public async Task Template_LayoutAssociation_IsValid()
        {
            // Arrange
            var template = await Db.Templates.FirstAsync();
            var layout = await Db.Layouts.FindAsync(template.LayoutId);

            // Assert
            Assert.IsNotNull(layout, "Template should have associated layout");
            Assert.AreEqual(template.LayoutId, layout.Id);
        }

        /// <summary>
        /// Tests creating template with default layout.
        /// </summary>
        [TestMethod]
        public async Task CreateTemplate_WithDefaultLayout_AssociatesCorrectly()
        {
            // Arrange
            var defaultLayout = await Db.Layouts.FirstOrDefaultAsync(l => l.IsDefault);
            Assert.IsNotNull(defaultLayout, "Default layout should exist");

            var template = new Template
            {
                Id = Guid.NewGuid(),
                PageType = "default-layout-test",
                Content = "<div>Test</div>",
                LayoutId = defaultLayout.Id
            };

            // Act
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Assert
            var savedTemplate = await Db.Templates
                .FirstAsync(t => t.Id == template.Id);
            var associatedLayout = await Db.Layouts.FindAsync(savedTemplate.LayoutId);
            Assert.IsTrue(associatedLayout.IsDefault, "Template should use default layout");
        }


        #endregion

        #region Blog Template Tests


        /// <summary>
        /// Tests that blog-post template has correct structure.
        /// </summary>
        [TestMethod]
        public async Task BlogPostTemplate_HasCorrectStructure()
        {
            // Arrange & Act
            var template = await Db.Templates.FirstOrDefaultAsync(t => t.PageType == "blog-post");

            // Assert
            Assert.IsNotNull(template, "blog-post template should exist");
            Assert.IsFalse(string.IsNullOrEmpty(template.Content), "Template should have content");
            Assert.IsNotNull(template.LayoutId, "Template should have layout");
        }

        /// <summary>
        /// Tests that blog-stream template has correct structure.
        /// </summary>
        [TestMethod]
        public async Task BlogStreamTemplate_HasCorrectStructure()
        {
            // Arrange & Act
            var template = await Db.Templates.FirstOrDefaultAsync(t => t.PageType == "blog-stream");

            // Assert
            Assert.IsNotNull(template, "blog-stream template should exist");
            Assert.IsFalse(string.IsNullOrEmpty(template.Content), "Template should have content");
            Assert.IsNotNull(template.LayoutId, "Template should have layout");
        }

        #endregion

        #region Template Cloning Tests

        /// <summary>
        /// Tests cloning a template creates independent copy.
        /// </summary>
        [TestMethod]
        public async Task CloneTemplate_CreatesIndependentCopy()
        {
            // Arrange
            var original = await Db.Templates.FirstAsync();

            var clone = new Template
            {
                Id = Guid.NewGuid(),
                PageType = original.PageType + "-clone",
                Content = original.Content,
                LayoutId = original.LayoutId,
                Description = original.Description + " (Clone)"
            };

            // Act
            Db.Templates.Add(clone);
            await Db.SaveChangesAsync();

            // Assert
            var savedClone = await Db.Templates.FindAsync(clone.Id);
            Assert.IsNotNull(savedClone);
            Assert.AreNotEqual(original.Id, savedClone.Id);
            Assert.AreEqual(original.Content, savedClone.Content);
        }

        #endregion

        #region Template Content Tests

        /// <summary>
        /// Tests that template content can contain HTML.
        /// </summary>
        [TestMethod]
        public async Task Template_SupportsHtmlContent()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var htmlContent = @"
                <div class=""container"">
                    <h1>{{title}}</h1>
                    <div class=""content"">{{content}}</div>
                </div>";

            var template = new Template
            {
                Id = Guid.NewGuid(),
                PageType = "html-test",
                Content = htmlContent,
                LayoutId = layout.Id
            };

            // Act
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Assert
            var savedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.AreEqual(htmlContent, savedTemplate.Content);
        }

        #endregion
    }
}
