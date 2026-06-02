// <copyright file="VsCodeControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using System;
    using System.Security.Claims;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Controllers;
    using Sky.Cms.Services;
    using Sky.Editor.Models;
    using Sky.Editor.Services.Layouts;

    /// <summary>
    /// Tests for <see cref="VsCodeController"/> browser auth and bearer-token flow.
    /// </summary>
    [TestClass]
    public class VsCodeControllerTests : SkyCmsTestBase
    {
        private VsCodeController controller = null!;
        private IMemoryCache cache = null!;

        /// <summary>
        /// Initializes test context before each test.
        /// </summary>
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
            cache = new MemoryCache(new MemoryCacheOptions());

            var mockStorageContext = new Mock<IStorageContext>();
            mockStorageContext
                .Setup(s => s.GetFilesAndDirectories(It.IsAny<string>()))
                .ReturnsAsync(new List<FileManagerEntry> { new FileManagerEntry { Name = "test.txt", IsDirectory = false, Size = 100 } });
            mockStorageContext
                .Setup(s => s.GetFileAsync(It.IsAny<string>()))
                .ReturnsAsync(new FileManagerEntry { Name = "test.txt", IsDirectory = false, Size = 100, ModifiedUtc = DateTime.UtcNow });
            mockStorageContext
                .Setup(s => s.GetStreamAsync(It.IsAny<string>()))
                .ReturnsAsync(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content")));
            mockStorageContext
                .Setup(s => s.DeleteFileAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            mockStorageContext
                .Setup(s => s.DeleteFolderAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            mockStorageContext
                .Setup(s => s.CreateFolder(It.IsAny<string>()))
                .ReturnsAsync(new FileManagerEntry { Name = "newfolder", IsDirectory = true, Path = "/pub/newfolder" });
            mockStorageContext
                .Setup(s => s.AppendBlob(It.IsAny<System.IO.MemoryStream>(), It.IsAny<Cosmos.BlobService.Models.FileUploadMetaData>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            mockStorageContext
                .Setup(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            mockStorageContext
                .Setup(s => s.MoveFolderAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var layoutVersioningService = new LayoutVersioningService(
                Db,
                ArticleHtmlService,
                NullLogger<LayoutVersioningService>.Instance);

            var titleResolver = new FileEntryTitleService(Db, Cache, DynamicConfigurationProvider);
            var contentCatalog = new ContentCatalogService(Db);
            var fileOperations = new FileOperationsService(mockStorageContext.Object, NullLogger<FileOperationsService>.Instance);
            controller = new VsCodeController(
                Db,
                NullLogger<VsCodeController>.Instance,
                cache,
                mockStorageContext.Object,
                layoutVersioningService,
                Mediator,
                TemplateService,
                DynamicConfigurationProvider,
                ArticleEditLogic,
                PublishingService,
                titleResolver,
                new FolderListingService(Db, mockStorageContext.Object, titleResolver),
                contentCatalog,
                fileOperations);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(),
            };
        }

        /// <summary>
        /// Cleanup after each test.
        /// </summary>
        [TestCleanup]
        public async Task Cleanup()
        {
            cache.Dispose();
            await DisposeAsync();
        }

        [TestMethod]
        public void StartBrowserAuth_ReturnsLoginUrlAndState()
        {
            var result = controller.StartBrowserAuth() as OkObjectResult;

            Assert.IsNotNull(result);
            var loginUrl = GetAnonymousProperty<string>(result.Value!, "loginUrl");
            var state = GetAnonymousProperty<string>(result.Value!, "state");
            var expiresInSeconds = GetAnonymousProperty<int>(result.Value!, "expiresInSeconds");

            Assert.IsFalse(string.IsNullOrWhiteSpace(loginUrl));
            Assert.IsFalse(string.IsNullOrWhiteSpace(state));
            Assert.IsTrue(loginUrl.Contains("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(loginUrl.Contains("returnUrl=", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(expiresInSeconds > 0);
        }

        [TestMethod]
        public async Task CompleteBrowserAuth_WithoutEditorRole_ReturnsForbiddenPage()
        {
            var start = controller.StartBrowserAuth() as OkObjectResult;
            var state = GetAnonymousProperty<string>(start!.Value!, "state");

            controller.ControllerContext.HttpContext = CreateHttpContext(
                isAuthenticated: true,
                username: "author@example.com",
                role: "Authors");

            var result = await controller.CompleteBrowserAuth(state) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("AuthFailed", result.ViewName);
            Assert.AreEqual(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
            var model = result.Model as Sky.Cms.Models.VsCodeAuthViewModel;
            Assert.IsNotNull(model);
            StringAssert.Contains(model.ErrorMessage, "Editor");
        }

        [TestMethod]
        public async Task BrowserAuthFlow_ExchangeProducesBearerToken_AndMeAcceptsIt()
        {
            var start = controller.StartBrowserAuth() as OkObjectResult;
            var state = GetAnonymousProperty<string>(start!.Value!, "state");

            controller.ControllerContext.HttpContext = CreateHttpContext(
                isAuthenticated: true,
                username: "editor@example.com",
                role: "Editors");

            var complete = await controller.CompleteBrowserAuth(state) as ViewResult;
            Assert.IsNotNull(complete);
            var completeModel = complete.Model as Sky.Cms.Models.VsCodeAuthViewModel;
            Assert.IsNotNull(completeModel);

            var code = completeModel.Code;
            Assert.IsFalse(string.IsNullOrWhiteSpace(code));

            controller.ControllerContext.HttpContext = CreateHttpContext();
            var exchange = controller.ExchangeBrowserAuth(new VsCodeController.AuthExchangeRequest
            {
                State = state,
                Code = code,
            }) as OkObjectResult;

            Assert.IsNotNull(exchange);
            var token = GetAnonymousProperty<string>(exchange.Value!, "token");
            Assert.IsFalse(string.IsNullOrWhiteSpace(token));

            var httpContext = CreateHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
            controller.ControllerContext.HttpContext = httpContext;

            var me = controller.Me() as OkObjectResult;
            Assert.IsNotNull(me);

            var username = GetAnonymousProperty<string>(me.Value!, "username");
            var role = GetAnonymousProperty<string>(me.Value!, "role");

            Assert.AreEqual("editor@example.com", username);
            Assert.AreEqual("Editors", role);
        }

        [TestMethod]
        public async Task ExchangeBrowserAuth_WithWrongState_ReturnsUnauthorized()
        {
            var start = controller.StartBrowserAuth() as OkObjectResult;
            var state = GetAnonymousProperty<string>(start!.Value!, "state");

            controller.ControllerContext.HttpContext = CreateHttpContext(
                isAuthenticated: true,
                username: "editor@example.com",
                role: "Editors");

            var complete = await controller.CompleteBrowserAuth(state) as ViewResult;
            Assert.IsNotNull(complete);
            var code = (complete.Model as Sky.Cms.Models.VsCodeAuthViewModel)?.Code ?? string.Empty;

            controller.ControllerContext.HttpContext = CreateHttpContext();
            var result = controller.ExchangeBrowserAuth(new VsCodeController.AuthExchangeRequest
            {
                State = "wrong-state-value",
                Code = code,
            });

            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
        }

        [TestMethod]
        public void ExchangeBrowserAuth_WithInvalidCode_ReturnsUnauthorized()
        {
            var start = controller.StartBrowserAuth() as OkObjectResult;
            var state = GetAnonymousProperty<string>(start!.Value!, "state");

            controller.ControllerContext.HttpContext = CreateHttpContext();
            var result = controller.ExchangeBrowserAuth(new VsCodeController.AuthExchangeRequest
            {
                State = state,
                Code = "BADCODE1",
            });

            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
        }

        [TestMethod]
        public async Task ExchangeBrowserAuth_CodeReplay_SecondExchangeReturnsUnauthorized()
        {
            var start = controller.StartBrowserAuth() as OkObjectResult;
            var state = GetAnonymousProperty<string>(start!.Value!, "state");

            controller.ControllerContext.HttpContext = CreateHttpContext(
                isAuthenticated: true,
                username: "editor@example.com",
                role: "Editors");

            var complete = await controller.CompleteBrowserAuth(state) as ViewResult;
            var code = (complete?.Model as Sky.Cms.Models.VsCodeAuthViewModel)?.Code ?? string.Empty;

            controller.ControllerContext.HttpContext = CreateHttpContext();
            var request = new VsCodeController.AuthExchangeRequest { State = state, Code = code };

            var first = controller.ExchangeBrowserAuth(request);
            Assert.IsInstanceOfType(first, typeof(OkObjectResult));

            // Second use of the same code must be rejected.
            var second = controller.ExchangeBrowserAuth(new VsCodeController.AuthExchangeRequest { State = state, Code = code });
            Assert.IsInstanceOfType(second, typeof(UnauthorizedObjectResult));
        }

        [TestMethod]
        public void ExchangeBrowserAuth_NullRequest_ReturnsBadRequest()
        {
            var result = controller.ExchangeBrowserAuth(null!);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task CompleteBrowserAuth_MissingState_ReturnsHtmlError()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext(
                isAuthenticated: true,
                username: "editor@example.com",
                role: "Editors");

            var result = await controller.CompleteBrowserAuth(null) as ViewResult;
            Assert.IsNotNull(result);
            Assert.AreEqual("AuthFailed", result.ViewName);
            var model = result.Model as Sky.Cms.Models.VsCodeAuthViewModel;
            Assert.IsNotNull(model);
            StringAssert.Contains(model.ErrorMessage, "state");
        }

        [TestMethod]
        public async Task CompleteBrowserAuth_ExpiredState_ReturnsHtmlError()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext(
                isAuthenticated: true,
                username: "editor@example.com",
                role: "Editors");

            var result = await controller.CompleteBrowserAuth("stale-state-that-was-never-registered") as ViewResult;
            Assert.IsNotNull(result);
            Assert.AreEqual("AuthFailed", result.ViewName);
            var model = result.Model as Sky.Cms.Models.VsCodeAuthViewModel;
            Assert.IsNotNull(model);
            StringAssert.Contains(model.ErrorMessage, "expired");
        }

        [TestMethod]
        public void Me_WithNoBearerToken_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();
            var result = controller.Me();
            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public void Me_WithInvalidBearerToken_ReturnsUnauthorized()
        {
            var httpContext = CreateHttpContext();
            httpContext.Request.Headers["Authorization"] = "Bearer totally-invalid-token";
            controller.ControllerContext.HttpContext = httpContext;

            var result = controller.Me();
            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        private static DefaultHttpContext CreateHttpContext(
            bool isAuthenticated = false,
            string username = "",
            string role = "")
        {
            var context = new DefaultHttpContext();
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("editor.example.com");

            if (isAuthenticated)
            {
                var identity = new ClaimsIdentity("TestAuth");
                identity.AddClaim(new Claim(ClaimTypes.Name, username));
                if (!string.IsNullOrWhiteSpace(role))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }

                context.User = new ClaimsPrincipal(identity);
            }

            return context;
        }

        private static string ExtractOneTimeCode(string html)
        {
            var match = Regex.Match(html, "[A-Z2-9]{8}");
            return match.Success ? match.Value : string.Empty;
        }

        private static T GetAnonymousProperty<T>(object source, string name)
        {
            var property = source.GetType().GetProperty(name);
            Assert.IsNotNull(property, $"Property '{name}' not found on anonymous response object.");

            var value = property!.GetValue(source);
            Assert.IsNotNull(value, $"Property '{name}' was null.");

            return (T)value!;
        }

        // ----------------------------------------------------------------
        // Helper: issue a bearer token for the default editor identity.
        // ----------------------------------------------------------------
        private async Task<string> IssueEditorBearerTokenAsync()
        {
            var start = controller.StartBrowserAuth() as OkObjectResult;
            var state = GetAnonymousProperty<string>(start!.Value!, "state");

            controller.ControllerContext.HttpContext = CreateHttpContext(
                isAuthenticated: true,
                username: "editor@example.com",
                role: "Editors");

            var complete = await controller.CompleteBrowserAuth(state) as ViewResult;
            var code = (complete?.Model as Sky.Cms.Models.VsCodeAuthViewModel)?.Code ?? string.Empty;

            controller.ControllerContext.HttpContext = CreateHttpContext();
            var exchange = controller.ExchangeBrowserAuth(new VsCodeController.AuthExchangeRequest
            {
                State = state,
                Code = code,
            }) as OkObjectResult;

            return GetAnonymousProperty<string>(exchange!.Value!, "token");
        }

        private async Task<DefaultHttpContext> CreateAuthorizedContextAsync()
        {
            var token = await IssueEditorBearerTokenAsync();
            var ctx = CreateHttpContext();
            ctx.Request.Headers["Authorization"] = $"Bearer {token}";
            return ctx;
        }

        #region Layouts endpoint tests

        [TestMethod]
        public async Task GetLayouts_ReturnsSeededLayout()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetLayouts() as OkObjectResult;

            Assert.IsNotNull(result);
            var list = result.Value as System.Collections.IEnumerable;
            Assert.IsNotNull(list);
            var items = new System.Collections.Generic.List<object>();
            foreach (var item in list) { items.Add(item); }
            Assert.IsTrue(items.Count >= 1);
        }

        [TestMethod]
        public async Task GetLayouts_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();

            var result = await controller.GetLayouts();

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public async Task GetLayoutVersions_ReturnsFamilyNewestFirst()
        {
            var seed = Db.Layouts.First();
            Db.Layouts.Add(new Cosmos.Common.Data.Layout
            {
                Id = Guid.NewGuid(),
                LayoutNumber = seed.LayoutNumber,
                Version = (seed.Version ?? 1) + 1,
                LayoutName = seed.LayoutName,
                Notes = seed.Notes,
                Head = seed.Head,
                HtmlHeader = seed.HtmlHeader,
                FooterHtmlContent = seed.FooterHtmlContent,
                BodyHtmlAttributes = seed.BodyHtmlAttributes,
                IsDefault = false,
                Published = null,
                LastModified = DateTimeOffset.UtcNow,
            });
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetLayoutVersions(seed.LayoutNumber) as OkObjectResult;

            Assert.IsNotNull(result);
            var list = result.Value as System.Collections.IEnumerable;
            Assert.IsNotNull(list);
            var items = new System.Collections.Generic.List<object>();
            foreach (var item in list!) { items.Add(item); }
            Assert.IsTrue(items.Count >= 2);

            var firstVersion = GetAnonymousProperty<int>(items[0], "version");
            var secondVersion = GetAnonymousProperty<int>(items[1], "version");
            Assert.IsTrue(firstVersion >= secondVersion);
        }

        [TestMethod]
        public async Task GetLayoutVersions_MissingFamily_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetLayoutVersions(99999);

            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task GetLayoutField_LayoutName_ReturnsValue()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var layout = Db.Layouts.First();

            var result = await controller.GetLayoutField(layout.LayoutNumber, "layoutname") as OkObjectResult;

            Assert.IsNotNull(result);
            var value = GetAnonymousProperty<string>(result.Value!, "value");
            Assert.AreEqual(layout.LayoutName, value);
        }

        [TestMethod]
        public async Task GetLayoutField_UnknownField_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var layout = Db.Layouts.First();

            var result = await controller.GetLayoutField(layout.LayoutNumber, "nonexistent");

            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task GetLayoutField_MissingLayout_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetLayoutField(99999, "layoutname");

            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task GetLayoutVersionField_ReturnsSpecificVersionData()
        {
            var seed = Db.Layouts.First();
            Db.Layouts.Add(new Cosmos.Common.Data.Layout
            {
                Id = Guid.NewGuid(),
                LayoutNumber = seed.LayoutNumber,
                Version = 7,
                LayoutName = "Historical Layout",
                Notes = "Old notes",
                Head = "<meta name='x' content='1'>",
                HtmlHeader = "<header>Old Header</header>",
                FooterHtmlContent = "<footer>Old Footer</footer>",
                BodyHtmlAttributes = seed.BodyHtmlAttributes,
                IsDefault = false,
                Published = DateTimeOffset.UtcNow.AddDays(-10),
                LastModified = DateTimeOffset.UtcNow.AddDays(-10),
            });
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetLayoutVersionField(seed.LayoutNumber, 7, "header") as OkObjectResult;

            Assert.IsNotNull(result);
            var content = GetAnonymousProperty<string>(result.Value!, "content");
            Assert.AreEqual("<header>Old Header</header>", content);
        }

        [TestMethod]
        public async Task GetLayoutVersionField_MissingVersion_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var layout = Db.Layouts.First();

            var result = await controller.GetLayoutVersionField(layout.LayoutNumber, 999, "head");

            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task SetLayoutField_LayoutName_UpdatesValue()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var layout = Db.Layouts.First();

            var putResult = await controller.SetLayoutField(
                layout.LayoutNumber,
                "layoutname",
                new VsCodeController.FieldUpdateRequest { Value = "Updated Name" });

            Assert.IsInstanceOfType(putResult, typeof(OkResult));

            var updated = Db.Layouts
                .Where(l => l.LayoutNumber == layout.LayoutNumber)
                .OrderByDescending(l => l.Version ?? 0)
                .First();
            Assert.AreEqual("Updated Name", updated.LayoutName);
        }

        [TestMethod]
        public async Task SetLayoutField_UnknownField_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var layout = Db.Layouts.First();

            var result = await controller.SetLayoutField(
                layout.LayoutNumber,
                "unknownfield",
                new VsCodeController.FieldUpdateRequest { Value = "x" });

            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task SetLayoutField_Head_UpdatesContent()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var layout = Db.Layouts.First();

            var putResult = await controller.SetLayoutField(
                layout.LayoutNumber,
                "head",
                new VsCodeController.FieldUpdateRequest { Content = "<style>body{}</style>" });

            Assert.IsInstanceOfType(putResult, typeof(OkResult));
            var updated = Db.Layouts
                .Where(l => l.LayoutNumber == layout.LayoutNumber)
                .OrderByDescending(l => l.Version ?? 0)
                .First();
            Assert.AreEqual("<style>body{}</style>", updated.Head);
        }

        [TestMethod]
        public async Task GetLayoutField_LegacyLayoutNumberZero_ReturnsContent()
        {
            var layout = Db.Layouts.First();
            layout.LayoutNumber = 0;
            layout.Notes = "legacy layout notes";
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetLayoutField(0, "notes") as OkObjectResult;

            Assert.IsNotNull(result);
            var content = GetAnonymousProperty<string>(result.Value!, "content");
            Assert.AreEqual("legacy layout notes", content);
        }

        [TestMethod]
        public async Task SetLayoutField_LegacyLayoutNumberZero_UpdatesValue()
        {
            var layout = Db.Layouts.First();
            layout.LayoutNumber = 0;
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var putResult = await controller.SetLayoutField(
                0,
                "layoutname",
                new VsCodeController.FieldUpdateRequest { Value = "Legacy Updated Name" });

            Assert.IsInstanceOfType(putResult, typeof(OkResult));

            var updated = Db.Layouts
                .Where(l => l.LayoutNumber == 0)
                .OrderByDescending(l => l.Version ?? 0)
                .First();
            Assert.AreEqual("Legacy Updated Name", updated.LayoutName);
        }

        #endregion

        #region Templates endpoint tests

        [TestMethod]
        public async Task GetTemplates_ReturnsSeededTemplates()
        {
            var templateId = Guid.NewGuid();
            Db.Templates.Add(new Cosmos.Common.Data.Template
            {
                Id = templateId,
                Title = "Test Template",
                Content = "<div>content</div>",
                Description = "A test template",
                LayoutId = Db.Layouts.First().Id,
            });
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetTemplates() as OkObjectResult;

            Assert.IsNotNull(result);
            var list = result.Value as System.Collections.IEnumerable;
            Assert.IsNotNull(list);
            var items = new System.Collections.Generic.List<object>();
            foreach (var item in list) { items.Add(item); }
            Assert.IsTrue(items.Count >= 1);
        }

        [TestMethod]
        public async Task GetTemplateField_Content_ReturnsContent()
        {
            var templateId = Guid.NewGuid();
            Db.Templates.Add(new Cosmos.Common.Data.Template
            {
                Id = templateId,
                Title = "T1",
                Content = "<h1>Hello</h1>",
                LayoutId = Db.Layouts.First().Id,
            });
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetTemplateField(templateId, "content") as OkObjectResult;

            Assert.IsNotNull(result);
            var content = GetAnonymousProperty<string>(result.Value!, "content");
            Assert.AreEqual("<h1>Hello</h1>", content);
        }

        [TestMethod]
        public async Task GetTemplateField_MissingTemplate_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetTemplateField(Guid.NewGuid(), "content");

            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task SetTemplateField_Title_UpdatesValue()
        {
            var templateId = Guid.NewGuid();
            Db.Templates.Add(new Cosmos.Common.Data.Template
            {
                Id = templateId,
                Title = "Original",
                Content = string.Empty,
                LayoutId = Db.Layouts.First().Id,
            });
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var putResult = await controller.SetTemplateField(
                templateId,
                "title",
                new VsCodeController.FieldUpdateRequest { Value = "New Title" });

            Assert.IsInstanceOfType(putResult, typeof(OkResult));
            var updated = Db.Templates.Find(templateId);
            Assert.AreEqual("New Title", updated!.Title);
        }

        #endregion

        #region Articles endpoint tests

        [TestMethod]
        public async Task GetArticles_ReturnsInventoryWithPublishedFlags()
        {
            await CreateArticleAsync("Root Article", TestUserId);
            await CreateArticleAsync("Draft Article", TestUserId);
            var published = await CreateArticleAsync("Published Article", TestUserId);

            var publishedEntity = Db.Articles.First(a => a.ArticleNumber == published.ArticleNumber);
            publishedEntity.Published = DateTimeOffset.UtcNow.AddDays(-1);
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetArticles() as OkObjectResult;

            Assert.IsNotNull(result);
            var rows = result.Value as System.Collections.Generic.List<Sky.Editor.Models.EditorInventoryItem>;

            Assert.IsNotNull(rows);
            Assert.IsTrue(rows.Count >= 2);
            Assert.IsTrue(rows.Any(r => r.IsPublished));
            Assert.IsTrue(rows.Any(r => !r.IsPublished));
        }

        [TestMethod]
        public async Task GetArticleField_Title_ReturnsValue()
        {
            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Field Test Article", TestUserId);

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetArticleField(article.ArticleNumber, "title") as OkObjectResult;

            Assert.IsNotNull(result);
            var value = GetAnonymousProperty<string>(result.Value!, "value");
            Assert.IsFalse(string.IsNullOrWhiteSpace(value));
        }

        [TestMethod]
        public async Task GetArticleField_MissingArticle_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetArticleField(99999, "title");

            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task SetArticleField_Title_UpdatesValue()
        {
            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Original Title", TestUserId);

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var putResult = await controller.SetArticleField(
                article.ArticleNumber,
                "title",
                new VsCodeController.FieldUpdateRequest { Value = "Updated Title" });

            Assert.IsInstanceOfType(putResult, typeof(OkResult));

            var updated = Db.Articles
                .OrderByDescending(a => a.VersionNumber)
                .First(a => a.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual("Updated Title", updated.Title);
        }

        [TestMethod]
        public async Task SetArticleField_Published_SetsDateValue()
        {
            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Schedule Article", TestUserId);

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var putResult = await controller.SetArticleField(
                article.ArticleNumber,
                "published",
                new VsCodeController.FieldUpdateRequest { Value = "2026-12-01T09:00:00Z" });

            Assert.IsInstanceOfType(putResult, typeof(OkResult));

            var updated = Db.Articles
                .OrderByDescending(a => a.VersionNumber)
                .First(a => a.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(updated.Published);
            Assert.AreEqual(2026, updated.Published!.Value.Year);
        }

        [TestMethod]
        public async Task SetArticleField_Published_ClearsDateWhenEmpty()
        {
            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Clear Date", TestUserId);
            var entity = Db.Articles.First(a => a.ArticleNumber == article.ArticleNumber);
            entity.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var putResult = await controller.SetArticleField(
                article.ArticleNumber,
                "published",
                new VsCodeController.FieldUpdateRequest { Value = null });

            Assert.IsInstanceOfType(putResult, typeof(OkResult));

            var updated = Db.Articles
                .OrderByDescending(a => a.VersionNumber)
                .First(a => a.ArticleNumber == article.ArticleNumber);
            Assert.IsNull(updated.Published);
        }

        [TestMethod]
        public async Task SetArticleField_UnknownField_ReturnsNotFound()
        {
            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Test Article", TestUserId);

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.SetArticleField(
                article.ArticleNumber,
                "nonexistentfield",
                new VsCodeController.FieldUpdateRequest { Value = "x" });

            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Blogs endpoint tests

        [TestMethod]
        public async Task GetBlogs_ReturnsSeededBlogStreams()
        {
            var streamType = (int)Cosmos.Cms.Common.ArticleType.BlogStream;
            Db.Articles.Add(new Cosmos.Common.Data.Article
            {
                ArticleNumber = 200,
                VersionNumber = 1,
                Title = "My Blog",
                BlogKey = "my-blog",
                ArticleType = streamType,
                UserId = string.Empty,
                Updated = DateTimeOffset.UtcNow,
            });
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetBlogs() as OkObjectResult;

            Assert.IsNotNull(result);
            var list = result.Value as System.Collections.IEnumerable;
            Assert.IsNotNull(list);
            var items = new System.Collections.Generic.List<object>();
            foreach (var item in list) { items.Add(item); }
            Assert.IsTrue(items.Count >= 1);

            var blog = items[0];
            var blogKey = GetAnonymousProperty<string>(blog, "blogKey");
            Assert.AreEqual("my-blog", blogKey);
        }

        [TestMethod]
        public async Task GetBlogs_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();

            var result = await controller.GetBlogs();

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public async Task GetBlogs_EmptyDatabase_ReturnsEmptyList()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetBlogs() as OkObjectResult;

            Assert.IsNotNull(result);
            var list = result.Value as System.Collections.IEnumerable;
            var items = new System.Collections.Generic.List<object>();
            foreach (var item in list!) { items.Add(item); }
            Assert.AreEqual(0, items.Count);
        }

        [TestMethod]
        public async Task GetBlogPosts_ReturnsPostsForBlogKey()
        {
            var postType = (int)Cosmos.Cms.Common.ArticleType.BlogPost;
            var now = DateTimeOffset.UtcNow;
            Db.Articles.AddRange(
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 301,
                    VersionNumber = 1,
                    Title = "Post Alpha",
                    BlogKey = "tech-blog",
                    ArticleType = postType,
                    Published = now.AddDays(-2),
                    UserId = string.Empty,
                    Updated = now,
                },
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 302,
                    VersionNumber = 1,
                    Title = "Post Beta",
                    BlogKey = "tech-blog",
                    ArticleType = postType,
                    Published = now.AddDays(-1),
                    UserId = string.Empty,
                    Updated = now,
                });
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetBlogPosts("tech-blog") as OkObjectResult;

            Assert.IsNotNull(result);
            var list = result.Value as System.Collections.IEnumerable;
            var items = new System.Collections.Generic.List<object>();
            foreach (var item in list!) { items.Add(item); }
            Assert.AreEqual(2, items.Count);

            // Newest post appears first (descending order by Published)
            var first = items[0];
            var firstTitle = GetAnonymousProperty<string>(first, "title");
            Assert.AreEqual("Post Beta", firstTitle);
        }

        [TestMethod]
        public async Task GetBlogPosts_MarksDraftAndPublishedCorrectly()
        {
            var postType = (int)Cosmos.Cms.Common.ArticleType.BlogPost;
            var now = DateTimeOffset.UtcNow;
            Db.Articles.AddRange(
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 401,
                    VersionNumber = 1,
                    Title = "Published Post",
                    BlogKey = "news",
                    ArticleType = postType,
                    Published = now.AddDays(-1),
                    UserId = string.Empty,
                    Updated = now,
                },
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 402,
                    VersionNumber = 1,
                    Title = "Draft Post",
                    BlogKey = "news",
                    ArticleType = postType,
                    Published = null,
                    UserId = string.Empty,
                    Updated = now,
                });
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetBlogPosts("news") as OkObjectResult;

            Assert.IsNotNull(result);
            var list = result.Value as System.Collections.IEnumerable;
            var items = new System.Collections.Generic.List<object>();
            foreach (var item in list!) { items.Add(item); }
            Assert.AreEqual(2, items.Count);

            var publishedPost = items.First(i => GetAnonymousProperty<string>(i, "title") == "Published Post");
            var draftPost = items.First(i => GetAnonymousProperty<string>(i, "title") == "Draft Post");

            Assert.IsTrue(GetAnonymousProperty<bool>(publishedPost, "isPublished"));
            Assert.IsFalse(GetAnonymousProperty<bool>(draftPost, "isPublished"));
        }

        [TestMethod]
        public async Task GetBlogPosts_UnknownBlogKey_ReturnsEmptyList()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetBlogPosts("no-such-blog") as OkObjectResult;

            Assert.IsNotNull(result);
            var list = result.Value as System.Collections.IEnumerable;
            var items = new System.Collections.Generic.List<object>();
            foreach (var item in list!) { items.Add(item); }
            Assert.AreEqual(0, items.Count);
        }

        [TestMethod]
        public async Task GetBlogPosts_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();

            var result = await controller.GetBlogPosts("any-blog");

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public async Task GetBlogPosts_OnlyLatestVersionPerPost_Returned()
        {
            var postType = (int)Cosmos.Cms.Common.ArticleType.BlogPost;
            var now = DateTimeOffset.UtcNow;
            Db.Articles.AddRange(
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 501,
                    VersionNumber = 1,
                    Title = "Post v1",
                    BlogKey = "multi-version",
                    ArticleType = postType,
                    Published = now.AddDays(-3),
                    UserId = string.Empty,
                    Updated = now,
                },
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 501,
                    VersionNumber = 2,
                    Title = "Post v2",
                    BlogKey = "multi-version",
                    ArticleType = postType,
                    Published = now.AddDays(-3),
                    UserId = string.Empty,
                    Updated = now,
                });
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetBlogPosts("multi-version") as OkObjectResult;

            Assert.IsNotNull(result);
            var list = result.Value as System.Collections.IEnumerable;
            var items = new System.Collections.Generic.List<object>();
            foreach (var item in list!) { items.Add(item); }
            Assert.AreEqual(1, items.Count);

            var post = items[0];
            var title = GetAnonymousProperty<string>(post, "title");
            Assert.AreEqual("Post v2", title);
        }

        #endregion

        #region Logout tests

        [TestMethod]
        public async Task Logout_RemovesToken_AndSubsequentMeReturnsUnauthorized()
        {
            var token = await IssueEditorBearerTokenAsync();
            var ctx = CreateHttpContext();
            ctx.Request.Headers["Authorization"] = $"Bearer {token}";
            controller.ControllerContext.HttpContext = ctx;

            // Confirm token is valid before logout
            var meBefore = controller.Me() as OkObjectResult;
            Assert.IsNotNull(meBefore);

            // Logout
            var logoutResult = controller.Logout();
            Assert.IsInstanceOfType(logoutResult, typeof(OkResult));

            // Me should now be unauthorized (token removed from cache)
            var meAfter = controller.Me();
            Assert.IsInstanceOfType(meAfter, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public void Logout_WithNoToken_ReturnsOk()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();

            var result = controller.Logout();

            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        #endregion

        #region Phase 3: Article workflow tests

        [TestMethod]
        public async Task PublishArticle_SetsPusblishedTimestamp()
        {
            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Publish Target", TestUserId);
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.PublishArticle(article.ArticleNumber);

            Assert.IsInstanceOfType(result, typeof(OkResult));
            var updated = Db.Articles.First(a => a.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(updated.Published);
        }

        [TestMethod]
        public async Task PublishArticle_MissingArticle_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var result = await controller.PublishArticle(99999);
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task PublishArticle_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();
            var result = await controller.PublishArticle(1);
            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public async Task UnpublishArticle_ClearsPublishedTimestamp()
        {
            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Unpublish Target", TestUserId);
            var entity = Db.Articles.First(a => a.ArticleNumber == article.ArticleNumber);
            entity.Published = DateTimeOffset.UtcNow.AddDays(-1);
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.UnpublishArticle(article.ArticleNumber);

            Assert.IsInstanceOfType(result, typeof(OkResult));
            var updated = Db.Articles.First(a => a.ArticleNumber == article.ArticleNumber);
            Assert.IsNull(updated.Published);
        }

        [TestMethod]
        public async Task UnpublishArticle_MissingArticle_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var result = await controller.UnpublishArticle(99999);
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task RestoreArticle_DeletedArticle_ReturnsOk()
        {
            var article = new Cosmos.Common.Data.Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 321,
                Title = "Deleted Article",
                VersionNumber = 1,
                StatusCode = (int)Cosmos.Common.Data.Logic.StatusCodeEnum.Deleted,
                UserId = TestUserId.ToString(),
                UrlPath = "deleted-article",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow,
            };

            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.RestoreArticle(article.ArticleNumber);

            Assert.IsInstanceOfType(result, typeof(OkResult));
            var restored = Db.Articles.First(a => a.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual((int)Cosmos.Common.Data.Logic.StatusCodeEnum.Active, restored.StatusCode);
            Assert.IsNull(restored.Published);
        }

        [TestMethod]
        public async Task RestoreArticle_MissingArticle_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var result = await controller.RestoreArticle(99999);
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task CreateArticle_ReturnsNewArticleNumber()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.CreateArticle(
                new VsCodeController.CreateArticleRequest { Title = "Brand New Article", ArticleType = 0 }) as OkObjectResult;

            Assert.IsNotNull(result);
            var articleNumber = GetAnonymousProperty<int>(result.Value!, "articleNumber");
            var title = GetAnonymousProperty<string>(result.Value!, "title");

            Assert.IsTrue(articleNumber > 0);
            Assert.AreEqual("Brand New Article", title);
            Assert.IsTrue(Db.Articles.Any(a => a.ArticleNumber == articleNumber));
        }

        [TestMethod]
        public async Task CreateArticle_EmptyTitle_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.CreateArticle(
                new VsCodeController.CreateArticleRequest { Title = "   " });

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task CreateArticle_NullRequest_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.CreateArticle(null!);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task CreateTemplate_CreatesTemplateAndInitialVersion()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var templateCountBefore = Db.Templates.Count();

            var result = await controller.CreateTemplate() as OkObjectResult;

            Assert.IsNotNull(result);
            var templateId = GetAnonymousProperty<Guid>(result.Value!, "templateId");
            var title = GetAnonymousProperty<string>(result.Value!, "title");

            Assert.IsFalse(templateId == Guid.Empty);
            Assert.IsFalse(string.IsNullOrWhiteSpace(title));
            Assert.AreEqual(templateCountBefore + 1, Db.Templates.Count());
            Assert.IsTrue(Db.PageDesignVersions.Any(v => v.TemplateId == templateId));
        }

        [TestMethod]
        public async Task CreateTemplate_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();

            var result = await controller.CreateTemplate();

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        #endregion

        #region Phase 3: Layout workflow tests

        [TestMethod]
        public async Task PublishLayoutVersion_SetsPublishedTimestamp()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var layout = Db.Layouts.First();

            var result = await controller.PublishLayoutVersion(layout.LayoutNumber, layout.Version ?? 1);

            Assert.IsInstanceOfType(result, typeof(OkResult));
            var updated = Db.Layouts.First(l => l.Id == layout.Id);
            Assert.IsNotNull(updated.Published);
        }

        [TestMethod]
        public async Task PublishLayoutVersion_MissingLayout_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var result = await controller.PublishLayoutVersion(99999, 1);
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task PublishLayoutVersion_WrongVersion_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var layout = Db.Layouts.First();
            var result = await controller.PublishLayoutVersion(layout.LayoutNumber, 9999);
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task SetDefaultLayoutVersion_SetsIsDefault()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var layout = Db.Layouts.First();
            layout.IsDefault = false;
            await Db.SaveChangesAsync();

            var result = await controller.SetDefaultLayoutVersion(layout.LayoutNumber, layout.Version ?? 1);

            Assert.IsInstanceOfType(result, typeof(OkResult));
            var updated = Db.Layouts.First(l => l.Id == layout.Id);
            Assert.IsTrue(updated.IsDefault);
        }

        [TestMethod]
        public async Task SetDefaultLayoutVersion_MissingLayout_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var result = await controller.SetDefaultLayoutVersion(99999, 1);
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task DuplicateLayoutVersion_CreatesNewVersion()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var layout = Db.Layouts.First();
            var countBefore = Db.Layouts.Count(l => l.LayoutNumber == layout.LayoutNumber);

            var result = await controller.DuplicateLayoutVersion(layout.LayoutNumber) as OkObjectResult;

            Assert.IsNotNull(result);
            var newVersion = GetAnonymousProperty<int>(result.Value!, "version");
            Assert.IsTrue(newVersion > (layout.Version ?? 1));

            var countAfter = Db.Layouts.Count(l => l.LayoutNumber == layout.LayoutNumber);
            Assert.AreEqual(countBefore + 1, countAfter);

            var duplicate = Db.Layouts.First(l => l.LayoutNumber == layout.LayoutNumber && l.Version == newVersion);
            Assert.AreEqual(layout.LayoutName, duplicate.LayoutName);
            Assert.IsNull(duplicate.Published);
            Assert.IsFalse(duplicate.IsDefault);
        }

        [TestMethod]
        public async Task DuplicateLayoutVersion_MissingLayout_ReturnsNotFound()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var result = await controller.DuplicateLayoutVersion(99999);
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task GetLayouts_IncludesVersionNumber()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var result = await controller.GetLayouts() as OkObjectResult;
            Assert.IsNotNull(result);

            var list = result.Value as System.Collections.IEnumerable;
            Assert.IsNotNull(list);
            var items = new System.Collections.Generic.List<object>();
            foreach (var item in list) { items.Add(item); }
            Assert.IsTrue(items.Count >= 1);

            var version = GetAnonymousProperty<int>(items[0], "version");
            Assert.IsTrue(version >= 1);
        }

        #endregion

        #region Phase 4: File System tests

        [TestMethod]
        public async Task GetFilesList_Root_ReturnsPublicFolderContents()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetFilesList(null) as OkObjectResult;

            Assert.IsNotNull(result);
            var items = result.Value as System.Collections.IEnumerable;
            Assert.IsNotNull(items);
            // Test expects at least some entries (actual count depends on mock storage)
        }

        [TestMethod]
        public async Task GetFilesList_Root_IncludesPathAndMimeType()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetFilesList(null) as OkObjectResult;

            Assert.IsNotNull(result);
            var items = (result.Value as System.Collections.IEnumerable)!.Cast<object>().ToList();
            Assert.IsTrue(items.Count >= 1);

            var first = items[0];
            var path = GetAnonymousProperty<string>(first, "path");
            var mimeType = GetAnonymousProperty<string>(first, "mimeType");
            var isDir = GetAnonymousProperty<bool>(first, "isDir");

            Assert.IsFalse(string.IsNullOrWhiteSpace(path));
            Assert.IsTrue(path.StartsWith("/", StringComparison.Ordinal));
            Assert.IsFalse(string.IsNullOrWhiteSpace(mimeType));
            if (isDir)
            {
                Assert.AreEqual("directory", mimeType);
            }
        }

        [TestMethod]
        public async Task GetFilesList_ArticlesRoot_MapsFolderNumbersToArticleTitles()
        {
            var article = await CreateArticleAsync("Mapped Folder Title", TestUserId);

            var localCache = new MemoryCache(new MemoryCacheOptions());
            var storageMock = new Mock<IStorageContext>();
            storageMock
                .Setup(s => s.GetFilesAndDirectories("/pub/articles"))
                .ReturnsAsync(new List<FileManagerEntry>
                {
                    new FileManagerEntry
                    {
                        Name = article.ArticleNumber.ToString(),
                        Path = $"/pub/articles/{article.ArticleNumber}",
                        IsDirectory = true,
                        Size = 0,
                        ModifiedUtc = DateTime.UtcNow,
                        Extension = string.Empty,
                    },
                });

            var layoutVersioningService = new LayoutVersioningService(
                Db,
                ArticleHtmlService,
                NullLogger<LayoutVersioningService>.Instance);

            var localTitleResolver = new FileEntryTitleService(Db, Cache, DynamicConfigurationProvider);
            var localContentCatalog = new ContentCatalogService(Db);
            var localFileOperations = new FileOperationsService(storageMock.Object, NullLogger<FileOperationsService>.Instance);
            var localController = new VsCodeController(
                Db,
                NullLogger<VsCodeController>.Instance,
                localCache,
                storageMock.Object,
                layoutVersioningService,
                Mediator,
                TemplateService,
                DynamicConfigurationProvider,
                ArticleEditLogic,
                PublishingService,
                localTitleResolver,
                new FolderListingService(Db, storageMock.Object, localTitleResolver),
                localContentCatalog,
                localFileOperations);

            localController.ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(
                    isAuthenticated: true,
                    username: "editor@example.com",
                    role: "Editors"),
            };

            var pathHash = EncodeTestPath("/pub/articles");
            var result = await localController.GetFilesList(pathHash) as OkObjectResult;

            Assert.IsNotNull(result);
            var items = (result.Value as System.Collections.IEnumerable)!.Cast<object>().ToList();
            Assert.AreEqual(1, items.Count);

            // friendly display name must be the article title, not the integer
            Assert.AreEqual("Mapped Folder Title", GetAnonymousProperty<string>(items[0], "name"),
                "name must be the article title, not the folder number.");

            // canonical storage path must be preserved
            Assert.AreEqual($"/pub/articles/{article.ArticleNumber}", GetAnonymousProperty<string>(items[0], "path"),
                "path must be the canonical storage path.");

            localCache.Dispose();
        }

        [TestMethod]
        public async Task GetFilesList_ArticlesRoot_DisplayPathContainsTitleNotNumber()
        {
            // Verifies that displayPath substitutes the article title in place of the
            // folder number (e.g. /pub/articles/My Great Article, not /pub/articles/42).
            var article = await CreateArticleAsync("My Great Article", TestUserId);

            var localCache = new MemoryCache(new MemoryCacheOptions());
            var storageMock = new Mock<IStorageContext>();
            storageMock
                .Setup(s => s.GetFilesAndDirectories("/pub/articles"))
                .ReturnsAsync(new List<FileManagerEntry>
                {
                    new FileManagerEntry
                    {
                        Name = article.ArticleNumber.ToString(),
                        Path = $"/pub/articles/{article.ArticleNumber}",
                        IsDirectory = true,
                        Size = 0,
                        ModifiedUtc = DateTime.UtcNow,
                        Extension = string.Empty,
                    },
                });

            var layoutVersioningService = new LayoutVersioningService(
                Db,
                ArticleHtmlService,
                NullLogger<LayoutVersioningService>.Instance);

            var localTitleResolver = new FileEntryTitleService(Db, Cache, DynamicConfigurationProvider);
            var localContentCatalog = new ContentCatalogService(Db);
            var localFileOperations = new FileOperationsService(storageMock.Object, NullLogger<FileOperationsService>.Instance);
            var localController = new VsCodeController(
                Db,
                NullLogger<VsCodeController>.Instance,
                localCache,
                storageMock.Object,
                layoutVersioningService,
                Mediator,
                TemplateService,
                DynamicConfigurationProvider,
                ArticleEditLogic,
                PublishingService,
                localTitleResolver,
                new FolderListingService(Db, storageMock.Object, localTitleResolver),
                localContentCatalog,
                localFileOperations);

            localController.ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(
                    isAuthenticated: true,
                    username: "editor@example.com",
                    role: "Editors"),
            };

            var pathHash = EncodeTestPath("/pub/articles");
            var result = await localController.GetFilesList(pathHash) as OkObjectResult;

            Assert.IsNotNull(result);
            var items = (result.Value as System.Collections.IEnumerable)!.Cast<object>().ToList();
            Assert.AreEqual(1, items.Count, "Expected exactly one article folder entry.");

            var displayPath = GetAnonymousProperty<string>(items[0], "displayPath");
            Assert.IsTrue(
                displayPath.Contains("My Great Article", StringComparison.OrdinalIgnoreCase),
                $"displayPath '{displayPath}' must contain the article title 'My Great Article', not the folder number.");

            Assert.IsFalse(
                displayPath.Contains(article.ArticleNumber.ToString(), StringComparison.Ordinal),
                $"displayPath '{displayPath}' must NOT contain the raw article number '{article.ArticleNumber}'. " +
                $"The integer must be replaced by the article title.");

            localCache.Dispose();
        }

        [TestMethod]
        public async Task GetFilesList_ArticlesRoot_CanonicalPathPreservesNumber()
        {
            // Verifies that 'path' (the real/canonical storage path) retains the integer
            // folder name, distinct from the friendly 'displayPath'.
            var article = await CreateArticleAsync("Canonical Path Test Article", TestUserId);

            var localCache = new MemoryCache(new MemoryCacheOptions());
            var storageMock = new Mock<IStorageContext>();
            storageMock
                .Setup(s => s.GetFilesAndDirectories("/pub/articles"))
                .ReturnsAsync(new List<FileManagerEntry>
                {
                    new FileManagerEntry
                    {
                        Name = article.ArticleNumber.ToString(),
                        Path = $"/pub/articles/{article.ArticleNumber}",
                        IsDirectory = true,
                        Size = 0,
                        ModifiedUtc = DateTime.UtcNow,
                        Extension = string.Empty,
                    },
                });

            var layoutVersioningService = new LayoutVersioningService(
                Db,
                ArticleHtmlService,
                NullLogger<LayoutVersioningService>.Instance);

            var localTitleResolver = new FileEntryTitleService(Db, Cache, DynamicConfigurationProvider);
            var localContentCatalog = new ContentCatalogService(Db);
            var localFileOperations = new FileOperationsService(storageMock.Object, NullLogger<FileOperationsService>.Instance);
            var localController = new VsCodeController(
                Db,
                NullLogger<VsCodeController>.Instance,
                localCache,
                storageMock.Object,
                layoutVersioningService,
                Mediator,
                TemplateService,
                DynamicConfigurationProvider,
                ArticleEditLogic,
                PublishingService,
                localTitleResolver,
                new FolderListingService(Db, storageMock.Object, localTitleResolver),
                localContentCatalog,
                localFileOperations);

            localController.ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(
                    isAuthenticated: true,
                    username: "editor@example.com",
                    role: "Editors"),
            };

            var pathHash = EncodeTestPath("/pub/articles");
            var result = await localController.GetFilesList(pathHash) as OkObjectResult;

            Assert.IsNotNull(result);
            var items = (result.Value as System.Collections.IEnumerable)!.Cast<object>().ToList();
            Assert.AreEqual(1, items.Count, "Expected exactly one article folder entry.");

            var canonicalPath = GetAnonymousProperty<string>(items[0], "path");
            var expectedPath = $"/pub/articles/{article.ArticleNumber}";
            Assert.AreEqual(expectedPath, canonicalPath,
                $"path must be the canonical storage path '{expectedPath}', not a title-substituted version. " +
                $"Consumers depend on 'path' to make API calls back to the server.");

            localCache.Dispose();
        }

        [TestMethod]
        public async Task GetFilesList_InvalidHash_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetFilesList("!!!invalid hash!!!") as BadRequestObjectResult;

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task GetFilesList_ArbitraryPath_ReturnsOk()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var outsidePathHash = EncodeTestPath("/private/secret.txt");

            var result = await controller.GetFilesList(outsidePathHash);

            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        }

        [TestMethod]
        public async Task GetFilesList_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();

            var result = await controller.GetFilesList(null);

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public async Task GetFileStat_ValidPath_ReturnsMetadata()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            // Use a valid path hash (base64 of "/pub")
            var pathHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("/pub"))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            var result = await controller.GetFileStat(pathHash) as OkObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            
            // The value is an anonymous object, so we verify it has the expected properties
            var value = result.Value as dynamic;
            Assert.IsNotNull(value);
            Assert.IsTrue(value.size >= 0);
            Assert.IsTrue(value.mtime >= 0);
            Assert.IsFalse((bool)value.isDir);
        }

        [TestMethod]
        public async Task GetFileStat_InvalidHash_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.GetFileStat("invalid!!!hash");

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task GetFileStat_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();
            var pathHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("/pub"))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            var result = await controller.GetFileStat(pathHash);

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public async Task GetFileContent_SmallFile_ReturnsBytes()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var pathHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("/pub"))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            var result = await controller.GetFileContent(pathHash);

            // Result should be a FileResult or OK with base64 content
            Assert.IsNotNull(result);
            Assert.IsTrue(
                result is FileResult || 
                result is OkObjectResult,
                "Expected FileResult or OkObjectResult for file content");
        }

        [TestMethod]
        public async Task GetFileContent_InvalidHash_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            // Use an invalid base64 hash with invalid characters
            var invalidHash = "!!!invalid!!!";

            var result = await controller.GetFileContent(invalidHash);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task GetFileContent_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();
            var pathHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("/pub"))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            var result = await controller.GetFileContent(pathHash);

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        #endregion

        #region Phase 5: Write operation tests

        private static string EncodeTestPath(string path)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(path);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        // --- DeleteFile ---

        [TestMethod]
        public async Task DeleteFile_ValidPath_ReturnsNoContent()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var pathHash = EncodeTestPath("/pub/test.txt");

            var result = await controller.DeleteFile(pathHash);

            Assert.IsInstanceOfType(result, typeof(NoContentResult));
        }

        [TestMethod]
        public async Task DeleteFile_InvalidHash_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.DeleteFile("!!!bad!!!");

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task DeleteFile_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();
            var pathHash = EncodeTestPath("/pub/test.txt");

            var result = await controller.DeleteFile(pathHash);

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        // --- DeleteFolder ---

        [TestMethod]
        public async Task DeleteFolder_ValidPath_ReturnsNoContent()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var pathHash = EncodeTestPath("/pub/images");

            var result = await controller.DeleteFolder(pathHash);

            Assert.IsInstanceOfType(result, typeof(NoContentResult));
        }

        [TestMethod]
        public async Task DeleteFolder_InvalidHash_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.DeleteFolder("!!!bad!!!");

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task DeleteFolder_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();
            var pathHash = EncodeTestPath("/pub/images");

            var result = await controller.DeleteFolder(pathHash);

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        // --- CreateFolder ---

        [TestMethod]
        public async Task CreateFolder_ValidPath_ReturnsCreated()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var pathHash = EncodeTestPath("/pub/newfolder");

            var result = await controller.CreateFolder(pathHash) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(201, result.StatusCode);
        }

        [TestMethod]
        public async Task CreateFolder_InvalidHash_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.CreateFolder("!!!bad!!!");

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task CreateFolder_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();
            var pathHash = EncodeTestPath("/pub/newfolder");

            var result = await controller.CreateFolder(pathHash);

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        // --- UploadFile ---

        [TestMethod]
        public async Task UploadFile_WithBody_ReturnsNoContent()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var pathHash = EncodeTestPath("/pub/test.txt");

            // Set up request body with content
            var body = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("hello world"));
            controller.ControllerContext.HttpContext.Request.Body = body;
            controller.ControllerContext.HttpContext.Request.ContentLength = body.Length;

            var result = await controller.UploadFile(pathHash);

            Assert.IsInstanceOfType(result, typeof(NoContentResult));
        }

        [TestMethod]
        public async Task UploadFile_EmptyBody_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var pathHash = EncodeTestPath("/pub/test.txt");

            // No content length set — defaults to null
            controller.ControllerContext.HttpContext.Request.ContentLength = null;

            var result = await controller.UploadFile(pathHash);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task UploadFile_InvalidHash_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();

            var result = await controller.UploadFile("!!!bad!!!");

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task UploadFile_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();
            var pathHash = EncodeTestPath("/pub/test.txt");

            var result = await controller.UploadFile(pathHash);

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        #endregion

        #region Phase 6: Move operation tests

        [TestMethod]
        public async Task MoveFile_ValidRequest_ReturnsNoContent()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var pathHash = EncodeTestPath("/pub/old.txt");
            var request = new VsCodeController.MoveRequest { Destination = "/pub/new.txt" };

            var result = await controller.MoveFile(pathHash, request);

            Assert.IsInstanceOfType(result, typeof(NoContentResult));
        }

        [TestMethod]
        public async Task MoveFile_InvalidHash_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var request = new VsCodeController.MoveRequest { Destination = "/pub/new.txt" };

            var result = await controller.MoveFile("!!!bad!!!", request);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task MoveFile_MissingDestination_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var pathHash = EncodeTestPath("/pub/old.txt");
            var request = new VsCodeController.MoveRequest { Destination = null };

            var result = await controller.MoveFile(pathHash, request);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task MoveFile_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();
            var pathHash = EncodeTestPath("/pub/old.txt");
            var request = new VsCodeController.MoveRequest { Destination = "/pub/new.txt" };

            var result = await controller.MoveFile(pathHash, request);

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public async Task MoveFolder_ValidRequest_ReturnsNoContent()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var pathHash = EncodeTestPath("/pub/old-folder");
            var request = new VsCodeController.MoveRequest { Destination = "/pub/new-folder" };

            var result = await controller.MoveFolder(pathHash, request);

            Assert.IsInstanceOfType(result, typeof(NoContentResult));
        }

        [TestMethod]
        public async Task MoveFolder_InvalidHash_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var request = new VsCodeController.MoveRequest { Destination = "/pub/new-folder" };

            var result = await controller.MoveFolder("!!!bad!!!", request);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task MoveFolder_MissingDestination_ReturnsBadRequest()
        {
            controller.ControllerContext.HttpContext = await CreateAuthorizedContextAsync();
            var pathHash = EncodeTestPath("/pub/old-folder");
            var request = new VsCodeController.MoveRequest { Destination = "" };

            var result = await controller.MoveFolder(pathHash, request);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task MoveFolder_Unauthenticated_ReturnsUnauthorized()
        {
            controller.ControllerContext.HttpContext = CreateHttpContext();
            var pathHash = EncodeTestPath("/pub/old-folder");
            var request = new VsCodeController.MoveRequest { Destination = "/pub/new-folder" };

            var result = await controller.MoveFolder(pathHash, request);

            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        #endregion
    }
}

