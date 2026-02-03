// <copyright file="ValidateCaptchaHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Features.ContactForm;

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Sky.Cms.Api.Shared.Features.ContactForm.ValidateCaptcha;
using Sky.Cms.Api.Shared.Models;

/// <summary>
/// Unit tests for <see cref="ValidateCaptchaHandler"/>.
/// Tests Turnstile validation, reCAPTCHA validation, and error responses.
/// Thread-safe for parallel execution.
/// </summary>
[TestClass]
public class ValidateCaptchaHandlerTests
{
    private Mock<IHttpClientFactory> httpClientFactoryMock;
    private Mock<ILogger<ValidateCaptchaHandler>> loggerMock;
    private ContactApiConfig config;
    private ValidateCaptchaHandler handler;

    [TestInitialize]
    public void Setup()
    {
        // Setup mocks
        httpClientFactoryMock = new Mock<IHttpClientFactory>();
        loggerMock = new Mock<ILogger<ValidateCaptchaHandler>>();

        // Setup default config
        config = new ContactApiConfig
        {
            RequireCaptcha = true,
            CaptchaProvider = "turnstile",
            CaptchaSiteKey = "test-site-key",
            CaptchaSecretKey = "test-secret-key"
        };

        // Create handler
        handler = new ValidateCaptchaHandler(
            httpClientFactoryMock.Object,
            loggerMock.Object,
            Options.Create(config));
    }

    #region Constructor Tests

    [TestMethod]
    public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            var handler = new ValidateCaptchaHandler(null, loggerMock.Object, Options.Create(config));
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            var handler = new ValidateCaptchaHandler(httpClientFactoryMock.Object, null, Options.Create(config));
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            var handler = new ValidateCaptchaHandler(httpClientFactoryMock.Object, loggerMock.Object, null);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    #endregion

    #region CAPTCHA Not Required Tests

    [TestMethod]
    public async Task HandleAsync_WhenCaptchaNotRequired_ReturnsTrue()
    {
        // Arrange
        config.RequireCaptcha = false;
        handler = new ValidateCaptchaHandler(
            httpClientFactoryMock.Object,
            loggerMock.Object,
            Options.Create(config));

        var query = new ValidateCaptchaQuery
        {
            Token = "any-token",
            RemoteIpAddress = "192.168.1.1"
        };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task HandleAsync_WhenCaptchaProviderEmpty_ReturnsTrue()
    {
        // Arrange
        config.RequireCaptcha = true;
        config.CaptchaProvider = string.Empty;
        handler = new ValidateCaptchaHandler(
            httpClientFactoryMock.Object,
            loggerMock.Object,
            Options.Create(config));

        var query = new ValidateCaptchaQuery { Token = "token" };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.IsTrue(result);
    }

    #endregion

    #region Turnstile Validation Tests

    [TestMethod]
    public async Task HandleAsync_WithValidTurnstileToken_ReturnsTrue()
    {
        // Arrange
        var successResponse = new
        {
            success = true,
            challenge_ts = "2024-01-01T00:00:00Z",
            hostname = "example.com"
        };

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(successResponse))
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var query = new ValidateCaptchaQuery
        {
            Token = "valid-token",
            RemoteIpAddress = "192.168.1.1",
            CaptchaProvider = "turnstile",
            SecretKey = "secret"
        };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task HandleAsync_WithInvalidTurnstileToken_ReturnsFalse()
    {
        // Arrange
        var failureResponse = new
        {
            success = false,
            error_codes = new[] { "invalid-input-response" }
        };

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(failureResponse))
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var query = new ValidateCaptchaQuery
        {
            Token = "invalid-token",
            RemoteIpAddress = "192.168.1.2",
            CaptchaProvider = "turnstile",
            SecretKey = "secret"
        };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HandleAsync_TurnstileApiError_ReturnsFalse()
    {
        // Arrange
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var query = new ValidateCaptchaQuery
        {
            Token = "token",
            RemoteIpAddress = "192.168.1.3",
            CaptchaProvider = "turnstile",
            SecretKey = "secret"
        };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HandleAsync_TurnstileValidation_LogsSuccess()
    {
        // Arrange
        var successResponse = new
        {
            success = true
        };

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(successResponse))
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var query = new ValidateCaptchaQuery
        {
            Token = "token",
            RemoteIpAddress = "192.168.1.100",
            CaptchaProvider = "turnstile",
            SecretKey = "secret"
        };

        // Act
        await handler.HandleAsync(query);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("validation successful")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_TurnstileValidation_LogsFailure()
    {
        // Arrange
        var failureResponse = new
        {
            success = false,
            error_codes = new[] { "timeout-or-duplicate" }
        };

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(failureResponse))
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var query = new ValidateCaptchaQuery
        {
            Token = "token",
            RemoteIpAddress = "192.168.1.200",
            CaptchaProvider = "turnstile",
            SecretKey = "secret"
        };

        // Act
        await handler.HandleAsync(query);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("validation failed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    #endregion

    #region reCAPTCHA Validation Tests

    [TestMethod]
    public async Task HandleAsync_WithValidReCaptchaToken_ReturnsTrue()
    {
        // Arrange
        config.CaptchaProvider = "recaptcha";
        handler = new ValidateCaptchaHandler(
            httpClientFactoryMock.Object,
            loggerMock.Object,
            Options.Create(config));

        var successResponse = new
        {
            success = true,
            challenge_ts = "2024-01-01T00:00:00Z",
            hostname = "example.com",
            score = 0.9
        };

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(successResponse))
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var query = new ValidateCaptchaQuery
        {
            Token = "valid-recaptcha-token",
            RemoteIpAddress = "192.168.1.10",
            CaptchaProvider = "recaptcha",
            SecretKey = "recaptcha-secret"
        };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task HandleAsync_WithInvalidReCaptchaToken_ReturnsFalse()
    {
        // Arrange
        config.CaptchaProvider = "recaptcha";
        handler = new ValidateCaptchaHandler(
            httpClientFactoryMock.Object,
            loggerMock.Object,
            Options.Create(config));

        var failureResponse = new
        {
            success = false,
            error_codes = new[] { "invalid-input-secret" }
        };

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(failureResponse))
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var query = new ValidateCaptchaQuery
        {
            Token = "invalid-token",
            RemoteIpAddress = "192.168.1.20",
            CaptchaProvider = "recaptcha",
            SecretKey = "secret"
        };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region Unknown Provider Tests

    [TestMethod]
    public async Task HandleAsync_WithUnknownProvider_ReturnsFalse()
    {
        // Arrange
        config.CaptchaProvider = "unknown-provider";
        handler = new ValidateCaptchaHandler(
            httpClientFactoryMock.Object,
            loggerMock.Object,
            Options.Create(config));

        var query = new ValidateCaptchaQuery
        {
            Token = "token",
            RemoteIpAddress = "192.168.1.50"
        };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region Exception Handling Tests

    [TestMethod]
    public async Task HandleAsync_WhenHttpClientThrows_ReturnsFalse()
    {
        // Arrange
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(httpMessageHandler.Object);
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var query = new ValidateCaptchaQuery
        {
            Token = "token",
            RemoteIpAddress = "192.168.1.99",
            CaptchaProvider = "turnstile",
            SecretKey = "secret"
        };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion
}
