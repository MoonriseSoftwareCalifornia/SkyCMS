// <copyright file="StaticProxyControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

#nullable enable

namespace Sky.Tests.Controllers
{
    using Cosmos.BlobService;
    using Cosmos.Cms.Publisher.Controllers;
    using Cosmos.Common.Data;
    using Cosmos.Common.Services.Caching;
    using Cosmos.Publisher.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Text;
    using System.Threading.Tasks;

    [TestClass]
    public class StaticProxyControllerTests
    {
        private static ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private static StaticProxyController CreateController(
            Mock<IStorageContext> storageContextMock,
            Mock<ICacheService<FileCacheObject>> cacheServiceMock,
            Mock<ICacheService<bool>> spaCacheServiceMock,
            Mock<ICacheKeyProvider> cacheKeyProviderMock,
            ApplicationDbContext dbContext)
        {
            var logger = new Mock<ILogger<StaticProxyController>>();
            var controller = new StaticProxyController(
                storageContextMock.Object,
                cacheServiceMock.Object,
                spaCacheServiceMock.Object,
                cacheKeyProviderMock.Object,
                dbContext,
                logger.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            };

            return controller;
        }

        [TestMethod]
        public async Task Index_ReturnsContentResult_WhenTextFileExistsInCache()
        {
            using var dbContext = CreateDbContext();
            var storageContextMock = new Mock<IStorageContext>();
            var cacheServiceMock = new Mock<ICacheService<FileCacheObject>>();
            var spaCacheServiceMock = new Mock<ICacheService<bool>>();
            var cacheKeyProviderMock = new Mock<ICacheKeyProvider>();

            var cachedFile = new FileCacheObject
            {
                Name = "index.html",
                ContentType = "text/html",
                FileData = Encoding.UTF8.GetBytes("<h1>Hello</h1>"),
                ModifiedUtc = DateTime.UtcNow,
            };

            cacheServiceMock
                .Setup(x => x.TryGet(It.IsAny<string>(), out cachedFile))
                .Returns(true);

            var controller = CreateController(storageContextMock, cacheServiceMock, spaCacheServiceMock, cacheKeyProviderMock, dbContext);
            controller.ControllerContext.HttpContext.Request.Path = "/index.html";

            var result = await controller.Index();

            Assert.IsInstanceOfType(result, typeof(ContentResult));
            var contentResult = (ContentResult)result;
            Assert.AreEqual("text/html", contentResult.ContentType);
            Assert.AreEqual("<h1>Hello</h1>", contentResult.Content);
        }

        [TestMethod]
        public async Task Index_ReturnsNotFound_WhenFileAndSpaFallbackDoNotExist()
        {
            using var dbContext = CreateDbContext();
            var storageContextMock = new Mock<IStorageContext>();
            var cacheServiceMock = new Mock<ICacheService<FileCacheObject>>();
            var spaCacheServiceMock = new Mock<ICacheService<bool>>();
            var cacheKeyProviderMock = new Mock<ICacheKeyProvider>();

            FileCacheObject? none = null;
            cacheServiceMock
                .Setup(x => x.TryGet(It.IsAny<string>(), out none))
                .Returns(false);

            bool spaResult = false;
            spaCacheServiceMock
                .Setup(x => x.TryGet(It.IsAny<string>(), out spaResult))
                .Returns(false);

            storageContextMock
                .Setup(x => x.GetFileAsync(It.IsAny<string>()))
                .ReturnsAsync((FileManagerEntry?)null);

            cacheKeyProviderMock
                .Setup(x => x.GenerateSpaCheckKey(It.IsAny<string>()))
                .Returns<string>(s => $"spa:{s}");

            var controller = CreateController(storageContextMock, cacheServiceMock, spaCacheServiceMock, cacheKeyProviderMock, dbContext);
            controller.ControllerContext.HttpContext.Request.Path = "/not-found";

            var result = await controller.Index();

            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }
    }
}
