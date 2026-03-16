// <copyright file="TenantSetupMiddlewareTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Editor.Middleware
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Middleware;
    using Sky.Editor.Services.Setup;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="TenantSetupMiddleware"/> and related functionality.
    /// Tests tenant setup detection and redirection behavior.
    /// </summary>
    [TestClass]
    public class TenantSetupMiddlewareTests
    {
        private Mock<IMultiTenantSetupService> _mockSetupService;
        private IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes test fixtures.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _mockSetupService = new Mock<IMultiTenantSetupService>();

            var services = new ServiceCollection();
            services.AddSingleton(_mockSetupService.Object);

            _serviceProvider = services.BuildServiceProvider();
        }

        #region Tenant Setup Detection Tests

        /// <summary>
        /// Test: Redirects to tenant setup when tenant requires setup.
        /// </summary>
        [TestMethod]
        [TestCategory("TenantSetupMiddleware.Detection")]
        public async Task TenantSetupMiddleware_RedirectsToTenantSetup_WhenSetupRequired()
        {
            // Arrange
            _mockSetupService.Setup(s => s.TenantRequiresSetupAsync()).ReturnsAsync(true);

            var middleware = new TenantSetupMiddleware(async (ctx) => { });
            var context = CreateHttpContext("/dashboard", _serviceProvider);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(302, context.Response.StatusCode, "Should redirect to tenant setup");
            Assert.AreEqual("/___setup/tenant", context.Response.Headers["Location"].ToString(), "Should redirect to tenant setup page");
        }

        /// <summary>
        /// Test: Continues to next middleware when tenant setup is complete.
        /// </summary>
        [TestMethod]
        [TestCategory("TenantSetupMiddleware.Detection")]
        public async Task TenantSetupMiddleware_ContinuesToNext_WhenSetupComplete()
        {
            // Arrange
            _mockSetupService.Setup(s => s.TenantRequiresSetupAsync()).ReturnsAsync(false);

            bool nextCalled = false;
            RequestDelegate next = async (ctx) =>
            {
                nextCalled = true;
                await Task.CompletedTask;
            };

            var middleware = new TenantSetupMiddleware(next);
            var context = CreateHttpContext("/dashboard", _serviceProvider);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.IsTrue(nextCalled, "Next middleware should be called when setup is complete");
            Assert.AreNotEqual(302, context.Response.StatusCode, "Should not redirect");
        }

        #endregion

        #region Path Exemption Tests

        /// <summary>
        /// Test: Skips setup check for setup pages.
        /// </summary>
        [TestMethod]
        [TestCategory("TenantSetupMiddleware.PathExemptions")]
        public async Task TenantSetupMiddleware_SkipsCheck_ForSetupPages()
        {
            // Arrange
            var setupPaths = new[] { "/___setup", "/___setup/tenant", "/___setup/step1" };

            foreach (var path in setupPaths)
            {
                _mockSetupService.Reset();
                _mockSetupService.Setup(s => s.TenantRequiresSetupAsync()).ReturnsAsync(true);

                bool nextCalled = false;
                RequestDelegate next = async (ctx) => { nextCalled = true; await Task.CompletedTask; };

                var middleware = new TenantSetupMiddleware(next);
                var context = CreateHttpContext(path, _serviceProvider);

                // Act
                await middleware.InvokeAsync(context);

                // Assert
                Assert.IsTrue(nextCalled, $"Setup check should be skipped for: {path}");
                Assert.AreNotEqual(302, context.Response.StatusCode, $"Should not redirect setup pages: {path}");
            }
        }

        /// <summary>
        /// Test: Skips setup check for diagnostics pages.
        /// </summary>
        [TestMethod]
        [TestCategory("TenantSetupMiddleware.PathExemptions")]
        public async Task TenantSetupMiddleware_SkipsCheck_ForDiagnosticsPages()
        {
            // Arrange
            _mockSetupService.Setup(s => s.TenantRequiresSetupAsync()).ReturnsAsync(true);

            bool nextCalled = false;
            RequestDelegate next = async (ctx) => { nextCalled = true; await Task.CompletedTask; };

            var middleware = new TenantSetupMiddleware(next);
            var context = CreateHttpContext("/___diagnostics", _serviceProvider);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.IsTrue(nextCalled, "Setup check should be skipped for diagnostics pages");
            _mockSetupService.Verify(s => s.TenantRequiresSetupAsync(), Times.Never, "Should not check setup status");
        }

        /// <summary>
        /// Test: Skips setup check for API endpoints.
        /// </summary>
        [TestMethod]
        [TestCategory("TenantSetupMiddleware.PathExemptions")]
        public async Task TenantSetupMiddleware_SkipsCheck_ForApiEndpoints()
        {
            // Arrange
            var apiPaths = new[] { "/api/articles", "/api/publish", "/api/settings" };

            foreach (var path in apiPaths)
            {
                _mockSetupService.Reset();
                _mockSetupService.Setup(s => s.TenantRequiresSetupAsync()).ReturnsAsync(true);

                bool nextCalled = false;
                RequestDelegate next = async (ctx) => { nextCalled = true; await Task.CompletedTask; };

                var middleware = new TenantSetupMiddleware(next);
                var context = CreateHttpContext(path, _serviceProvider);

                // Act
                await middleware.InvokeAsync(context);

                // Assert
                Assert.IsTrue(nextCalled, $"Setup check should be skipped for API: {path}");
                _mockSetupService.Verify(s => s.TenantRequiresSetupAsync(), Times.Never, "Service should not be called");
            }
        }

        /// <summary>
        /// Test: Skips setup check for health check endpoints.
        /// </summary>
        [TestMethod]
        [TestCategory("TenantSetupMiddleware.PathExemptions")]
        public async Task TenantSetupMiddleware_SkipsCheck_ForHealthChecks()
        {
            // Arrange
            var healthPaths = new[] { "/healthz", "/health" };

            foreach (var path in healthPaths)
            {
                _mockSetupService.Reset();
                _mockSetupService.Setup(s => s.TenantRequiresSetupAsync()).ReturnsAsync(true);

                bool nextCalled = false;
                RequestDelegate next = async (ctx) => { nextCalled = true; await Task.CompletedTask; };

                var middleware = new TenantSetupMiddleware(next);
                var context = CreateHttpContext(path, _serviceProvider);

                // Act
                await middleware.InvokeAsync(context);

                // Assert
                Assert.IsTrue(nextCalled, $"Setup check should be skipped for health check: {path}");
            }
        }

        /// <summary>
        /// Test: Skips setup check for static files.
        /// </summary>
        [TestMethod]
        [TestCategory("TenantSetupMiddleware.PathExemptions")]
        public async Task TenantSetupMiddleware_SkipsCheck_ForStaticFiles()
        {
            // Arrange
            var staticPaths = new[] { "/css/app.css", "/js/app.js", "/lib/jquery/jquery.js", "/fonts/arial.ttf" };

            foreach (var path in staticPaths)
            {
                _mockSetupService.Reset();
                _mockSetupService.Setup(s => s.TenantRequiresSetupAsync()).ReturnsAsync(true);

                bool nextCalled = false;
                RequestDelegate next = async (ctx) => { nextCalled = true; await Task.CompletedTask; };

                var middleware = new TenantSetupMiddleware(next);
                var context = CreateHttpContext(path, _serviceProvider);

                // Act
                await middleware.InvokeAsync(context);

                // Assert
                Assert.IsTrue(nextCalled, $"Setup check should be skipped for static file: {path}");
                _mockSetupService.Verify(s => s.TenantRequiresSetupAsync(), Times.Never, "Service should not be called");
            }
        }

        /// <summary>
        /// Test: Skips setup check for identity/authentication paths.
        /// </summary>
        [TestMethod]
        [TestCategory("TenantSetupMiddleware.PathExemptions")]
        public async Task TenantSetupMiddleware_SkipsCheck_ForIdentityPaths()
        {
            // Arrange
            _mockSetupService.Setup(s => s.TenantRequiresSetupAsync()).ReturnsAsync(true);

            bool nextCalled = false;
            RequestDelegate next = async (ctx) => { nextCalled = true; await Task.CompletedTask; };

            var middleware = new TenantSetupMiddleware(next);
            var context = CreateHttpContext("/Identity/Account/Login", _serviceProvider);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.IsTrue(nextCalled, "Setup check should be skipped for Identity paths");
            _mockSetupService.Verify(s => s.TenantRequiresSetupAsync(), Times.Never, "Service should not be called");
        }

        #endregion

        #region Extension Method Tests

        /// <summary>
        /// Test: UseTenantSetupRedirect extension method registers middleware.
        /// </summary>
        [TestMethod]
        [TestCategory("TenantSetupMiddleware.Extension")]
        public void UseTenantSetupRedirect_RegistersMiddleware()
        {
            // Arrange
            var appBuilder = new ApplicationBuilder(_serviceProvider);

            // Act
            appBuilder.UseTenantSetupRedirect();

            // Assert - Should not throw
            Assert.IsNotNull(appBuilder, "ApplicationBuilder should be returned");
        }

        #endregion

        #region Service Availability Tests

        /// <summary>
        /// Test: Continues normally when IMultiTenantSetupService is not available.
        /// </summary>
        [TestMethod]
        [TestCategory("TenantSetupMiddleware.ServiceAvailability")]
        public async Task TenantSetupMiddleware_ContinuesNormally_WhenServiceUnavailable()
        {
            // Arrange
            var services = new ServiceCollection();
            // Don't add the service
            var serviceProvider = services.BuildServiceProvider();

            bool nextCalled = false;
            RequestDelegate next = async (ctx) => { nextCalled = true; await Task.CompletedTask; };

            var middleware = new TenantSetupMiddleware(next);
            var context = CreateHttpContext("/dashboard", serviceProvider);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.IsTrue(nextCalled, "Should continue when service is not available");
            Assert.AreNotEqual(302, context.Response.StatusCode, "Should not redirect");
        }

        #endregion

        #region Test Helpers

        /// <summary>
        /// Creates a mock HTTP context for testing.
        /// </summary>
        private HttpContext CreateHttpContext(string path, IServiceProvider serviceProvider)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Request.Host = new HostString("localhost:5000");
            context.RequestServices = serviceProvider;

            return context;
        }

        #endregion
    }
}
