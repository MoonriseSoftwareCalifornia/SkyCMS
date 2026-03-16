// <copyright file="TitleChangeServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Titles
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Domain.Events;
    using Cosmos.Common.Services.BlogPublishing;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Redirects;
    using Sky.Editor.Services.ReservedPaths;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Titles;
    using Sky.Tests.Editor.Features.Articles;

    [TestClass]
    public class TitleChangeServiceTests : ArticleTestBase
    {
        private TitleChangeService _service;
        private SlugService _slugService;  // Use real SlugService
        private Mock<IReservedPaths> _mockReservedPaths;
        private Mock<IBlogStreamRenderingService> _mockBlogRenderingService;
        private Mock<IRedirectService> _mockRedirectService;
        private Mock<IDomainEventDispatcher> _mockEventDispatcher;

        [TestInitialize]
        public new void TestInitialize()
        {
            base.TestInitialize();

            // Create real SlugService (it's a simple utility with no dependencies)
            _slugService = new SlugService();
            
            // Create local mocks
            _mockReservedPaths = new Mock<IReservedPaths>();
            _mockBlogRenderingService = new Mock<IBlogStreamRenderingService>();
            _mockRedirectService = new Mock<IRedirectService>();
            _mockEventDispatcher = new Mock<IDomainEventDispatcher>();

            // ✅ FIX: Setup GetReservedPaths to return a list of reserved paths
            var reservedPathsList = new System.Collections.Generic.List<Sky.Cms.Models.ReservedPath>
            {
                new Sky.Cms.Models.ReservedPath { Path = "api", CosmosRequired = true, Notes = "API routes" },
                new Sky.Cms.Models.ReservedPath { Path = "editor", CosmosRequired = true, Notes = "Editor routes" },
                new Sky.Cms.Models.ReservedPath { Path = "setup", CosmosRequired = true, Notes = "Setup routes" },
                new Sky.Cms.Models.ReservedPath { Path = "pub", CosmosRequired = true, Notes = "Publisher routes" }
            };

            _mockReservedPaths
                .Setup(x => x.GetReservedPaths())
                .ReturnsAsync(reservedPathsList);

            // Also setup IsReserved for individual checks (returns false by default)
            _mockReservedPaths
                .Setup(x => x.IsReserved(It.IsAny<string>()))
                .ReturnsAsync(false);

            var titleChangeContext = new Sky.Editor.Services.Titles.TitleChangeContext(
                DbContext,
                MockClock.Object,
                _mockEventDispatcher.Object);

            _service = new TitleChangeService(
                titleChangeContext,
                _slugService,
                _mockRedirectService.Object,
                MockPublishingService.Object,
                _mockReservedPaths.Object,
                _mockBlogRenderingService.Object,
                Mock.Of<ILogger<TitleChangeService>>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            DbContext?.Dispose();
        }

        /// <summary>
        /// Verifies that validating a new, unique title returns true.
        /// </summary>
        [TestMethod]
        public async Task ValidateTitle_NewTitle_ReturnsTrue()
        {
            // Arrange
            await SeedArticleAsync("Existing Article", 1);
            
            // Act
            var result = await _service.ValidateTitle("New Unique Title", null);

            // Assert
            Assert.IsTrue(result, "New unique title should be valid");
        }

        /// <summary>
        /// Verifies that validating a duplicate title returns false.
        /// </summary>
        [TestMethod]
        public async Task ValidateTitle_DuplicateTitle_ReturnsFalse()
        {
            // Arrange
            await SeedArticleAsync("Existing Article", 1);
            
            // Act - try to use the exact same title for a different article
            var result = await _service.ValidateTitle("Existing Article", null);

            // Assert
            Assert.IsFalse(result, "Duplicate title should not be valid");
        }

        /// <summary>
        /// Ensures that titles matching reserved paths are considered invalid.
        /// </summary>
        [TestMethod]
        public async Task ValidateTitle_ReservedPath_ReturnsFalse()
        {
            // Arrange - Setup specific reserved paths
            _mockReservedPaths
                .Setup(x => x.IsReserved("api"))
                .ReturnsAsync(true);  // ✅ FIXED: Use ReturnsAsync
            
            _mockReservedPaths
                .Setup(x => x.IsReserved("editor"))
                .ReturnsAsync(true);  // ✅ FIXED: Use ReturnsAsync
            
            _mockReservedPaths
                .Setup(x => x.IsReserved("setup"))
                .ReturnsAsync(true);  // ✅ FIXED: Use ReturnsAsync

            // Act & Assert - Test common reserved paths
            var apiResult = await _service.ValidateTitle("api", null);
            Assert.IsFalse(apiResult, "'api' should be reserved and validation should fail");

            var editorResult = await _service.ValidateTitle("editor", null);
            Assert.IsFalse(editorResult, "'editor' should be reserved and validation should fail");

            var setupResult = await _service.ValidateTitle("setup", null);
            Assert.IsFalse(setupResult, "'setup' should be reserved and validation should fail");

            // Test that normal titles pass validation
            var normalResult = await _service.ValidateTitle("about-us", null);
            Assert.IsTrue(normalResult, "Normal title should not be reserved");
        }

        /// <summary>
        /// Confirms that re-saving the same article (same article number) allows the same title.
        /// </summary>
        [TestMethod]
        public async Task ValidateTitle_SameArticleNumber_AllowsSameTitle()
        {
            // Arrange
            var article = await SeedArticleAsync("Existing Title", 1);
            
            // Act - Re-save the same article with the same title
            var result = await _service.ValidateTitle("Existing Title", articleNumber: 1);

            // Assert
            Assert.IsTrue(result, "Should allow keeping the same title when editing the same article");
        }

        /// <summary>
        /// Generates a slug from the article title and verifies the expected URL path.
        /// </summary>
        [TestMethod]
        public void BuildArticleUrl_GeneratesCorrectSlug()
        {
            // Arrange
            var article = new Cosmos.Common.Data.Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                Title = "About Our Company",
                UrlPath = "will-be-overwritten",
                Content = "<div>Content</div>",
                StatusCode = (int)StatusCodeEnum.Active,
                VersionNumber = 1,
                Updated = TestNow,
                UserId = Guid.NewGuid().ToString()
            };

            // Act
            var urlPath = _service.BuildArticleUrl(article);

            // Assert
            Assert.IsNotNull(urlPath, "URL path should be generated");
            Assert.AreEqual("about-our-company", urlPath, 
                "Should generate slug from title");
        }

        /// <summary>
        /// When an article title changes, creates a redirect from the old URL to the new URL.
        /// </summary>
        [TestMethod]
        public async Task HandleTitleChangeAsync_CreatesRedirect()
        {
            // Arrange
            var article = await SeedArticleAsync("New Title", 1, urlPath: "new-title", published: true);
            var oldTitle = "Old Title";
            var oldUrlPath = "old-title";
            var userId = Guid.NewGuid();

            // Setup redirect service mock to capture the redirect creation
            string capturedFromUrl = null;
            string capturedToUrl = null;
            Guid capturedUserId = Guid.Empty;
            
            _mockRedirectService
                .Setup(x => x.CreateOrUpdateRedirectAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>()))
                .Callback<string, string, Guid>((from, to, user) =>
                {
                    capturedFromUrl = from;
                    capturedToUrl = to;
                    capturedUserId = user;
                })
                .ReturnsAsync((Cosmos.Common.Data.Article)null);  // Return null (redirect created)

            // Act
            await _service.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);

            // Assert
            // Verify redirect service was called
            _mockRedirectService.Verify(
                x => x.CreateOrUpdateRedirectAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>()),
                Times.Once,
                "Should create redirect from old URL to new URL");

            Assert.AreEqual("old-title", capturedFromUrl, 
                "Redirect should be from old URL path");
            Assert.AreEqual("new-title", capturedToUrl, 
                "Redirect should be to new URL path");
        }
    }
}