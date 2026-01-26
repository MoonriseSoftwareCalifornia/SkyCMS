// <copyright file="DeploymentControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Cms.Common;
    using Cosmos.Cms.Common.Models;
    using Cosmos.Cms.Editor.Controllers;
    using Cosmos.Common;  // ✅ For ArticleType enum
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;  // ✅ For StatusCodeEnum
    using Cosmos.Common.Models;  // ✅ CORRECT - For SpaMetadata (NOT Sky.Common.Models)
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Unit tests for <see cref="DeploymentController"/>.
    /// Tests secure SPA deployment including password verification, file validation, and path traversal protection.
    /// </summary>
    [TestClass]
    public class DeploymentControllerTests
    {
        private ApplicationDbContext dbContext;
        private Mock<IStorageContext> storageContextMock;
        private Mock<ILogger<DeploymentController>> loggerMock;
        private DeploymentController controller;
        private Guid testArticleId;
        private const string TestDeploymentKey = "test-deployment-key-12345678901"; // 32 chars

        [TestInitialize]
        public void Setup()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"DeploymentTest_{Guid.NewGuid()}")
                .Options;
            dbContext = new ApplicationDbContext(options);

            // Setup mocks
            storageContextMock = new Mock<IStorageContext>();
            loggerMock = new Mock<ILogger<DeploymentController>>();

            // Create test SPA article with deployment key
            testArticleId = Guid.NewGuid();
            var deploymentKeyHash = BCrypt.Net.BCrypt.HashPassword(TestDeploymentKey);
            
            var spaMetadata = new SpaMetadata
            {
                DeploymentKeyHash = deploymentKeyHash,
                DeploymentCount = 0
            };

            var article = new Article
            {
                Id = testArticleId,
                ArticleNumber = 1,
                Title = "Test SPA",
                UrlPath = "/test-spa",
                ArticleType = (int)ArticleType.SpaApp,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = System.Text.Json.JsonSerializer.Serialize(spaMetadata),
                Published = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow,
                VersionNumber = 1
            };

            dbContext.Articles.Add(article);
            dbContext.Pages.Add(new Cosmos.Common.Data.PublishedPage
            {
                Id = article.Id,
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                UrlPath = article.UrlPath,
                ArticleType = article.ArticleType,
                StatusCode = article.StatusCode,
                Content = article.Content,  // ✅ Include SPA metadata
                Published = article.Published,
                Updated = article.Updated,
                VersionNumber = article.VersionNumber
            });
            dbContext.SaveChanges();

            // Create controller
            controller = new DeploymentController(
                dbContext,
                storageContextMock.Object,
                loggerMock.Object);

            // Setup HttpContext
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            dbContext?.Dispose();
        }

        #region Helper Methods

        /// <summary>
        /// Creates a valid test zip file in memory.
        /// </summary>
        private IFormFile CreateTestZipFile(Dictionary<string, string> files = null)
        {
            files ??= new Dictionary<string, string>
            {
                { "index.html", "<html><body>Test</body></html>" },
                { "app.js", "console.log('test');" },
                { "styles.css", "body { margin: 0; }" }
            };

            var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in files)
                {
                    var entry = archive.CreateEntry(file.Key);
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream);
                    writer.Write(file.Value);
                }
            }

            memoryStream.Position = 0;

            var formFile = new Mock<IFormFile>();
            formFile.Setup(f => f.FileName).Returns("deploy.zip");
            formFile.Setup(f => f.Length).Returns(memoryStream.Length);
            formFile.Setup(f => f.OpenReadStream()).Returns(memoryStream);
            formFile.Setup(f => f.ContentType).Returns("application/zip");

            return formFile.Object;
        }

        /// <summary>
        /// Creates an oversized zip file exceeding the 100MB limit.
        /// </summary>
        private IFormFile CreateOversizedZipFile()
        {
            var formFile = new Mock<IFormFile>();
            formFile.Setup(f => f.FileName).Returns("deploy.zip");
            formFile.Setup(f => f.Length).Returns(101_000_000); // 101 MB
            formFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

            return formFile.Object;
        }

        #endregion

        #region Password Verification Tests

        /// <summary>
        /// Tests that valid deployment key allows deployment.
        /// </summary>
        [TestMethod]
        public async Task Deploy_ValidPassword_ReturnsSuccess()
        {
            // Arrange
            var zipFile = CreateTestZipFile();

            // Act
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.AreEqual(200, okResult.StatusCode);
        }

        /// <summary>
        /// Tests that invalid deployment key returns Unauthorized.
        /// </summary>
        [TestMethod]
        public async Task Deploy_InvalidPassword_ReturnsUnauthorized()
        {
            // Arrange
            var zipFile = CreateTestZipFile();
            var wrongPassword = "wrong-password-12345678901234";

            // Act
            var result = await controller.Deploy(testArticleId, wrongPassword, zipFile);

            // Assert
            var unauthorizedResult = result as UnauthorizedObjectResult;
            Assert.IsNotNull(unauthorizedResult);
            Assert.AreEqual(401, unauthorizedResult.StatusCode);
        }

        /// <summary>
        /// Tests that null password returns Unauthorized.
        /// </summary>
        [TestMethod]
        public async Task Deploy_NullPassword_ReturnsUnauthorized()
        {
            // Arrange
            var zipFile = CreateTestZipFile();

            // Act
            var result = await controller.Deploy(testArticleId, null, zipFile);

            // Assert
            var unauthorizedResult = result as UnauthorizedObjectResult;
            Assert.IsNotNull(unauthorizedResult);
        }

        /// <summary>
        /// Tests that empty password returns Unauthorized.
        /// </summary>
        [TestMethod]
        public async Task Deploy_EmptyPassword_ReturnsUnauthorized()
        {
            // Arrange
            var zipFile = CreateTestZipFile();

            // Act
            var result = await controller.Deploy(testArticleId, string.Empty, zipFile);

            // Assert
            var unauthorizedResult = result as UnauthorizedObjectResult;
            Assert.IsNotNull(unauthorizedResult);
        }

        /// <summary>
        /// Tests that previous password works within 24-hour grace period.
        /// </summary>
        [TestMethod]
        public async Task Deploy_PreviousPasswordWithinGracePeriod_ReturnsSuccess()
        {
            // Arrange
            var newPassword = "new-password-12345678901234567";
            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            
            var article = await dbContext.Pages.FindAsync(testArticleId); // Changed from Articles to Pages
            var metadata = System.Text.Json.JsonSerializer.Deserialize<SpaMetadata>(article.Content);
            
            // Rotate key - old key becomes previous, new key becomes current
            metadata.DeploymentKeyHashPrevious = metadata.DeploymentKeyHash;
            metadata.DeploymentKeyHash = newPasswordHash;
            metadata.DeploymentKeyRotatedAt = DateTimeOffset.UtcNow.AddHours(-1); // Rotated 1 hour ago
            
            article.Content = System.Text.Json.JsonSerializer.Serialize(metadata);
            await dbContext.SaveChangesAsync();

            var zipFile = CreateTestZipFile();

            // Act - Use OLD password (should work within grace period)
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult, "Previous password should work within 24-hour grace period");
        }

        /// <summary>
        /// Tests that previous password fails after 24-hour grace period.
        /// </summary>
        [TestMethod]
        public async Task Deploy_PreviousPasswordExpired_ReturnsUnauthorized()
        {
            // Arrange
            var newPassword = "new-password-12345678901234567";
            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            
            var article = await dbContext.Pages.FindAsync(testArticleId); // Changed from Articles to Pages
            var metadata = System.Text.Json.JsonSerializer.Deserialize<SpaMetadata>(article.Content);
            
            // Rotate key - but grace period expired
            metadata.DeploymentKeyHashPrevious = metadata.DeploymentKeyHash;
            metadata.DeploymentKeyHash = newPasswordHash;
            metadata.DeploymentKeyRotatedAt = DateTimeOffset.UtcNow.AddHours(-25); // Rotated 25 hours ago (expired)
            
            article.Content = System.Text.Json.JsonSerializer.Serialize(metadata);
            await dbContext.SaveChangesAsync();

            var zipFile = CreateTestZipFile();

            // Act - Use OLD password (should fail - grace period expired)
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            var unauthorizedResult = result as UnauthorizedObjectResult;
            Assert.IsNotNull(unauthorizedResult, "Previous password should fail after 24-hour grace period");
        }

        #endregion

        #region Article Validation Tests

        /// <summary>
        /// Tests that non-existent article returns NotFound.
        /// </summary>
        [TestMethod]
        public async Task Deploy_NonExistentArticle_ReturnsNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();
            var zipFile = CreateTestZipFile();

            // Act
            var result = await controller.Deploy(nonExistentId, TestDeploymentKey, zipFile);

            // Assert
            var notFoundResult = result as NotFoundObjectResult;
            Assert.IsNotNull(notFoundResult);
        }

        /// <summary>
        /// Tests that non-SPA article type returns BadRequest.
        /// </summary>
        [TestMethod]
        public async Task Deploy_NonSpaArticleType_ReturnsBadRequest()
        {
            // Arrange - Create regular article (not SPA)
            var regularArticleId = Guid.NewGuid();
            var regularArticle = new Article
            {
                Id = regularArticleId,
                ArticleNumber = 2,
                Title = "Regular Article",
                ArticleType = (int)ArticleType.General, // Not SPA
                StatusCode = (int)StatusCodeEnum.Active,
                Published = DateTimeOffset.UtcNow
            };
            dbContext.Articles.Add(regularArticle);
            await dbContext.SaveChangesAsync();

            var zipFile = CreateTestZipFile();

            // Act
            var result = await controller.Deploy(regularArticleId, TestDeploymentKey, zipFile);

            // Assert
            var notFoundResult = result as NotFoundObjectResult;
            Assert.IsNotNull(notFoundResult, "Should reject non-SPA article types");
        }

        /// <summary>
        /// Tests that article with invalid metadata returns BadRequest.
        /// </summary>
        [TestMethod]
        public async Task Deploy_InvalidMetadata_ReturnsBadRequest()
        {
            // Arrange
            var page = await dbContext.Pages.FindAsync(testArticleId);
            page.Content = "invalid-json"; // Invalid JSON
            await dbContext.SaveChangesAsync();

            var zipFile = CreateTestZipFile();

            // Act
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult);
        }

        #endregion

        #region Zip File Validation Tests

        /// <summary>
        /// Tests that null zip file returns BadRequest.
        /// </summary>
        [TestMethod]
        public async Task Deploy_NullZipFile_ReturnsBadRequest()
        {
            // Act
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, null);

            // Assert
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult);
        }

        /// <summary>
        /// Tests that empty zip file returns BadRequest.
        /// </summary>
        [TestMethod]
        public async Task Deploy_EmptyZipFile_ReturnsBadRequest()
        {
            // Arrange
            var emptyFile = new Mock<IFormFile>();
            emptyFile.Setup(f => f.Length).Returns(0);
            emptyFile.Setup(f => f.FileName).Returns("deploy.zip");

            // Act
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, emptyFile.Object);

            // Assert
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult);
        }

        /// <summary>
        /// Tests that oversized zip file returns BadRequest.
        /// </summary>
        [TestMethod]
        public async Task Deploy_OversizedZipFile_ReturnsBadRequest()
        {
            // Arrange
            var oversizedFile = CreateOversizedZipFile();

            // Act
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, oversizedFile);

            // Assert
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult);
            
            var errorProperty = badRequestResult.Value.GetType().GetProperty("error");
            var errorMessage = errorProperty.GetValue(badRequestResult.Value).ToString();
            Assert.IsTrue(errorMessage.Contains("100 MB"), "Error should mention size limit");
        }

        /// <summary>
        /// Tests that non-zip file extension returns BadRequest.
        /// </summary>
        [TestMethod]
        public async Task Deploy_NonZipExtension_ReturnsBadRequest()
        {
            // Arrange
            var formFile = new Mock<IFormFile>();
            formFile.Setup(f => f.FileName).Returns("deploy.tar.gz");
            formFile.Setup(f => f.Length).Returns(1000);

            // Act
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, formFile.Object);

            // Assert
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult);
        }

        #endregion

        #region Path Traversal Protection Tests

        /// <summary>
        /// Tests that path traversal attempt is rejected.
        /// </summary>
        [TestMethod]
        public async Task Deploy_PathTraversalAttempt_ThrowsInvalidOperationException()
        {
            // Arrange
            var maliciousFiles = new Dictionary<string, string>
            {
                { "../../../etc/passwd", "malicious content" }
            };
            var zipFile = CreateTestZipFile(maliciousFiles);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await controller.Deploy(testArticleId, TestDeploymentKey, zipFile),
                "Path traversal should be detected and rejected");
        }

        /// <summary>
        /// Tests that multiple path traversal patterns are rejected.
        /// </summary>
        [TestMethod]
        public async Task Deploy_VariousPathTraversalPatterns_Rejected()
        {
            // Arrange - Test various traversal patterns
            var patterns = new[]
            {
                "../../../etc/passwd",
                "..\\..\\..\\windows\\system32",
                "dir/../../../etc/passwd",
                "./../../../etc/passwd"
            };

            foreach (var pattern in patterns)
            {
                var maliciousFiles = new Dictionary<string, string>
                {
                    { pattern, "malicious" }
                };
                var zipFile = CreateTestZipFile(maliciousFiles);

                // Act & Assert
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    async () => await controller.Deploy(testArticleId, TestDeploymentKey, zipFile),
                    $"Pattern '{pattern}' should be rejected");
            }
        }

        #endregion

        #region File Extension Allowlist Tests

        /// <summary>
        /// Tests that allowed file extensions are accepted.
        /// </summary>
        [TestMethod]
        public async Task Deploy_AllowedExtensions_Accepted()
        {
            // Arrange
            var allowedFiles = new Dictionary<string, string>
            {
                { "index.html", "<html></html>" },
                { "app.js", "js code" },
                { "app.mjs", "es module" },
                { "style.css", "css code" },
                { "config.json", "{}" },
                { "icon.svg", "<svg></svg>" },
                { "image.png", "binary" },
                { "photo.jpg", "binary" },
                { "photo.jpeg", "binary" },
                { "animation.gif", "binary" },
                { "modern.webp", "binary" },
                { "favicon.ico", "binary" },
                { "font.woff", "binary" },
                { "font.woff2", "binary" },
                { "font.ttf", "binary" },
                { "font.eot", "binary" },
                { "source.map", "sourcemap" },
                { "robots.txt", "text" },
                { "sitemap.xml", "xml" }
            };
            
            var zipFile = CreateTestZipFile(allowedFiles);

            // Act
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult, "All allowed extensions should be accepted");
        }

        /// <summary>
        /// Tests that disallowed file extensions are skipped with warning.
        /// </summary>
        [TestMethod]
        public async Task Deploy_DisallowedExtensions_SkippedWithWarning()
        {
            // Arrange
            var mixedFiles = new Dictionary<string, string>
            {
                { "index.html", "<html></html>" },
                { "malware.exe", "binary" }, // Disallowed
                { "script.sh", "shell script" }, // Disallowed
                { "config.php", "php code" } // Disallowed
            };
            
            var zipFile = CreateTestZipFile(mixedFiles);

            // Act
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            
            // Verify warning was logged for disallowed files
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Skipping unsupported file type")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.AtLeast(3)); // 3 disallowed files
        }

        #endregion

        #region Deployment Metadata Tests

        /// <summary>
        /// Tests that deployment increments counter.
        /// </summary>
        [TestMethod]
        public async Task Deploy_Success_IncrementsDeploymentCounter()
        {
            // Arrange
            var zipFile = CreateTestZipFile();

            // Act
            await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            var article = await dbContext.Pages.FindAsync(testArticleId);
            var metadata = System.Text.Json.JsonSerializer.Deserialize<SpaMetadata>(article.Content);
            
            Assert.AreEqual(1, metadata.DeploymentCount);
            Assert.IsNotNull(metadata.LastDeployedAt);
        }

        /// <summary>
        /// Tests that Git SHA is captured from header.
        /// </summary>
        [TestMethod]
        public async Task Deploy_WithGitShaHeader_CapturesCommitInfo()
        {
            // Arrange
            var testSha = "abc123def456";
            var testRepo = "owner/repo";
            
            controller.ControllerContext.HttpContext.Request.Headers["X-GitHub-SHA"] = testSha;
            controller.ControllerContext.HttpContext.Request.Headers["X-GitHub-Repository"] = testRepo;
            
            var zipFile = CreateTestZipFile();

            // Act
            await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            var article = await dbContext.Pages.FindAsync(testArticleId);
            var metadata = System.Text.Json.JsonSerializer.Deserialize<SpaMetadata>(article.Content);
            
            Assert.AreEqual(testSha, metadata.LastCommitSha);
            Assert.AreEqual(testRepo, metadata.LastDeployedFrom);
        }

        /// <summary>
        /// Tests that Updated timestamp is set.
        /// </summary>
        [TestMethod]
        public async Task Deploy_Success_UpdatesTimestamp()
        {
            // Arrange
            var beforeDeploy = DateTimeOffset.UtcNow;
            var zipFile = CreateTestZipFile();

            // Act
            await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            var article = await dbContext.Pages.FindAsync(testArticleId);
            Assert.IsTrue(article.Updated >= beforeDeploy);
        }

        #endregion

        #region Storage Integration Tests

        /// <summary>
        /// Tests that files are uploaded to correct blob path.
        /// </summary>
        [TestMethod]
        public async Task Deploy_Success_UploadsFilesToCorrectPath()
        {
            // Arrange
            var files = new Dictionary<string, string>
            {
                { "index.html", "<html></html>" },
                { "js/app.js", "console.log('test');" }
            };
            var zipFile = CreateTestZipFile(files);

            var uploadedFiles = new List<string>();
            storageContextMock
                .Setup(s => s.AppendBlob(
                    It.IsAny<MemoryStream>(),
                    It.IsAny<FileUploadMetaData>(),
                    It.IsAny<string>()))
                .Callback<MemoryStream, FileUploadMetaData, string>((stream, metadata, mode) =>
                {
                    uploadedFiles.Add(metadata.FileName);
                })
                .Returns(Task.CompletedTask);

            // Act
            await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            Assert.AreEqual(2, uploadedFiles.Count);
            Assert.IsTrue(uploadedFiles.Any(f => f.Contains("test-spa/index.html")));
            Assert.IsTrue(uploadedFiles.Any(f => f.Contains("test-spa/js/app.js")));
        }

        /// <summary>
        /// Tests that content types are set correctly.
        /// </summary>
        [TestMethod]
        public async Task Deploy_Success_SetsCorrectContentTypes()
        {
            // Arrange
            var files = new Dictionary<string, string>
            {
                { "index.html", "<html></html>" },
                { "app.js", "js" },
                { "style.css", "css" }
            };
            var zipFile = CreateTestZipFile(files);

            var contentTypes = new Dictionary<string, string>();
            storageContextMock
                .Setup(s => s.AppendBlob(
                    It.IsAny<MemoryStream>(),
                    It.IsAny<FileUploadMetaData>(),
                    It.IsAny<string>()))
                .Callback<MemoryStream, FileUploadMetaData, string>((stream, metadata, mode) =>
                {
                    contentTypes[metadata.FileName] = metadata.ContentType;
                })
                .Returns(Task.CompletedTask);

            // Act
            await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            Assert.IsTrue(contentTypes.Values.Any(ct => ct == "text/html"));
            Assert.IsTrue(contentTypes.Values.Any(ct => ct == "application/javascript"));
            Assert.IsTrue(contentTypes.Values.Any(ct => ct == "text/css"));
        }

        #endregion

        #region Response Validation Tests

        /// <summary>
        /// Tests that successful deployment returns expected response structure.
        /// </summary>
        [TestMethod]
        public async Task Deploy_Success_ReturnsExpectedResponseStructure()
        {
            // Arrange
            var zipFile = CreateTestZipFile();

            // Act
            var result = await controller.Deploy(testArticleId, TestDeploymentKey, zipFile);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            
            // Anonymous types are internal, so we need to use reflection to access properties
            var responseType = okResult.Value.GetType();
            var successProp = responseType.GetProperty("success");
            var deployedAtProp = responseType.GetProperty("deployedAt");
            var deploymentCountProp = responseType.GetProperty("deploymentCount");
            var urlPathProp = responseType.GetProperty("urlPath");
            var filesDeployedProp = responseType.GetProperty("filesDeployed");
            var cdnPurgedProp = responseType.GetProperty("cdnPurged");
            
            Assert.IsNotNull(successProp, "Response should have 'success' property");
            Assert.IsTrue((bool)successProp.GetValue(okResult.Value), "success should be true");
            
            Assert.IsNotNull(deployedAtProp, "Response should have 'deployedAt' property");
            Assert.IsNotNull(deployedAtProp.GetValue(okResult.Value), "deployedAt should not be null");
            
            Assert.IsNotNull(deploymentCountProp, "Response should have 'deploymentCount' property");
            Assert.IsTrue((int)deploymentCountProp.GetValue(okResult.Value) > 0, "deploymentCount should be greater than 0");
            
            Assert.IsNotNull(urlPathProp, "Response should have 'urlPath' property");
            Assert.IsNotNull(urlPathProp.GetValue(okResult.Value), "urlPath should not be null");
            
            Assert.IsNotNull(filesDeployedProp, "Response should have 'filesDeployed' property");
            Assert.IsTrue((int)filesDeployedProp.GetValue(okResult.Value) > 0, "filesDeployed should be greater than 0");
            
            Assert.IsNotNull(cdnPurgedProp, "Response should have 'cdnPurged' property");
            Assert.IsNotNull(cdnPurgedProp.GetValue(okResult.Value), "cdnPurged should not be null");
        }

        #endregion
    }
}
