// <copyright file="SetupMiddlewareExtensionsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Editor.Middleware
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Middleware;
    using Sky.Editor.Services.Setup;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="SetupMiddlewareExtensions"/> setup detection and access control middleware.
    /// </summary>
    [TestClass]
    public class SetupMiddlewareExtensionsTests
    {
        private IMemoryCache _cache;
        private Mock<ISetupService> _mockSetupService;
        private Mock<IMultiTenantSetupService> _mockMultiTenantSetupService;
        private Mock<IConfiguration> _mockConfiguration;
        private IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes test fixtures.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _mockSetupService = new Mock<ISetupService>();
            _mockMultiTenantSetupService = new Mock<IMultiTenantSetupService>();
            _mockConfiguration = new Mock<IConfiguration>();

            var services = new ServiceCollection();
            services.AddSingleton(_cache);
            services.AddSingleton(_mockSetupService.Object);
            services.AddSingleton(_mockMultiTenantSetupService.Object);
            services.AddSingleton(_mockConfiguration.Object);

            _serviceProvider = services.BuildServiceProvider();
        }

        #region Setup Detection - Path Exemptions Tests

        /// <summary>
        /// Test: Setup pages are exempted from setup checks.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.PathExemptions")]
        public async Task UseSetupDetection_SkipsSetupPages_WhenPathIsSetup()
        {
            // Arrange
            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupDetection(isMultiTenantEditor: false);

            bool nextCalled = false;
            app.Use(async (ctx, next) => { nextCalled = true; await next(); });

            var context = CreateHttpContext("/___setup", _serviceProvider);

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert
            Assert.IsTrue(nextCalled, "Next middleware should be called for setup pages");
            Assert.AreNotEqual(302, context.Response.StatusCode, "Should not redirect setup pages");
        }

        /// <summary>
        /// Test: Static files are exempted from setup checks.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.PathExemptions")]
        public async Task UseSetupDetection_SkipsStaticFiles_WhenPathIsStaticAsset()
        {
            // Arrange
            var staticPaths = new[]
            {
                "/css/bootstrap.css",
                "/js/app.js",
                "/lib/jquery/jquery.min.js",
                "/images/logo.png",
                "/fonts/arial.ttf",
                "/app.js.map"
            };

            foreach (var path in staticPaths)
            {
                var app = new ApplicationBuilder(_serviceProvider);
                app.UseSetupDetection(isMultiTenantEditor: false);

                bool nextCalled = false;
                app.Use(async (ctx, next) => { nextCalled = true; await next(); });

                var context = CreateHttpContext(path, _serviceProvider);

                // Act
                var middleware = app.Build();
                await middleware(context);

                // Assert
                Assert.IsTrue(nextCalled, $"Next middleware should be called for static path: {path}");
                Assert.AreNotEqual(302, context.Response.StatusCode, $"Should not redirect static path: {path}");
            }
        }

        /// <summary>
        /// Test: Health check endpoints are exempted from setup checks.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.PathExemptions")]
        public async Task UseSetupDetection_SkipsHealthChecks_WhenPathIsHealthCheckEndpoint()
        {
            // Arrange
            var healthCheckPaths = new[] { "/healthz", "/___healthz", "/.well-known/ready" };

            foreach (var path in healthCheckPaths)
            {
                var app = new ApplicationBuilder(_serviceProvider);
                app.UseSetupDetection(isMultiTenantEditor: false);

                bool nextCalled = false;
                app.Use(async (ctx, next) => { nextCalled = true; await next(); });

                var context = CreateHttpContext(path, _serviceProvider);

                // Act
                var middleware = app.Build();
                await middleware(context);

                // Assert
                Assert.IsTrue(nextCalled, $"Next middleware should be called for health check path: {path}");
            }
        }

        #endregion

        #region Setup Detection - Single Tenant Tests

        /// <summary>
        /// Test: Redirects to setup when setup is incomplete in single-tenant mode.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.Detection.SingleTenant")]
        public async Task UseSetupDetection_RedirectsToSetup_WhenSetupIncomplete_SingleTenant()
        {
            // Arrange
            _mockSetupService.Setup(s => s.IsSetupCompleteAsync()).ReturnsAsync(false);

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupDetection(isMultiTenantEditor: false);

            var context = CreateHttpContext("/home", _serviceProvider);

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert
            Assert.AreEqual(302, context.Response.StatusCode, "Should redirect to setup");
            Assert.AreEqual("/___setup", context.Response.Headers["Location"].ToString(), "Should redirect to setup page");
        }

        /// <summary>
        /// Test: Allows access when setup is complete in single-tenant mode.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.Detection.SingleTenant")]
        public async Task UseSetupDetection_AllowsAccess_WhenSetupComplete_SingleTenant()
        {
            // Arrange
            _mockSetupService.Setup(s => s.IsSetupCompleteAsync()).ReturnsAsync(true);

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupDetection(isMultiTenantEditor: false);

            bool nextCalled = false;
            app.Use(async (ctx, next) => { nextCalled = true; await next(); });

            var context = CreateHttpContext("/home", _serviceProvider);

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert
            Assert.IsTrue(nextCalled, "Next middleware should be called when setup is complete");
            Assert.AreNotEqual(302, context.Response.StatusCode, "Should not redirect");
        }

        #endregion

        #region Setup Detection - Multi-Tenant Tests

        /// <summary>
        /// Test: Redirects to setup when tenant requires setup in multi-tenant mode.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.Detection.MultiTenant")]
        public async Task UseSetupDetection_RedirectsToSetup_WhenTenantRequiresSetup_MultiTenant()
        {
            // Arrange
            _mockMultiTenantSetupService.Setup(s => s.TenantRequiresSetupAsync()).ReturnsAsync(true);

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupDetection(isMultiTenantEditor: true);

            var context = CreateHttpContext("/admin/dashboard", _serviceProvider);

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert
            Assert.AreEqual(302, context.Response.StatusCode, "Should redirect to setup");
            Assert.AreEqual("/___setup", context.Response.Headers["Location"].ToString());
        }

        /// <summary>
        /// Test: Allows access when tenant setup is complete in multi-tenant mode.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.Detection.MultiTenant")]
        public async Task UseSetupDetection_AllowsAccess_WhenTenantSetupComplete_MultiTenant()
        {
            // Arrange
            _mockMultiTenantSetupService.Setup(s => s.TenantRequiresSetupAsync()).ReturnsAsync(false);

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupDetection(isMultiTenantEditor: true);

            bool nextCalled = false;
            app.Use(async (ctx, next) => { nextCalled = true; await next(); });

            var context = CreateHttpContext("/admin/dashboard", _serviceProvider);

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert
            Assert.IsTrue(nextCalled, "Next middleware should be called when tenant setup is complete");
        }

        #endregion

        #region Setup Detection - Caching Tests

        /// <summary>
        /// Test: Caches setup complete status for 24 hours.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.Caching")]
        public async Task UseSetupDetection_CachesSetupComplete_For24Hours()
        {
            // Arrange
            _mockSetupService.Setup(s => s.IsSetupCompleteAsync()).ReturnsAsync(true);

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupDetection(isMultiTenantEditor: false);

            var context1 = CreateHttpContext("/page1", _serviceProvider);
            var context2 = CreateHttpContext("/page2", _serviceProvider);

            // Act - First request should call service
            var middleware = app.Build();
            await middleware(context1);

            // Reset mock to verify second call doesn't invoke service
            _mockSetupService.Reset();

            // Second request should use cache
            await middleware(context2);

            // Assert - Service should not be called twice (cached)
            _mockSetupService.Verify(
                s => s.IsSetupCompleteAsync(),
                Times.Never, // Because it was mocked as returning true, second call should use cache
                "Service should not be called on second request (should use cache)");
        }

        /// <summary>
        /// Test: Caches setup incomplete status for 5 minutes in single-tenant.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.Caching")]
        public async Task UseSetupDetection_CachesSetupIncomplete_For5Minutes_SingleTenant()
        {
            // Arrange
            _mockSetupService.Setup(s => s.IsSetupCompleteAsync()).ReturnsAsync(false);
            var initialCallCount = 0;

            _mockSetupService
                .Setup(s => s.IsSetupCompleteAsync())
                .Callback(() => initialCallCount++)
                .ReturnsAsync(false);

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupDetection(isMultiTenantEditor: false);

            var context = CreateHttpContext("/page", _serviceProvider);

            // Act
            var middleware = app.Build();
            await middleware(context);

            var callCountAfterFirstRequest = initialCallCount;

            // Second request with same hostname should use cache
            await middleware(context);

            // Assert
            Assert.AreEqual(1, callCountAfterFirstRequest, "Service should be called once for setup incomplete");
            Assert.AreEqual(callCountAfterFirstRequest, initialCallCount, "Service should not be called again (cached)");
        }

        #endregion

        #region Setup Access Control Tests

        /// <summary>
        /// Test: Redirects away from setup page when setup is complete.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.AccessControl")]
        public async Task UseSetupAccessControl_RedirectsAway_WhenSetupComplete()
        {
            // Arrange - Pre-populate cache indicating setup is complete
            _cache.Set("SetupComplete:localhost", false); // false = no setup required = complete

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupAccessControl(isMultiTenantEditor: false);

            var context = CreateHttpContext("/___setup", _serviceProvider);

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert
            Assert.AreEqual(302, context.Response.StatusCode, "Should redirect away from setup");
            Assert.AreEqual("/", context.Response.Headers["Location"].ToString(), "Should redirect to home");
        }

        /// <summary>
        /// Test: Allows access to setup page when setup is needed.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.AccessControl")]
        public async Task UseSetupAccessControl_AllowsAccess_WhenSetupNeeded()
        {
            // Arrange - Mock the configuration indexer (GetValue uses this internally)
            var mockConfigSection = new Mock<IConfigurationSection>();
            mockConfigSection.Setup(s => s.Value).Returns("true");
            _mockConfiguration.Setup(c => c.GetSection("CosmosAllowSetup")).Returns(mockConfigSection.Object);

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupAccessControl(isMultiTenantEditor: false);

            bool nextCalled = false;
            app.Use(async (ctx, next) => { nextCalled = true; await next(); });

            var context = CreateHttpContext("/___setup", _serviceProvider);

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert
            Assert.IsTrue(nextCalled, "Next middleware should be called when setup is allowed");
        }

        /// <summary>
        /// Test: Redirects away from setup when CosmosAllowSetup is false in single-tenant.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.AccessControl")]
        public async Task UseSetupAccessControl_RedirectsAway_WhenSetupDisabled_SingleTenant()
        {
            // Arrange - Mock the configuration indexer (GetValue uses this internally)
            var mockConfigSection = new Mock<IConfigurationSection>();
            mockConfigSection.Setup(s => s.Value).Returns("false");
            _mockConfiguration.Setup(c => c.GetSection("CosmosAllowSetup")).Returns(mockConfigSection.Object);

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupAccessControl(isMultiTenantEditor: false);

            bool nextCalled = false;
            app.Use(async (ctx, next) => { nextCalled = true; await next(); });

            var context = CreateHttpContext("/___setup", _serviceProvider);

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert
            Assert.AreEqual(302, context.Response.StatusCode, "Should redirect when setup is disabled");
            Assert.AreEqual("/", context.Response.Headers["Location"].ToString());
            Assert.IsFalse(nextCalled, "Next middleware should not be called when redirecting");
        }

        /// <summary>
        /// Test: Allows setup access in multi-tenant mode when not cached as complete.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.AccessControl")]
        public async Task UseSetupAccessControl_AllowsSetupAccess_InMultiTenantMode()
        {
            // Arrange
            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupAccessControl(isMultiTenantEditor: true);

            bool nextCalled = false;
            app.Use(async (ctx, next) => { nextCalled = true; await next(); });

            var context = CreateHttpContext("/___setup", _serviceProvider);

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert
            Assert.IsTrue(nextCalled, "Multi-tenant should allow setup access when not cached as complete");
        }

        #endregion

        #region Hostname Handling Tests

        /// <summary>
        /// Test: Uses x-origin-hostname header when present.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.Hostname")]
        public async Task UseSetupDetection_UsesOriginHostnameHeader_WhenPresent()
        {
            // Arrange
            _mockSetupService.Setup(s => s.IsSetupCompleteAsync()).ReturnsAsync(true);

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupDetection(isMultiTenantEditor: false);

            bool nextCalled = false;
            app.Use(async (ctx, next) => { nextCalled = true; await next(); });

            var context = CreateHttpContext("/page", _serviceProvider, originHostname: "custom.example.com");

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert - Should complete successfully with header
            Assert.IsTrue(nextCalled, "Should process request with custom hostname header");
        }

        /// <summary>
        /// Test: Falls back to Host header when x-origin-hostname is not present.
        /// </summary>
        [TestMethod]
        [TestCategory("SetupMiddleware.Hostname")]
        public async Task UseSetupDetection_FallsBackToHostHeader_WhenOriginHostnameAbsent()
        {
            // Arrange
            _mockSetupService.Setup(s => s.IsSetupCompleteAsync()).ReturnsAsync(true);

            var app = new ApplicationBuilder(_serviceProvider);
            app.UseSetupDetection(isMultiTenantEditor: false);

            bool nextCalled = false;
            app.Use(async (ctx, next) => { nextCalled = true; await next(); });

            var context = CreateHttpContext("/page", _serviceProvider, originHostname: null);

            // Act
            var middleware = app.Build();
            await middleware(context);

            // Assert
            Assert.IsTrue(nextCalled, "Should fall back to Host header");
        }

        #endregion

        #region Test Helpers

        /// <summary>
        /// Creates a mock HTTP context for testing.
        /// </summary>
        private HttpContext CreateHttpContext(string path, IServiceProvider serviceProvider, string originHostname = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Request.Host = new HostString("localhost:5000");
            context.RequestServices = serviceProvider;

            if (!string.IsNullOrEmpty(originHostname))
            {
                context.Request.Headers["x-origin-hostname"] = originHostname;
            }

            return context;
        }

        #endregion
    }
}
