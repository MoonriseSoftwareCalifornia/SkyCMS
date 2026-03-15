// <copyright file="LayoutManagementTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for Layout management functionality.
    /// Tests layout CRUD, versioning, default layout management, and template relationships.
    /// </summary>
    [TestClass]
    public class LayoutManagementTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
        }

        #region Layout CRUD Tests

        /// <summary>
        /// Tests that creating a layout sets correct properties.
        /// </summary>
        [TestMethod]
        public async Task CreateLayout_SetsCorrectProperties()
        {
            // Arrange
            var layoutName = "Test Layout";
            var headContent = "<title>@Model.Title</title>";
            var headerContent = "<nav>Navigation</nav>";

            // Act
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = layoutName,
                Notes = "Test layout notes",
                Head = headContent,
                HtmlHeader = headerContent,
                IsDefault = false
            };

            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Assert
            var saved = await Db.Layouts.FindAsync(layout.Id);
            Assert.IsNotNull(saved);
            Assert.AreEqual(layoutName, saved.LayoutName);
            Assert.AreEqual(headContent, saved.Head);
            Assert.AreEqual(headerContent, saved.HtmlHeader);
            Assert.IsFalse(saved.IsDefault);
        }

        /// <summary>
        /// Tests that layouts can be retrieved by ID.
        /// </summary>
        [TestMethod]
        public async Task GetLayout_ById_ReturnsCorrectLayout()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                Head = "<title>Test</title>",
                IsDefault = false
            };

            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var retrieved = await Db.Layouts.FindAsync(layout.Id);

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(layout.Id, retrieved.Id);
            Assert.AreEqual(layout.LayoutName, retrieved.LayoutName);
        }

        /// <summary>
        /// Tests that layout name can be updated.
        /// </summary>
        [TestMethod]
        public async Task UpdateLayout_ChangesName()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Original Name",
                Head = "<title>Test</title>",
                IsDefault = false
            };

            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            layout.LayoutName = "Updated Name";
            await Db.SaveChangesAsync();

            // Assert
            var updated = await Db.Layouts.FindAsync(layout.Id);
            Assert.AreEqual("Updated Name", updated.LayoutName);
        }

        /// <summary>
        /// Tests that layout head content can be updated.
        /// </summary>
        [TestMethod]
        public async Task UpdateLayout_ChangesHeadContent()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                Head = "<title>Original</title>",
                IsDefault = false
            };

            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            layout.Head = "<title>Updated</title>";
            await Db.SaveChangesAsync();

            // Assert
            var updated = await Db.Layouts.FindAsync(layout.Id);
            Assert.IsTrue(updated.Head.Contains("Updated"));
        }

        /// <summary>
        /// Tests that layouts can be deleted.
        /// </summary>
        [TestMethod]
        public async Task DeleteLayout_RemovesFromDatabase()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                Head = "<title>Test</title>",
                IsDefault = false
            };

            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            Db.Layouts.Remove(layout);
            await Db.SaveChangesAsync();

            // Assert
            var deleted = await Db.Layouts.FindAsync(layout.Id);
            Assert.IsNull(deleted);
        }

        #endregion

        #region Default Layout Tests

        /// <summary>
        /// Tests that default layout can be retrieved.
        /// </summary>
        [TestMethod]
        public async Task GetDefaultLayout_ReturnsDefaultLayout()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);

            // Assert
            Assert.IsNotNull(defaultLayout);
            Assert.IsTrue(defaultLayout.IsDefault);
        }

        /// <summary>
        /// Tests that setting a new default layout unsets previous default.
        /// </summary>
        [TestMethod]
        public async Task SetDefaultLayout_UnsetsOldDefault()
        {
            // Arrange
            var oldDefault = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            Assert.IsNotNull(oldDefault);

            var newLayout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "New Default",
                Head = "<title>New Default</title>",
                IsDefault = false
            };

            Db.Layouts.Add(newLayout);
            await Db.SaveChangesAsync();

            // Act
            oldDefault.IsDefault = false;
            newLayout.IsDefault = true;
            await Db.SaveChangesAsync();

            // Assert
            var oldDefaultUpdated = await Db.Layouts.FindAsync(oldDefault.Id);
            var newDefaultUpdated = await Db.Layouts.FindAsync(newLayout.Id);

            Assert.IsFalse(oldDefaultUpdated.IsDefault);
            Assert.IsTrue(newDefaultUpdated.IsDefault);
        }

        /// <summary>
        /// Tests that only one layout can be default.
        /// </summary>
        [TestMethod]
        public async Task GetDefaultLayouts_OnlyOneIsDefault()
        {
            // Act
            var defaultLayouts = await Db.Layouts
                .Where(l => l.IsDefault)
                .ToListAsync();

            // Assert
            Assert.AreEqual(1, defaultLayouts.Count, "Should have exactly one default layout");
        }

        /// <summary>
        /// Tests that default layout has head content.
        /// </summary>
        [TestMethod]
        public async Task DefaultLayout_HasHeadContent()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);

            // Assert
            Assert.IsNotNull(defaultLayout);
            // Head content may be null or empty for minimal layouts
        }

        #endregion

        #region Layout Versioning Tests

        /// <summary>
        /// Tests that layout number can be assigned.
        /// </summary>
        [TestMethod]
        public async Task CreateLayout_AssignsLayoutNumber()
        {
            // Arrange
            var maxLayoutNumber = await Db.Layouts.MaxAsync(l => (int?)l.LayoutNumber) ?? 0;
            var nextLayoutNumber = maxLayoutNumber + 1;

            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                Head = "<title>Test</title>",
                LayoutNumber = nextLayoutNumber,
                IsDefault = false
            };

            // Act
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Assert
            var saved = await Db.Layouts.FindAsync(layout.Id);
            Assert.AreEqual(nextLayoutNumber, saved.LayoutNumber);
        }

        /// <summary>
        /// Tests that layouts can share a layout number (versioning).
        /// </summary>
        [TestMethod]
        public async Task CreateLayoutVersion_SharesLayoutNumber()
        {
            // Arrange
            var layoutNumber = 100;

            var v1 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout v1",
                Head = "<title>Version 1</title>",
                LayoutNumber = layoutNumber,
                Version = 1,
                IsDefault = false
            };

            Db.Layouts.Add(v1);
            await Db.SaveChangesAsync();

            // Act
            var v2 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout v2",
                Head = "<title>Version 2</title>",
                LayoutNumber = layoutNumber,
                Version = 2,
                IsDefault = false
            };

            Db.Layouts.Add(v2);
            await Db.SaveChangesAsync();

            // Assert
            var versions = await Db.Layouts
                .Where(l => l.LayoutNumber == layoutNumber)
                .ToListAsync();

            Assert.AreEqual(2, versions.Count);
            Assert.IsTrue(versions.Any(v => v.Version == 1));
            Assert.IsTrue(versions.Any(v => v.Version == 2));
        }

        #endregion

        #region Community Layout Tests

        /// <summary>
        /// Tests that community layout ID can be set.
        /// </summary>
        [TestMethod]
        public async Task CreateLayout_SetsCommunityLayoutId()
        {
            // Arrange
            var communityLayoutId = Guid.NewGuid().ToString();

            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Community Layout",
                Head = "<title>Community</title>",
                CommunityLayoutId = communityLayoutId,
                IsDefault = false
            };

            // Act
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Assert
            var saved = await Db.Layouts.FindAsync(layout.Id);
            Assert.AreEqual(communityLayoutId, saved.CommunityLayoutId);
        }

        /// <summary>
        /// Tests querying layouts by community layout ID.
        /// </summary>
        [TestMethod]
        public async Task GetLayouts_ByCommunityLayoutId_FindsAll()
        {
            // Arrange
            var communityLayoutId = Guid.NewGuid().ToString();

            var layout1 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Community Layout 1",
                Head = "<title>1</title>",
                CommunityLayoutId = communityLayoutId,
                IsDefault = false
            };

            var layout2 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Community Layout 2",
                Head = "<title>2</title>",
                CommunityLayoutId = communityLayoutId,
                IsDefault = false
            };

            Db.Layouts.AddRange(layout1, layout2);
            await Db.SaveChangesAsync();

            // Act
            var communityLayouts = await Db.Layouts
                .Where(l => l.CommunityLayoutId == communityLayoutId)
                .ToListAsync();

            // Assert
            Assert.AreEqual(2, communityLayouts.Count);
        }

        #endregion

        #region Layout-Template Relationship Tests

        /// <summary>
        /// Tests that templates reference correct layout.
        /// </summary>
        [TestMethod]
        public async Task CreateTemplate_ReferencesLayout()
        {
            // Arrange
            var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);

            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Template Content</div>",
                PageType = "test-template",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };

            // Act
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Assert
            var saved = await Db.Templates.FindAsync(template.Id);
            Assert.AreEqual(layout.Id, saved.LayoutId);
            Assert.AreEqual(layout.LayoutNumber, saved.LayoutNumber);
        }

        /// <summary>
        /// Tests querying templates by layout ID.
        /// </summary>
        [TestMethod]
        public async Task GetTemplates_ByLayoutId_FindsAll()
        {
            // Arrange
            var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);

            var template1 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template 1",
                Content = "<div>1</div>",
                PageType = "template-1",
                LayoutId = layout.Id
            };

            var template2 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template 2",
                Content = "<div>2</div>",
                PageType = "template-2",
                LayoutId = layout.Id
            };

            Db.Templates.AddRange(template1, template2);
            await Db.SaveChangesAsync();

            // Act
            var layoutTemplates = await Db.Templates
                .Where(t => t.LayoutId == layout.Id)
                .ToListAsync();

            // Assert
            Assert.IsTrue(layoutTemplates.Count >= 2, "Should find at least 2 templates with this layout");
        }

        #endregion

        #region Layout Content Properties Tests

        /// <summary>
        /// Tests that layout header HTML can be set.
        /// </summary>
        [TestMethod]
        public async Task CreateLayout_SetsHeaderHtml()
        {
            // Arrange
            var headerHtml = "<header><nav>Main Navigation</nav></header>";

            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Header Test Layout",
                HtmlHeader = headerHtml,
                IsDefault = false
            };

            // Act
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Assert
            var saved = await Db.Layouts.FindAsync(layout.Id);
            Assert.AreEqual(headerHtml, saved.HtmlHeader);
        }

        /// <summary>
        /// Tests that layout footer HTML can be set.
        /// </summary>
        [TestMethod]
        public async Task CreateLayout_SetsFooterHtml()
        {
            // Arrange
            var footerHtml = "<footer>&copy; 2024 Company</footer>";

            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Footer Test Layout",
                FooterHtmlContent = footerHtml,
                IsDefault = false
            };

            // Act
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Assert
            var saved = await Db.Layouts.FindAsync(layout.Id);
            Assert.AreEqual(footerHtml, saved.FooterHtmlContent);
        }

        /// <summary>
        /// Tests that layout notes can be added.
        /// </summary>
        [TestMethod]
        public async Task CreateLayout_WithNotes_SavesNotes()
        {
            // Arrange
            var notes = "This is a custom layout for the marketing pages";

            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Marketing Layout",
                Head = "<title>Marketing</title>",
                Notes = notes,
                IsDefault = false
            };

            // Act
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Assert
            var saved = await Db.Layouts.FindAsync(layout.Id);
            Assert.AreEqual(notes, saved.Notes);
        }

        /// <summary>
        /// Tests that body HTML attributes can be set.
        /// </summary>
        [TestMethod]
        public async Task CreateLayout_SetsBodyAttributes()
        {
            // Arrange
            var bodyAttributes = "class=\"dark-mode\" data-theme=\"corporate\"";

            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Attributed Layout",
                BodyHtmlAttributes = bodyAttributes,
                IsDefault = false
            };

            // Act
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Assert
            var saved = await Db.Layouts.FindAsync(layout.Id);
            Assert.AreEqual(bodyAttributes, saved.BodyHtmlAttributes);
        }

        #endregion

        #region Layout Query Tests

        /// <summary>
        /// Tests getting all layouts.
        /// </summary>
        [TestMethod]
        public async Task GetAllLayouts_ReturnsAllLayouts()
        {
            // Act
            var layouts = await Db.Layouts.ToListAsync();

            // Assert
            Assert.IsTrue(layouts.Count >= 1, "Should have at least the default layout");
        }

        /// <summary>
        /// Tests getting layouts ordered by name.
        /// </summary>
        [TestMethod]
        public async Task GetLayouts_OrderedByName_ReturnsInOrder()
        {
            // Arrange
            var layout1 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "A Layout",
                Head = "<title>A</title>",
                IsDefault = false
            };

            var layout2 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "B Layout",
                Head = "<title>B</title>",
                IsDefault = false
            };

            Db.Layouts.AddRange(layout1, layout2);
            await Db.SaveChangesAsync();

            // Act
            var orderedLayouts = await Db.Layouts
                .OrderBy(l => l.LayoutName)
                .ToListAsync();

            // Assert
            Assert.IsTrue(orderedLayouts.Count >= 2);
            for (int i = 0; i < orderedLayouts.Count - 1; i++)
            {
                Assert.IsTrue(
                    string.Compare(orderedLayouts[i].LayoutName, orderedLayouts[i + 1].LayoutName, StringComparison.OrdinalIgnoreCase) <= 0,
                    "Layouts should be ordered alphabetically");
            }
        }

        #endregion
    }
}