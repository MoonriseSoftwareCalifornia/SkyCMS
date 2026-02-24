// <copyright file="PublishingServiceErrorHandlingTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Publishing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Security.Claims;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Cms.Services;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Publishing;

    /// <summary>
    /// Error handling and resilience tests for <see cref="PublishingService"/>.
    /// </summary>
    [TestClass]
    public class PublishingServiceErrorHandlingTests
    {
        #region Transient Exception Detection

        [TestMethod]
        [TestCategory("Publishing")]
        public void IsTransientException_WithIOException_ReturnsTrue()
        {
            var result = InvokeIsTransientException(new IOException("disk error"));
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public void IsTransientException_WithTimeoutException_ReturnsTrue()
        {
            var result = InvokeIsTransientException(new TimeoutException("timeout"));
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public void IsTransientException_WithHttpRequest500_ReturnsTrue()
        {
            var result = InvokeIsTransientException(new HttpRequestException("server error", null, HttpStatusCode.InternalServerError));
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public void IsTransientException_WithHttpRequest429_ReturnsTrue()
        {
            var result = InvokeIsTransientException(new HttpRequestException("throttled", null, HttpStatusCode.TooManyRequests));
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public void IsTransientException_WithHttpRequest400_ReturnsFalse()
        {
            var result = InvokeIsTransientException(new HttpRequestException("bad request", null, HttpStatusCode.BadRequest));
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public void IsTransientException_WithNonTransientException_ReturnsFalse()
        {
            var result = InvokeIsTransientException(new InvalidOperationException("not transient"));
            Assert.IsFalse(result);
        }

        #endregion

        #region Retry Logic and Static File Race Conditions

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticFileWithRetrySafeAsync_RetriesOnTransientExceptionsAndSucceeds()
        {
            // Arrange
            var storageMock = new Mock<IStorageContext>();
            var viewRendererMock = new Mock<IViewRenderService>();
            var loggerMock = new Mock<ILogger<PublishingService>>();
            var settingsMock = CreateSettingsMock(staticPagesEnabled: true);

            viewRendererMock
                .Setup(r => r.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html>ok</html>");

            storageMock
                .SetupSequence(s => s.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()))
                .ThrowsAsync(new IOException("transient-1"))
                .ThrowsAsync(new IOException("transient-2"))
                .Returns(Task.CompletedTask);

            var service = CreatePublishingService(storageMock.Object, settingsMock.Object, loggerMock.Object);

            var page = CreatePublishedPage("retry-test");
            var layout = CreateLayout();

            // Act
            await InvokePrivateAsync(
                service,
                "CreateStaticFileWithRetrySafeAsync",
                page,
                layout,
                storageMock.Object,
                viewRendererMock.Object,
                loggerMock.Object,
                CancellationToken.None);

            // Assert
            storageMock.Verify(
                s => s.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()),
                Times.Exactly(3));
            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeast(2));
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticFileWithRetrySafeAsync_DoesNotRetryOnNonTransientException()
        {
            // Arrange
            var storageMock = new Mock<IStorageContext>();
            var viewRendererMock = new Mock<IViewRenderService>();
            var loggerMock = new Mock<ILogger<PublishingService>>();
            var settingsMock = CreateSettingsMock(staticPagesEnabled: true);

            viewRendererMock
                .Setup(r => r.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html>ok</html>");

            storageMock
                .Setup(s => s.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("non-transient"));

            var service = CreatePublishingService(storageMock.Object, settingsMock.Object, loggerMock.Object);
            var page = CreatePublishedPage("no-retry");
            var layout = CreateLayout();

            // Act
            await InvokePrivateAsync(
                service,
                "CreateStaticFileWithRetrySafeAsync",
                page,
                layout,
                storageMock.Object,
                viewRendererMock.Object,
                loggerMock.Object,
                CancellationToken.None);

            // Assert
            storageMock.Verify(
                s => s.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()),
                Times.Once);
            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region CDN Purge Failure Handling

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PurgeCdnAsync_WhenDriverThrows_ReturnsEmptyResultsAndLogsWarning()
        {
            // Arrange
            await using var db = CreateDbContext();

            var invalidCloudFrontSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = "{}" // Missing required config, driver constructor will throw
            };

            db.Settings.Add(new Setting
            {
                Id = Guid.NewGuid(),
                Group = CdnService.CDNGROUPNAME,
                Name = "CloudFront",
                Value = JsonConvert.SerializeObject(invalidCloudFrontSetting)
            });
            await db.SaveChangesAsync();

            var storageMock = new Mock<IStorageContext>();
            var loggerMock = new Mock<ILogger<PublishingService>>();
            var settingsMock = CreateSettingsMock(staticPagesEnabled: false);
            var service = CreatePublishingService(storageMock.Object, settingsMock.Object, loggerMock.Object, db);

            var page = CreatePublishedPage("cdn-test");

            // Act
            var results = await InvokePrivateAsync<List<CdnResult>>(service, "PurgeCdnAsync", page);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Blog Stream Edge Cases

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishBlogStreamAsync_WhenStreamDoesNotExist_CreatesNewStreamWithDefaults()
        {
            // Arrange
            await using var db = CreateDbContext();
            var storageMock = new Mock<IStorageContext>();
            var loggerMock = new Mock<ILogger<PublishingService>>();
            var settingsMock = CreateSettingsMock(staticPagesEnabled: false);
            var blogStreamRenderingMock = new Mock<Cosmos.Common.Services.BlogPublishing.IBlogStreamRenderingService>();
            blogStreamRenderingMock.Setup(b => b.GenerateBlogStreamWrapperAsync(It.IsAny<Article>(), It.IsAny<string>())).ReturnsAsync("<div>stream</div>");

            var service = CreatePublishingService(storageMock.Object, settingsMock.Object, loggerMock.Object, db, blogStreamRenderingMock.Object);

            var blog = new Article
            {
                BlogKey = "my-blog",
                Title = "My Blog",
                Updated = DateTimeOffset.UtcNow,
                BannerImage = "banner.png",
                Introduction = "Intro"
            };

            // Act
            await service.PublishBlogStreamAsync(blog);

            // Assert
            var stream = await db.Articles.FirstOrDefaultAsync(a => a.BlogKey == "my-blog" && a.ArticleType == (int)ArticleType.BlogStream);
            Assert.IsNotNull(stream);
            Assert.AreEqual(1, stream.VersionNumber);
            Assert.AreEqual("blog-stream", stream.Category);
            Assert.AreEqual("my-blog", stream.UrlPath);
            Assert.AreEqual("<div>stream</div>", stream.Content);
            Assert.IsNotNull(stream.Published);
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishBlogStreamAsync_WhenStreamExists_IncrementsVersionAndUpdatesContent()
        {
            // Arrange
            await using var db = CreateDbContext();
            var storageMock = new Mock<IStorageContext>();
            var loggerMock = new Mock<ILogger<PublishingService>>();
            var settingsMock = CreateSettingsMock(staticPagesEnabled: false);
            var blogRenderingMock = new Mock<Cosmos.Common.Services.BlogPublishing.IBlogStreamRenderingService>();
            blogRenderingMock.Setup(b => b.GenerateBlogStreamWrapperAsync(It.IsAny<Article>(), It.IsAny<string>())).ReturnsAsync("<div>updated</div>");

            var existing = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 10,
                VersionNumber = 2,
                BlogKey = "edge-blog",
                Title = "Old",
                Content = "Old",
                UrlPath = "edge-blog",
                Updated = DateTimeOffset.UtcNow.AddDays(-1),
                Published = DateTimeOffset.UtcNow.AddDays(-1),
                UserId = Guid.NewGuid().ToString(),
                StatusCode = (int)StatusCodeEnum.Active,
                ArticleType = (int)ArticleType.BlogStream,
                Category = "blog-stream"
            };
            db.Articles.Add(existing);
            await db.SaveChangesAsync();

            var service = CreatePublishingService(storageMock.Object, settingsMock.Object, loggerMock.Object, db, blogRenderingMock.Object);

            var blog = new Article
            {
                BlogKey = "edge-blog",
                Title = "New Title",
                Updated = DateTimeOffset.UtcNow,
                BannerImage = "new-banner.png",
                Introduction = "New intro"
            };

            // Act
            await service.PublishBlogStreamAsync(blog);

            // Assert
            var updated = await db.Articles.FirstOrDefaultAsync(a => a.BlogKey == "edge-blog" && a.ArticleType == (int)ArticleType.BlogStream);
            Assert.IsNotNull(updated);
            Assert.AreEqual(3, updated.VersionNumber);
            Assert.AreEqual("New Title", updated.Title);
            Assert.AreEqual("<div>updated</div>", updated.Content);
            Assert.AreEqual("new-banner.png", updated.BannerImage);
            Assert.AreEqual("New intro", updated.Introduction);
        }

        #endregion

        #region Step 1: Exponential Backoff & Additional Retry Scenarios

        [TestMethod]
        [TestCategory("Publishing")]
        public void IsTransientException_WithHttpRequest503_ReturnsTrue()
        {
            // Arrange & Act
            var result = InvokeIsTransientException(
                new HttpRequestException("service unavailable", null, HttpStatusCode.ServiceUnavailable));

            // Assert
            Assert.IsTrue(result, "HTTP 503 Service Unavailable should be treated as transient");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public void IsTransientException_WithHttpRequest502_ReturnsTrue()
        {
            // Arrange & Act
            var result = InvokeIsTransientException(
                new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway));

            // Assert
            Assert.IsTrue(result, "HTTP 502 Bad Gateway should be treated as transient");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticFileWithRetrySafeAsync_ExhaustsAllRetries_LogsErrorAndContinues()
        {
            // Arrange
            var storageMock = new Mock<IStorageContext>();
            var viewRendererMock = new Mock<IViewRenderService>();
            var loggerMock = new Mock<ILogger<PublishingService>>();
            var settingsMock = CreateSettingsMock(staticPagesEnabled: true);

            viewRendererMock
                .Setup(r => r.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html>ok</html>");

            // Fail all 4 attempts (initial + 3 retries)
            storageMock
                .Setup(s => s.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()))
                .ThrowsAsync(new IOException("persistent failure"));

            var service = CreatePublishingService(storageMock.Object, settingsMock.Object, loggerMock.Object);
            var page = CreatePublishedPage("exhaust-retries");
            var layout = CreateLayout();

            // Act
            await InvokePrivateAsync(
                service,
                "CreateStaticFileWithRetrySafeAsync",
                page,
                layout,
                storageMock.Object,
                viewRendererMock.Object,
                loggerMock.Object,
                CancellationToken.None);

            // Assert - Should attempt 4 times total (initial + 3 retries)
            storageMock.Verify(
                s => s.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()),
                Times.Exactly(4));

            // Should log warnings for first 3 failures and error for final failure
            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Transient error")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(3));

            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to create static file")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticFileWithRetrySafeAsync_SucceedsAfterRetry_LogsSuccessMessage()
        {
            // Arrange
            var storageMock = new Mock<IStorageContext>();
            var viewRendererMock = new Mock<IViewRenderService>();
            var loggerMock = new Mock<ILogger<PublishingService>>();
            var settingsMock = CreateSettingsMock(staticPagesEnabled: true);

            viewRendererMock
                .Setup(r => r.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html>ok</html>");

            // Fail twice, then succeed
            storageMock
                .SetupSequence(s => s.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()))
                .ThrowsAsync(new TimeoutException("timeout-1"))
                .ThrowsAsync(new TimeoutException("timeout-2"))
                .Returns(Task.CompletedTask);

            var service = CreatePublishingService(storageMock.Object, settingsMock.Object, loggerMock.Object);
            var page = CreatePublishedPage("success-after-retry");
            var layout = CreateLayout();

            // Act
            await InvokePrivateAsync(
                service,
                "CreateStaticFileWithRetrySafeAsync",
                page,
                layout,
                storageMock.Object,
                viewRendererMock.Object,
                loggerMock.Object,
                CancellationToken.None);

            // Assert
            storageMock.Verify(
                s => s.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()),
                Times.Exactly(3));

            // Should log success after retry
            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Successfully created static file") && v.ToString().Contains("after 3 attempt")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticFileSafeAsync_WhenStaticWebPagesDisabled_DoesNotCreateFile()
        {
            // Arrange
            var storageMock = new Mock<IStorageContext>();
            var viewRendererMock = new Mock<IViewRenderService>();
            var settingsMock = CreateSettingsMock(staticPagesEnabled: false); // Disabled
            var service = CreatePublishingService(storageMock.Object, settingsMock.Object, Mock.Of<ILogger<PublishingService>>());

            var page = CreatePublishedPage("disabled-test");
            var layout = CreateLayout();

            // Act
            await InvokePrivateAsync(
                service,
                "CreateStaticFileSafeAsync",
                page,
                layout,
                storageMock.Object,
                viewRendererMock.Object);

            // Assert - Storage should never be called
            storageMock.Verify(
                s => s.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()),
                Times.Never);

            viewRendererMock.Verify(
                v => v.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()),
                Times.Never);
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticFileWithRetrySafeAsync_RespectsCancellationToken()
        {
            // Arrange
            var storageMock = new Mock<IStorageContext>();
            var viewRendererMock = new Mock<IViewRenderService>();
            var loggerMock = new Mock<ILogger<PublishingService>>();
            var settingsMock = CreateSettingsMock(staticPagesEnabled: true);

            viewRendererMock
                .Setup(r => r.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html>ok</html>");

            // Always fail to trigger retries
            storageMock
                .Setup(s => s.AppendBlob(It.IsAny<MemoryStream>(), It.IsAny<FileUploadMetaData>(), It.IsAny<string>()))
                .ThrowsAsync(new IOException("fail"));

            var service = CreatePublishingService(storageMock.Object, settingsMock.Object, loggerMock.Object);
            var page = CreatePublishedPage("cancellation-test");
            var layout = CreateLayout();

            var cts = new CancellationTokenSource();
            cts.CancelAfter(100); // Cancel after 100ms

            // Act & Assert - Should throw TaskCanceledException or OperationCanceledException
            var exceptionThrown = false;
            try
            {
                await InvokePrivateAsync(
                    service,
                    "CreateStaticFileWithRetrySafeAsync",
                    page,
                    layout,
                    storageMock.Object,
                    viewRendererMock.Object,
                    loggerMock.Object,
                    cts.Token);
            }
            catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
            {
                exceptionThrown = true;
            }

            Assert.IsTrue(exceptionThrown, "Expected cancellation exception to be thrown");
        }

        #endregion

        #region Helpers

        private static ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private static PublishedPage CreatePublishedPage(string urlPath)
        {
            return new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 42,
                UrlPath = urlPath,
                Title = "Test",
                Content = "<p>content</p>",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow,
                StatusCode = (int)StatusCodeEnum.Active
            };
        }

        private static LayoutViewModel CreateLayout()
        {
            return new LayoutViewModel(new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Default",
                IsDefault = true,
                Head = string.Empty,
                HtmlHeader = string.Empty,
                FooterHtmlContent = string.Empty
            });
        }

        private static Mock<IEditorSettings> CreateSettingsMock(bool staticPagesEnabled)
        {
            var settingsMock = new Mock<IEditorSettings>();
            settingsMock.SetupGet(s => s.StaticWebPages).Returns(staticPagesEnabled);
            settingsMock.SetupGet(s => s.PublisherUrl).Returns("https://example.com");
            settingsMock.SetupGet(s => s.BlobPublicUrl).Returns("https://example.com");
            settingsMock.SetupGet(s => s.StaticPageParallelism).Returns(2);
            return settingsMock;
        }

        private static PublishingService CreatePublishingService(
            IStorageContext storage,
            IEditorSettings settings,
            ILogger<PublishingService> logger,
            ApplicationDbContext? db = null,
            Cosmos.Common.Services.BlogPublishing.IBlogStreamRenderingService? blogStreamRenderingService = null)
        {
            db ??= CreateDbContext();

            var accessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext()
            };
            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString())
            }));

            var authors = new Mock<Sky.Editor.Services.Authors.IAuthorInfoService>();
            authors.Setup(a => a.GetOrCreateAsync(It.IsAny<Guid>())).ReturnsAsync((AuthorInfo)null);

            var viewRenderer = new Mock<IViewRenderService>();
            viewRenderer.Setup(v => v.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync("<html>ok</html>");

            blogStreamRenderingService ??= new Mock<Cosmos.Common.Services.BlogPublishing.IBlogStreamRenderingService>().Object;

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            return new PublishingService(
                db,
                storage,
                settings,
                logger,
                accessor,
                authors.Object,
                new SystemClock(),
                blogStreamRenderingService,
                viewRenderer.Object,
                serviceProvider,
                new NoOpPublishingProgressReporter());
        }

        private static bool InvokeIsTransientException(Exception ex)
        {
            var method = typeof(PublishingService).GetMethod("IsTransientException", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("Unable to find IsTransientException via reflection.");
            }

            return (bool)method.Invoke(null, new object[] { ex });
        }

        private static Task InvokePrivateAsync(object instance, string methodName, params object[] args)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new InvalidOperationException($"Unable to find method '{methodName}' via reflection.");
            }

            return (Task)method.Invoke(instance, args);
        }

        private static Task<T> InvokePrivateAsync<T>(object instance, string methodName, params object[] args)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new InvalidOperationException($"Unable to find method '{methodName}' via reflection.");
            }

            return (Task<T>)method.Invoke(instance, args);
        }

        #endregion
    }
}
