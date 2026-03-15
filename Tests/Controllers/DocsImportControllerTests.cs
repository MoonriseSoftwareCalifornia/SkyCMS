// <copyright file="DocsImportControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers;

using Cosmos.BlobService;
using Cosmos.BlobService.Models;
using Cosmos.Cms.Editor.Controllers;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Editor.Data.Logic;
using Sky.Editor.Features.Articles.Create;
using Sky.Editor.Features.Articles.Save;
using Sky.Editor.Infrastructure.Time;
using Sky.Editor.Services.Catalog;
using Sky.Editor.Services.EditorSettings;
using Sky.Editor.Services.Html;
using Sky.Editor.Services.Publishing;
using Sky.Editor.Services.Redirects;
using Sky.Editor.Services.Slugs;
using Sky.Editor.Services.Templates;
using Sky.Editor.Services.Titles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommonMediator = Cosmos.Common.Features.Shared.IMediator;

/// <summary>
/// Unit tests for <see cref="DocsImportController"/>.
/// </summary>
[TestClass]
public class DocsImportControllerTests
{
    private const string ApiKey = "test-key";

    private ApplicationDbContext dbContext;
    private Mock<CommonMediator> mediatorMock;
    private Mock<IStorageContext> storageContextMock;
    private Mock<IEditorSettings> editorSettingsMock;
    private Mock<ILogger<DocsImportController>> loggerMock;
    private Mock<IPublishingService> publishingServiceMock;
    private DocsImportController controller;
    private ArticleEditLogic articleLogic;
    private IConfiguration configuration;

    /// <summary>
    /// Initializes test context.
    /// </summary>
    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"DocsImport_{Guid.NewGuid()}")
            .Options;

        dbContext = new ApplicationDbContext(options);
        mediatorMock = new Mock<CommonMediator>();
        storageContextMock = new Mock<IStorageContext>();
        editorSettingsMock = new Mock<IEditorSettings>();
        loggerMock = new Mock<ILogger<DocsImportController>>();
        publishingServiceMock = new Mock<IPublishingService>();

        editorSettingsMock.SetupGet(x => x.BlobPublicUrl).Returns("https://cdn.test");
        editorSettingsMock.SetupGet(x => x.PublisherUrl).Returns("https://site.test");
        editorSettingsMock.SetupGet(x => x.StaticWebPages).Returns(false);
        editorSettingsMock.SetupGet(x => x.AllowedFileTypes).Returns(".png,.jpg");

        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["DocsImport:ApiKey"] = ApiKey,
                ["DocsImport:UserId"] = Guid.NewGuid().ToString()
            })
            .Build();

        articleLogic = CreateArticleLogic(dbContext, editorSettingsMock.Object, storageContextMock.Object, publishingServiceMock.Object);

        controller = CreateController(configuration);
        SetAuthorization(controller, ApiKey);
    }

    /// <summary>
    /// Cleans up resources after each test.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        dbContext?.Dispose();
    }

    /// <summary>
    /// Tests that Upsert returns unauthorized when the Authorization header is missing.
    /// </summary>
    [TestMethod]
    public async Task Upsert_ShouldReturnUnauthorized_WhenAuthorizationMissing()
    {
        // Arrange
        var request = CreateRequest();
        controller.ControllerContext.HttpContext.Request.Headers.Remove("Authorization");

        // Act
        var result = await controller.Upsert("docs/guide/index.md", request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
    }

    /// <summary>
    /// Tests that Upsert accepts any configured API key in a multi-key list.
    /// </summary>
    [TestMethod]
    public async Task Upsert_ShouldAllowSecondaryApiKey()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["DocsImport:ApiKey"] = "primary;secondary",
                ["DocsImport:UserId"] = Guid.NewGuid().ToString()
            })
            .Build();

        var localController = CreateController(config);
        SetAuthorization(localController, "secondary");

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<CreateArticleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult<ArticleViewModel>.Success(new ArticleViewModel { ArticleNumber = 10 }));

        var request = CreateRequest();

        // Act
        var result = await localController.Upsert("docs/guide/index.md", request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
    }

    /// <summary>
    /// Tests that Upsert rejects oversized HTML payloads.
    /// </summary>
    [TestMethod]
    public async Task Upsert_ShouldReturnPayloadTooLarge_WhenHtmlExceedsLimit()
    {
        // Arrange
        var request = CreateRequest(html: new string('a', 1_048_577));

        // Act
        var result = await controller.Upsert("docs/guide/index.md", request);

        // Assert
        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, objectResult.StatusCode);
    }

    /// <summary>
    /// Tests that Upsert creates a new article and rewrites links.
    /// </summary>
    [TestMethod]
    public async Task Upsert_ShouldCreateArticle_AndRewriteLinks()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        dbContext.Templates.Add(new Template
        {
            Id = templateId,
            Title = "docs-page"
        });
        await dbContext.SaveChangesAsync();

        CreateArticleCommand captured = null;

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<CreateArticleCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<CommandResult<ArticleViewModel>>, CancellationToken>((command, _) =>
            {
                captured = (CreateArticleCommand)command;
            })
            .ReturnsAsync(CommandResult<ArticleViewModel>.Success(new ArticleViewModel { ArticleNumber = 123 }));

        var request = CreateRequest(
            html: "<p><img src=\"img/logo.png\"></p><a href=\"./intro.md\">Intro</a>",
            source: new DocsImportController.DocsSourceInfo
            {
                Path = "Docs/Guide/index.md",
                Hash = "sha256:abc"
            });

        // Act
        var result = await controller.Upsert("docs/guide/index.md", request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        Assert.IsNotNull(captured);
        Assert.AreEqual(templateId, captured.TemplateId);
        StringAssert.Contains(captured.ContentOverride, "/pub/docs/Guide/img/logo.png");
        StringAssert.Contains(captured.ContentOverride, "/docs/intro");
    }

    /// <summary>
    /// Tests that Upsert updates an existing article version.
    /// </summary>
    [TestMethod]
    public async Task Upsert_ShouldUpdateArticle_WhenExisting()
    {
        // Arrange
        dbContext.Articles.Add(new Article
        {
            ArticleNumber = 7,
            VersionNumber = 1,
            UrlPath = "docs/guide",
            Title = "Existing",
            Content = "<p>Existing</p>",
            StatusCode = (int)StatusCodeEnum.Active,
            UserId = Guid.NewGuid().ToString()
        });
        await dbContext.SaveChangesAsync();

        SaveArticleCommand captured = null;

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SaveArticleCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<CommandResult<ArticleUpdateResult>>, CancellationToken>((command, _) =>
            {
                captured = (SaveArticleCommand)command;
            })
            .ReturnsAsync(CommandResult<ArticleUpdateResult>.Success(new ArticleUpdateResult
            {
                ServerSideSuccess = true,
                Model = new ArticleViewModel { ArticleNumber = 7 }
            }));

        var request = CreateRequest(
            html: "<img src=\"images/asset.png\" />",
            source: new DocsImportController.DocsSourceInfo
            {
                Path = "Docs/Guide/index.md",
                Hash = "sha256:def"
            });

        // Act
        var result = await controller.Upsert("docs/guide/index.md", request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        Assert.IsNotNull(captured);
        Assert.AreEqual(7, captured.ArticleNumber);
        StringAssert.Contains(captured.Content, "/pub/docs/Guide/images/asset.png");
    }

    /// <summary>
    /// Tests that Rename returns bad request when ToTitle is missing.
    /// </summary>
    [TestMethod]
    public async Task Rename_ShouldReturnBadRequest_WhenTitleMissing()
    {
        // Arrange
        var request = new DocsImportController.DocsRenameRequest
        {
            FromPath = "Docs/Old.md",
            ToPath = "Docs/New.md"
        };

        // Act
        var result = await controller.Rename(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
    }

    /// <summary>
    /// Tests that Rename updates UrlPath when valid input is provided.
    /// </summary>
    [TestMethod]
    public async Task Rename_ShouldUpdateUrlPath_WhenValid()
    {
        // Arrange
        dbContext.Articles.Add(new Article
        {
            ArticleNumber = 42,
            VersionNumber = 1,
            UrlPath = "docs/old",
            Title = "Old",
            Content = "<p>Content</p>",
            StatusCode = (int)StatusCodeEnum.Active,
            UserId = Guid.NewGuid().ToString()
        });
        await dbContext.SaveChangesAsync();

        SaveArticleCommand captured = null;

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SaveArticleCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<CommandResult<ArticleUpdateResult>>, CancellationToken>((command, _) =>
            {
                captured = (SaveArticleCommand)command;
            })
            .ReturnsAsync(CommandResult<ArticleUpdateResult>.Success(new ArticleUpdateResult
            {
                ServerSideSuccess = true,
                Model = new ArticleViewModel { ArticleNumber = 42 }
            }));

        var request = new DocsImportController.DocsRenameRequest
        {
            FromPath = "Docs/Old.md",
            ToPath = "Docs/New.md",
            ToTitle = "New"
        };

        // Act
        var result = await controller.Rename(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        Assert.IsNotNull(captured);
        Assert.AreEqual("docs/new", captured.UrlPath);
    }

    /// <summary>
    /// Tests that Delete returns not found when no catalog entry exists.
    /// </summary>
    [TestMethod]
    public async Task Delete_ShouldReturnNotFound_WhenCatalogEntryMissing()
    {
        // Act
        var result = await controller.Delete("docs/missing.md");

        // Assert
        Assert.IsInstanceOfType(result, typeof(NotFoundResult));
    }

    /// <summary>
    /// Tests that Delete soft-deletes matching articles when a catalog entry exists.
    /// </summary>
    [TestMethod]
    public async Task Delete_ShouldSoftDeleteArticle_WhenCatalogEntryExists()
    {
        // Arrange
        var article = new Article
        {
            ArticleNumber = 7,
            VersionNumber = 1,
            UrlPath = "docs/guide",
            Title = "Guide",
            Content = "<p>Content</p>",
            StatusCode = (int)StatusCodeEnum.Active,
            UserId = Guid.NewGuid().ToString()
        };

        dbContext.Articles.Add(article);
        dbContext.ArticleCatalog.Add(new CatalogEntry
        {
            ArticleNumber = 7,
            UrlPath = "docs/guide",
            Title = "Guide",
            Status = "Active",
            Updated = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        publishingServiceMock
            .Setup(x => x.WriteTocAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await controller.Delete("docs/guide/index.md");

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));

        var updated = await dbContext.Articles.FirstAsync(a => a.ArticleNumber == 7);
        Assert.AreEqual((int)StatusCodeEnum.Deleted, updated.StatusCode);
    }

    /// <summary>
    /// Tests that UploadAsset rejects dangerous extensions.
    /// </summary>
    [TestMethod]
    public async Task UploadAsset_ShouldRejectDangerousExtension()
    {
        // Arrange
        var file = CreateFormFile("payload", "virus.exe", "application/octet-stream");

        // Act
        var result = await controller.UploadAsset(file, "docs/virus.exe");

        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
    }

    /// <summary>
    /// Tests that UploadAsset sends files to storage and returns a public URL.
    /// </summary>
    [TestMethod]
    public async Task UploadAsset_ShouldUploadFile_WhenValid()
    {
        // Arrange
        storageContextMock
            .Setup(x => x.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        storageContextMock
            .Setup(x => x.CreateFolder(It.IsAny<string>()))
            .ReturnsAsync(new FileManagerEntry());

        var file = CreateFormFile("asset", "logo.png", "image/png");

        // Act
        var result = await controller.UploadAsset(file, "Guide/logo.png");

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        storageContextMock.Verify(
            x => x.AppendBlob(
                It.IsAny<MemoryStream>(),
                It.Is<FileUploadMetaData>(meta => meta.RelativePath.Contains("pub/docs/Guide/logo.png")),
                It.IsAny<string>()),
            Times.Once);
    }

    private DocsImportController CreateController(IConfiguration config)
    {
        var controllerInstance = new DocsImportController(
            dbContext,
            mediatorMock.Object,
            articleLogic,
            config,
            loggerMock.Object,
            storageContextMock.Object,
            editorSettingsMock.Object);

        controllerInstance.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controllerInstance;
    }

    private static void SetAuthorization(ControllerBase target, string key)
    {
        target.ControllerContext.HttpContext.Request.Headers["Authorization"] = $"Bearer {key}";
    }

    private static DocsImportController.DocsUpsertRequest CreateRequest(
        string html = null,
        string urlPath = null,
        string templateKey = null,
        DocsImportController.DocsSourceInfo source = null)
    {
        return new DocsImportController.DocsUpsertRequest
        {
            Title = "Guide",
            UrlPath = urlPath ?? "docs/guide",
            Html = html ?? "<p>Hello</p>",
            TemplateKey = templateKey ?? "docs-page",
            Published = true,
            Source = source ?? new DocsImportController.DocsSourceInfo
            {
                Path = "Docs/Guide/index.md",
                Hash = "sha256:stub"
            }
        };
    }

    private static IFormFile CreateFormFile(string content, string fileName, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static ArticleEditLogic CreateArticleLogic(
        ApplicationDbContext context,
        IEditorSettings settings,
        IStorageContext storageContext,
        IPublishingService publishingService)
    {
        return new ArticleEditLogic(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            storageContext,
            new Mock<ILogger<ArticleEditLogic>>().Object,
            settings,
            new Mock<IClock>().Object,
            new Mock<ISlugService>().Object,
            new Mock<IArticleHtmlService>().Object,
            new Mock<ICatalogService>().Object,
            publishingService,
            new Mock<ITitleChangeService>().Object,
            new Mock<IRedirectService>().Object,
            new Mock<ITemplateService>().Object);
    }
}
