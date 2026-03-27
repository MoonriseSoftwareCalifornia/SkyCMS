// <copyright file="EditorControllerReservedPathsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using System;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for EditorController reserved paths management.
    /// Covers ReservedPaths, CreateReservedPath, and EditReservedPath methods.
    /// </summary>
    [TestClass]
    public class EditorControllerReservedPathsTests : SkyCmsTestBase
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
                LayoutCacheService,
                DynamicConfigurationProvider);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "admin@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        #region ReservedPaths GET Tests

        /// <summary>
        /// Tests that ReservedPaths returns view with paths list.
        /// </summary>
        [TestMethod]
        public async Task ReservedPaths_Get_ReturnsViewWithPathsList()
        {
            // Act
            var result = await controller.ReservedPaths(
                sortOrder: "asc",
                currentSort: "Path",
                pageNo: 0,
                pageSize: 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
        }

        /// <summary>
        /// Tests that ReservedPaths handles sorting by Path ascending.
        /// </summary>
        [TestMethod]
        public async Task ReservedPaths_Get_SortsByPathAscending()
        {
            // Act
            var result = await controller.ReservedPaths(
                sortOrder: "asc",
                currentSort: "Path");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual("asc", controller.ViewData["sortOrder"]);
            Assert.AreEqual("Path", controller.ViewData["currentSort"]);
        }

        /// <summary>
        /// Tests that ReservedPaths handles sorting by Path descending.
        /// </summary>
        [TestMethod]
        public async Task ReservedPaths_Get_SortsByPathDescending()
        {
            // Act
            var result = await controller.ReservedPaths(
                sortOrder: "desc",
                currentSort: "Path");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual("desc", controller.ViewData["sortOrder"]);
        }

        /// <summary>
        /// Tests that ReservedPaths handles paging.
        /// </summary>
        [TestMethod]
        public async Task ReservedPaths_Get_HandlesPaging()
        {
            // Act
            var result = await controller.ReservedPaths(
                sortOrder: "asc",
                currentSort: "Path",
                pageNo: 1,
                pageSize: 5);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual(1, controller.ViewData["pageNo"]);
            Assert.AreEqual(5, controller.ViewData["pageSize"]);
        }

        /// <summary>
        /// Tests that ReservedPaths handles filtering.
        /// </summary>
        [TestMethod]
        public async Task ReservedPaths_Get_HandlesFiltering()
        {
            // Act
            var result = await controller.ReservedPaths(
                sortOrder: "asc",
                currentSort: "Path",
                pageNo: 0,
                pageSize: 10,
                filter: "test");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual("test", controller.ViewData["Filter"]);
        }

        /// <summary>
        /// Tests that ReservedPaths uses default sorting when not specified.
        /// </summary>
        [TestMethod]
        public async Task ReservedPaths_Get_UsesDefaultSorting()
        {
            // Act - No sort order specified
            var result = await controller.ReservedPaths(
                sortOrder: null!,
                currentSort: null!);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        #endregion

        #region CreateReservedPath Tests

        /// <summary>
        /// Tests that CreateReservedPath returns view with empty model.
        /// </summary>
        [TestMethod]
        public void CreateReservedPath_Get_ReturnsViewWithEmptyModel()
        {
            // Act
            var result = controller.CreateReservedPath();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(ReservedPath));

            Assert.AreEqual("Create a Reserved Path", controller.ViewData["Title"]);
        }

        /// <summary>
        /// Tests that CreateReservedPath uses EditReservedPath view.
        /// </summary>
        [TestMethod]
        public void CreateReservedPath_Get_UsesEditReservedPathView()
        {
            // Act
            var result = controller.CreateReservedPath();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("~/Views/Editor/EditReservedPath.cshtml", viewResult.ViewName);
        }

        #endregion

        #region EditReservedPath Tests

        /// <summary>
        /// Tests that EditReservedPath returns view with path model.
        /// </summary>
        [TestMethod]
        public async Task EditReservedPath_Get_ReturnsViewWithPathModel()
        {
            // Arrange - Get a reserved path from the service
            var paths = await ReservedPaths.GetReservedPaths();
            if (paths.Count == 0)
            {
                Assert.Inconclusive("No reserved paths available for testing");
                return;
            }

            var testPath = paths[0];

            // Act
            var result = await controller.EditReservedPath(testPath.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(ReservedPath));
        }

        /// <summary>
        /// Tests that EditReservedPath returns NotFound for non-existent path.
        /// </summary>
        [TestMethod]
        public async Task EditReservedPath_Get_ReturnsNotFoundForNonExistentPath()
        {
            // Arrange - Use a non-existent GUID
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await controller.EditReservedPath(nonExistentId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that EditReservedPath sets correct ViewData title.
        /// </summary>
        [TestMethod]
        public async Task EditReservedPath_Get_SetsCorrectTitle()
        {
            // Arrange
            var paths = await ReservedPaths.GetReservedPaths();
            if (paths.Count == 0)
            {
                Assert.Inconclusive("No reserved paths available for testing");
                return;
            }

            var testPath = paths[0];

            // Act
            var result = await controller.EditReservedPath(testPath.Id);

            // Assert
            Assert.AreEqual("Edit Reserved Path", controller.ViewData["Title"]);
        }

        #endregion
    }
}
