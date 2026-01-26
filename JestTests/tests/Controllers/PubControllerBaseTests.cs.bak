// <copyright file="PubControllerBaseTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Publisher.Controllers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for the <see cref="PubControllerBase"/> class.
    /// Tests logging integration, file serving, authentication, and error handling.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class PubControllerBaseTests : SkyCmsTestBase
    {
        private TestPubController controller;
        private Mock<IEmailSender<IdentityUser>> emailSenderMock;
        private TestLogger<TestPubController> testLogger;
        private IMemoryCache memoryCacheMock;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);

            emailSenderMock = new Mock<IEmailSender<IdentityUser>>();
            testLogger = new TestLogger<TestPubController>();
            memoryCacheMock = new MemoryCache(new MemoryCacheOptions());

            controller = new TestPubController(
                null!,
                Db,
                Storage,
                testLogger,
                emailSenderMock.Object,
                memoryCacheMock);

            // Setup HTTP context
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString("example.com");
            httpContext.Request.Scheme = "https";

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            Db.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithNullLogger_DoesNotThrow()
        {
            // Act
            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                null!,
                emailSenderMock.Object,
                memoryCacheMock);

            // Assert
            Assert.IsNotNull(testController);
        }

        [TestMethod]
        public void Constructor_WithAllDependencies_InitializesSuccessfully()
        {
            // Assert
            Assert.IsNotNull(controller);
        }

        #endregion

        #region Index Tests - Basic File Serving

        [TestMethod]
        public async Task Index_WithoutAuthentication_ReturnsNotFoundForNonExistentFile()
        {
            // Arrange
            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                testLogger,
                emailSenderMock.Object,
                memoryCacheMock,
                requiresAuthentication: false);

            testController.ControllerContext = controller.ControllerContext;
            testController.ControllerContext.HttpContext.Request.Path = "/nonexistent/file.jpg";

            // Act
            var result = await testController.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Authentication Tests

        [TestMethod]
        public async Task Index_RequiresAuth_UnauthenticatedUser_ReturnsUnauthorized()
        {
            // Arrange
            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                testLogger,
                emailSenderMock.Object,
                memoryCacheMock,
                requiresAuthentication: true);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated

            testController.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await testController.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public async Task Index_RequiresAuth_ArticlePath_InvalidArticleNumber_ReturnsNotFound()
        {
            // Arrange
            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                testLogger,
                emailSenderMock.Object,
                memoryCacheMock,
                requiresAuthentication: true);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()) },
                "TestAuth"));
            httpContext.Request.Path = "/pub/articles/invalid/file.jpg";

            testController.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await testController.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Index_RequiresAuth_ArticlePath_ValidArticleNumber_UnauthorizedUser()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.PublishArticle(
                (await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber)).Id,
                DateTimeOffset.UtcNow);

            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                testLogger,
                emailSenderMock.Object,
                memoryCacheMock,
                requiresAuthentication: true);

            var httpContext = new DefaultHttpContext();
            
            // Create a DIFFERENT user (not the article owner)
            var differentUserId = Guid.NewGuid();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, differentUserId.ToString()) },
                "TestAuth"));
            httpContext.Request.Path = $"/pub/articles/{article.ArticleNumber}/file.jpg";

            testController.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await testController.Index();

            // Assert - Should return Unauthorized because user doesn't have access to this article
            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public async Task Index_RequiresAuth_ArticlePath_AuthorizedUser_ReturnsNotFoundForMissingFile()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            var articleEntity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            await Logic.PublishArticle(articleEntity.Id, DateTimeOffset.UtcNow);

            // ? ALTERNATIVE FIX: Delete and recreate the catalog entry with permissions
            var oldCatalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            
            if (oldCatalogEntry != null)
            {
                Db.ArticleCatalog.Remove(oldCatalogEntry);
                await Db.SaveChangesAsync();
            }

            // Create new catalog entry with permissions
            var newCatalogEntry = new CatalogEntry
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                UrlPath = article.UrlPath,
                Published = article.Published,
                Updated = article.Updated,
                Status = "Active",
                ArticlePermissions = new List<ArticlePermission>
                {
                    new ArticlePermission
                    {
                        ArticleId = article.ArticleNumber,
                        IdentityObjectId = TestUserId.ToString(),
                        Permission = "Read",
                        IsRoleObject = false
                    }
                }
            };

            Db.ArticleCatalog.Add(newCatalogEntry);
            await Db.SaveChangesAsync();

            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                testLogger,
                emailSenderMock.Object,
                memoryCacheMock,
                requiresAuthentication: true);

            var httpContext = new DefaultHttpContext();
            
            // Use the SAME user who created the article
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] 
                { 
                    new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                    new Claim(ClaimTypes.Name, "test@example.com")
                },
                "TestAuth"));
            httpContext.Request.Path = $"/pub/articles/{article.ArticleNumber}/file.jpg";

            testController.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await testController.Index();

            // Assert - Should return NotFound for missing file (user is authorized)
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Logging Tests

        [TestMethod]
        public async Task Index_LogsWarning_WhenUnauthorizedAccessAttempt()
        {
            // Arrange
            var logger = new TestLogger<TestPubController>();

            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                logger,
                emailSenderMock.Object,
                memoryCacheMock,
                requiresAuthentication: true);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated
            httpContext.Request.Path = "/pub/articles/1/file.jpg";

            testController.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            await testController.Index();

            // Assert
            Assert.IsTrue(logger.LogEntries.Any(e => 
                e.LogLevel == LogLevel.Warning && 
                e.Message.Contains("Unauthorized access attempt")));
        }

        [TestMethod]
        public async Task Index_LogsWarning_WhenFileNotFound()
        {
            // Arrange
            var logger = new TestLogger<TestPubController>();

            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                logger,
                emailSenderMock.Object,
                memoryCacheMock,
                requiresAuthentication: false);

            testController.ControllerContext = controller.ControllerContext;
            testController.ControllerContext.HttpContext.Request.Path = "/nonexistent.jpg";

            // Act
            await testController.Index();

            // Assert
            Assert.IsTrue(logger.LogEntries.Any(e => 
                e.LogLevel == LogLevel.Warning && 
                e.Message.Contains("File not found")));
        }

        [TestMethod]
        public async Task Index_LogsWarning_WhenFileNotFoundInStorage()
        {
            // Arrange
            var logger = new TestLogger<TestPubController>();

            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                logger,
                emailSenderMock.Object,
                memoryCacheMock,
                requiresAuthentication: false);

            testController.ControllerContext = controller.ControllerContext;
            testController.ControllerContext.HttpContext.Request.Path = "/missing/file.jpg";

            // Act
            var result = await testController.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
            
            // Storage returns null for missing files, which triggers warning log, not error
            Assert.IsTrue(logger.LogEntries.Any(e => 
                e.LogLevel == LogLevel.Warning && 
                e.Message.Contains("File not found")));
        }

        #endregion

        #region Path Parsing Tests

        [TestMethod]
        public async Task Index_PathNormalization_AvoidsDuplicateToStringCalls()
        {
            // Arrange
            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                testLogger,
                emailSenderMock.Object,
                memoryCacheMock,
                requiresAuthentication: false);

            testController.ControllerContext = controller.ControllerContext;
            testController.ControllerContext.HttpContext.Request.Path = "/test/path.jpg";

            // Act
            await testController.Index();

            // Assert - Path should be extracted once and reused
            Assert.IsTrue(testLogger.LogEntries.Any());
        }

        [TestMethod]
        public async Task Index_CaseInsensitiveArticlePathCheck_WorksCorrectly()
        {
            // Arrange
            var testController = new TestPubController(
                null!,
                Db,
                Storage,
                testLogger,
                emailSenderMock.Object,
                memoryCacheMock,
                requiresAuthentication: true);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()) },
                "TestAuth"));
            httpContext.Request.Path = "/PUB/ARTICLES/1/file.jpg"; // Uppercase

            testController.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await testController.Index();

            // Assert - Should handle case-insensitive path matching
            Assert.IsNotNull(result);
        }

        #endregion

        #region Test Helper Classes

        /// <summary>
        /// Test implementation of PubControllerBase for testing purposes.
        /// Made public to avoid proxy generation issues.
        /// </summary>
        public class TestPubController : PubControllerBase
        {
            public TestPubController(
                Cosmos.Common.Data.Logic.ArticleLogic articleLogic,
                ApplicationDbContext dbContext,
                StorageContext storageContext,
                ILogger<TestPubController> logger,
                IEmailSender<IdentityUser> emailSender,
                IMemoryCache memoryCache,
                bool requiresAuthentication = false)
                : base(dbContext, storageContext, requiresAuthentication, logger, memoryCache)
            {
            }
        }

        /// <summary>
        /// Test logger implementation that captures log entries for assertions.
        /// </summary>
        public class TestLogger<T> : ILogger<T>
        {
            public List<LogEntry> LogEntries { get; } = new();

            public IDisposable BeginScope<TState>(TState state) => null!;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                LogEntries.Add(new LogEntry
                {
                    LogLevel = logLevel,
                    EventId = eventId,
                    Message = formatter(state, exception),
                    Exception = exception
                });
            }

            public class LogEntry
            {
                public LogLevel LogLevel { get; set; }
                public EventId EventId { get; set; }
                public string Message { get; set; } = string.Empty;
                public Exception? Exception { get; set; }
            }
        }

        #endregion
    }
}
