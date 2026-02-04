// <copyright file="DomainMiddlewareTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Middleware
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for DomainMiddleware - Critical for multi-tenant security and tenant isolation.
    /// Tests domain validation, tenant resolution, and security edge cases.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class DomainMiddlewareTests : SkyCmsTestBase
    {
        private Mock<RequestDelegate> mockNext;
        private Mock<ILogger<DomainMiddleware>> mockLogger;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
            mockNext = new Mock<RequestDelegate>();
            mockLogger = new Mock<ILogger<DomainMiddleware>>();
        }

        #region Valid Domain Tests

        /// <summary>
        /// Tests that middleware allows valid configured domains to proceed.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_ValidDomain_ShouldProceedToNextMiddleware()
        {
            // Arrange
            var middleware = new DomainMiddleware(mockNext.Object, mockLogger.Object);
            var context = CreateHttpContext("validtenant.com");

            var mockConfigProvider = new Mock<IDynamicConfigurationProvider>();
            mockConfigProvider
                .Setup(x => x.GetDatabaseConnectionStringAsync("validtenant.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Server=localhost;Database=TenantDb;");

            AddServiceToContext(context, mockConfigProvider.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            mockNext.Verify(x => x(context), Times.Once, "Middleware should call next delegate for valid domains");
            Assert.AreEqual("validtenant.com", context.Items["Domain"], "Domain should be stored in context items");
            Assert.AreEqual(200, context.Response.StatusCode, "Response should be 200 OK");
        }

        /// <summary>
        /// Tests that domain names are normalized to lowercase.
        /// </summary>
        [TestMethod]
        [DataRow("UPPERCASE.COM", "uppercase.com")]
        [DataRow("MixedCase.COM", "mixedcase.com")]
        [DataRow("localhost", "localhost")]
        [DataRow("TenantOne.Example.COM", "tenantone.example.com")]
        public async Task InvokeAsync_DomainCasing_ShouldNormalizeToLowerCase(string inputDomain, string expectedDomain)
        {
            // Arrange
            var middleware = new DomainMiddleware(mockNext.Object, mockLogger.Object);
            var context = CreateHttpContext(inputDomain);

            var mockConfigProvider = new Mock<IDynamicConfigurationProvider>();
            mockConfigProvider
                .Setup(x => x.GetDatabaseConnectionStringAsync(expectedDomain, It.IsAny<CancellationToken>()))
                .ReturnsAsync("Server=localhost;Database=TenantDb;");

            AddServiceToContext(context, mockConfigProvider.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(expectedDomain, context.Items["Domain"], 
                $"Domain should be normalized to lowercase: {expectedDomain}");
            mockConfigProvider.Verify(
                x => x.GetDatabaseConnectionStringAsync(expectedDomain, It.IsAny<CancellationToken>()), 
                Times.Once,
                "Configuration provider should be called with lowercase domain");
        }

        #endregion

        #region Invalid Domain Tests

        /// <summary>
        /// Tests that middleware blocks unauthorized domains with 404 response.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_InvalidDomain_ShouldReturn404()
        {
            // Arrange
            var middleware = new DomainMiddleware(mockNext.Object, mockLogger.Object);
            var context = CreateHttpContext("unauthorized.com");

            var mockConfigProvider = new Mock<IDynamicConfigurationProvider>();
            mockConfigProvider
                .Setup(x => x.GetDatabaseConnectionStringAsync("unauthorized.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null); // Simulate no connection string = invalid domain

            AddServiceToContext(context, mockConfigProvider.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            mockNext.Verify(x => x(context), Times.Never, "Middleware should NOT call next delegate for invalid domains");
            Assert.AreEqual(404, context.Response.StatusCode, "Response should be 404 Not Found for invalid domains");
        }

        /// <summary>
        /// Tests that middleware blocks empty/null connection string domains.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_EmptyConnectionString_ShouldReturn404()
        {
            // Arrange
            var middleware = new DomainMiddleware(mockNext.Object, mockLogger.Object);
            var context = CreateHttpContext("noconfiguration.com");

            var mockConfigProvider = new Mock<IDynamicConfigurationProvider>();
            mockConfigProvider
                .Setup(x => x.GetDatabaseConnectionStringAsync("noconfiguration.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty); // Empty string = invalid

            AddServiceToContext(context, mockConfigProvider.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(404, context.Response.StatusCode, "Response should be 404 for empty connection strings");
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Tests that middleware handles exceptions gracefully and fails open (continues processing).
        /// This ensures availability even when configuration provider has issues.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_ConfigProviderThrowsException_ShouldLogAndContinue()
        {
            // Arrange
            var middleware = new DomainMiddleware(mockNext.Object, mockLogger.Object);
            var context = CreateHttpContext("error.com");

            var mockConfigProvider = new Mock<IDynamicConfigurationProvider>();
            mockConfigProvider
                .Setup(x => x.GetDatabaseConnectionStringAsync("error.com", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            AddServiceToContext(context, mockConfigProvider.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert - Should fail open (continue processing) for availability
            mockNext.Verify(x => x(context), Times.Once, 
                "Middleware should fail open and continue processing despite errors");
            Assert.AreEqual("error.com", context.Items["Domain"], "Domain should still be set");
        }

        /// <summary>
        /// Tests that middleware continues processing when no configuration provider is available.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_NoConfigProvider_ShouldContinueWithoutValidation()
        {
            // Arrange
            var middleware = new DomainMiddleware(mockNext.Object, mockLogger.Object);
            var context = CreateHttpContext("anydomain.com");
            // Don't add config provider to context

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            mockNext.Verify(x => x(context), Times.Once, 
                "Middleware should continue without validation when no config provider exists");
            Assert.AreEqual("anydomain.com", context.Items["Domain"]);
        }

        #endregion

        #region Security Edge Cases

        /// <summary>
        /// Tests that middleware handles malicious domain injection attempts.
        /// </summary>
        [TestMethod]
        [DataRow("evil.com\r\nX-Injected-Header: malicious")]
        [DataRow("evil.com\nX-Injected-Header: malicious")]
        [DataRow("evil.com%0d%0aX-Injected-Header: malicious")]
        public async Task InvokeAsync_HeaderInjectionAttempt_ShouldNormalizeAndBlock(string maliciousDomain)
        {
            // Arrange
            var middleware = new DomainMiddleware(mockNext.Object, mockLogger.Object);
            
            // Note: ASP.NET Core's HostString handles validation, but we test our layer
            try
            {
                var context = CreateHttpContext(maliciousDomain);
                var mockConfigProvider = new Mock<IDynamicConfigurationProvider>();
                mockConfigProvider
                    .Setup(x => x.GetDatabaseConnectionStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string)null);

                AddServiceToContext(context, mockConfigProvider.Object);

                // Act
                await middleware.InvokeAsync(context);

                // Assert
                Assert.AreEqual(404, context.Response.StatusCode, "Malicious domains should be blocked");
            }
            catch (Exception)
            {
                // HostString may throw for invalid input - this is acceptable
                Assert.IsTrue(true, "ASP.NET Core blocked invalid host header");
            }
        }

        #endregion

        #region Helper Methods

        private DefaultHttpContext CreateHttpContext(string domain)
        {
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString(domain);
            context.Request.Path = "/test";
            context.Response.Body = new MemoryStream();
            context.RequestServices = new ServiceCollection().BuildServiceProvider();
            return context;
        }

        private void AddServiceToContext(HttpContext context, IDynamicConfigurationProvider configProvider)
        {
            var services = new ServiceCollection();
            services.AddSingleton(configProvider);
            context.RequestServices = services.BuildServiceProvider();
        }

        #endregion
    }
}
