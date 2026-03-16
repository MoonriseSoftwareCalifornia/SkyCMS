// <copyright file="PageDesignVersionCommandTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Tests.Features.Templates
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Templates.Create;
    using Sky.Editor.Features.Templates.GetEditable;
    using Sky.Editor.Features.Templates.Publishing;
    using Sky.Editor.Features.Templates.Save;
    using Sky.Tests;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for PageDesignVersion command handlers.
    /// </summary>
    [TestClass]
    public class PageDesignVersionCommandTests : SkyCmsTestBase
    {
        private CreatePageDesignVersionHandler createHandler = null!;
        private SavePageDesignVersionHandler saveHandler = null!;
        private PublishPageDesignVersionHandler publishHandler = null!;
        private GetEditablePageDesignVersionHandler getEditableHandler = null!;
        private Template testTemplate = null!;

        protected override void AfterInitialize()
        {
            // Create handlers with test dependencies
            createHandler = new CreatePageDesignVersionHandler(
                Db,
                ArticleHtmlService,
                Clock,
                new NullLogger<CreatePageDesignVersionHandler>());

            saveHandler = new SavePageDesignVersionHandler(
                Db,
                ArticleHtmlService,
                Clock,
                new NullLogger<SavePageDesignVersionHandler>());

            publishHandler = new PublishPageDesignVersionHandler(
                Db,
                PublishingService,
                Clock,
                new NullLogger<PublishPageDesignVersionHandler>(),
                Mediator);

            getEditableHandler = new GetEditablePageDesignVersionHandler(
                Db,
                ArticleHtmlService,
                Clock,
                new NullLogger<GetEditablePageDesignVersionHandler>());

            // Seed a test template
            testTemplate = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Description = "Test Description",
                Content = "<div data-ccms-ceid=\"test-region\">Original Content</div>",
                PageType = "test-page",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(testTemplate);
            Db.SaveChanges();
        }

        /// <summary>
        /// Tests that CreatePageDesignVersion_Should_CreateNewVersion.
        /// </summary>
        [TestMethod]
        public async Task CreatePageDesignVersion_Should_CreateNewVersion()
        {
            // Arrange
            var command = new CreatePageDesignVersionCommand
            {
                TemplateId = testTemplate.Id,
                Title = "Version 1",
                Description = "First version",
                Content = "<div>Test content</div>",
                PageType = "test-page",
                LayoutId = testTemplate.LayoutId
            };

            // Act
            var result = await createHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(1, result.Data.Version);
            Assert.AreEqual(command.Title, result.Data.Title);
            Assert.IsNull(result.Data.Published); // Should be unpublished

            var dbVersion = await Db.PageDesignVersions.FirstOrDefaultAsync(v => v.Id == result.Data.Id);
            Assert.IsNotNull(dbVersion);
        }

        /// <summary>
        /// Tests that CreatePageDesignVersion_Should_IncrementVersionNumber.
        /// </summary>
        [TestMethod]
        public async Task CreatePageDesignVersion_Should_IncrementVersionNumber()
        {
            // Arrange - Create first version
            var firstVersion = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 1,
                Title = "Version 1",
                Content = "<div>V1</div>",
                PageType = "test-page"
            };
            Db.PageDesignVersions.Add(firstVersion);
            await Db.SaveChangesAsync();

            var command = new CreatePageDesignVersionCommand
            {
                TemplateId = testTemplate.Id,
                Title = "Version 2",
                Content = "<div>V2</div>",
                PageType = "test-page"
            };

            // Act
            var result = await createHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Data.Version);
        }

        /// <summary>
        /// Tests that SavePageDesignVersion_Should_UpdateExistingVersion.
        /// </summary>
        [TestMethod]
        public async Task SavePageDesignVersion_Should_UpdateExistingVersion()
        {
            // Arrange - Create a version first
            var version = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 1,
                Title = "Original Title",
                Description = "Original Description",
                Content = "<div>Original</div>",
                PageType = "test-page",
                Modified = DateTimeOffset.UtcNow.AddHours(-1)
            };
            Db.PageDesignVersions.Add(version);
            await Db.SaveChangesAsync();

            var command = new SavePageDesignVersionCommand
            {
                Id = version.Id,
                Title = "Updated Title",
                Description = "Updated Description",
                Content = "<div>Updated</div>",
                PageType = "test-page"
            };

            // Act
            var result = await saveHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(command.Title, result.Data.Title);
            Assert.AreEqual(command.Description, result.Data.Description);

            var dbVersion = await Db.PageDesignVersions.FirstOrDefaultAsync(v => v.Id == version.Id);
            Assert.AreEqual("Updated Title", dbVersion.Title);
        }

        /// <summary>
        /// Tests that SavePageDesignVersion_Should_ReturnFailure_WhenVersionNotFound.
        /// </summary>
        [TestMethod]
        public async Task SavePageDesignVersion_Should_ReturnFailure_WhenVersionNotFound()
        {
            // Arrange
            var command = new SavePageDesignVersionCommand
            {
                Id = Guid.NewGuid(), // Non-existent ID
                Title = "Test",
                Content = "<div>Test</div>"
            };

            // Act
            var result = await saveHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("not found"));
        }

        /// <summary>
        /// Tests that PublishPageDesignVersion_Should_UpdateTemplate.
        /// </summary>
        [TestMethod]
        public async Task PublishPageDesignVersion_Should_UpdateTemplate()
        {
            // Arrange - Create an unpublished version
            var version = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 1,
                Title = "Published Version",
                Description = "Published Description",
                Content = "<div data-ccms-ceid=\"test-region\">Published Content</div>",
                PageType = "test-page",
                LayoutId = testTemplate.LayoutId,
                Published = null
            };
            Db.PageDesignVersions.Add(version);
            await Db.SaveChangesAsync();

            var command = new PublishPageDesignVersionCommand
            {
                Id = version.Id,
                UserId = TestUserId
            };

            // Act
            var result = await publishHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);

            // Check version is marked as published
            var publishedVersion = await Db.PageDesignVersions.FirstOrDefaultAsync(v => v.Id == version.Id);
            Assert.IsNotNull(publishedVersion.Published);

            // Check template is updated
            var updatedTemplate = await Db.Templates.FirstOrDefaultAsync(t => t.Id == testTemplate.Id);
            Assert.AreEqual("Published Version", updatedTemplate.Title);
            Assert.AreEqual("Published Description", updatedTemplate.Description);
            Assert.IsTrue(updatedTemplate.Content.Contains("Published Content"));
        }

        /// <summary>
        /// Tests that PublishPageDesignVersion_Should_UpdateArticlesUsingTemplate.
        /// </summary>
        [TestMethod]
        public async Task PublishPageDesignVersion_Should_UpdateArticlesUsingTemplate()
        {
            // Arrange - Create version and article
            var version = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 1,
                Title = "New Template Design",
                Content = "<div class=\"new-template-wrapper\"><h1>New Template Structure</h1><div data-ccms-ceid=\"test-region\">Default Content</div></div>",
                PageType = "test-page"
            };
            Db.PageDesignVersions.Add(version);
            await Db.SaveChangesAsync();

            // Create an article using this template
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 100,
                VersionNumber = 1,
                Title = "Test Article",
                Content = "<div data-ccms-ceid=\"test-region\">Article Specific Content</div>",
                UrlPath = "test-article",
                StatusCode = (int)StatusCodeEnum.Active,
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(article);

            var catalogEntry = new CatalogEntry
            {
                ArticleNumber = article.ArticleNumber,
                TemplateId = testTemplate.Id,
                Title = article.Title,
                UrlPath = article.UrlPath,
                Published = article.Published,
                Updated = article.Updated
            };
            Db.ArticleCatalog.Add(catalogEntry);
            await Db.SaveChangesAsync();

            var command = new PublishPageDesignVersionCommand
            {
                Id = version.Id,
                UserId = TestUserId
            };

            // Act
            var result = await publishHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Check that a new article version was created
            var articleVersions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .ToListAsync();

            Assert.IsTrue(articleVersions.Count >= 2, "New article version should be created");

            var latestVersion = articleVersions.First();
            Assert.IsTrue(latestVersion.Content.Contains("New Template Structure"),
                "Should have new template structure");
            Assert.IsTrue(latestVersion.Content.Contains("Article Specific Content"),
                "Should preserve editable content");
        }

        /// <summary>
        /// Tests that PublishPageDesignVersion_Should_PreservePublishDate_ForPublishedArticles.
        /// </summary>
        [TestMethod]
        public async Task PublishPageDesignVersion_Should_PreservePublishDate_ForPublishedArticles()
        {
            // Arrange
            var publishedDate = DateTimeOffset.UtcNow.AddDays(-7);

            var version = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 1,
                Content = "<div data-ccms-ceid=\"test-region\">Updated Template</div>",
                PageType = "test-page"
            };
            Db.PageDesignVersions.Add(version);

            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 101,
                VersionNumber = 1,
                Title = "Published Article",
                Content = "<div data-ccms-ceid=\"test-region\">Content</div>",
                UrlPath = "published-article",
                StatusCode = (int)StatusCodeEnum.Active,
                Updated = publishedDate,
                Published = publishedDate, // Was published a week ago
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(article);

            var catalogEntry = new CatalogEntry
            {
                ArticleNumber = article.ArticleNumber,
                TemplateId = testTemplate.Id,
                Title = article.Title,
                UrlPath = article.UrlPath,
                Published = publishedDate,
                Updated = article.Updated
            };
            Db.ArticleCatalog.Add(catalogEntry);
            await Db.SaveChangesAsync();

            var command = new PublishPageDesignVersionCommand
            {
                Id = version.Id,
                UserId = TestUserId
            };

            // Act
            var result = await publishHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var latestArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();

            Assert.AreEqual(publishedDate, latestArticle.Published,
                "Published date should be preserved");
        }

        /// <summary>
        /// Tests that PublishPageDesignVersion_Should_ReturnFailure_WhenVersionNotFound.
        /// </summary>
        [TestMethod]
        public async Task PublishPageDesignVersion_Should_ReturnFailure_WhenVersionNotFound()
        {
            // Arrange
            var command = new PublishPageDesignVersionCommand
            {
                Id = Guid.NewGuid(), // Non-existent
                UserId = TestUserId
            };

            // Act
            var result = await publishHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.ErrorMessage);
        }

        /// <summary>
        /// Tests that CreatePageDesignVersion_Should_EnsureEditableMarkers.
        /// </summary>
        [TestMethod]
        public async Task CreatePageDesignVersion_Should_EnsureEditableMarkers()
        {
            // Arrange
            var command = new CreatePageDesignVersionCommand
            {
                TemplateId = testTemplate.Id,
                Title = "Test Markers",
                Content = "<div contenteditable=\"true\">Content without marker</div>",
                PageType = "test-page"
            };

            // Act
            var result = await createHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Data.Content.Contains("data-ccms-ceid"),
                "Should add editable markers");
        }

        /// <summary>
        /// Tests that GetEditablePageDesignVersion returns the latest unpublished version.
        /// </summary>
        [TestMethod]
        public async Task GetEditablePageDesignVersion_Should_ReturnLatestEditableVersion()
        {
            // Arrange
            Db.PageDesignVersions.AddRange(
                new PageDesignVersion
                {
                    Id = Guid.NewGuid(),
                    TemplateId = testTemplate.Id,
                    Version = 1,
                    Title = "Published",
                    Content = "<div>Published</div>",
                    PageType = "test-page",
                    Published = DateTimeOffset.UtcNow.AddHours(-2)
                },
                new PageDesignVersion
                {
                    Id = Guid.NewGuid(),
                    TemplateId = testTemplate.Id,
                    Version = 2,
                    Title = "Draft v2",
                    Content = "<div>Draft 2</div>",
                    PageType = "test-page",
                    Published = null
                },
                new PageDesignVersion
                {
                    Id = Guid.NewGuid(),
                    TemplateId = testTemplate.Id,
                    Version = 3,
                    Title = "Draft v3",
                    Content = "<div>Draft 3</div>",
                    PageType = "test-page",
                    Published = null
                });
            await Db.SaveChangesAsync();

            // Act
            var result = await getEditableHandler.HandleAsync(new GetEditablePageDesignVersionCommand
            {
                TemplateId = testTemplate.Id
            });

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(3, result.Data.EditableVersion.Version);
            Assert.AreEqual("Draft v3", result.Data.EditableVersion.Title);
            Assert.IsNull(result.Data.EditableVersion.Published);
        }

        /// <summary>
        /// Tests that GetEditablePageDesignVersion creates a new editable version from template when none exists.
        /// </summary>
        [TestMethod]
        public async Task GetEditablePageDesignVersion_Should_CreateEditableVersion_WhenNoneExists()
        {
            // Arrange
            Db.PageDesignVersions.Add(new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 1,
                Title = "Published v1",
                Description = "Published",
                Content = "<div data-ccms-ceid=\"test-region\">Published Content</div>",
                PageType = "test-page",
                Published = DateTimeOffset.UtcNow.AddDays(-1)
            });
            await Db.SaveChangesAsync();

            // Act
            var result = await getEditableHandler.HandleAsync(new GetEditablePageDesignVersionCommand
            {
                TemplateId = testTemplate.Id
            });

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data?.EditableVersion);
            Assert.AreEqual(2, result.Data.EditableVersion.Version);
            Assert.IsNull(result.Data.EditableVersion.Published);
            Assert.AreEqual(testTemplate.Title, result.Data.EditableVersion.Title);
            Assert.AreEqual(testTemplate.Description, result.Data.EditableVersion.Description);
            Assert.AreEqual(testTemplate.Content, result.Data.EditableVersion.Content);
        }

        /// <summary>
        /// Tests that PublishPageDesignVersion creates a new editable version after publish.
        /// </summary>
        [TestMethod]
        public async Task PublishPageDesignVersion_Should_CreateNewEditableVersion_AfterPublish()
        {
            // Arrange
            var editableVersion = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 1,
                Title = "Editable v1",
                Description = "Editable",
                Content = "<div data-ccms-ceid=\"test-region\">Editable Content</div>",
                PageType = "test-page",
                LayoutId = testTemplate.LayoutId,
                Published = null
            };
            Db.PageDesignVersions.Add(editableVersion);
            await Db.SaveChangesAsync();

            // Act
            var result = await publishHandler.HandleAsync(new PublishPageDesignVersionCommand
            {
                Id = editableVersion.Id,
                UserId = TestUserId
            });

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var versions = await Db.PageDesignVersions
                .Where(v => v.TemplateId == testTemplate.Id)
                .OrderBy(v => v.Version)
                .ToListAsync();

            Assert.AreEqual(2, versions.Count, "Should have original published version plus new editable version");
            Assert.IsNotNull(versions[0].Published, "Published version should have Published timestamp");
            Assert.IsNull(versions[1].Published, "New editable version should be unpublished");
            Assert.AreEqual(2, versions[1].Version);
            Assert.AreEqual(versions[0].Content, versions[1].Content);
            Assert.AreEqual(versions[0].Title, versions[1].Title);
        }
    }
}
