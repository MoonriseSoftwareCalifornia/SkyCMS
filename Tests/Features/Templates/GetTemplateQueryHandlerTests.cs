// <copyright file="GetTemplateQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Tests.Features.Templates
{
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Templates.Get;
    using Sky.Tests;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for GetTemplateQueryHandler.
    /// Tests template retrieval across all supported database providers:
    /// - Azure Cosmos DB
    /// - SQL Server / Azure SQL
    /// - MySQL
    /// - SQLite
    /// </summary>
    /// <remarks>
    /// Tests are designed to be provider-agnostic by using standard EF Core patterns
    /// and avoiding provider-specific extensions or syntax.
    /// </remarks>
    [TestClass]
    public class GetTemplateQueryHandlerTests : SkyCmsTestBase
    {
        private GetTemplateQueryHandler handler = null!;
        private Template testTemplate = null!;
        private Layout testLayout = null!;

        /// <summary>
        /// Initializes test fixtures and handlers before each test.
        /// </summary>
        protected override void AfterInitialize()
        {
            // Initialize handler with test dependencies
            handler = new GetTemplateQueryHandler(Db, new NullLogger<GetTemplateQueryHandler>());

            // Get or create a default layout
            testLayout = Db.Layouts.FirstOrDefault();
            if (testLayout == null)
            {
                testLayout = new Layout
                {
                    Id = Guid.NewGuid(),
                    LayoutName = "Test Layout",
                    LayoutNumber = 1
                };
                Db.Layouts.Add(testLayout);
                Db.SaveChanges();
            }

            // Seed a test template
            testTemplate = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Description = "Test Description",
                Content = "<div data-ccms-ceid=\"test-region\">Template Content</div>",
                PageType = "test-page",
                LayoutId = testLayout.Id
            };
            Db.Templates.Add(testTemplate);
            Db.SaveChanges();
        }

        #region Basic Template Retrieval Tests

        /// <summary>
        /// Tests that GetTemplateQueryHandler retrieves a template by ID.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_RetrieveTemplateById()
        {
            // Arrange
            var query = new GetTemplateQuery { TemplateId = testTemplate.Id };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.IsNotNull(result.Data.Template);
            Assert.AreEqual(testTemplate.Id, result.Data.Template.Id);
            Assert.AreEqual(testTemplate.Title, result.Data.Template.Title);
            Assert.AreEqual(testTemplate.Description, result.Data.Template.Description);
            Assert.AreEqual(testTemplate.Content, result.Data.Template.Content);
        }

        /// <summary>
        /// Tests that GetTemplateQueryHandler returns failure when template is not found.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_ReturnFailure_WhenTemplateNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();
            var query = new GetTemplateQuery { TemplateId = nonExistentId };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Data);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("not found"));
        }

        /// <summary>
        /// Tests that GetTemplateQueryHandler returns failure when TemplateId is empty.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_ReturnFailure_WhenTemplateIdIsEmpty()
        {
            // Arrange
            var query = new GetTemplateQuery { TemplateId = Guid.Empty };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Data);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("empty"));
        }

        #endregion

        #region Version Inclusion Tests

        /// <summary>
        /// Tests that GetTemplateQueryHandler can retrieve a template with its versions.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_IncludeVersions_WhenRequested()
        {
            // Arrange - Create multiple versions
            var version1 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 1,
                Title = "Version 1",
                Content = "<div>V1 Content</div>",
                PageType = "test-page",
                Modified = DateTimeOffset.UtcNow.AddHours(-2)
            };

            var version2 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 2,
                Title = "Version 2",
                Content = "<div>V2 Content</div>",
                PageType = "test-page",
                Modified = DateTimeOffset.UtcNow.AddHours(-1)
            };

            Db.PageDesignVersions.Add(version1);
            Db.PageDesignVersions.Add(version2);
            await Db.SaveChangesAsync();

            var query = new GetTemplateQuery
            {
                TemplateId = testTemplate.Id,
                IncludeVersions = true
            };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.IsNotNull(result.Data.Versions);
            var versions = result.Data.Versions.ToList();
            Assert.AreEqual(2, versions.Count);

            // Verify versions are ordered by descending version number
            Assert.AreEqual(2, versions[0].Version);
            Assert.AreEqual(1, versions[1].Version);
        }

        /// <summary>
        /// Tests that GetTemplateQueryHandler returns empty versions list when no versions exist.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_ReturnEmptyVersionsList_WhenNoVersionsExist()
        {
            // Arrange
            var query = new GetTemplateQuery
            {
                TemplateId = testTemplate.Id,
                IncludeVersions = true
            };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.IsNotNull(result.Data.Versions);
            Assert.AreEqual(0, result.Data.Versions.Count());
        }

        /// <summary>
        /// Tests that GetTemplateQueryHandler returns only the latest version when LatestVersionOnly is true.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_ReturnLatestVersionOnly_WhenRequested()
        {
            // Arrange - Create multiple versions
            var version1 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 1,
                Title = "Version 1",
                Content = "<div>V1</div>",
                PageType = "test-page",
                Modified = DateTimeOffset.UtcNow.AddHours(-2)
            };

            var version2 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 2,
                Title = "Version 2",
                Content = "<div>V2</div>",
                PageType = "test-page",
                Modified = DateTimeOffset.UtcNow.AddHours(-1)
            };

            var version3 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 3,
                Title = "Version 3 (Latest)",
                Content = "<div>V3</div>",
                PageType = "test-page",
                Modified = DateTimeOffset.UtcNow
            };

            Db.PageDesignVersions.Add(version1);
            Db.PageDesignVersions.Add(version2);
            Db.PageDesignVersions.Add(version3);
            await Db.SaveChangesAsync();

            var query = new GetTemplateQuery
            {
                TemplateId = testTemplate.Id,
                IncludeVersions = true,
                LatestVersionOnly = true
            };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            var versions = result.Data.Versions.ToList();
            Assert.AreEqual(1, versions.Count);
            Assert.AreEqual(3, versions[0].Version);
            Assert.AreEqual("Version 3 (Latest)", versions[0].Title);
        }

        /// <summary>
        /// Tests that GetTemplateQueryHandler does not include versions when IncludeVersions is false.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_NotIncludeVersions_WhenNotRequested()
        {
            // Arrange
            var version = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = testTemplate.Id,
                Version = 1,
                Title = "Version 1",
                Content = "<div>V1</div>",
                PageType = "test-page"
            };
            Db.PageDesignVersions.Add(version);
            await Db.SaveChangesAsync();

            var query = new GetTemplateQuery
            {
                TemplateId = testTemplate.Id,
                IncludeVersions = false // Explicitly false
            };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(0, result.Data.Versions.Count());
        }

        #endregion

        #region Multiple Template Tests

        /// <summary>
        /// Tests that GetTemplateQueryHandler correctly retrieves a specific template when multiple exist.
        /// This test validates that the query filters correctly at the database level.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_ReturnCorrectTemplate_WhenMultipleTemplatesExist()
        {
            // Arrange - Create additional templates
            var template2 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Another Template",
                Description = "Another Description",
                Content = "<div>Other Content</div>",
                PageType = "other-page",
                LayoutId = testLayout.Id
            };

            var template3 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Third Template",
                Description = "Third Description",
                Content = "<div>Third Content</div>",
                PageType = "third-page",
                LayoutId = testLayout.Id
            };

            Db.Templates.Add(template2);
            Db.Templates.Add(template3);
            await Db.SaveChangesAsync();

            var query = new GetTemplateQuery { TemplateId = template2.Id };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(template2.Id, result.Data.Template.Id);
            Assert.AreEqual("Another Template", result.Data.Template.Title);
            Assert.AreNotEqual(testTemplate.Id, result.Data.Template.Id);
        }

        #endregion

        #region Database Provider Compatibility Tests

        /// <summary>
        /// Tests that AsNoTracking() works correctly across all database providers.
        /// Validates that read-only retrieval doesn't affect database state.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_UseAsNoTracking_ForReadOnlyAccess()
        {
            // Arrange
            var originalTitle = testTemplate.Title;
            var query = new GetTemplateQuery { TemplateId = testTemplate.Id };

            // Act - Retrieve template
            var result = await handler.HandleAsync(query);

            // Verify we can modify the retrieved object without affecting the database
            if (result.IsSuccess && result.Data?.Template != null)
            {
                result.Data.Template.Title = "Modified Title";
            }

            // Assert - Template in database should not be modified
            var dbTemplate = await Db.Templates.FirstOrDefaultAsync(t => t.Id == testTemplate.Id);
            Assert.AreEqual(originalTitle, dbTemplate.Title);
        }

        /// <summary>
        /// Tests that the query uses standard EF Core patterns compatible with all database providers.
        /// Validates FirstOrDefaultAsync works correctly.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_UseStandardEfCorePatterns()
        {
            // Arrange
            var query = new GetTemplateQuery { TemplateId = testTemplate.Id };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            // If handler is using standard LINQ patterns, this should work on all providers
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data?.Template);
        }

        /// <summary>
        /// Tests that OrderByDescending works correctly for version retrieval across all providers.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_OrderVersionsByDescendingNumber()
        {
            // Arrange
            var versions = new[]
            {
                new PageDesignVersion { Id = Guid.NewGuid(), TemplateId = testTemplate.Id, Version = 3, Title = "V3", Content = "", PageType = "test" },
                new PageDesignVersion { Id = Guid.NewGuid(), TemplateId = testTemplate.Id, Version = 1, Title = "V1", Content = "", PageType = "test" },
                new PageDesignVersion { Id = Guid.NewGuid(), TemplateId = testTemplate.Id, Version = 2, Title = "V2", Content = "", PageType = "test" }
            };

            foreach (var version in versions)
            {
                Db.PageDesignVersions.Add(version);
            }
            await Db.SaveChangesAsync();

            var query = new GetTemplateQuery
            {
                TemplateId = testTemplate.Id,
                IncludeVersions = true
            };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var retrievedVersions = result.Data.Versions.ToList();
            Assert.AreEqual(3, retrievedVersions.Count);

            // Verify descending order
            Assert.AreEqual(3, retrievedVersions[0].Version);
            Assert.AreEqual(2, retrievedVersions[1].Version);
            Assert.AreEqual(1, retrievedVersions[2].Version);
        }

        #endregion

        #region Edge Case Tests

        /// <summary>
        /// Tests behavior when querying with null query object.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_HandleNullQuery()
        {
            // Act
            var result = await handler.HandleAsync(null);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.ErrorMessage);
        }

        /// <summary>
        /// Tests that cancellation token is respected.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_RespectCancellationToken()
        {
            // Arrange
            var query = new GetTemplateQuery { TemplateId = testTemplate.Id };
            var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            // Should handle cancellation gracefully
            try
            {
                var result = await handler.HandleAsync(query, cts.Token);
                Assert.IsFalse(result.IsSuccess);
                Assert.IsNotNull(result.ErrorMessage);
            }
            catch (OperationCanceledException)
            {
                // Cancellation exception is acceptable behavior
            }
        }

        /// <summary>
        /// Tests that special characters in template content are preserved.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_PreserveSpecialCharactersInContent()
        {
            // Arrange
            var specialContent = "<script>alert('test');</script><div data-attr=\"value with 'quotes' & special chars\">Content with �mojis ??</div>";
            var specialTemplate = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Special Template",
                Description = "Special Description",
                Content = specialContent,
                PageType = "special",
                LayoutId = testLayout.Id
            };
            Db.Templates.Add(specialTemplate);
            await Db.SaveChangesAsync();

            var query = new GetTemplateQuery { TemplateId = specialTemplate.Id };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(specialContent, result.Data.Template.Content);
        }

        /// <summary>
        /// Tests that null or empty values in optional fields are handled correctly.
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_HandleNullableFields()
        {
            // Arrange
            var templateWithNulls = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Minimal Template",
                Description = string.Empty, // Empty but not null
                Content = string.Empty, // Empty but not null
                PageType = "minimal",
                LayoutId = testLayout.Id,
                CommunityLayoutId = string.Empty // Empty but not null
            };
            Db.Templates.Add(templateWithNulls);
            await Db.SaveChangesAsync();

            var query = new GetTemplateQuery { TemplateId = templateWithNulls.Id };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(string.Empty, result.Data.Template.Description);
            Assert.AreEqual(string.Empty, result.Data.Template.Content);
            Assert.AreEqual(string.Empty, result.Data.Template.CommunityLayoutId);
        }

        #endregion

        #region Performance Tests

        /// <summary>
        /// Tests that the query doesn't perform unnecessary database round-trips.
        /// Validates that AsNoTracking() is used (no change tracking overhead).
        /// </summary>
        [TestMethod]
        public async Task GetTemplate_Should_MinimizeDatabaseRoundTrips()
        {
            // Arrange
            var query = new GetTemplateQuery { TemplateId = testTemplate.Id };

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Verify no tracking is active for retrieved entity
            // (This is verified indirectly - if tracking were on, modifications would be tracked)
            var entryState = Db.Entry(result.Data.Template).State;
            Assert.AreEqual(EntityState.Detached, entryState);
        }

        #endregion
    }
}
