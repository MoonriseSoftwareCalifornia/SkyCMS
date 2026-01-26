// <copyright file="DeploymentControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Threading.Tasks;
    using BCrypt.Net;
    using Cosmos.BlobService.Models;
    using Cosmos.Cms.Common;
    using Cosmos.Cms.Common.Models;
    using Cosmos.Cms.Editor.Controllers;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Controllers;

    /// <summary>
    /// Comprehensive unit tests for the DeploymentController.
    /// </summary>
    [TestClass]
    public class DeploymentControllerTests : SkyCmsTestBase
    {
        private DeploymentController controller;
        private Guid testSpaArticleId;
        private string testDeploymentKey;
        private string testWebhookSecret;

        [TestInitialize]
        public void DeploymentSetup()
        {
            // Call base initialization
            InitializeTestContext();

            // Generate test credentials
            testDeploymentKey = "TestDeployKey123!";
            testWebhookSecret = "TestWebhookSecret456!";

            // Create a test SPA article with a unique path for this test
            testSpaArticleId = Guid.NewGuid();
            var metadata = new SpaMetadata
            {
                DeploymentKeyHash = BCrypt.HashPassword(testDeploymentKey),
                WebhookSecretHash = BCrypt.HashPassword(testWebhookSecret),
                DeploymentCount = 0
            };

            var spaArticle = new PublishedPage
            {
                Id = testSpaArticleId,
                ArticleNumber = 1,
                Title = "Test SPA Application",
                UrlPath = $"/test-spa-{testSpaArticleId:N}",  // Unique path per test
                ArticleType = (int)ArticleType.SpaApp,
                Content = System.Text.Json.JsonSerializer.Serialize(metadata),
                Published = DateTimeOffset.UtcNow,
                StatusCode = 0,
                Updated = DateTimeOffset.UtcNow,
                VersionNumber = 1
            };

            Db.Pages.Add(spaArticle);
            Db.SaveChanges();

            // Create controller (using Storage from base class which is properly configured)
            controller = new DeploymentController(
                Db,
                Storage,
                new NullLogger<DeploymentController>());

            // Setup HttpContext with headers
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Hub-Signature-256"] = "sha256=test";
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #region Deploy Method Tests

        [TestMethod]
        public async Task Deploy_ValidRequest_ReturnsOkWithMetadata()
        {
            // Arrange
            var zipFile = CreateTestZipFile();

            // Act
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, zipFile);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var okResult = (OkObjectResult)result;
            Assert.IsNotNull(okResult.Value);
            
            var resultType = okResult.Value.GetType();
            var successProp = resultType.GetProperty("success");
            var deploymentCountProp = resultType.GetProperty("deploymentCount");
            
            Assert.IsNotNull(successProp, "Response should have 'success' property");
            Assert.IsNotNull(deploymentCountProp, "Response should have 'deploymentCount' property");
            Assert.IsTrue((bool)successProp.GetValue(okResult.Value)!, "Deployment should succeed");
            Assert.AreEqual(1, (int)deploymentCountProp.GetValue(okResult.Value)!, "Deployment count should be 1");
        }

        [TestMethod]
        public async Task Deploy_InvalidArticleId_ReturnsNotFound()
        {
            // Arrange
            var invalidId = Guid.NewGuid();
            var zipFile = CreateTestZipFile();

            // Act
            var result = await controller.Deploy(invalidId, testDeploymentKey, zipFile);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
            var notFoundResult = (NotFoundObjectResult)result;
            Assert.IsNotNull(notFoundResult.Value);
            
            var resultType = notFoundResult.Value.GetType();
            var successProp = resultType.GetProperty("success");
            var errorProp = resultType.GetProperty("error");
            
            Assert.IsNotNull(successProp);
            Assert.IsNotNull(errorProp);
            Assert.IsFalse((bool)successProp.GetValue(notFoundResult.Value)!);
            Assert.AreEqual("SPA article not found", (string)errorProp.GetValue(notFoundResult.Value)!);
        }

        [TestMethod]
        public async Task Deploy_InvalidPassword_ReturnsUnauthorized()
        {
            // Arrange
            var zipFile = CreateTestZipFile();
            var wrongPassword = "WrongPassword123!";

            // Act
            var result = await controller.Deploy(testSpaArticleId, wrongPassword, zipFile);

            // Assert
            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
            var unauthorizedResult = (UnauthorizedObjectResult)result;
            Assert.IsNotNull(unauthorizedResult.Value);
            
            var resultType = unauthorizedResult.Value.GetType();
            var successProp = resultType.GetProperty("success");
            var errorProp = resultType.GetProperty("error");
            
            Assert.IsNotNull(successProp);
            Assert.IsNotNull(errorProp);
            Assert.IsFalse((bool)successProp.GetValue(unauthorizedResult.Value)!);
            Assert.AreEqual("Invalid deployment key", (string)errorProp.GetValue(unauthorizedResult.Value)!);
        }

        [TestMethod]
        public async Task Deploy_NonSpaArticle_ReturnsNotFound()
        {
            // Arrange
            var regularArticle = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                Title = "Regular Article",
                UrlPath = "/regular",
                ArticleType = (int)ArticleType.General,
                Published = DateTimeOffset.UtcNow,
                StatusCode = 0
            };
            Db.Pages.Add(regularArticle);
            Db.SaveChanges();

            var zipFile = CreateTestZipFile();

            // Act
            var result = await controller.Deploy(regularArticle.Id, testDeploymentKey, zipFile);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        [TestMethod]
        public async Task Deploy_NullZipFile_ReturnsBadRequest()
        {
            // Act
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = (BadRequestObjectResult)result;
            Assert.IsNotNull(badRequestResult.Value);
            
            var resultType = badRequestResult.Value.GetType();
            var successProp = resultType.GetProperty("success");
            var errorProp = resultType.GetProperty("error");
            
            Assert.IsNotNull(successProp);
            Assert.IsNotNull(errorProp);
            Assert.IsFalse((bool)successProp.GetValue(badRequestResult.Value)!);
            Assert.AreEqual("No file uploaded", (string)errorProp.GetValue(badRequestResult.Value)!);
        }

        [TestMethod]
        public async Task Deploy_EmptyZipFile_ReturnsBadRequest()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(0);
            mockFile.Setup(f => f.FileName).Returns("test.zip");

            // Act
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, mockFile.Object);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Deploy_OversizedZipFile_ReturnsBadRequest()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(101_000_000); // 101 MB (over 100 MB limit)
            mockFile.Setup(f => f.FileName).Returns("test.zip");

            // Act
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, mockFile.Object);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = (BadRequestObjectResult)result;
            Assert.IsNotNull(badRequestResult.Value);
            
            var resultType = badRequestResult.Value.GetType();
            var errorProp = resultType.GetProperty("error");
            Assert.IsNotNull(errorProp);
            
            var errorMessage = (string)errorProp.GetValue(badRequestResult.Value)!;
            Assert.IsTrue(errorMessage.Contains("exceeds maximum"), $"Error message should mention 'exceeds maximum', got: {errorMessage}");
        }

        [TestMethod]
        public async Task Deploy_NonZipFile_ReturnsBadRequest()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1000);
            mockFile.Setup(f => f.FileName).Returns("test.txt");

            // Act
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, mockFile.Object);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = (BadRequestObjectResult)result;
            Assert.IsNotNull(badRequestResult.Value);
            
            var resultType = badRequestResult.Value.GetType();
            var errorProp = resultType.GetProperty("error");
            Assert.IsNotNull(errorProp);
            Assert.AreEqual("File must be a .zip archive", (string)errorProp.GetValue(badRequestResult.Value)!);
        }

        [TestMethod]
        public async Task Deploy_UpdatesDeploymentCount()
        {
            // Arrange
            var zipFile = CreateTestZipFile();

            // Act - First deployment
            var firstResult = await controller.Deploy(testSpaArticleId, testDeploymentKey, zipFile);
            Assert.IsInstanceOfType(firstResult, typeof(OkObjectResult), "First deployment should succeed");

            // Wait a moment to ensure database update completes
            await Task.Delay(100);

            // Reload the article to get fresh state
            Db.ChangeTracker.Clear();
            var article = await Db.Pages.FindAsync(testSpaArticleId);
            Assert.IsNotNull(article);

            // Create new zip for second deployment
            var zipFile2 = CreateTestZipFile();

            // Act - Second deployment
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, zipFile2);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var okResult = (OkObjectResult)result;
            Assert.IsNotNull(okResult.Value);
            
            var resultType = okResult.Value.GetType();
            var deploymentCountProp = resultType.GetProperty("deploymentCount");
            Assert.IsNotNull(deploymentCountProp);
            Assert.AreEqual(2, (int)deploymentCountProp.GetValue(okResult.Value)!, "Deployment count should be 2 after second deployment");
        }

        [TestMethod]
        public async Task Deploy_ExtractsGitHubHeaders()
        {
            // Arrange
            var zipFile = CreateTestZipFile();
            controller.ControllerContext.HttpContext.Request.Headers["X-GitHub-SHA"] = "abc123commit";
            controller.ControllerContext.HttpContext.Request.Headers["X-GitHub-Repository"] = "owner/repo";

            // Act
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, zipFile);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));

            // Wait a moment to ensure database update completes
            await Task.Delay(100);

            // Reload to get fresh state
            Db.ChangeTracker.Clear();
            
            // Verify metadata was updated
            var article = await Db.Pages.FindAsync(testSpaArticleId);
            Assert.IsNotNull(article, "Article should exist");
            Assert.IsNotNull(article.Content, "Article content should not be null");
            
            var metadata = System.Text.Json.JsonSerializer.Deserialize<SpaMetadata>(article.Content);
            Assert.IsNotNull(metadata, "Metadata should deserialize successfully");
            Assert.AreEqual("abc123commit", metadata.LastCommitSha, "Last commit SHA should be extracted from header");
            Assert.AreEqual("owner/repo", metadata.LastDeployedFrom, "Last deployed from should be extracted from header");
        }

        [TestMethod]
        public async Task Deploy_WithoutWebhookSignature_StillSucceeds()
        {
            // Arrange
            var zipFile = CreateTestZipFile();
            controller.ControllerContext.HttpContext.Request.Headers.Remove("X-Hub-Signature-256");

            // Act
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, zipFile);

            // Assert - Should succeed (development mode allows missing signature)
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        }

        #endregion

        #region Password Rotation Tests

        [TestMethod]
        public async Task Deploy_WithRotatedPassword_AcceptsPreviousKeyInGracePeriod()
        {
            // Arrange
            var newPassword = "NewPassword789!";
            
            Db.ChangeTracker.Clear();
            var article = await Db.Pages.FindAsync(testSpaArticleId);
            Assert.IsNotNull(article);
            Assert.IsNotNull(article.Content);
            
            var metadata = System.Text.Json.JsonSerializer.Deserialize<SpaMetadata>(article.Content);
            Assert.IsNotNull(metadata);

            // Simulate password rotation
            metadata.DeploymentKeyHashPrevious = metadata.DeploymentKeyHash;
            metadata.DeploymentKeyHash = BCrypt.HashPassword(newPassword);
            metadata.DeploymentKeyRotatedAt = DateTimeOffset.UtcNow.AddHours(-1); // 1 hour ago

            article.Content = System.Text.Json.JsonSerializer.Serialize(metadata);
            await Db.SaveChangesAsync();

            var zipFile = CreateTestZipFile();

            // Act - Use OLD password (should work within grace period)
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, zipFile);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Deployment with old password should succeed within grace period");
        }

        [TestMethod]
        public async Task Deploy_WithRotatedPassword_RejectsOldKeyAfterGracePeriod()
        {
            // Arrange
            var newPassword = "NewPassword789!";
            
            Db.ChangeTracker.Clear();
            var article = await Db.Pages.FindAsync(testSpaArticleId);
            Assert.IsNotNull(article);
            Assert.IsNotNull(article.Content);
            
            var metadata = System.Text.Json.JsonSerializer.Deserialize<SpaMetadata>(article.Content);
            Assert.IsNotNull(metadata);

            // Simulate password rotation 25 hours ago (beyond 24-hour grace period)
            metadata.DeploymentKeyHashPrevious = metadata.DeploymentKeyHash;
            metadata.DeploymentKeyHash = BCrypt.HashPassword(newPassword);
            metadata.DeploymentKeyRotatedAt = DateTimeOffset.UtcNow.AddHours(-25);

            article.Content = System.Text.Json.JsonSerializer.Serialize(metadata);
            await Db.SaveChangesAsync();

            var zipFile = CreateTestZipFile();

            // Act - Use OLD password (should fail after grace period)
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, zipFile);

            // Assert
            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult), "Deployment with old password should fail after grace period");
        }

        #endregion

        #region File Validation Tests

        [TestMethod]
        public async Task Deploy_WithPathTraversalAttempt_ThrowsException()
        {
            // Arrange
            var zipFile = CreateMaliciousZipFile("../../../etc/passwd");

            // Act & Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            {
                await controller.Deploy(testSpaArticleId, testDeploymentKey, zipFile);
            });
        }

        [TestMethod]
        public async Task Deploy_WithValidHtmlCssJs_Succeeds()
        {
            // Arrange
            var zipFile = CreateTestZipFile(
                ("index.html", "<html><body>Test</body></html>"),
                ("styles.css", "body { color: red; }"),
                ("app.js", "console.log('test');")
            );

            // Act
            var result = await controller.Deploy(testSpaArticleId, testDeploymentKey, zipFile);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        }

        [TestMethod]
        public async Task Deploy_UpdatesArticleTimestamp()
        {
            // Arrange
            var zipFile = CreateTestZipFile();
            
            Db.ChangeTracker.Clear();
            var articleBefore = await Db.Pages.FindAsync(testSpaArticleId);
            Assert.IsNotNull(articleBefore);
            var originalUpdated = articleBefore.Updated;

            // Wait a moment to ensure timestamp changes
            await Task.Delay(100);

            // Act
            await controller.Deploy(testSpaArticleId, testDeploymentKey, zipFile);

            // Wait for update to complete
            await Task.Delay(100);

            // Assert
            Db.ChangeTracker.Clear();
            var article = await Db.Pages.FindAsync(testSpaArticleId);
            Assert.IsNotNull(article);
            Assert.IsTrue(article.Updated > originalUpdated, $"Updated timestamp should increase. Before: {originalUpdated}, After: {article.Updated}");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a test zip file with sample SPA content.
        /// </summary>
        private IFormFile CreateTestZipFile(params (string filename, string content)[] files)
        {
            if (files == null || files.Length == 0)
            {
                // Default files
                files =
                [
                    ("index.html", "<html><body>Test SPA</body></html>"),
                    ("static/js/main.js", "console.log('test');"),
                    ("static/css/style.css", "body { margin: 0; }")
                ];
            }

            var memoryStream = new MemoryStream();

            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                foreach (var (filename, content) in files)
                {
                    var entry = archive.CreateEntry(filename);
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream);
                    writer.Write(content);
                }
            }

            memoryStream.Position = 0;

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.OpenReadStream()).Returns(() =>
            {
                // Return a new MemoryStream with the same data to avoid disposal issues
                var newStream = new MemoryStream(memoryStream.ToArray());
                newStream.Position = 0;
                return newStream;
            });
            mockFile.Setup(f => f.FileName).Returns("spa-deployment.zip");
            mockFile.Setup(f => f.Length).Returns(memoryStream.Length);
            mockFile.Setup(f => f.ContentType).Returns("application/zip");

            return mockFile.Object;
        }

        /// <summary>
        /// Creates a malicious zip file with path traversal attempt.
        /// </summary>
        private IFormFile CreateMaliciousZipFile(string maliciousPath)
        {
            var memoryStream = new MemoryStream();

            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry(maliciousPath);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write("malicious content");
            }

            memoryStream.Position = 0;

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.OpenReadStream()).Returns(() =>
            {
                var newStream = new MemoryStream(memoryStream.ToArray());
                newStream.Position = 0;
                return newStream;
            });
            mockFile.Setup(f => f.FileName).Returns("malicious.zip");
            mockFile.Setup(f => f.Length).Returns(memoryStream.Length);
            mockFile.Setup(f => f.ContentType).Returns("application/zip");

            return mockFile.Object;
        }

        #endregion
    }
}