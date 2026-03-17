// <copyright file="PublisherHomeControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

#nullable enable

namespace Sky.Tests.Controllers
{
    using Cosmos.BlobService;
    using Cosmos.Cms.Common.Services.Configurations;
    using Cosmos.Cms.Publisher.Controllers;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services;
    using Cosmos.Publisher.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class PublisherHomeControllerTests
    {
        private static HomeController CreateController(
            Mock<IMediator> mediatorMock,
            ApplicationDbContext dbContext,
            IRequestContextProvider requestContextProvider,
            bool requiresAuthentication = false,
            string microsoftAppId = "test-app-id")
        {
            var configuration = new Mock<IConfiguration>();
            var logger = new Mock<ILogger<HomeController>>();
            var storageContext = new Mock<IStorageContext>();
            var emailSender = new Mock<IEmailSender>();
            var contactManagementService = new Mock<IContactManagementService>();
            var graphIntegrationService = new Mock<IGraphIntegrationService>();
            graphIntegrationService.SetupGet(x => x.IsAvailable).Returns(false);

            var options = Options.Create(new SiteSettings
            {
                CosmosRequiresAuthentication = requiresAuthentication,
                MicrosoftAppId = microsoftAppId,
            });

            var controller = new HomeController(
                configuration.Object,
                logger.Object,
                mediatorMock.Object,
                options,
                dbContext,
                storageContext.Object,
                emailSender.Object,
                contactManagementService.Object,
                graphIntegrationService.Object,
                requestContextProvider);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            };

            return controller;
        }

        private static ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [TestMethod]
        public async Task CCMS___Head_ReturnsOk_WhenPublishedHeaderExists()
        {
            using var dbContext = CreateDbContext();
            var mediatorMock = new Mock<IMediator>();
            var requestContextProvider = new Mock<IRequestContextProvider>();
            requestContextProvider.Setup(x => x.GetPathValue()).Returns(new PathString("/home"));

            mediatorMock
                .Setup(x => x.QueryAsync(
                    It.IsAny<GetPublishedPageHeaderByUrlQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ArticleViewModel { Id = Guid.NewGuid(), Updated = DateTimeOffset.UtcNow });

            var controller = CreateController(mediatorMock, dbContext, requestContextProvider.Object);

            var result = await controller.CCMS___Head();

            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        }

        [TestMethod]
        public async Task CCMS___Head_ReturnsNotFound_WhenPublishedHeaderMissing()
        {
            using var dbContext = CreateDbContext();
            var mediatorMock = new Mock<IMediator>();
            var requestContextProvider = new Mock<IRequestContextProvider>();
            requestContextProvider.Setup(x => x.GetPathValue()).Returns(new PathString("/missing"));

            mediatorMock
                .Setup(x => x.QueryAsync(
                    It.IsAny<GetPublishedPageHeaderByUrlQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ArticleViewModel?)null);

            var controller = CreateController(mediatorMock, dbContext, requestContextProvider.Object);

            var result = await controller.CCMS___Head();

            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Index_ReturnsJsonResult_WhenModeIsJson()
        {
            using var dbContext = CreateDbContext();
            var mediatorMock = new Mock<IMediator>();
            var requestContextProvider = new Mock<IRequestContextProvider>();
            requestContextProvider.Setup(x => x.GetPathValue()).Returns(new PathString("/home"));

            mediatorMock
                .Setup(x => x.QueryAsync(
                    It.IsAny<GetPublishedPageByUrlQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ArticleViewModel
                {
                    Id = Guid.NewGuid(),
                    UrlPath = "home",
                    Updated = DateTimeOffset.UtcNow,
                    Layout = new LayoutViewModel(),
                });

            var controller = CreateController(mediatorMock, dbContext, requestContextProvider.Object);

            var result = await controller.Index(mode: "json");

            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        [TestMethod]
        public void Error_ReturnsErrorViewModel()
        {
            using var dbContext = CreateDbContext();
            var mediatorMock = new Mock<IMediator>();
            var requestContextProvider = new Mock<IRequestContextProvider>();

            var controller = CreateController(mediatorMock, dbContext, requestContextProvider.Object);

            var result = controller.Error() as ViewResult;

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result.Model, typeof(Cosmos.Cms.Publisher.Models.ErrorViewModel));
        }

        [TestMethod]
        public void GetMicrosoftIdentityAssociation_ReturnsJsonFile()
        {
            using var dbContext = CreateDbContext();
            var mediatorMock = new Mock<IMediator>();
            var requestContextProvider = new Mock<IRequestContextProvider>();

            var controller = CreateController(
                mediatorMock,
                dbContext,
                requestContextProvider.Object,
                microsoftAppId: "12345-app-id");

            var result = controller.GetMicrosoftIdentityAssociation();

            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            Assert.AreEqual("application/json", fileResult.ContentType);
            Assert.AreEqual("microsoft-identity-association.json", fileResult.FileDownloadName);
        }
    }
}
