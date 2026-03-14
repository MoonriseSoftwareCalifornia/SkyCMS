// <copyright file="EditorControllerSaveTests.cs" company="Moonrise Software, LLC">
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
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for EditorController save operations using SaveArticle feature.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class EditorControllerSaveTests : SkyCmsTestBase
    {
        private EditorController controller = null!;

        [TestInitialize]
        public new void Setup()
        {
            base.Setup();

            // Use the Mediator from the base class (already configured)
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

            // Setup user context
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "test@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        #region EditCode Method Tests

        /// <summary>
        /// Tests that EditCode_Post_UsesSaveArticleCommand.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_UsesSaveArticleCommand()
        {
            // Arrange - Create an article first
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated via EditCode",
                Payload = CryptoJsDecryption.Encrypt("<p>Updated content</p>"),
                HeadJavaScript = CryptoJsDecryption.Encrypt("<script>console.log('head');</script>"),
                FooterJavaScript = CryptoJsDecryption.Encrypt("<script>console.log('footer');</script>"),
                Updated = DateTimeOffset.UtcNow,
                Command = "SaveCode"
            };

            // Act - Call unified Edit endpoint with SaveCode command
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;

            // Verify article was updated
            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.AreEqual("Updated via EditCode", updatedArticle.Title);
        }

        /// <summary>
        /// Tests that EditCode_Post_WithValidationErrors_ReturnsErrors.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_WithValidationErrors_ReturnsErrors()
        {
            // Arrange - Create an article
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = string.Empty, // Invalid - empty title
                Payload = CryptoJsDecryption.Encrypt("<p>Content</p>"),
                HeadJavaScript = CryptoJsDecryption.Encrypt(string.Empty),
                FooterJavaScript = CryptoJsDecryption.Encrypt(string.Empty),
                Updated = DateTimeOffset.UtcNow,
                Command = "SaveCode"
            };

            // Act - Call unified Edit endpoint with SaveCode command
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);

            // Verify error structure from unified endpoint
            var json = System.Text.Json.JsonSerializer.Serialize(jsonResult.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Should have ServerSideSuccess = false for error case
            Assert.IsTrue(root.TryGetProperty("ServerSideSuccess", out var successProp),
                "Response should include ServerSideSuccess");
            Assert.IsFalse(successProp.GetBoolean(), "ServerSideSuccess should be false for validation errors");

            // Should have errors property
            Assert.IsTrue(root.TryGetProperty("errors", out var errorsProp),
                "Response should include errors property");
            Assert.IsNotNull(errorsProp, "Errors should not be null");
        }

        #endregion

        #region Edit (WYSIWYG) Method Tests

        /// <summary>
        /// Tests that Edit_Post_UsesSaveArticleCommand.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_UsesSaveArticleCommand()
        {
            // Arrange
            var article = await CreateArticleAsync("Original Title", TestUserId);
            article.Content = "<div contenteditable='true'><p>Original content</p></div>";
            await SaveArticleAsync(article, TestUserId);

            // **INVESTIGATION 1**: Verify content was saved to database
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNotNull(savedArticle?.Content, "Content should be saved in database");
            Assert.Contains("Original content", savedArticle.Content, "Content should contain expected text");
            Console.WriteLine($"Database Article Content: '{savedArticle.Content}'");

            // **INVESTIGATION 2**: Verify GetArticleByArticleNumber retrieves content properly
            var retrievedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.IsNotNull(retrievedArticle?.Content, "Retrieved article should have content");
            Assert.Contains("Original content", retrievedArticle.Content, "Retrieved content should match saved content");
            Console.WriteLine($"Retrieved Article Content: '{retrievedArticle.Content}'");

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated via WYSIWYG",
                BannerImage = "https://example.com/banner.jpg",
                ArticleType = ArticleType.General,
                Category = "Technology",
                Introduction = "Test intro",
                Command = "SavePageProperties" // Metadata-only update, preserves existing content
                // Note: EditorId and Data are NOT set, so existing content should be preserved
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult), "Controller should return JsonResult");
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value, "JsonResult.Value should not be null");

            // Serialize to JSON to inspect the actual structure
            var json = System.Text.Json.JsonSerializer.Serialize(jsonResult.Value, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            Console.WriteLine($"Controller Response:\n{json}");

            // Use JsonDocument for safer property checking
            var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            // Check which response structure we got
            if (root.TryGetProperty("success", out var successProp))
            {
                // This is the failure response format
                var successValue = successProp.GetBoolean();
                var errors = root.TryGetProperty("errors", out var errorsProp)
                    ? System.Text.Json.JsonSerializer.Serialize(errorsProp)
                    : "No errors property";

                Assert.Fail($"SaveArticle command failed. success={successValue}, errors={errors}");
            }

            // Should have ServerSideSuccess for success response
            Assert.IsTrue(root.TryGetProperty("ServerSideSuccess", out var serverSideSuccessProp),
                "Response should have ServerSideSuccess property");
            Assert.IsTrue(serverSideSuccessProp.GetBoolean(), "ServerSideSuccess should be true");

            Assert.IsTrue(root.TryGetProperty("Model", out var modelProp), "Response should have Model property");

            // Verify article was updated in database
            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.AreEqual("Updated via WYSIWYG", updatedArticle.Title);
            Assert.AreEqual("https://example.com/banner.jpg", updatedArticle.BannerImage);
            Assert.AreEqual(ArticleType.General, updatedArticle.ArticleType);
            Assert.AreEqual("Technology", updatedArticle.Category);
            Assert.AreEqual("Test intro", updatedArticle.Introduction);
            Assert.IsNotNull(updatedArticle.Content);
            Assert.Contains("Original content",
updatedArticle.Content, "Content should be preserved when EditorId is not specified");
        }

        /// <summary>
        /// Tests that Edit_Post_WithEditorRegion_UpdatesContentCorrectly.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_WithEditorRegion_UpdatesContentCorrectly()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            article.Content = "<div data-ccms-ceid=\"region1\">Original Content</div>";
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                EditorId = "region1",
                Payload = CryptoJsDecryption.Encrypt("<p>Updated Region Content</p>"),
                BannerImage = string.Empty,
                ArticleType = ArticleType.General,
                Category = string.Empty,
                Introduction = string.Empty,
                Command = "SaveRegion"
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));

            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.Contains("Updated Region Content", updatedArticle.Content);
        }

        /// <summary>
        /// Tests unified Edit POST SaveBody command updates full content and preserves title when omitted.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_SaveBodyCommand_UpdatesBodyAndPreservesTitle()
        {
            // Arrange
            var article = await CreateArticleAsync("Original Title", TestUserId);
            article.Content = "<div>Old body</div>";
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title, // Preserve original title when not changing it
                Command = "SaveBody",
                Payload = CryptoJsDecryption.Encrypt("<section><h1>New Body</h1></section>")
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            var root = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            Assert.AreEqual("Original Title", updatedArticle.Title);
            Assert.AreEqual("<section><h1>New Body</h1></section>", updatedArticle.Content);
            Assert.IsTrue(root.TryGetProperty("CdnResults", out _), "Response should include CdnResults.");
        }

        /// <summary>
        /// Tests unified Edit POST SaveRegion command updates only target region.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_SaveRegionCommand_UpdatesOnlyTargetRegion()
        {
            // Arrange
            var article = await CreateArticleAsync("Region Test", TestUserId);
            article.Content = "<div data-ccms-ceid=\"r1\">One</div><div data-ccms-ceid=\"r2\">Two</div>";
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                EditorId = "r1",
                Command = "SaveRegion",
                Payload = CryptoJsDecryption.Encrypt("<p>Updated One</p>")
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            Assert.Contains("Updated One", updatedArticle.Content);
            Assert.Contains("data-ccms-ceid=\"r2\"", updatedArticle.Content);
            Assert.Contains(">Two<", updatedArticle.Content);
        }

        /// <summary>
        /// Tests unified Edit POST can decrypt the v2 JSON envelope payload produced by the browser script.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_SaveBodyCommand_DecryptsV2EnvelopePayload()
        {
            // Arrange
            var article = await CreateArticleAsync("Envelope Test", TestUserId);
            article.Content = "<div>Old</div>";
            await SaveArticleAsync(article, TestUserId);

            const string body = "<article><p>Envelope body</p></article>";
            var envelopePayload = EncryptV2Envelope(body);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Command = "SaveBody",
                Payload = envelopePayload
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            Assert.AreEqual(body, updatedArticle.Content);
        }

        /// <summary>
        /// Tests unified Edit POST returns bad request for null model and invalid model state.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_WithInvalidInput_ReturnsBadRequest()
        {
            var scenarios = new[]
            {
                new
                {
                    Name = "NullModel",
                    Setup = (Action)(() => { }),
                    Model = (EditPostViewModel)null,
                    AssertBadRequest = (Action<BadRequestObjectResult>)(badRequest =>
                    {
                        Assert.AreEqual("No data sent.", badRequest.Value);
                    }),
                },
                new
                {
                    Name = "InvalidModelState",
                    Setup = (Action)(() => controller.ModelState.AddModelError("ArticleNumber", "ArticleNumber is required.")),
                    Model = new EditPostViewModel
                    {
                        ArticleNumber = 0,
                        Command = "SaveBody",
                        Payload = CryptoJsDecryption.Encrypt("<p>Ignored</p>")
                    },
                    AssertBadRequest = (Action<BadRequestObjectResult>)(badRequest =>
                    {
                        Assert.IsNotNull(badRequest.Value);
                    }),
                },
            };

            foreach (var scenario in scenarios)
            {
                controller.ModelState.Clear();
                scenario.Setup();

                var result = await controller.Edit(model: scenario.Model!);

                Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), scenario.Name);
                var badRequest = (BadRequestObjectResult)result;
                scenario.AssertBadRequest(badRequest);
            }
        }

        /// <summary>
        /// Tests unified Edit POST throws not found when article does not exist.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_WithUnknownArticle_ThrowsNotFoundException()
        {
            // Arrange
            var model = new EditPostViewModel
            {
                ArticleNumber = 999999,
                Title = "Test Title",
                Command = "SaveBody",
                Payload = CryptoJsDecryption.Encrypt("<p>Missing article</p>")
            };

            // Act + Assert
            try
            {
                _ = await controller.Edit(model);
                Assert.Fail("Expected NotFoundException was not thrown.");
            }
            catch (NotFoundException)
            {
            }
        }

        /// <summary>
        /// Tests unified Edit POST throws format exception for malformed encrypted payload.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_WithMalformedEncryptedData_ThrowsFormatException()
        {
            // Arrange
            var article = await CreateArticleAsync("Malformed Payload Test", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveBody",
                Payload = "not-base64-and-not-json",
                Title = "Valid Title" // Provide valid title so execution reaches decryption
            };

            // Act + Assert
            try
            {
                _ = await controller.Edit(model);
                Assert.Fail("Expected FormatException was not thrown.");
            }
            catch (FormatException)
            {
            }
        }

        /// <summary>
        /// Tests unified Edit POST SaveRegion with missing editor id keeps content unchanged but still returns success payload.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_SaveRegionWithoutEditorId_KeepsContentUnchangedAndReturnsSuccess()
        {
            // Arrange
            var article = await CreateArticleAsync("Missing Region Id", TestUserId);
            article.Content = "<div data-ccms-ceid=\"region-a\">Original</div>";
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Command = "SaveRegion",
                EditorId = string.Empty,
                Payload = CryptoJsDecryption.Encrypt("<p>Ignored update</p>")
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            Assert.AreEqual("<div data-ccms-ceid=\"region-a\">Original</div>", updatedArticle.Content);
        }

        #endregion

        #region Designer Method Tests

        /// <summary>
        /// Tests that SaveDesigner uses SaveArticleCommand.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveDesigner_UsesSaveArticleCommand()
        {
            // Arrange
            var article = await CreateArticleAsync("Designer Test", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Command = "SaveDesigner",
                Title = "Updated via Designer",
                Payload = CryptoJsDecryption.Encrypt("<div>New HTML</div>"),
                CssContent = CryptoJsDecryption.Encrypt(".test { color: red; }")
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that SaveDesigner with valid HTML and CSS succeeds and updates content.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveDesigner_WithValidHtmlAndCss_UpdatesContent()
        {
            // Arrange
            var article = await CreateArticleAsync("Designer HTML+CSS Test", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var htmlContent = "<div class='container'><h1>Welcome</h1><p>Content</p></div>";
            var cssContent = "h1 { color: blue; font-size: 32px; } p { margin: 10px; }";

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Id = article.Id,
                Title = "Designer HTML+CSS Update",
                Command = "SaveDesigner",
                Payload = CryptoJsDecryption.Encrypt(htmlContent),
                CssContent = CryptoJsDecryption.Encrypt(cssContent)
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            // Verify content was saved
            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.IsNotNull(updatedArticle.Content);
            StringAssert.Contains(updatedArticle.Content, "Welcome", "HTML content should be present");
        }

        /// <summary>
        /// Tests that SaveDesigner with HTML only (no CSS) succeeds.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveDesigner_WithHtmlOnly_Succeeds()
        {
            // Arrange
            var article = await CreateArticleAsync("Designer HTML Only Test", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var htmlContent = "<section><h2>Section Title</h2></section>";

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Id = article.Id,
                Title = "HTML Only Test",
                Command = "SaveDesigner",
                Payload = CryptoJsDecryption.Encrypt(htmlContent),
                CssContent = CryptoJsDecryption.Encrypt(string.Empty) // Empty CSS
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);
        }

        /// <summary>
        /// Tests that SaveDesigner validates nested editable regions and rejects them.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveDesigner_WithNestedEditableRegions_ReturnsFalse()
        {
            // Arrange
            var article = await CreateArticleAsync("Nested Regions Test", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // HTML with nested data-ccms-ceid attributes (invalid)
            var htmlContent = @"
                <div data-ccms-ceid='region-1'>
                    <p>Outer region</p>
                    <div data-ccms-ceid='region-2'>
                        <p>Inner nested region - NOT ALLOWED</p>
                    </div>
                </div>";

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Id = article.Id,
                Title = "Nested Regions Test",
                Command = "SaveDesigner",
                Payload = CryptoJsDecryption.Encrypt(htmlContent),
                CssContent = CryptoJsDecryption.Encrypt(string.Empty)
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            dynamic value = jsonResult.Value;
            Assert.IsNotNull(value);
            Assert.IsFalse(value!.ServerSideSuccess, "Should reject nested regions");
            Assert.IsNotNull(value.errors);
        }

        /// <summary>
        /// Tests that SaveDesigner handles GrapesJS typical output structure.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveDesigner_WithGrapesJsStructure_HandlesCorrectly()
        {
            // Arrange
            var article = await CreateArticleAsync("GrapesJS Test", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // Typical GrapesJS output
            var htmlContent = @"
                <div class='gjs-row'>
                    <div class='gjs-cell'>
                        <div data-gjs-type='text'>
                            <h1>GrapesJS Page</h1>
                        </div>
                    </div>
                </div>";

            var cssContent = @"
                .gjs-row { display: flex; margin: 10px; }
                .gjs-cell { flex: 1; padding: 10px; }
                h1 { font-size: 28px; }";

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Id = article.Id,
                Title = "GrapesJS Update",
                Command = "SaveDesigner",
                Payload = CryptoJsDecryption.Encrypt(htmlContent),
                CssContent = CryptoJsDecryption.Encrypt(cssContent)
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);
        }

        /// <summary>
        /// Tests that SaveDesigner preserves complex CSS including media queries.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveDesigner_WithComplexCss_PreservesStructure()
        {
            // Arrange
            var article = await CreateArticleAsync("Complex CSS Test", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var htmlContent = "<div class='container'><p>Responsive design</p></div>";
            var cssContent = @"
                @media (max-width: 768px) {
                    body { font-size: 14px; }
                    .container { padding: 0; }
                }
                .container {
                    display: grid;
                    grid-template-columns: repeat(3, 1fr);
                    gap: 20px;
                }
                p { margin: 0; }";

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Id = article.Id,
                Title = "Complex CSS Test",
                Command = "SaveDesigner",
                Payload = CryptoJsDecryption.Encrypt(htmlContent),
                CssContent = CryptoJsDecryption.Encrypt(cssContent)
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);
        }

        /// <summary>
        /// Tests that SaveDesigner handles special characters and Unicode.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveDesigner_WithSpecialCharacters_Preserved()
        {
            // Arrange
            var article = await CreateArticleAsync("Special Chars Test", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var htmlContent = @"
                <div>
                    <p>Special: © ® ™ € £ ¥</p>
                    <p>Emoji: 🎨 ✨ 🚀 💻 📱</p>
                    <p>Quotes: 'single' &quot;double&quot;</p>
                </div>";

            var cssContent = @"body::before { content: '✓ Styled'; }";

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Id = article.Id,
                Title = "Special Characters",
                Command = "SaveDesigner",
                Payload = CryptoJsDecryption.Encrypt(htmlContent),
                CssContent = CryptoJsDecryption.Encrypt(cssContent)
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            // Verify special characters preserved
            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.IsNotNull(updatedArticle.Content);
            StringAssert.Contains(updatedArticle.Content, "©", "Copyright symbol should be preserved");
        }

        /// <summary>
        /// Tests that SaveDesigner with large HTML content handles stress test.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveDesigner_WithLargeContent_Succeeds()
        {
            // Arrange
            var article = await CreateArticleAsync("Large Content Test", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // Generate large HTML (~250KB)
            var largeHtmlBuilder = new StringBuilder();
            largeHtmlBuilder.Append("<div>");
            for (int i = 0; i < 2500; i++)
            {
                largeHtmlBuilder.Append($"<p>Paragraph {i}: Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.</p>");
            }
            largeHtmlBuilder.Append("</div>");
            var htmlContent = largeHtmlBuilder.ToString();

            var cssContent = "p { margin: 10px; line-height: 1.6; }";

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Id = article.Id,
                Title = "Large Content",
                Command = "SaveDesigner",
                Payload = CryptoJsDecryption.Encrypt(htmlContent),
                CssContent = CryptoJsDecryption.Encrypt(cssContent)
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);
        }

        /// <summary>
        /// Tests that SaveDesigner updates metadata (title, category, etc.) correctly.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveDesigner_UpdatesMetadata()
        {
            // Arrange
            var article = await CreateArticleAsync("Metadata Test", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Id = article.Id,
                Title = "New Designer Title",
                BannerImage = "https://example.com/designer-banner.jpg",
                ArticleType = ArticleType.General,
                Category = "DesignGallery",
                Introduction = "Created with designer",
                Command = "SaveDesigner",
                Payload = CryptoJsDecryption.Encrypt("<div>Designed</div>"),
                CssContent = CryptoJsDecryption.Encrypt("div { padding: 20px; }")
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            // Verify metadata
            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.AreEqual("New Designer Title", updatedArticle.Title);
            Assert.AreEqual("DesignGallery", updatedArticle.Category);
        }

        /// <summary>
        /// Tests that SaveDesigner does not modify head/footer JavaScript.
        /// </summary>
        [TestMethod]
        public async Task Edit_SaveDesigner_DoesNotModifyScripts()
        {
            // Arrange
            var article = await CreateArticleAsync("Scripts Test", TestUserId);
            article.HeadJavaScript = "<script>// Head script</script>";
            article.FooterJavaScript = "<script>// Footer script</script>";
            await SaveArticleAsync(article, TestUserId);

            var headScriptBefore = article.HeadJavaScript;
            var footerScriptBefore = article.FooterJavaScript;

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Id = article.Id,
                Title = "Scripts Regression Test",
                Command = "SaveDesigner",
                Payload = CryptoJsDecryption.Encrypt("<div>New Design</div>"),
                CssContent = CryptoJsDecryption.Encrypt("div { color: green; }")
                // Note: HeadJavaScript and FooterJavaScript NOT provided
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            // Verify scripts not modified
            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.AreEqual(headScriptBefore, updatedArticle.HeadJavaScript, "Head script should not change");
            Assert.AreEqual(footerScriptBefore, updatedArticle.FooterJavaScript, "Footer script should not change");
        }

        #endregion

        #region Title Change Integration

        /// <summary>
        /// Tests that EditCode_Post_WithTitleChange_CreatesRedirect.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_WithTitleChange_CreatesRedirect()
        {
            // Arrange
            // **FIX**: Create a root article first, so the next article is NOT the root
            await CreateArticleAsync("Home Page", TestUserId); // This becomes the root page

            // Now create the article we want to test - this will NOT be root
            var article = await CreateArticleAsync("Original Title", TestUserId);
            article.Content = "<p>Content</p>";
            await SaveArticleAsync(article, TestUserId);

            // Publish the article with a past date to enable redirect creation
            var pastDate = DateTimeOffset.UtcNow.AddMinutes(-5);
            await Logic.PublishArticle(article.Id, pastDate);

            // Reload the article after publishing to get the updated Published date
            article = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });

            // Verify article is published
            Assert.IsNotNull(article.Published, "Article should have a Published date");
            Assert.IsTrue(article.Published <= DateTimeOffset.UtcNow, "Published date should be in the past");

            // **KEY**: Save the original URL before changing the title
            var originalUrlPath = article.UrlPath;
            Console.WriteLine($"Original UrlPath: '{originalUrlPath}'");

            // Verify this is NOT the root page
            Assert.AreNotEqual("root", originalUrlPath, "Test article should not be the root page");

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Completely Different New Title", // Make sure this creates a different slug
                Payload = CryptoJsDecryption.Encrypt("<p>Content</p>"),
                HeadJavaScript = CryptoJsDecryption.Encrypt(string.Empty),
                FooterJavaScript = CryptoJsDecryption.Encrypt(string.Empty),
                Updated = article.Updated,
                Command = "SaveCode"
            };

            // Act - Call unified Edit endpoint with SaveCode command
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));

            // Verify the controller returned success
            var jsonResult = (JsonResult)result;
            var json = System.Text.Json.JsonSerializer.Serialize(jsonResult.Value);
            Console.WriteLine($"Controller Response: {json}");

            // Debug: Check all articles
            var allArticles = await Db.Articles.ToListAsync();
            Console.WriteLine($"Total articles in DB: {allArticles.Count}");
            foreach (var a in allArticles)
            {
                Console.WriteLine($"  Article {a.ArticleNumber}: Title='{a.Title}', UrlPath='{a.UrlPath}', StatusCode={a.StatusCode}, Published={a.Published}");
            }

            // Verify redirect was created
            // Redirects are Article entities with StatusCode = Redirect
            var redirectArticles = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            Console.WriteLine($"Redirect articles found: {redirectArticles.Count}");
            foreach (var r in redirectArticles)
            {
                Console.WriteLine($"  Redirect: From '{r.UrlPath}' to (content): '{r.Content}'");
                Console.WriteLine($"  Redirect HeaderJavaScript: '{r.HeaderJavaScript}'");
            }

            Assert.HasCount(1, redirectArticles, "Expected 1 redirect article to be created");

            var redirect = redirectArticles.First();
            Assert.AreEqual(originalUrlPath, redirect.UrlPath, "Redirect should be from the original URL");

            // Verify the article was updated with new title and new URL
            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.AreEqual("Completely Different New Title", updatedArticle.Title);

            // Verify the URL changed
            Assert.AreNotEqual(originalUrlPath, updatedArticle.UrlPath, "URL path should have changed");

            // **FIX**: The redirect Content field contains HTML, not just the URL
            // We need to verify the Content contains a link to the new URL
            var expectedNewUrl = updatedArticle.UrlPath;
            Assert.Contains(expectedNewUrl,
redirect.Content, $"Redirect content should contain the new URL path '{expectedNewUrl}'");

            // Also verify the HeaderJavaScript contains the redirect logic
            Assert.IsNotNull(redirect.HeaderJavaScript, "Redirect should have HeaderJavaScript");
            Assert.Contains($"window.location.href = '{expectedNewUrl}';",
redirect.HeaderJavaScript, "Redirect JavaScript should set window.location to the new URL");

            // Verify the redirect Content contains expected HTML structure
            Assert.Contains("<h1>Redirecting to",
redirect.Content, "Redirect content should have redirect heading");
            Assert.Contains($"<a href=\"/{expectedNewUrl}\">here</a>",
redirect.Content, "Redirect content should have clickable link to new URL");
        }

        #endregion

        #region Mediator Tests

        /// <summary>
        /// Tests that Mediator_CanResolve_SaveArticleHandler.
        /// </summary>
        [TestMethod]
        public void Mediator_CanResolve_SaveArticleHandler()
        {
            // Verify the handler is properly registered
            var command = new Sky.Editor.Features.Articles.Save.SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "Test",
                Content = "<p>Test</p>",
                ArticleType = ArticleType.General,
                UserId = TestUserId
            };

            // This will throw if handler is not registered
            try
            {
                var handlerType = typeof(Cosmos.Common.Features.Shared.ICommandHandler<,>)
                    .MakeGenericType(
                        command.GetType(),
                        typeof(Cosmos.Common.Features.Shared.CommandResult<Sky.Editor.Features.Articles.Save.ArticleUpdateResult>));

                var handler = Services.GetService(handlerType);
                Assert.IsNotNull(handler, $"Handler not registered: {handlerType.Name}");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to resolve handler: {ex.Message}");
            }
        }

        #endregion

        private static JsonElement AssertUnifiedEditSuccessResponse(IActionResult result, int expectedArticleNumber)
        {
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);

            var json = JsonSerializer.Serialize(jsonResult.Value);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var successProp))
            {
                var errors = root.TryGetProperty("errors", out var errorsProp)
                    ? JsonSerializer.Serialize(errorsProp)
                    : "No errors property";
                Assert.Fail($"Unified edit returned failure payload. success={successProp.GetBoolean()}, errors={errors}");
            }

            Assert.IsTrue(root.TryGetProperty("ServerSideSuccess", out var serverSideSuccess),
                "Response should include ServerSideSuccess.");
            Assert.IsTrue(serverSideSuccess.GetBoolean(), "ServerSideSuccess should be true.");

            Assert.IsTrue(root.TryGetProperty("Model", out var modelProp), "Response should include Model.");
            Assert.IsTrue(modelProp.TryGetProperty("ArticleNumber", out var articleNumberProp),
                "Response Model should include ArticleNumber.");
            Assert.AreEqual(expectedArticleNumber, articleNumberProp.GetInt32(),
                "Response Model.ArticleNumber should match saved article.");

            return root.Clone();
        }

        private static string EncryptV2Envelope(string plainText, string keyText = "1234567890123456")
        {
            var key = Encoding.UTF8.GetBytes(keyText);
            var iv = new byte[16];
            RandomNumberGenerator.Fill(iv);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new System.IO.MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new System.IO.StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            var payload = new
            {
                v = 2,
                iv = Convert.ToBase64String(iv),
                ct = Convert.ToBase64String(ms.ToArray())
            };

            return JsonSerializer.Serialize(payload);
        }

        /// <summary>
        /// Tests unified Edit POST SaveBody command clears content when payload is null.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_SaveBodyCommand_WithNullPayload_ClearsBody()
        {
            // Arrange
            var article = await CreateArticleAsync("Null Payload Test", TestUserId);
            article.Content = "<div>Existing body</div>";
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Command = "SaveBody",
                Payload = null!
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            Assert.AreEqual(string.Empty, updatedArticle.Content);
        }

        /// <summary>
        /// Tests unified Edit POST SaveCode command clears scripts when null values are sent.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_SaveCodeCommand_WithNullScripts_ClearsScripts()
        {
            // Arrange
            var article = await CreateArticleAsync("Null Scripts Test", TestUserId);
            article.Content = "<div>Code body</div>";
            article.HeadJavaScript = "<script>head</script>";
            article.FooterJavaScript = "<script>footer</script>";
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Command = "SaveCode",
                Payload = null!,
                HeadJavaScript = null!,
                FooterJavaScript = null!
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            Assert.AreEqual(string.Empty, updatedArticle.Content);
            Assert.AreEqual(string.Empty, updatedArticle.HeadJavaScript);
            Assert.AreEqual(string.Empty, updatedArticle.FooterJavaScript);
        }

        /// <summary>
        /// Tests unified Edit POST metadata save allows clearing introduction.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_MetadataOnly_WithEmptyIntroduction_ClearsIntroduction()
        {
            // Arrange
            var article = await CreateArticleAsync("Introduction Clear Test", TestUserId);
            article.Introduction = "Old intro";
            await SaveArticleAsync(article, TestUserId);

            var model = new EditPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Command = "SavePageProperties",
                Introduction = string.Empty,
                Category = string.Empty,
                BannerImage = string.Empty
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            _ = AssertUnifiedEditSuccessResponse(result, article.ArticleNumber);

            var updatedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            Assert.AreEqual(string.Empty, updatedArticle.Introduction);
        }
    }
}


