// <copyright file="LayoutImportServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Layouts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Cms.Data.Logic;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;
    using Newtonsoft.Json;
    using Sky.Editor.Services.Layouts;

    /// <summary>
    /// Unit tests for the <see cref="LayoutImportService"/> class.
    /// </summary>
    [TestClass]
    public class LayoutImportServiceTests : SkyCmsTestBase
    {
        private Mock<IHttpClientFactory> mockHttpClientFactory = null!;
        private Mock<ILogger<LayoutImportService>> mockLogger = null!;
        private IMemoryCache memoryCache = null!;
        private LayoutImportService service = null!;

        /// <summary>
        /// Initializes test context before each test.
        /// </summary>
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);

            mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockLogger = new Mock<ILogger<LayoutImportService>>();
            memoryCache = new MemoryCache(new MemoryCacheOptions());

            service = new LayoutImportService(
                mockHttpClientFactory.Object,
                memoryCache,
                mockLogger.Object);
        }

        /// <summary>
        /// Cleanup after each test.
        /// </summary>
        [TestCleanup]
        public async Task Cleanup()
        {
            memoryCache?.Dispose();
            await DisposeAsync();
        }

        #region GetCommunityCatalogAsync Tests

        /// <summary>
        /// Tests that GetCommunityCatalogAsync returns catalog when HTTP call succeeds.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityCatalogAsync_SuccessfulHttpCall_ReturnsCatalog()
        {
            // Arrange
            var expectedCatalog = new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem
                    {
                        Id = "bs5-strt",
                        Name = "Bootstrap 5 Starter",
                        Description = "A basic Bootstrap 5 starter template",
                        License = "MIT"
                    },
                    new LayoutCatalogItem
                    {
                        Id = "tailwind-basic",
                        Name = "Tailwind Basic",
                        Description = "A basic Tailwind CSS template",
                        License = "MIT"
                    }
                }
            };

            var json = JsonConvert.SerializeObject(expectedCatalog);
            var mockHttpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.OK, json);
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetCommunityCatalogAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.LayoutCatalog);
            Assert.AreEqual(2, result.LayoutCatalog.Count);
            Assert.AreEqual("bs5-strt", result.LayoutCatalog[0].Id);
            Assert.AreEqual("Bootstrap 5 Starter", result.LayoutCatalog[0].Name);
            Assert.AreEqual("tailwind-basic", result.LayoutCatalog[1].Id);
        }

        /// <summary>
        /// Tests that GetCommunityCatalogAsync returns empty catalog when HTTP call fails.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityCatalogAsync_HttpCallFails_ReturnsEmptyCatalog()
        {
            // Arrange
            var mockHttpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty);
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetCommunityCatalogAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.LayoutCatalog);
            Assert.AreEqual(0, result.LayoutCatalog.Count);

            // Verify error was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        /// <summary>
        /// Tests that GetCommunityCatalogAsync caches the result.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityCatalogAsync_SuccessfulCall_CachesResult()
        {
            // Arrange
            var expectedCatalog = new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem { Id = "test-layout", Name = "Test Layout" }
                }
            };

            var json = JsonConvert.SerializeObject(expectedCatalog);
            var mockHttpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.OK, json);
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act - First call
            var result1 = await service.GetCommunityCatalogAsync();

            // Act - Second call (should use cache)
            var result2 = await service.GetCommunityCatalogAsync();

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(result1.LayoutCatalog.Count, result2.LayoutCatalog.Count);

            // Verify HTTP call was made only once (cached on second call)
            mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        /// <summary>
        /// Tests that GetCommunityCatalogAsync handles malformed JSON gracefully.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityCatalogAsync_MalformedJson_ReturnsEmptyCatalog()
        {
            // Arrange
            var malformedJson = "{ invalid json }";
            var mockHttpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.OK, malformedJson);
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetCommunityCatalogAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.LayoutCatalog);
            Assert.AreEqual(0, result.LayoutCatalog.Count);
        }

        #endregion

        #region GetCommunityLayoutAsync Tests

        /// <summary>
        /// Tests that GetCommunityLayoutAsync returns layout when found.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityLayoutAsync_ValidLayoutId_ReturnsLayout()
        {
            // Arrange
            var layoutId = "bs5-strt";
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem
                    {
                        Id = layoutId,
                        Name = "Bootstrap 5 Starter",
                        Description = "A basic template"
                    }
                }
            });

            var layoutHtml = @"
<!DOCTYPE html>
<html>
<head><title>Bootstrap 5 Layout</title></head>
<body class='bg-light'>
    <cosmos-layout-header>
        <header>Test Header</header>
    </cosmos-layout-header>
    <main>Content Area</main>
    <cosmos-layout-footer>
        <footer>Test Footer</footer>
    </cosmos-layout-footer>
</body>
</html>";

            var mockHttpMessageHandler = CreateSequentialMockHttpMessageHandler(
                new[] { catalogJson, layoutHtml });
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetCommunityLayoutAsync(layoutId, false);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(layoutId, result.CommunityLayoutId);
            Assert.AreEqual("Bootstrap 5 Starter", result.LayoutName);
            Assert.AreEqual("A basic template", result.Notes);
            Assert.IsFalse(result.IsDefault);
            Assert.IsNotNull(result.Head);
            Assert.IsTrue(result.Head.Contains("Bootstrap 5 Layout"));
            Assert.IsNotNull(result.HtmlHeader);
            Assert.IsTrue(result.HtmlHeader.Contains("Test Header"));
            Assert.IsNotNull(result.FooterHtmlContent);
            Assert.IsTrue(result.FooterHtmlContent.Contains("Test Footer"));
        }

        /// <summary>
        /// Tests that GetCommunityLayoutAsync throws exception for invalid layout ID.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityLayoutAsync_InvalidLayoutId_ThrowsException()
        {
            // Arrange
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>()
            });

            var mockHttpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.OK, catalogJson);
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act & Assert
            try
            {
                await service.GetCommunityLayoutAsync("non-existent-layout", false);
                Assert.Fail("Expected InvalidOperationException was not thrown.");
            }
            catch (InvalidOperationException)
            {
                // Test passes
            }
        }

        /// <summary>
        /// Tests that GetCommunityLayoutAsync handles HTTP errors when fetching layout HTML.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityLayoutAsync_LayoutHtmlFetchFails_ThrowsException()
        {
            // Arrange
            var layoutId = "test-layout";
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem { Id = layoutId, Name = "Test" }
                }
            });

            var mockHttpMessageHandler = CreateSequentialMockHttpMessageHandlerWithError(
                new[] { catalogJson },
                HttpStatusCode.NotFound);
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act & Assert
            try
            {
                await service.GetCommunityLayoutAsync(layoutId, false);
                Assert.Fail("Expected HttpRequestException was not thrown.");
            }
            catch (HttpRequestException)
            {
                // Test passes
            }
        }

        /// <summary>
        /// Tests that GetCommunityLayoutAsync sets isDefault correctly.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityLayoutAsync_IsDefaultTrue_SetsIsDefaultProperty()
        {
            // Arrange
            var layoutId = "test-layout";
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem { Id = layoutId, Name = "Test", Description = "Test" }
                }
            });

            var layoutHtml = "<html><head></head><body></body></html>";

            var mockHttpMessageHandler = CreateSequentialMockHttpMessageHandler(
                new[] { catalogJson, layoutHtml });
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetCommunityLayoutAsync(layoutId, true);

            // Assert
            Assert.IsTrue(result.IsDefault);
        }

        #endregion

        #region GetPageTemplatesAsync Tests

        /// <summary>
        /// Tests that GetPageTemplatesAsync returns templates when found.
        /// </summary>
        [TestMethod]
        public async Task GetPageTemplatesAsync_ValidLayoutId_ReturnsTemplates()
        {
            // Arrange
            var layoutId = "bs5-strt";
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem { Id = layoutId, Name = "Bootstrap 5" }
                }
            });

            var pageRoot = new PageRoot
            {
                Pages = new List<Page>
                {
                    new Page
                    {
                        Title = "Home Page",
                        Type = "home",
                        Description = "Main landing page",
                        Path = "pages/home.html"
                    },
                    new Page
                    {
                        Title = "About Page",
                        Type = "content",
                        Description = "About us page",
                        Path = "pages/about.html"
                    }
                }
            };
            var pagesJson = JsonConvert.SerializeObject(pageRoot);

            var mockHttpMessageHandler = CreateSequentialMockHttpMessageHandler(
                new[] { catalogJson, pagesJson });
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetPageTemplatesAsync(layoutId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("About Page", result[0].Title); // Ordered by title
            Assert.AreEqual("Home Page", result[1].Title);
            Assert.AreEqual("home", result[1].Type);
        }

        /// <summary>
        /// Tests that GetPageTemplatesAsync returns empty list for invalid layout ID.
        /// </summary>
        [TestMethod]
        public async Task GetPageTemplatesAsync_InvalidLayoutId_ReturnsEmptyList()
        {
            // Arrange
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>()
            });

            var mockHttpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.OK, catalogJson);
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetPageTemplatesAsync("non-existent");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        /// <summary>
        /// Tests that GetPageTemplatesAsync handles HTTP errors gracefully.
        /// </summary>
        [TestMethod]
        public async Task GetPageTemplatesAsync_HttpError_ReturnsEmptyList()
        {
            // Arrange
            var layoutId = "bs5-strt";
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem { Id = layoutId, Name = "Test" }
                }
            });

            var mockHttpMessageHandler = CreateSequentialMockHttpMessageHandlerWithError(
                new[] { catalogJson },
                HttpStatusCode.NotFound);
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetPageTemplatesAsync(layoutId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            // Verify error was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        #endregion

        #region GetCommunityTemplatePagesAsync Tests

        /// <summary>
        /// Tests that GetCommunityTemplatePagesAsync returns templates.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityTemplatePagesAsync_ValidLayoutId_ReturnsTemplates()
        {
            // Arrange
            var layoutId = "bs5-strt";
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem { Id = layoutId, Name = "Bootstrap 5" }
                }
            });

            var pageRoot = new PageRoot
            {
                Pages = new List<Page>
                {
                    new Page
                    {
                        Title = "Home",
                        Type = "home",
                        Description = "Home page template",
                        Path = "pages/home.html"
                    }
                }
            };
            var pagesJson = JsonConvert.SerializeObject(pageRoot);

            var pageHtml = @"
<html>
<body>
    <cosmos-layout-header>Header</cosmos-layout-header>
    <div class='content'>Page Content</div>
    <cosmos-layout-footer>Footer</cosmos-layout-footer>
</body>
</html>";

            var mockHttpMessageHandler = CreateSequentialMockHttpMessageHandler(
                new[] { catalogJson, pagesJson, pageHtml });
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetCommunityTemplatePagesAsync(layoutId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Home Page", result[0].Title); // Type "home" becomes "Home Page"
            Assert.AreEqual("home", result[0].PageType);
            Assert.AreEqual(layoutId, result[0].CommunityLayoutId);
            Assert.IsNotNull(result[0].Content);
            Assert.IsTrue(result[0].Content.Contains("Page Content"));
            Assert.IsFalse(result[0].Content.Contains("cosmos-layout-header"));
            Assert.IsFalse(result[0].Content.Contains("cosmos-layout-footer"));
        }

        /// <summary>
        /// Tests that GetCommunityTemplatePagesAsync uses default layout ID when empty.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityTemplatePagesAsync_EmptyLayoutId_UsesDefaultLayout()
        {
            // Arrange
            var defaultLayoutId = "bs5-strt";
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem { Id = defaultLayoutId, Name = "Default" }
                }
            });

            var pagesJson = JsonConvert.SerializeObject(new PageRoot { Pages = new List<Page>() });

            var mockHttpMessageHandler = CreateSequentialMockHttpMessageHandler(
                new[] { catalogJson, pagesJson });
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetCommunityTemplatePagesAsync(string.Empty);

            // Assert
            Assert.IsNotNull(result);
        }

        /// <summary>
        /// Tests that GetCommunityTemplatePagesAsync returns empty list for invalid layout.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityTemplatePagesAsync_InvalidLayoutId_ReturnsEmptyList()
        {
            // Arrange
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>()
            });

            var mockHttpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.OK, catalogJson);
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetCommunityTemplatePagesAsync("non-existent");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        /// <summary>
        /// Tests that GetCommunityTemplatePagesAsync handles individual page failures gracefully.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityTemplatePagesAsync_PageLoadFailure_ContinuesProcessing()
        {
            // Arrange
            var layoutId = "test-layout";
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem { Id = layoutId, Name = "Test" }
                }
            });

            var pageRoot = new PageRoot
            {
                Pages = new List<Page>
                {
                    new Page { Title = "Page1", Type = "content", Description = "Page 1", Path = "page1.html" },
                    new Page { Title = "Page2", Type = "content", Description = "Page 2", Path = "page2.html" }
                }
            };
            var pagesJson = JsonConvert.SerializeObject(pageRoot);

            var page2Html = "<html><body>Page 2 Content</body></html>";

            // First call returns catalog, second returns page list, third fails, fourth succeeds
            var mockHttpMessageHandler = CreateSequentialMockHttpMessageHandlerWithMixedResults(
                new[] { catalogJson, pagesJson },
                new[] { page2Html },
                new[] { 2 }); // Fail on the 3rd call (index 2)

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetCommunityTemplatePagesAsync(layoutId);

            // Assert - Should have one successful template despite one failure
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);

            // Verify warning was logged for the failed page
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        /// <summary>
        /// Tests that GetCommunityTemplatePagesAsync handles content type correctly for home pages.
        /// </summary>
        [TestMethod]
        public async Task GetCommunityTemplatePagesAsync_HomePageType_SetsCorrectTitle()
        {
            // Arrange
            var layoutId = "test-layout";
            var catalogJson = JsonConvert.SerializeObject(new Root
            {
                LayoutCatalog = new List<LayoutCatalogItem>
                {
                    new LayoutCatalogItem { Id = layoutId, Name = "Test" }
                }
            });

            var pageRoot = new PageRoot
            {
                Pages = new List<Page>
                {
                    new Page
                    {
                        Title = "Index",
                        Type = "home",
                        Description = "Homepage",
                        Path = "index.html"
                    }
                }
            };
            var pagesJson = JsonConvert.SerializeObject(pageRoot);
            var pageHtml = "<html><body>Home Content</body></html>";

            var mockHttpMessageHandler = CreateSequentialMockHttpMessageHandler(
                new[] { catalogJson, pagesJson, pageHtml });
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await service.GetCommunityTemplatePagesAsync(layoutId);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Home Page", result[0].Title); // Should convert to "Home Page"
            Assert.AreEqual("home", result[0].PageType);
        }

        #endregion

        #region ParseHtml Tests

        /// <summary>
        /// Tests that ParseHtml creates Layout with correct properties.
        /// </summary>
        [TestMethod]
        public void ParseHtml_ValidHtml_CreatesLayoutWithCorrectProperties()
        {
            // Arrange
            var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Test Layout</title>
    <meta charset='utf-8'>
</head>
<body class='main-body' data-theme='light'>
    <cosmos-layout-header>
        <nav>Navigation</nav>
    </cosmos-layout-header>
    <main>Content Area</main>
    <cosmos-layout-footer>
        <footer>Footer Content</footer>
    </cosmos-layout-footer>
</body>
</html>";

            // Act
            var result = service.ParseHtml(html);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsDefault);
            Assert.AreEqual(string.Empty, result.CommunityLayoutId);
            Assert.IsNotNull(result.Head);
            Assert.IsTrue(result.Head.Contains("Test Layout"));
            Assert.IsNotNull(result.BodyHtmlAttributes);
            Assert.IsTrue(result.BodyHtmlAttributes.Contains("class=\"main-body\""));
            Assert.IsNotNull(result.HtmlHeader);
            Assert.IsTrue(result.HtmlHeader.Contains("Navigation"));
            Assert.IsNotNull(result.FooterHtmlContent);
            Assert.IsTrue(result.FooterHtmlContent.Contains("Footer Content"));
        }

        /// <summary>
        /// Tests that ParseHtml handles missing header gracefully.
        /// </summary>
        [TestMethod]
        public void ParseHtml_MissingHeader_SetsHeaderToNull()
        {
            // Arrange
            var html = @"
<html>
<head><title>Test</title></head>
<body>
    <main>Content</main>
    <cosmos-layout-footer>Footer</cosmos-layout-footer>
</body>
</html>";

            // Act
            var result = service.ParseHtml(html);

            // Assert
            Assert.IsNull(result.HtmlHeader);
            Assert.IsNotNull(result.FooterHtmlContent);
        }

        /// <summary>
        /// Tests that ParseHtml handles missing footer gracefully.
        /// </summary>
        [TestMethod]
        public void ParseHtml_MissingFooter_SetsFooterToNull()
        {
            // Arrange
            var html = @"
<html>
<head><title>Test</title></head>
<body>
    <cosmos-layout-header>Header</cosmos-layout-header>
    <main>Content</main>
</body>
</html>";

            // Act
            var result = service.ParseHtml(html);

            // Assert
            Assert.IsNotNull(result.HtmlHeader);
            Assert.IsNull(result.FooterHtmlContent);
        }

        #endregion

        #region ParseHtml<T> Tests

        /// <summary>
        /// Tests that ParseHtml with Template type creates correct Template.
        /// </summary>
        [TestMethod]
        public void ParseHtmlGeneric_TemplateType_CreatesTemplateWithContent()
        {
            // Arrange
            var html = @"
<html>
<body>
    <cosmos-layout-header>Header to be removed</cosmos-layout-header>
    <div class='content'>Page Content</div>
    <cosmos-layout-footer>Footer to be removed</cosmos-layout-footer>
</body>
</html>";

            // Act
            var result = service.ParseHtml<Template>(html);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Content);
            Assert.IsTrue(result.Content.Contains("Page Content"));
            Assert.IsFalse(result.Content.Contains("Header to be removed"));
            Assert.IsFalse(result.Content.Contains("Footer to be removed"));
        }

        /// <summary>
        /// Tests that ParseHtml with Article type creates correct Article.
        /// </summary>
        [TestMethod]
        public void ParseHtmlGeneric_ArticleType_CreatesArticleWithContent()
        {
            // Arrange
            var html = @"
<html>
<body>
    <cosmos-layout-header>Header</cosmos-layout-header>
    <article>Article Content</article>
    <cosmos-layout-footer>Footer</cosmos-layout-footer>
</body>
</html>";

            // Act
            var result = service.ParseHtml<Article>(html);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Content);
            Assert.IsTrue(result.Content.Contains("Article Content"));
            Assert.IsFalse(result.Content.Contains("cosmos-layout-header"));
            Assert.AreEqual((int)StatusCodeEnum.Active, result.StatusCode);
        }

        /// <summary>
        /// Tests that ParseHtml with unsupported type throws exception.
        /// </summary>
        [TestMethod]
        public void ParseHtmlGeneric_UnsupportedType_ThrowsException()
        {
            // Arrange
            var html = "<html><body>Content</body></html>";

            // Act & Assert
            try
            {
                service.ParseHtml<Layout>(html);
                Assert.Fail("Expected NotSupportedException was not thrown.");
            }
            catch (NotSupportedException)
            {
                // Test passes
            }
        }

        /// <summary>
        /// Tests that ParseHtml removes both header and footer for Template.
        /// </summary>
        [TestMethod]
        public void ParseHtmlGeneric_TemplateWithBothLayoutElements_RemovesBoth()
        {
            // Arrange
            var html = @"
<html>
<body>
    <cosmos-layout-header><nav>Nav</nav></cosmos-layout-header>
    <main>Main Content</main>
    <cosmos-layout-footer><footer>Footer</footer></cosmos-layout-footer>
</body>
</html>";

            // Act
            var result = service.ParseHtml<Template>(html);

            // Assert
            Assert.IsFalse(result.Content.Contains("cosmos-layout-header"));
            Assert.IsFalse(result.Content.Contains("cosmos-layout-footer"));
            Assert.IsFalse(result.Content.Contains("Nav"));
            Assert.IsFalse(result.Content.Contains("Footer"));
            Assert.IsTrue(result.Content.Contains("Main Content"));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a mock HttpMessageHandler that returns a specified response.
        /// </summary>
        private Mock<HttpMessageHandler> CreateMockHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                });

            return mockHttpMessageHandler;
        }

        /// <summary>
        /// Creates a mock HttpMessageHandler that returns different responses in sequence.
        /// </summary>
        private Mock<HttpMessageHandler> CreateSequentialMockHttpMessageHandler(string[] responses)
        {
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var callCount = 0;

            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    var response = responses[callCount % responses.Length];
                    callCount++;
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(response)
                    };
                });

            return mockHttpMessageHandler;
        }

        /// <summary>
        /// Creates a mock HttpMessageHandler that returns responses then an error.
        /// </summary>
        private Mock<HttpMessageHandler> CreateSequentialMockHttpMessageHandlerWithError(
            string[] successResponses,
            HttpStatusCode errorStatusCode)
        {
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var callCount = 0;

            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    if (callCount < successResponses.Length)
                    {
                        var response = successResponses[callCount];
                        callCount++;
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(response)
                        };
                    }
                    else
                    {
                        callCount++;
                        return new HttpResponseMessage
                        {
                            StatusCode = errorStatusCode,
                            Content = new StringContent(string.Empty)
                        };
                    }
                });

            return mockHttpMessageHandler;
        }

        /// <summary>
        /// Creates a mock HttpMessageHandler with mixed success/failure responses.
        /// </summary>
        private Mock<HttpMessageHandler> CreateSequentialMockHttpMessageHandlerWithMixedResults(
            string[] initialResponses,
            string[] successfulResponses,
            int[] failureIndices)
        {
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var callCount = 0;

            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    var currentCall = callCount;
                    callCount++;

                    // Initial responses (catalog, page list, etc.)
                    if (currentCall < initialResponses.Length)
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(initialResponses[currentCall])
                        };
                    }

                    // Check if this call should fail
                    if (failureIndices.Contains(currentCall))
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.NotFound,
                            Content = new StringContent(string.Empty)
                        };
                    }

                    // Successful page response
                    var successIndex = currentCall - initialResponses.Length;
                    var adjustedIndex = successIndex;
                    
                    // Adjust index for failed calls
                    foreach (var failIndex in failureIndices)
                    {
                        if (failIndex < currentCall && failIndex >= initialResponses.Length)
                        {
                            adjustedIndex--;
                        }
                    }

                    if (adjustedIndex >= 0 && adjustedIndex < successfulResponses.Length)
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(successfulResponses[adjustedIndex])
                        };
                    }

                    // Default response
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(string.Empty)
                    };
                });

            return mockHttpMessageHandler;
        }

        #endregion
    }
}
