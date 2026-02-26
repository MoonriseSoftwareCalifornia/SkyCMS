// <copyright file="DeleteTemplateCommandTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Templates
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Templates.Delete;

    /// <summary>
    /// Tests for the <see cref="DeleteTemplateHandler"/> class.
    /// </summary>
    [TestClass]
    public class DeleteTemplateCommandTests : SkyCmsTestBase
    {
        private DeleteTemplateHandler handler;

        /// <summary>
        /// Initializes test context before each test.
        /// </summary>
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
            handler = new DeleteTemplateHandler(Db);
        }

        /// <summary>
        /// Cleanup after each test.
        /// </summary>
        [TestCleanup]
        public async Task Cleanup()
        {
            await DisposeAsync();
        }

        #region Success Cases

        /// <summary>
        /// Tests that deletion succeeds when template has no pages using it.
        /// </summary>
        [TestMethod]
        public async Task DeleteTemplate_SucceedsWhenNoPages_UsingTemplate()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Unused Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var command = new DeleteTemplateCommand
            {
                TemplateId = template.Id,
                UserId = TestUserId
            };

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Deletion should succeed");
            Assert.IsTrue(result.Data, "Result data should be true");

            // Verify template was deleted
            var deletedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNull(deletedTemplate, "Template should be deleted from database");
        }

        /// <summary>
        /// Tests that PageDesignVersions are cascade deleted with template.
        /// </summary>
        [TestMethod]
        public async Task DeleteTemplate_CascadeDeletesPageDesignVersions()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template with Versions",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Create multiple PageDesignVersions
            var version1 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Title = "Version 1",
                Content = "<div>Content v1</div>",
                LayoutId = layout.Id,
                PageType = "content",
                Version = 1
            };
            var version2 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Title = "Version 2",
                Content = "<div>Content v2</div>",
                LayoutId = layout.Id,
                PageType = "content",
                Version = 2
            };
            Db.PageDesignVersions.AddRange(version1, version2);
            await Db.SaveChangesAsync();

            var command = new DeleteTemplateCommand
            {
                TemplateId = template.Id,
                UserId = TestUserId
            };

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Deletion should succeed");

            // Verify template was deleted
            var deletedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNull(deletedTemplate, "Template should be deleted");

            // Verify all PageDesignVersions were deleted
            var remainingVersions = await Db.PageDesignVersions
                .Where(v => v.TemplateId == template.Id)
                .ToListAsync();
            Assert.AreEqual(0, remainingVersions.Count, "All PageDesignVersions should be deleted");

            // Verify specific versions don't exist
            var deletedVersion1 = await Db.PageDesignVersions.FindAsync(version1.Id);
            var deletedVersion2 = await Db.PageDesignVersions.FindAsync(version2.Id);
            Assert.IsNull(deletedVersion1, "Version 1 should be deleted");
            Assert.IsNull(deletedVersion2, "Version 2 should be deleted");
        }

        /// <summary>
        /// Tests that deletion only affects the specified template.
        /// </summary>
        [TestMethod]
        public async Task DeleteTemplate_OnlyDeletesSpecifiedTemplate()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template1 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template to Delete",
                Content = "<div>Content 1</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            var template2 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template to Keep",
                Content = "<div>Content 2</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.AddRange(template1, template2);
            await Db.SaveChangesAsync();

            var command = new DeleteTemplateCommand
            {
                TemplateId = template1.Id,
                UserId = TestUserId
            };

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Deletion should succeed");

            // Verify only template1 was deleted
            var deletedTemplate = await Db.Templates.FindAsync(template1.Id);
            var remainingTemplate = await Db.Templates.FindAsync(template2.Id);
            
            Assert.IsNull(deletedTemplate, "Template 1 should be deleted");
            Assert.IsNotNull(remainingTemplate, "Template 2 should still exist");
        }

        #endregion

        #region Validation Failures

        /// <summary>
        /// Tests that deletion fails when template does not exist.
        /// </summary>
        [TestMethod]
        public async Task DeleteTemplate_FailsWhenTemplateNotFound()
        {
            // Arrange
            var nonExistentTemplateId = Guid.NewGuid();
            var command = new DeleteTemplateCommand
            {
                TemplateId = nonExistentTemplateId,
                UserId = TestUserId
            };

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Deletion should fail");
            Assert.AreEqual("Template not found.", result.ErrorMessage);
        }

        /// <summary>
        /// Tests that deletion fails when template has pages using it.
        /// </summary>
        [TestMethod]
        public async Task DeleteTemplate_FailsWhenPagesAreUsingTemplate()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template In Use",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Create root article first
            await CreateArticleAsync("Root", TestUserId);

            // Create articles using this template
            var article1 = await CreateArticleAsync("Article 1", TestUserId, template.Id);
            var article2 = await CreateArticleAsync("Article 2", TestUserId, template.Id);

            // Create catalog entries (simulating published articles)
            var catalog1 = new CatalogEntry
            {
                ArticleNumber = article1.ArticleNumber,
                Title = article1.Title,
                UrlPath = article1.UrlPath,
                Status = "Active",
                Updated = DateTimeOffset.UtcNow,
                TemplateId = template.Id
            };
            var catalog2 = new CatalogEntry
            {
                ArticleNumber = article2.ArticleNumber,
                Title = article2.Title,
                UrlPath = article2.UrlPath,
                Status = "Active",
                Updated = DateTimeOffset.UtcNow,
                TemplateId = template.Id
            };
            Db.ArticleCatalog.Add(catalog1);
            Db.ArticleCatalog.Add(catalog2);
            await Db.SaveChangesAsync();

            var command = new DeleteTemplateCommand
            {
                TemplateId = template.Id,
                UserId = TestUserId
            };

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Deletion should fail");
            Assert.IsTrue(
                result.ErrorMessage.Contains("2 page(s) are currently using it"),
                $"Error message should indicate 2 pages are using it. Actual: {result.ErrorMessage}");
            Assert.IsTrue(
                result.ErrorMessage.Contains("Template In Use"),
                "Error message should include template title");

            // Verify template still exists
            var templateStillExists = await Db.Templates.FindAsync(template.Id);
            Assert.IsNotNull(templateStillExists, "Template should not be deleted");
        }

        /// <summary>
        /// Tests that deletion fails with invalid (empty) template ID.
        /// </summary>
        [TestMethod]
        public async Task DeleteTemplate_FailsWithEmptyTemplateId()
        {
            // Arrange
            var command = new DeleteTemplateCommand
            {
                TemplateId = Guid.Empty,
                UserId = TestUserId
            };

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Deletion should fail");
            Assert.AreEqual("Invalid template ID.", result.ErrorMessage);
        }

        #endregion

        #region Edge Cases

        /// <summary>
        /// Tests that handler throws ArgumentNullException when command is null.
        /// </summary>
        [TestMethod]
        public async Task DeleteTemplate_ThrowsWhenCommandIsNull()
        {
            // Act & Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                async () => await handler.HandleAsync(null));
        }

        /// <summary>
        /// Tests deletion when template has no PageDesignVersions.
        /// </summary>
        [TestMethod]
        public async Task DeleteTemplate_SucceedsWhenNoPageDesignVersions()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template Without Versions",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var command = new DeleteTemplateCommand
            {
                TemplateId = template.Id,
                UserId = TestUserId
            };

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Deletion should succeed even without versions");

            var deletedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNull(deletedTemplate, "Template should be deleted");
        }

        /// <summary>
        /// Tests that only PageDesignVersions for the deleted template are removed.
        /// </summary>
        [TestMethod]
        public async Task DeleteTemplate_DoesNotDeleteOtherTemplateVersions()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template1 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template to Delete",
                Content = "<div>Content 1</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            var template2 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template to Keep",
                Content = "<div>Content 2</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.AddRange(template1, template2);
            await Db.SaveChangesAsync();

            // Create versions for both templates
            var version1ForTemplate1 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template1.Id,
                Title = "Version 1 Template 1",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                PageType = "content",
                Version = 1
            };
            var version1ForTemplate2 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template2.Id,
                Title = "Version 1 Template 2",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                PageType = "content",
                Version = 1
            };
            Db.PageDesignVersions.AddRange(version1ForTemplate1, version1ForTemplate2);
            await Db.SaveChangesAsync();

            var command = new DeleteTemplateCommand
            {
                TemplateId = template1.Id,
                UserId = TestUserId
            };

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Deletion should succeed");

            // Verify template1's version is deleted
            var deletedVersion = await Db.PageDesignVersions.FindAsync(version1ForTemplate1.Id);
            Assert.IsNull(deletedVersion, "Template 1's version should be deleted");

            // Verify template2's version still exists
            var remainingVersion = await Db.PageDesignVersions.FindAsync(version1ForTemplate2.Id);
            Assert.IsNotNull(remainingVersion, "Template 2's version should still exist");
        }

        #endregion
    }
}
