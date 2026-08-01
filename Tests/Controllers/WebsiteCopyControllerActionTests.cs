// <copyright file="WebsiteCopyControllerActionTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using System;
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
    [TestCategory("WebsiteCopy")]
    public class WebsiteCopyControllerActionTests
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
                .UseInMemoryDatabase($"WebsiteCopyControllerActionTests_{Guid.NewGuid()}")
                .Options;

            dbContext = new DynamicConfigDbContext(options);
            dbContext.Database.EnsureCreated();

            sourceConnection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "source-action.local" },
                DbConn = "Data Source=source-action.db",
                StorageConn = "DefaultEndpointsProtocol=https;AccountName=sourceaction;AccountKey=abc",
                WebsiteUrl = "https://source-action.local",
                ResourceGroup = "source-action-rg"
            };

            destinationConnection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "destination-action.local" },
                DbConn = "Data Source=destination-action.db",
                StorageConn = "DefaultEndpointsProtocol=https;AccountName=destaction;AccountKey=abc",
                WebsiteUrl = "https://destination-action.local",
                ResourceGroup = "destination-action-rg"
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
        public async Task Details_WhenJobMissing_ReturnsNotFound()
        {
            orchestrator
                .Setup(x => x.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((WebsiteCopyJob?)null);

            var result = await controller.Details(Guid.NewGuid());

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Details_WhenJobExists_ReturnsViewModelWithConnections()
        {
            var jobId = Guid.NewGuid();
            var job = new WebsiteCopyJob
            {
                Id = jobId,
                SourceConnectionId = sourceConnection.Id,
                DestinationConnectionId = destinationConnection.Id,
                CopyDatabase = true,
                CopyStorage = true
            };

            orchestrator
                .Setup(x => x.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(job);

            var result = await controller.Details(jobId);

            var view = result as ViewResult;
            Assert.IsNotNull(view);

            var model = view.Model as WebsiteCopyDetailsViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(jobId, model.Job.Id);
            Assert.AreEqual(sourceConnection.Id, model.SourceConnection?.Id);
            Assert.AreEqual(destinationConnection.Id, model.DestinationConnection?.Id);
        }

        [TestMethod]
        public async Task Retry_CallsOrchestratorAndRedirectsToDetails()
        {
            var jobId = Guid.NewGuid();
            orchestrator
                .Setup(x => x.RetryJobAsync(jobId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await controller.Retry(jobId);

            var redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual("Details", redirect.ActionName);
            Assert.AreEqual(jobId, redirect.RouteValues?["id"]);
            orchestrator.Verify(x => x.RetryJobAsync(jobId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ApplySwitch_CallsOrchestratorAndRedirectsToDetails()
        {
            var jobId = Guid.NewGuid();
            orchestrator
                .Setup(x => x.ApplyConnectionSwitchAsync(jobId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await controller.ApplySwitch(jobId);

            var redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual("Details", redirect.ActionName);
            Assert.AreEqual(jobId, redirect.RouteValues?["id"]);
            orchestrator.Verify(x => x.ApplyConnectionSwitchAsync(jobId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Progress_WhenJobExists_ReturnsJsonPayload()
        {
            var jobId = Guid.NewGuid();
            var job = new WebsiteCopyJob
            {
                Id = jobId,
                SourceConnectionId = sourceConnection.Id,
                Status = (int)WebsiteCopyJobStatus.Running,
                ProgressPercent = 42,
                LastMessage = "Copy in progress",
                ErrorMessage = null,
                AttemptCount = 2,
                MaxAttempts = 5
            };

            orchestrator
                .Setup(x => x.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(job);

            var result = await controller.Progress(jobId);

            var json = result as JsonResult;
            Assert.IsNotNull(json);
            Assert.IsNotNull(json.Value);

            Assert.AreEqual(WebsiteCopyJobStatus.Running, GetAnonymousPropertyValue(json.Value, "Status"));
            Assert.AreEqual(42, GetAnonymousPropertyValue(json.Value, "ProgressPercent"));
            Assert.AreEqual("Copy in progress", GetAnonymousPropertyValue(json.Value, "LastMessage"));
            Assert.AreEqual(null, GetAnonymousPropertyValue(json.Value, "ErrorMessage"));
            Assert.AreEqual(2, GetAnonymousPropertyValue(json.Value, "AttemptCount"));
            Assert.AreEqual(5, GetAnonymousPropertyValue(json.Value, "MaxAttempts"));
        }

        [TestMethod]
        public async Task Start_WithNewDestinationMissingDatabaseConnection_ReturnsIndexWithModelError()
        {
            var model = new WebsiteCopyStartViewModel
            {
                SourceConnectionId = sourceConnection.Id,
                MoveDatabase = true,
                MoveStorage = false,
                UseExistingDestination = false,
                DestinationDbConn = "  "
            };

            var result = await controller.Start(model);

            var view = result as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual("Index", view.ViewName);
            Assert.IsTrue(controller.ModelState.ContainsKey(nameof(model.DestinationDbConn)));
            orchestrator.Verify(x => x.StartJobAsync(It.IsAny<WebsiteCopyJob>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task Start_WithNewDestination_UsesProvidedDestinationConnections()
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
                UseExistingDestination = false,
                DestinationDbConn = "Data Source=adhoc.db",
                DestinationStorageConn = "DefaultEndpointsProtocol=https;AccountName=adhoc;AccountKey=AAAA==",
                DryRun = false
            };

            var result = await controller.Start(model);

            var redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual("Details", redirect.ActionName);
            Assert.IsNotNull(captured);
            Assert.AreEqual(sourceConnection.Id, captured.SourceConnectionId);
            Assert.AreEqual(null, captured.DestinationConnectionId);
            Assert.AreEqual("Data Source=adhoc.db", captured.DestinationDbConn);
            Assert.AreEqual("DefaultEndpointsProtocol=https;AccountName=adhoc;AccountKey=AAAA==", captured.DestinationStorageConn);
            Assert.AreEqual("controller-admin@local.test", captured.StartedBy);
        }

        private static object? GetAnonymousPropertyValue(object source, string propertyName)
        {
            return source.GetType().GetProperty(propertyName)?.GetValue(source);
        }

        private static HttpContext BuildHttpContext()
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Email, "controller-admin@local.test"),
                new Claim(ClaimTypes.Name, "controller-admin")
            ], "TestAuth"));

            return context;
        }
    }
}
