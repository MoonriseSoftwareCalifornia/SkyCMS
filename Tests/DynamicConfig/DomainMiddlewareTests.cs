// <copyright file="DomainMiddlewareTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.DynamicConfig;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Sky.Tests.DynamicConfig
{
    /// <summary>
    /// Tests for DomainMiddleware - Priority 1 multi-tenant core infrastructure.
    /// Tests tenant resolution, header handling, and middleware execution flow.
    /// </summary>
    [TestClass]
    public class DomainMiddlewareTests
    {
        private Mock<ILogger<DomainMiddleware>> _loggerMock = null!;
        private Mock<IDynamicConfigurationProvider> _configProviderMock = null!;
        private Mock<RequestDelegate> _nextMock = null!;
        private DomainMiddleware _middleware = null!;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<DomainMiddleware>>();
            _configProviderMock = new Mock<IDynamicConfigurationProvider>();
            _nextMock = new Mock<RequestDelegate>();
            _middleware = new DomainMiddleware(_nextMock.Object, _loggerMock.Object);
        }

        [TestMethod]
        public async Task InvokeAsync_WithValidDomain_CallsNextMiddleware()
        {
            // Arrange
            var context = CreateHttpContext("valid-domain.com");
            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("valid-domain.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Data Source=valid.db");

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            _nextMock.Verify(x => x(context), Times.Once);
            Assert.AreEqual("valid-domain.com", context.Items["Domain"]);
        }

        [TestMethod]
        public async Task InvokeAsync_WithInvalidDomain_Returns404()
        {
            // Arrange
            var context = CreateHttpContext("invalid-domain.com");
            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("invalid-domain.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(404, context.Response.StatusCode);
            _nextMock.Verify(x => x(context), Times.Never);
        }

        [TestMethod]
        public async Task InvokeAsync_WithEmptyConnectionString_Returns404()
        {
            // Arrange
            var context = CreateHttpContext("empty-conn-domain.com");
            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("empty-conn-domain.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(404, context.Response.StatusCode);
            _nextMock.Verify(x => x(context), Times.Never);
        }

        [TestMethod]
        public async Task InvokeAsync_NormalizesToLowercase()
        {
            // Arrange
            var context = CreateHttpContext("UPPERCASE-DOMAIN.COM");
            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("uppercase-domain.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Data Source=uppercase.db");

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual("uppercase-domain.com", context.Items["Domain"]);
            _configProviderMock.Verify(
                x => x.GetDatabaseConnectionStringAsync("uppercase-domain.com", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task InvokeAsync_WithException_ContinuesProcessing()
        {
            // Arrange
            var context = CreateHttpContext("exception-domain.com");
            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("exception-domain.com", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            // Should fail open for availability
            _nextMock.Verify(x => x(context), Times.Once);
            Assert.AreEqual("exception-domain.com", context.Items["Domain"]);
        }

        [TestMethod]
        public async Task InvokeAsync_WithNoConfigProvider_ContinuesProcessing()
        {
            // Arrange
            var context = CreateHttpContext("no-provider-domain.com", registerProvider: false);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            // Should continue when no config provider is registered
            _nextMock.Verify(x => x(context), Times.Once);
            Assert.AreEqual("no-provider-domain.com", context.Items["Domain"]);
        }

        [TestMethod]
        public async Task InvokeAsync_LogsDebugForAllRequests()
        {
            // Arrange
            var context = CreateHttpContext("logged-domain.com");
            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("logged-domain.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Data Source=logged.db");

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("logged-domain.com")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task InvokeAsync_LogsWarningForInvalidDomain()
        {
            // Arrange
            var context = CreateHttpContext("unauthorized-domain.com");
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");
            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("unauthorized-domain.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unauthorized domain access attempt")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task InvokeAsync_LogsInformationForValidDomain()
        {
            // Arrange
            var context = CreateHttpContext("valid-info-domain.com");
            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("valid-info-domain.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Data Source=valid-info.db");

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Valid domain access")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task InvokeAsync_LogsErrorForExceptions()
        {
            // Arrange
            var context = CreateHttpContext("error-domain.com");
            var expectedException = new InvalidOperationException("Test exception");
            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("error-domain.com", It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error validating domain")),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task InvokeAsync_IncludesPathAndIpInWarningLog()
        {
            // Arrange
            var context = CreateHttpContext("unauthorized-path.com");
            context.Request.Path = "/admin/sensitive";
            context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.50");
            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("unauthorized-path.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("/admin/sensitive") && v.ToString()!.Contains("10.0.0.50")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task InvokeAsync_HandlesPortInHostHeader()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString("domain-with-port.com", 8080);
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_configProviderMock.Object);
            context.RequestServices = serviceCollection.BuildServiceProvider();

            _configProviderMock
                .Setup(x => x.GetDatabaseConnectionStringAsync("domain-with-port.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Data Source=port.db");

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual("domain-with-port.com", context.Items["Domain"]);
            _configProviderMock.Verify(
                x => x.GetDatabaseConnectionStringAsync("domain-with-port.com", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private HttpContext CreateHttpContext(string host, bool registerProvider = true)
        {
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString(host);
            
            var serviceCollection = new ServiceCollection();
            if (registerProvider)
            {
                serviceCollection.AddSingleton(_configProviderMock.Object);
            }
            context.RequestServices = serviceCollection.BuildServiceProvider();
            
            return context;
        }
    }
}
