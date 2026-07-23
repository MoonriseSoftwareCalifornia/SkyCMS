// <copyright file="WebsiteCopyControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.DynamicConfig;
    using Cosmos.MultiTenant.Administrator.Controllers;
    using Cosmos.MultiTenant.Administrator.Models;
    using Cosmos.MultiTenant.Administrator.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class WebsiteCopyControllerTests
    {
        private DynamicConfigDbContext dbContext = null!;
        private Mock<IWebsiteCopyOrchestrator> orchestrator = null!;
        private WebsiteCopyController controller = null!;
        private Connection sourceConnection = null!;
        private Connection destinationConnection = null!;

        [TestInitialize]
        public void Initialize()
        {
            var options = new DbContextOptionsBuilder<DynamicConfigDbContext>()
                .UseInMemoryDatabase($"WebsiteCopyControllerTests_{Guid.NewGuid()}")
                .Options;

            dbContext = new DynamicConfigDbContext(options);
            dbContext.Database.EnsureCreated();

            sourceConnection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "source.local" },
                DbConn = "Data Source=source.db",
                StorageConn = "DefaultEndpointsProtocol=https;AccountName=source;AccountKey=abc",
                WebsiteUrl = "https://source.local",
                ResourceGroup = "source-rg"
            };

            destinationConnection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "destination.local" },
                DbConn = "Data Source=destination.db",
                StorageConn = "DefaultEndpointsProtocol=https;AccountName=dest;AccountKey=abc",
                WebsiteUrl = "https://destination.local",
                ResourceGroup = "destination-rg"
            };

            dbContext.Connections.Add(sourceConnection);
            dbContext.Connections.Add(destinationConnection);
            dbContext.SaveChanges();

            orchestrator = new Mock<IWebsiteCopyOrchestrator>();
            controller = new WebsiteCopyController(dbContext, orchestrator.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = BuildHttpContext()
                }
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }

        [TestMethod]
        public async Task Index_ReturnsViewWithConnectionLists()
        {
            var result = await controller.Index();

            var view = result as ViewResult;
            Assert.IsNotNull(view);

            var model = view.Model as WebsiteCopyStartViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.SourceConnections.Count);
            Assert.AreEqual(2, model.DestinationConnections.Count);
        }

        [TestMethod]
        public async Task Start_WhenNeitherDatabaseNorStorageSelected_ReturnsIndexWithModelError()
        {
            var model = new WebsiteCopyStartViewModel
            {
                SourceConnectionId = sourceConnection.Id,
                MoveDatabase = false,
                MoveStorage = false,
                UseExistingDestination = true,
                DestinationConnectionId = destinationConnection.Id
            };

            var result = await controller.Start(model);

            var view = result as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual("Index", view.ViewName);
            Assert.IsTrue(controller.ModelState.Any(), "Expected model validation errors.");

            orchestrator.Verify(x => x.StartJobAsync(It.IsAny<WebsiteCopyJob>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task Start_WithExistingDestination_StartsJobAndRedirectsToDetails()
        {
            WebsiteCopyJob? captured = null;
            orchestrator
                .Setup(x => x.StartJobAsync(It.IsAny<WebsiteCopyJob>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((WebsiteCopyJob job, CancellationToken _) =>
                {
                    captured = job;
                    job.Id = Guid.NewGuid();
                    return job;
                });

            var model = new WebsiteCopyStartViewModel
            {
                SourceConnectionId = sourceConnection.Id,
                MoveDatabase = true,
                MoveStorage = true,
                UseExistingDestination = true,
                DestinationConnectionId = destinationConnection.Id,
                DryRun = true,
                AllowDestinationOverwrite = true
            };

            var result = await controller.Start(model);

            var redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual("Details", redirect.ActionName);
            Assert.IsNotNull(captured);
            Assert.AreEqual(sourceConnection.Id, captured.SourceConnectionId);
            Assert.AreEqual(destinationConnection.Id, captured.DestinationConnectionId);
            Assert.AreEqual(destinationConnection.DbConn, captured.DestinationDbConn);
            Assert.AreEqual(destinationConnection.StorageConn, captured.DestinationStorageConn);
            Assert.IsTrue(captured.DryRun);
            Assert.IsTrue(captured.AllowDestinationOverwrite);
            Assert.AreEqual("copy-admin@local.test", captured.StartedBy);
        }

        [TestMethod]
        public async Task Progress_WhenJobNotFound_ReturnsNotFound()
        {
            orchestrator
                .Setup(x => x.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((WebsiteCopyJob?)null);

            var result = await controller.Progress(Guid.NewGuid());

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        private static HttpContext BuildHttpContext()
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Email, "copy-admin@local.test"),
                new Claim(ClaimTypes.Name, "copy-admin")
            ], "TestAuth"));

            return context;
        }
    }
}
