// <copyright file="EditorUnifiedEndpointTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Articles.EditorQueries;
    using Cosmos.Common.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SendGrid.Helpers.Errors.Model;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using Sky.Editor.Models;
    using Sky.Tests;
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Comprehensive tests for the unified /Editor/Edit endpoint.
    /// Tests command routing, encryption, validation, error handling, and response formats.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class EditorUnifiedEndpointTests : SkyCmsTestBase
    {
        private EditorController controller = null!;

        [TestInitialize]
        public new void Setup()
        {
            base.Setup();

            controller = new EditorController(
                Logger,
                Db,
                UserManager,
                RoleManager,
                Logic,
                EditorSettings,
                ViewRenderService,
                Storage,
                Hub.Object,
                PublishingService,
                ArticleHtmlService,
                ReservedPaths,
                TitleChangeService,
                TemplateService,
                Mediator,
                Cache,
                DynamicConfigurationProvider);

            // Set up controller context with claims
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(ClaimTypes.Role, "Editors")
            }, "test"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        // ============================================================================
        // COMMAND ROUTING TESTS
        // ============================================================================

        /// <summary>
        /// Test that SaveBody command is routed correctly.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveBodyCommand_UpdatesArticleBody()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveBody",
                Payload = CryptoJsDecryption.Encrypt("<p>New body content</p>"),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var response = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value)).RootElement;
            Assert.IsTrue(response.GetProperty("ServerSideSuccess").GetBoolean());
        }

        /// <summary>
        /// Test that SaveRegion command is routed correctly.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveRegionCommand_UpdatesSpecificRegion()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            // Set initial content with editable regions
            article.Content = @"
                <div data-ccms-ceid='region-1'>
                    <p>Original region content</p>
                </div>
                <div data-ccms-ceid='region-2'>
                    <p>Other region</p>
                </div>";
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveRegion",
                EditorId = "region-1",
                Payload = CryptoJsDecryption.Encrypt("<p>Updated region</p>"),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var response = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value)).RootElement;
            Assert.IsTrue(response.GetProperty("ServerSideSuccess").GetBoolean());

            // Verify region-1 was updated
            var updated = await Mediator.QueryAsync(
                new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.IsTrue(updated.Content.Contains("<p>Updated region</p>"));
        }

        /// <summary>
        /// Test that SaveCode command is routed correctly.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveCodeCommand_UpdatesCodeFields()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveCode",
                Payload = CryptoJsDecryption.Encrypt("<p>Updated content</p>"),
                HeadJavaScript = CryptoJsDecryption.Encrypt("<script>console.log('head');</script>"),
                FooterJavaScript = CryptoJsDecryption.Encrypt("<script>console.log('footer');</script>"),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var response = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value)).RootElement;
            Assert.IsTrue(response.GetProperty("ServerSideSuccess").GetBoolean());
        }

        /// <summary>
        /// Test that SavePageProperties command updates article metadata.
        /// </summary>
        [TestMethod]
        public async Task Edit_SavePagePropertiesCommand_UpdatesArticleMetadata()
        {
            // Arrange
            var article = await CreateArticleAsync("Original Title", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SavePageProperties",
                Title = "Updated Title",
                BannerImage = "https://example.com/image.jpg",
                Published = DateTimeOffset.UtcNow,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var response = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value)).RootElement;
            Assert.IsTrue(response.GetProperty("ServerSideSuccess").GetBoolean());
        }

        // ============================================================================
        // ENCRYPTION & CONTEXT TOKEN TESTS
        // ============================================================================

        /// <summary>
        /// Tests that invalid requests (null model or invalid crypto token) return bad request.
        /// </summary>
        [TestMethod]
        public async Task Edit_WithInvalidRequests_ReturnsBadRequest()
        {
            var scenarios = new[]
            {
                new
                {
                    Name = "NullModel",
                    BuildModel = (Func<Task<EditPostViewModel>>)(() =>
                        Task.FromResult<EditPostViewModel>(null)),
                },
                new
                {
                    Name = "InvalidCryptoContextToken",
                    BuildModel = (Func<Task<EditPostViewModel>>)(async () =>
                    {
                        var article = await CreateArticleAsync("Test Article", TestUserId);
                        await SaveArticleAsync(article, TestUserId);

                        return new EditPostViewModel
                        {
                            ArticleNumber = article.ArticleNumber,
                            Command = "SaveBody",
                            Payload = CryptoJsDecryption.Encrypt("<p>Content</p>"),
                            CryptoContextToken = "invalid-token-12345",
                            Title = article.Title,
                            VersionNumber = article.VersionNumber
                        };
                    }),
                },
            };

            foreach (var scenario in scenarios)
            {
                var model = await scenario.BuildModel();
                var result = await controller.Edit(model!);
                Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult),
                    $"{scenario.Name} should return BadRequest");
            }
        }

        // ============================================================================
        // VALIDATION TESTS
        // ============================================================================

        /// <summary>
        /// Test that SaveCode validates against nested editable regions.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveCode_RejectsNestedEditableRegions()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var nestedContent = @"
                <div data-ccms-ceid='outer'>
                    <div data-ccms-ceid='inner'>
                        Nested regions not allowed
                    </div>
                </div>";

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveCode",
                Payload = CryptoJsDecryption.Encrypt(nestedContent),
                HeadJavaScript = CryptoJsDecryption.Encrypt(string.Empty),
                FooterJavaScript = CryptoJsDecryption.Encrypt(string.Empty),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert - Should return error (BadRequest or JsonResult with error)
            Assert.IsNotNull(result);
            // The controller may return BadRequest or a JsonResult with error structure
            // Both indicate validation failure
            if (result is BadRequestObjectResult badResult)
            {
                Assert.IsNotNull(badResult.Value);
            }
            else if (result is JsonResult jsonResult)
            {
                // Should contain error information
                var json = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value)).RootElement;
                Assert.IsFalse(json.GetProperty("ServerSideSuccess").GetBoolean(),
                    "Nested regions should result in failed save");
            }
            else
            {
                Assert.Fail($"Expected BadRequest or JsonResult with error, got {result.GetType().Name}");
            }
        }

        // ============================================================================
        // ERROR HANDLING TESTS
        // ============================================================================

        /// <summary>
        /// Test that unknown article returns NotFound.
        /// </summary>
        [TestMethod]
        public async Task Edit_WithUnknownArticle_ThrowsNotFound()
        {
            // Arrange
            var model = new EditPostViewModel
            {
                ArticleNumber = 999999, // Non-existent
                Command = "SaveBody",
                Payload = CryptoJsDecryption.Encrypt("<p>Content</p>"),
                Title = "Test"
            };

            // Act & Assert
            try
            {
                await controller.Edit(model);
                Assert.Fail("Expected NotFoundException");
            }
            catch (NotFoundException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Could not find article"));
            }
        }

        // ============================================================================
        // RESPONSE FORMAT TESTS
        // ============================================================================

        /// <summary>
        /// Test that successful response contains EditorResponse structure.
        /// </summary>
        [TestMethod]
        public async Task Edit_SuccessfulSave_ReturnsProperEditorResponse()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveBody",
                Payload = CryptoJsDecryption.Encrypt("<p>Updated content</p>"),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var response = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value)).RootElement;

            // Verify EditorResponse structure
            Assert.IsTrue(response.TryGetProperty("ServerSideSuccess", out var success));
            Assert.IsTrue(response.TryGetProperty("Model", out var modelProp));
            Assert.IsTrue(success.GetBoolean());
        }

        // ============================================================================
        // EDGE CASE TESTS
        // ============================================================================

        /// <summary>
        /// Test SaveRegion without EditorId keeps content unchanged.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveRegionWithoutEditorId_KeepsContentUnchanged()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            var originalContent = "<p>Original content</p>";
            article.Content = originalContent;
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveRegion",
                // No EditorId specified
                Payload = CryptoJsDecryption.Encrypt("<p>Should not be used</p>"),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));

            // Verify content unchanged
            var updated = await Mediator.QueryAsync(
                new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.AreEqual(originalContent, updated.Content);
        }

        /// <summary>
        /// Test that multiple sequential saves work correctly.
        /// </summary>
        [TestMethod]
        public async Task Edit_MultipleSequentialSaves_WorkCorrectly()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            article.Content = "<p>Initial content</p>";
            await SaveArticleAsync(article, TestUserId);

            // First save - SaveBody command
            var model1 = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveBody",
                Payload = CryptoJsDecryption.Encrypt("<p>First save</p>"),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act - First save
            var result1 = await controller.Edit(model1);
            Assert.IsInstanceOfType(result1, typeof(JsonResult), "First save should return JsonResult");

            // Refresh article from database
            var updated1 = await Mediator.QueryAsync(
                new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.IsNotNull(updated1.Content, "Content should not be null after first save");
            Assert.IsTrue(updated1.Content.Contains("First save"), 
                $"Expected content to contain 'First save', but got: {updated1.Content}");

            // Second save
            var model2 = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveBody",
                Payload = CryptoJsDecryption.Encrypt("<p>Second save</p>"),
                Title = article.Title,
                VersionNumber = updated1.VersionNumber
            };

            // Act - Second save
            var result2 = await controller.Edit(model2);
            Assert.IsInstanceOfType(result2, typeof(JsonResult), "Second save should return JsonResult");

            // Verify second save persisted
            var updated2 = await Mediator.QueryAsync(
                new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.IsNotNull(updated2.Content, "Content should not be null after second save");
            Assert.IsTrue(updated2.Content.Contains("Second save"),
                $"Expected content to contain 'Second save', but got: {updated2.Content}");
        }

        // ============================================================================
        // CRITICAL EDGE CASES - HIGH PRIORITY
        // ============================================================================

        /// <summary>
        /// Test SavePageProperties with null Data field (metadata-only update).
        /// </summary>
        [TestMethod]
        public async Task Edit_SavePageProperties_WithNullData_UpdatesMetadataOnly()
        {
            // Arrange
            var article = await CreateArticleAsync("Original Title", TestUserId);
            var originalContent = "<p>Original content</p>";
            article.Content = originalContent;
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SavePageProperties",
                Title = "Updated Title",
                BannerImage = "https://example.com/banner.jpg",
                // Data is null/not set
                Payload = null,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));

            // Verify metadata updated but content unchanged
            var updated = await Mediator.QueryAsync(
                new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.AreEqual("Updated Title", updated.Title, "Title should be updated");
            Assert.AreEqual(originalContent, updated.Content, "Content should remain unchanged");
        }

        /// <summary>
        /// Test SaveRegion with non-existent EditorId keeps content unchanged.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveRegion_WithNonExistentEditorId_KeepsContentUnchanged()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            article.Content = @"
                <div data-ccms-ceid='region-1'>
                    <p>Only region</p>
                </div>";
            await SaveArticleAsync(article, TestUserId);

            // Retrieve from database to get the actual stored content (which may be normalized)
            var savedArticle = await Mediator.QueryAsync(
                new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            var originalContent = savedArticle.Content;

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveRegion",
                EditorId = "region-999", // Non-existent
                Payload = CryptoJsDecryption.Encrypt("<p>This should not be applied</p>"),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));

            // Verify content unchanged when region not found
            var updated = await Mediator.QueryAsync(
                new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.AreEqual(originalContent, updated.Content, 
                "Content should remain unchanged when EditorId doesn't exist");
        }

        /// <summary>
        /// Test empty Command defaults to SavePageProperties or returns error.
        /// </summary>
        [TestMethod]
        public async Task Edit_WithEmptyCommand_ReturnsBadRequest()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = string.Empty, // Empty command
                Payload = CryptoJsDecryption.Encrypt("<p>Content</p>"),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert - Should return error for invalid/empty command
            Assert.IsNotNull(result);
            Assert.IsTrue(
                result is BadRequestObjectResult || 
                (result is JsonResult jr && JsonDocument.Parse(
                    JsonSerializer.Serialize(jr.Value)).RootElement.TryGetProperty("ServerSideSuccess", out var success) 
                    && !success.GetBoolean()),
                "Empty command should result in error");
        }

        /// <summary>
        /// Test response contains updated Model after successful save.
        /// </summary>
        [TestMethod]
        public async Task Edit_SuccessfulSave_ResponseContainsUpdatedModel()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            article.Content = "<p>Original</p>";
            await SaveArticleAsync(article, TestUserId);
            var originalVersion = article.VersionNumber;

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveBody",
                Payload = CryptoJsDecryption.Encrypt("<p>Updated content</p>"),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var response = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value)).RootElement;

            // Verify response contains Model
            Assert.IsTrue(response.TryGetProperty("Model", out var modelElement), 
                "Response should contain Model property");
            Assert.IsTrue(response.GetProperty("ServerSideSuccess").GetBoolean(),
                "Save should be successful");

            // Verify version was incremented in response
            Assert.IsTrue(modelElement.TryGetProperty("VersionNumber", out var versionElement),
                "Model should contain VersionNumber");
            var newVersion = versionElement.GetInt32();
            Assert.IsTrue(newVersion > originalVersion,
                $"Version should be incremented. Original: {originalVersion}, New: {newVersion}");
        }

        /// <summary>
        /// Test SavePageProperties preserves article content when only updating metadata.
        /// </summary>
        [TestMethod]
        public async Task Edit_SavePageProperties_PreservesArticleContent()
        {
            // Arrange
            var article = await CreateArticleAsync("Original Title", TestUserId);
            var originalContent = "<p>Important content that must not change</p>";
            article.Content = originalContent;
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SavePageProperties",
                Title = "New Title",
                BannerImage = "https://example.com/new-image.jpg",
                VersionNumber = article.VersionNumber
                // No Data field - metadata only
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var response = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value)).RootElement;
            Assert.IsTrue(response.GetProperty("ServerSideSuccess").GetBoolean());

            // Verify content preserved
            var updated = await Mediator.QueryAsync(
                new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.AreEqual(originalContent, updated.Content,
                "Article content should be preserved during metadata-only update");
            Assert.AreEqual("New Title", updated.Title,
                "Article title should be updated");
        }

        /// <summary>
        /// Test SaveCode with empty Content field.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveCode_WithEmptyContent_Succeeds()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveCode",
                Payload = CryptoJsDecryption.Encrypt(string.Empty), // Empty content
                HeadJavaScript = CryptoJsDecryption.Encrypt("<script>head</script>"),
                FooterJavaScript = CryptoJsDecryption.Encrypt("<script>footer</script>"),
                Title = article.Title,
                VersionNumber = article.VersionNumber
            };

            // Act
            var result = await controller.Edit(model);

            // Assert - Should succeed (empty content is valid)
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var response = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value)).RootElement;
            Assert.IsTrue(response.GetProperty("ServerSideSuccess").GetBoolean(),
                "SaveCode should succeed with empty Content field");
        }
    }
}

