// <copyright file="TemplateCatalogServiceTests.cs" company="Moonrise Software, LLC">
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
    using Cosmos.DynamicConfig;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Services.Templates;

    /// <summary>
    /// Unit tests for <see cref="TemplateService"/>.
    /// Tests template CRUD, versioning, seeding, and application to articles.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class TemplateCatalogServiceTests : SkyCmsTestBase
    {
        private Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> environmentMock;
        private Mock<ILogger<TemplateService>> loggerMock;
        private Mock<IDynamicConfigurationProvider> dynamicConfigProviderMock; // ✅ Add this
        private TemplateService templateService;

        [TestInitialize]
        public void Setup()
        {
            InitializeTestContext(seedLayout: true);

            environmentMock = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            environmentMock.Setup(e => e.ContentRootPath).Returns(AppDomain.CurrentDomain.BaseDirectory);
            
            loggerMock = new Mock<ILogger<TemplateService>>();
            
            // Create mock for IDynamicConfigurationProvider
            dynamicConfigProviderMock = new Mock<IDynamicConfigurationProvider>();

            // For single-tenant tests, pass null for the dynamic config provider
            // This enables single-tenant mode which uses Guid.Empty as the tenant sentinel
            templateService = new TemplateService(
                environmentMock.Object,
                loggerMock.Object,
                Db,
                null); // Single-tenant mode
            
            // Clear the static _seededTenants cache between tests to avoid cross-test pollution
            ClearSeededTenantsCache();
        }
        
        /// <summary>
        /// Clears the static SeededTenants cache in TemplateService using reflection.
        /// This ensures tests don't affect each other via shared static state.
        /// </summary>
        private void ClearSeededTenantsCache()
        {
            var seededTenantsField = typeof(TemplateService).GetField(
                "SeededTenants", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (seededTenantsField != null)
            {
                var dictionary = seededTenantsField.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<Guid, bool>;
                dictionary?.Clear();
            }
        }

        #region Template Retrieval Tests

        /// <summary>
        /// Tests that GetAllTemplatesAsync returns standard templates.
        /// </summary>
        [TestMethod]
        public async Task GetAllTemplatesAsync_ReturnsStandardTemplates()
        {
            // Act
            var templates = await templateService.GetAllTemplatesAsync();

            // Assert
            Assert.IsNotNull(templates);
            Assert.IsTrue(templates.Count >= 2, "Should have at least 2 standard templates");
            Assert.IsTrue(templates.Any(t => t.Key == "blog-stream"));
            Assert.IsTrue(templates.Any(t => t.Key == "blog-post"));
        }

        /// <summary>
        /// Tests that templates are cached after first retrieval.
        /// </summary>
        [TestMethod]
        public async Task GetAllTemplatesAsync_CachesResults()
        {
            // Act
            var templates1 = await templateService.GetAllTemplatesAsync();
            var templates2 = await templateService.GetAllTemplatesAsync();

            // Assert
            Assert.AreSame(templates1, templates2, "Should return cached instance");
        }

        /// <summary>
        /// Tests getting templates by category.
        /// </summary>
        [TestMethod]
        public async Task GetTemplatesByCategoryAsync_FiltersByCategory()
        {
            // Act
            var blogTemplates = await templateService.GetTemplatesByCategoryAsync("Blog");

            // Assert
            Assert.IsNotNull(blogTemplates);
            Assert.IsTrue(blogTemplates.All(t => t.Category == "Blog"));
            Assert.IsTrue(blogTemplates.Count >= 2);
        }

        /// <summary>
        /// Tests getting template by key.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateByKeyAsync_ReturnsCorrectTemplate()
        {
            // Act
            var template = await templateService.GetTemplateByKeyAsync("blog-post");

            // Assert
            Assert.IsNotNull(template);
            Assert.AreEqual("blog-post", template.Key);
            Assert.AreEqual("Blog Post", template.Name);
        }

        /// <summary>
        /// Tests that getting non-existent template returns null.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateByKeyAsync_NonExistent_ReturnsNull()
        {
            // Act
            var template = await templateService.GetTemplateByKeyAsync("non-existent-key");

            // Assert
            Assert.IsNull(template);
        }

        #endregion

        #region Template Search Tests

        /// <summary>
        /// Tests searching templates by term.
        /// </summary>
        [TestMethod]
        public async Task SearchTemplatesAsync_FindsMatchingTemplates()
        {
            // Act
            var results = await templateService.SearchTemplatesAsync("blog");

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count >= 2);
            Assert.IsTrue(results.All(t => 
                t.Name.Contains("blog", StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains("blog", StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains("blog", StringComparison.OrdinalIgnoreCase))));
        }

        /// <summary>
        /// Tests that empty search term returns all templates.
        /// </summary>
        [TestMethod]
        public async Task SearchTemplatesAsync_EmptyTerm_ReturnsAll()
        {
            // Act
            var results = await templateService.SearchTemplatesAsync("");

            // Assert
            var allTemplates = await templateService.GetAllTemplatesAsync();
            Assert.AreEqual(allTemplates.Count, results.Count);
        }

        #endregion

        #region Template Seeding Tests

        /// <summary>
        /// Tests that EnsureDefaultTemplatesExistAsync creates templates.
        /// </summary>
        [TestMethod]
        public async Task EnsureDefaultTemplatesExist_CreatesTemplates()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            Assert.IsNotNull(defaultLayout, "Must have default layout");

            // Act
            await templateService.EnsureDefaultTemplatesExistAsync();

            // Assert
            var templates = await Db.Templates.ToListAsync();
            Assert.IsTrue(templates.Count >= 2);
            Assert.IsTrue(templates.Any(t => t.PageType == "blog-stream"));
            Assert.IsTrue(templates.Any(t => t.PageType == "blog-post"));
        }

        /// <summary>
        /// Tests that seeding is idempotent (doesn't create duplicates).
        /// </summary>
        [TestMethod]
        public async Task EnsureDefaultTemplatesExist_Idempotent_NoDuplicates()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            Assert.IsNotNull(defaultLayout);

            // Act
            await templateService.EnsureDefaultTemplatesExistAsync();
            var count1 = await Db.Templates.CountAsync();
            
            await templateService.EnsureDefaultTemplatesExistAsync(); // Second call
            var count2 = await Db.Templates.CountAsync();

            // Assert
            Assert.AreEqual(count1, count2, "Should not create duplicates");
        }

        /// <summary>
        /// Tests that templates use correct layout.
        /// </summary>
        [TestMethod]
        public async Task EnsureDefaultTemplatesExist_UsesDefaultLayout()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);

            // Act
            await templateService.EnsureDefaultTemplatesExistAsync();

            // Assert
            var templates = await Db.Templates.ToListAsync();
            Assert.IsTrue(templates.All(t => t.LayoutId == defaultLayout.Id));
        }

        /// <summary>
        /// Tests that seeding without layout logs warning and returns.
        /// </summary>
        [TestMethod]
        public async Task EnsureDefaultTemplatesExist_NoLayout_LogsWarning()
        {
            // Arrange - Set up a valid tenant ID so the method doesn't exit early
            var testTenantId = Guid.NewGuid();
            dynamicConfigProviderMock
                .Setup(p => p.GetCurrentTenantIdAsync())
                .ReturnsAsync(testTenantId);
            
            // Arrange - Remove any layouts
            var layouts = await Db.Layouts.ToListAsync();
            Db.Layouts.RemoveRange(layouts);
            await Db.SaveChangesAsync();

            // Act
            await templateService.EnsureDefaultTemplatesExistAsync();

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No default layout")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        #endregion

        #region Page Design Version Tests

        /// <summary>
        /// Tests getting design versions for a template.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateDesignVersionsAsync_ReturnsVersions()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            await templateService.EnsureDefaultTemplatesExistAsync();

            // Act
            var versions = await templateService.GetTemplateDesignVersionsAsync("blog-post");

            // Assert
            Assert.IsNotNull(versions);
            Assert.AreEqual(1, versions.Count); // Should create default version
            Assert.AreEqual(1, versions[0].Version);
        }

        /// <summary>
        /// Tests that versions are ordered descending.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateDesignVersionsAsync_OrdersDescending()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            await templateService.EnsureDefaultTemplatesExistAsync();
            
            var template = await Db.Templates.FirstAsync(t => t.PageType == "blog-post");
            
            // Create initial version
            Db.PageDesignVersions.Add(new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                PageType = "blog-post",
                Version = 1,
                Content = "<div>Version 1</div>",
                Title = "V1",
                Published = DateTimeOffset.UtcNow
            });
            
            // Create additional version
            Db.PageDesignVersions.Add(new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                PageType = "blog-post",
                Version = 2,
                Content = "<div>Version 2</div>",
                Title = "V2",
                Published = DateTimeOffset.UtcNow
            });
            await Db.SaveChangesAsync();

            // Act
            var versions = await templateService.GetTemplateDesignVersionsAsync("blog-post");

            // Assert
            Assert.AreEqual(2, versions.Count);
            Assert.IsTrue(versions[0].Version > versions[1].Version);
        }

        /// <summary>
        /// Tests getting version for edit creates new if published.
        /// </summary>
        [TestMethod]
        public async Task GetVersionForEdit_PublishedVersion_CreatesNew()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            await templateService.EnsureDefaultTemplatesExistAsync();
            
            var versions = await templateService.GetTemplateDesignVersionsAsync("blog-post");
            var publishedVersion = versions.First();
            Assert.IsNotNull(publishedVersion.Published);

            // Act
            var editVersion = await templateService.GetVersionForEdit("blog-post");

            // Assert
            Assert.IsNotNull(editVersion);
            Assert.AreEqual(publishedVersion.Version + 1, editVersion.Version);
            Assert.IsNull(editVersion.Published, "Edit version should be unpublished");
        }

        /// <summary>
        /// Tests getting version for edit returns draft if exists.
        /// </summary>
        [TestMethod]
        public async Task GetVersionForEdit_DraftExists_ReturnsDraft()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            await templateService.EnsureDefaultTemplatesExistAsync();
            
            // Create draft version
            var template = await Db.Templates.FirstAsync(t => t.PageType == "blog-post");
            var draftVersion = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                PageType = "blog-post",
                Version = 2,
                Content = "<div>Draft</div>",
                Title = "Draft",
                Published = null // Unpublished
            };
            Db.PageDesignVersions.Add(draftVersion);
            await Db.SaveChangesAsync();

            // Act
            var editVersion = await templateService.GetVersionForEdit("blog-post");

            // Assert
            Assert.AreEqual(draftVersion.Id, editVersion.Id);
            Assert.IsNull(editVersion.Published);
        }

        #endregion

        #region Save and Publish Tests

        /// <summary>
        /// Tests saving a new page design version.
        /// </summary>
        [TestMethod]
        public async Task Save_NewVersion_AddsToDatabase()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            await templateService.EnsureDefaultTemplatesExistAsync();
            var template = await Db.Templates.FirstAsync(t => t.PageType == "blog-post");

            var newVersion = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                PageType = "blog-post",
                Version = 10,
                Content = "<div>New content</div>",
                Title = "New Version",
                Description = "Test"
            };

            // Act
            await templateService.Save(newVersion);

            // Assert
            var saved = await Db.PageDesignVersions.FindAsync(newVersion.Id);
            Assert.IsNotNull(saved);
            Assert.AreEqual("<div>New content</div>", saved.Content);
        }

        /// <summary>
        /// Tests saving updates existing version.
        /// </summary>
        [TestMethod]
        public async Task Save_ExistingVersion_Updates()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            await templateService.EnsureDefaultTemplatesExistAsync();
            
            var versions = await templateService.GetTemplateDesignVersionsAsync("blog-post");
            var version = versions.First();
            version.Content = "<div>Updated content</div>";

            // Act
            await templateService.Save(version);

            // Assert
            var updated = await Db.PageDesignVersions.FindAsync(version.Id);
            Assert.AreEqual("<div>Updated content</div>", updated.Content);
        }

        /// <summary>
        /// Tests publishing a version.
        /// </summary>
        [TestMethod]
        public async Task Publish_Version_SetsPublishedAndUpdatesTemplate()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            await templateService.EnsureDefaultTemplatesExistAsync();
            
            var template = await Db.Templates.FirstAsync(t => t.PageType == "blog-post");
            var version = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                PageType = "blog-post",
                Version = 2,
                Content = "<div>Version 2 content</div>",
                Title = "V2",
                Description = "Version 2",
                Published = null
            };
            Db.PageDesignVersions.Add(version);
            await Db.SaveChangesAsync();

            // Act
            await templateService.Publish(version);

            // Assert
            var published = await Db.PageDesignVersions.FindAsync(version.Id);
            Assert.IsNotNull(published.Published);
            
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.AreEqual("<div>Version 2 content</div>", updatedTemplate.Content);
        }

        /// <summary>
        /// Tests publishing unpublishes other versions.
        /// </summary>
        [TestMethod]
        public async Task Publish_UnpublishesOtherVersions()
        {
            // Arrange
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            await templateService.EnsureDefaultTemplatesExistAsync();
            
            var template = await Db.Templates.FirstAsync(t => t.PageType == "blog-post");
            
            // Create version 1 and publish it
            var version1 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                PageType = "blog-post",
                Version = 1,
                Content = "<div>V1</div>",
                Title = "V1",
                Published = DateTimeOffset.UtcNow
            };
            Db.PageDesignVersions.Add(version1);
            
            // Create version 2 (unpublished)
            var version2 = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                PageType = "blog-post",
                Version = 2,
                Content = "<div>V2</div>",
                Title = "V2",
                Published = null
            };
            Db.PageDesignVersions.Add(version2);
            await Db.SaveChangesAsync();

            // Act - Publish version 2, which should unpublish version 1
            await templateService.Publish(version2);

            // Assert
            var version1Updated = await Db.PageDesignVersions.FindAsync(version1.Id);
            Assert.IsNull(version1Updated.Published, "Version 1 should be unpublished");
            
            var version2Published = await Db.PageDesignVersions.FindAsync(version2.Id);
            Assert.IsNotNull(version2Published.Published, "Version 2 should be published");
        }

        #endregion
    }
}