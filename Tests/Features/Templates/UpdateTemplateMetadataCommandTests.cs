// <copyright file="UpdateTemplateMetadataCommandTests.cs" company="Moonrise Software, LLC">
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
    using Sky.Editor.Features.Templates.UpdateMetadata;

    /// <summary>
    /// Tests for <see cref="UpdateTemplateMetadataCommand"/> and <see cref="UpdateTemplateMetadataHandler"/>.
    /// </summary>
    [TestClass]
    public class UpdateTemplateMetadataCommandTests : SkyCmsTestBase
    {
        /// <summary>
        /// Initialize test context.
        /// </summary>
        [TestInitialize]
        public void Setup()
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
        /// Tests that updating template metadata succeeds with valid data.
        /// </summary>
        [TestMethod]
        public async Task UpdateMetadata_SucceedsWithValidData()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Description = "Original Description",
                Content = "<p>Template content</p>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var command = new UpdateTemplateMetadataCommand
            {
                TemplateId = template.Id,
                Title = "Updated Title",
                Description = "Updated Description"
            };

            var handler = new UpdateTemplateMetadataHandler(Db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTemplateMetadataHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.IsNotNull(result.Data, "Result should contain template");
            Assert.AreEqual("Updated Title", result.Data.Title);
            Assert.AreEqual("Updated Description", result.Data.Description);

            // Verify in database
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.AreEqual("Updated Title", updatedTemplate.Title);
            Assert.AreEqual("Updated Description", updatedTemplate.Description);
            Assert.AreEqual("<p>Template content</p>", updatedTemplate.Content, "Content should remain unchanged");
        }

        /// <summary>
        /// Tests that updating metadata trims whitespace from title.
        /// </summary>
        [TestMethod]
        public async Task UpdateMetadata_TrimsWhitespaceFromTitle()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Description = "Original Description",
                Content = "<p>Content</p>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var command = new UpdateTemplateMetadataCommand
            {
                TemplateId = template.Id,
                Title = "   Whitespace Title   ",
                Description = "Description"
            };

            var handler = new UpdateTemplateMetadataHandler(Db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTemplateMetadataHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Whitespace Title", result.Data.Title, "Title should be trimmed");
        }

        /// <summary>
        /// Tests that updating metadata fails when template ID is empty.
        /// </summary>
        [TestMethod]
        public async Task UpdateMetadata_FailsWithEmptyTemplateId()
        {
            // Arrange
            var command = new UpdateTemplateMetadataCommand
            {
                TemplateId = Guid.Empty,
                Title = "New Title",
                Description = "New Description"
            };

            var handler = new UpdateTemplateMetadataHandler(Db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTemplateMetadataHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail with empty ID");
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("required"), "Error should mention ID is required");
        }

        /// <summary>
        /// Tests that updating metadata fails when title is empty.
        /// </summary>
        [TestMethod]
        public async Task UpdateMetadata_FailsWithEmptyTitle()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Description = "Description",
                Content = "<p>Content</p>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var command = new UpdateTemplateMetadataCommand
            {
                TemplateId = template.Id,
                Title = string.Empty,
                Description = "New Description"
            };

            var handler = new UpdateTemplateMetadataHandler(Db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTemplateMetadataHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail with empty title");
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("title"), "Error should mention title");
        }

        /// <summary>
        /// Tests that updating metadata fails when title is whitespace only.
        /// </summary>
        [TestMethod]
        public async Task UpdateMetadata_FailsWithWhitespaceOnlyTitle()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Description = "Description",
                Content = "<p>Content</p>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var command = new UpdateTemplateMetadataCommand
            {
                TemplateId = template.Id,
                Title = "   ",
                Description = "New Description"
            };

            var handler = new UpdateTemplateMetadataHandler(Db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTemplateMetadataHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail with whitespace-only title");
            Assert.IsNotNull(result.ErrorMessage);
        }

        /// <summary>
        /// Tests that updating metadata fails when template does not exist.
        /// </summary>
        [TestMethod]
        public async Task UpdateMetadata_FailsWhenTemplateNotFound()
        {
            // Arrange
            var command = new UpdateTemplateMetadataCommand
            {
                TemplateId = Guid.NewGuid(), // Non-existent ID
                Title = "New Title",
                Description = "New Description"
            };

            var handler = new UpdateTemplateMetadataHandler(Db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTemplateMetadataHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail when template not found");
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("not found"), "Error should mention template not found");
        }

        /// <summary>
        /// Tests that updating metadata allows empty description.
        /// </summary>
        [TestMethod]
        public async Task UpdateMetadata_AllowsEmptyDescription()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Description = "Original Description",
                Content = "<p>Content</p>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var command = new UpdateTemplateMetadataCommand
            {
                TemplateId = template.Id,
                Title = "New Title",
                Description = string.Empty
            };

            var handler = new UpdateTemplateMetadataHandler(Db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTemplateMetadataHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed with empty description");
            Assert.AreEqual(string.Empty, result.Data.Description);
        }

        /// <summary>
        /// Tests that updating metadata handles null description.
        /// </summary>
        [TestMethod]
        public async Task UpdateMetadata_HandlesNullDescription()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Description = "Original Description",
                Content = "<p>Content</p>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var command = new UpdateTemplateMetadataCommand
            {
                TemplateId = template.Id,
                Title = "New Title",
                Description = null
            };

            var handler = new UpdateTemplateMetadataHandler(Db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTemplateMetadataHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed with null description");
            Assert.AreEqual(string.Empty, result.Data.Description, "Null description should be converted to empty string");
        }

        /// <summary>
        /// Tests that updating metadata does not affect Content field.
        /// </summary>
        [TestMethod]
        public async Task UpdateMetadata_DoesNotAffectContent()
        {
            // Arrange
            var originalContent = "<div><p>Original complex content with markup</p></div>";
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Description = "Original Description",
                Content = originalContent,
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var command = new UpdateTemplateMetadataCommand
            {
                TemplateId = template.Id,
                Title = "Updated Title",
                Description = "Updated Description"
            };

            var handler = new UpdateTemplateMetadataHandler(Db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTemplateMetadataHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(originalContent, result.Data.Content, "Content should remain unchanged");

            // Verify in database
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.AreEqual(originalContent, updatedTemplate.Content, "Content in DB should remain unchanged");
        }

        /// <summary>
        /// Tests that handler throws ArgumentNullException when command is null.
        /// </summary>
        [TestMethod]
        public async Task UpdateMetadata_ThrowsWhenCommandIsNull()
        {
            // Arrange
            var handler = new UpdateTemplateMetadataHandler(Db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTemplateMetadataHandler>());

            // Act & Assert
            try
            {
                await handler.HandleAsync(null);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }
    }
}
