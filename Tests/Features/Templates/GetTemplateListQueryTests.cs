// <copyright file="GetTemplateListQueryTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Templates
{
    using Cosmos.Common.Data;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Templates.GetList;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for <see cref="GetTemplateListQuery"/> and <see cref="GetTemplateListQueryHandler"/>.
    /// </summary>
    [TestClass]
    public class GetTemplateListQueryTests : SkyCmsTestBase
    {
        /// <summary>
        /// Initialize test context.
        /// </summary>
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
        }

        /// <summary>
        /// Cleanup after each test.
        /// </summary>
        [TestCleanup]
        public async Task Cleanup()
        {
            await DisposeAsync();
        }

        /// <summary>
        /// Tests that query returns templates with pagination.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateList_ReturnsPaginatedResults()
        {
            // Arrange
            var layout = Db.Layouts.First();
            for (int i = 0; i < 15; i++)
            {
                Db.Templates.Add(new Template
                {
                    Id = Guid.NewGuid(),
                    Title = $"Template {i:D2}",
                    Description = $"Description {i}",
                    Content = "<p>Content</p>",
                    LayoutId = layout.Id
                });
            }
            await Db.SaveChangesAsync();

            var query = new GetTemplateListQuery
            {
                PageNo = 0,
                PageSize = 10,
                SortOrder = "asc",
                CurrentSort = "Title"
            };

            var handler = new GetTemplateListQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetTemplateListQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(10, result.Data.Templates.Count, "Should return page size items");
            Assert.AreEqual(17, result.Data.TotalCount, "Should return total count (15 created + 2 seeded blog templates)");
        }

        /// <summary>
        /// Tests that query sorts by title ascending.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateList_SortsByTitleAscending()
        {
            // Arrange
            var layout = Db.Layouts.First();
            Db.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                Title = "Zebra Template",
                Content = "<p>Content</p>",
                LayoutId = layout.Id
            });
            Db.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                Title = "Alpha Template",
                Content = "<p>Content</p>",
                LayoutId = layout.Id
            });
            await Db.SaveChangesAsync();

            var query = new GetTemplateListQuery
            {
                PageNo = 0,
                PageSize = 10,
                SortOrder = "asc",
                CurrentSort = "Title"
            };

            var handler = new GetTemplateListQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetTemplateListQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Data.Templates[0].Title.StartsWith("Alpha"));
        }

        /// <summary>
        /// Tests that query sorts by title descending.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateList_SortsByTitleDescending()
        {
            // Arrange
            var layout = Db.Layouts.First();
            Db.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                Title = "Alpha Template",
                Content = "<p>Content</p>",
                LayoutId = layout.Id
            });
            Db.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                Title = "Zebra Template",
                Content = "<p>Content</p>",
                LayoutId = layout.Id
            });
            await Db.SaveChangesAsync();

            var query = new GetTemplateListQuery
            {
                PageNo = 0,
                PageSize = 10,
                SortOrder = "desc",
                CurrentSort = "Title"
            };

            var handler = new GetTemplateListQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetTemplateListQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Data.Templates[0].Title.StartsWith("Zebra"));
        }

        /// <summary>
        /// Tests that query detects HTML editor usage.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateList_DetectsHtmlEditorUsage()
        {
            // Arrange
            var layout = Db.Layouts.First();
            Db.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                Title = "HTML Editor Template",
                Content = "<div data-ccms-ceid='test'>Content</div>",
                LayoutId = layout.Id
            });
            Db.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                Title = "Regular Template",
                Content = "<p>Content</p>",
                LayoutId = layout.Id
            });
            await Db.SaveChangesAsync();

            var query = new GetTemplateListQuery
            {
                PageNo = 0,
                PageSize = 10
            };

            var handler = new GetTemplateListQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetTemplateListQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var htmlEditorTemplate = result.Data.Templates.First(t => t.Title == "HTML Editor Template");
            var regularTemplate = result.Data.Templates.First(t => t.Title == "Regular Template");

            Assert.IsTrue(htmlEditorTemplate.UsesHtmlEditor, "Template with data-ccms-ceid should use HTML editor");
            Assert.IsFalse(regularTemplate.UsesHtmlEditor, "Regular template should not use HTML editor");
        }

        /// <summary>
        /// Tests that query includes layout name.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateList_IncludesLayoutName()
        {
            // Arrange
            var layout = Db.Layouts.First();
            Db.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<p>Content</p>",
                LayoutId = layout.Id
            });
            await Db.SaveChangesAsync();

            var query = new GetTemplateListQuery
            {
                PageNo = 0,
                PageSize = 10
            };

            var handler = new GetTemplateListQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetTemplateListQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(layout.LayoutName, result.Data.Templates[0].LayoutName);
        }

        /// <summary>
        /// Tests that query handles null descriptions.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateList_HandlesNullDescription()
        {
            // Arrange
            var layout = Db.Layouts.First();
            Db.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Description = null,
                Content = "<p>Content</p>",
                LayoutId = layout.Id
            });
            await Db.SaveChangesAsync();

            var query = new GetTemplateListQuery
            {
                PageNo = 0,
                PageSize = 10
            };

            var handler = new GetTemplateListQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetTemplateListQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var testTemplate = result.Data.Templates.FirstOrDefault(t => t.Title == "Test Template");
            Assert.IsNotNull(testTemplate, "Test Template should be in the result");
            Assert.AreEqual(string.Empty, testTemplate.Description, "Null description should be empty string");
        }

        /// <summary>
        /// Tests that handler throws ArgumentNullException when query is null.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateList_ThrowsWhenQueryIsNull()
        {
            // Arrange
            var handler = new GetTemplateListQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetTemplateListQueryHandler>());

            // Act & Assert
            try
            {
                await handler.HandleAsync(null);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }
    }
}
