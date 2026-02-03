// <copyright file="ContactApiControllerExtendedTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Controllers;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using Cosmos.Common.Services.Email;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Cms.Api.Shared.Controllers;
using Sky.Cms.Api.Shared.Features.ContactForm.Submit;
using Sky.Cms.Api.Shared.Features.ContactForm.ValidateCaptcha;
using Sky.Cms.Api.Shared.Models;

/// <summary>
/// Extended unit tests for ContactApiController.
/// Test 5: ContactApiController gaps (reCAPTCHA script generation, error handling, rate limiting).
/// Thread-safe for parallel execution.
/// </summary>
[TestClass]
public class ContactApiControllerExtendedTests
{
    private Mock<IMediator> mediatorMock;
    private Mock<IAntiforgery> antiforgeryMock;
    private Mock<ILogger<ContactApiController>> loggerMock;
    private ApplicationDbContext dbContext;
    private Mock<IEmailConfigurationService> emailConfigServiceMock;
    private ContactApiController controller;

    [TestInitialize]
    public void Setup()
    {
        // Setup in-memory database with unique name for parallel execution
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ContactApiExtendedTest_{Guid.NewGuid()}")
            .Options;
        dbContext = new ApplicationDbContext(options);

        // Setup mocks
        mediatorMock = new Mock<IMediator>();
        antiforgeryMock = new Mock<IAntiforgery>();
        loggerMock = new Mock<ILogger<ContactApiController>>();
        emailConfigServiceMock = new Mock<IEmailConfigurationService>();

        // Setup default email config service
        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ReturnsAsync(new EmailSettings
            {
                SenderEmail = "sender@example.com",
                IsConfigured = true
            });

        // Create controller
        controller = new ContactApiController(
            mediatorMock.Object,
            antiforgeryMock.Object,
            loggerMock.Object,
            dbContext,
            emailConfigServiceMock.Object);

        // Setup HttpContext with remote IP
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        dbContext?.Dispose();
    }

    #region reCAPTCHA Script Generation Tests

    [TestMethod]
    public async Task GetContactScript_WithReCaptcha_IncludesReCaptchaConfig()
    {
        // Arrange
        await SeedReCaptchaSettings();

        var tokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
        antiforgeryMock.Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokens);

        // Act
        var result = await controller.GetContactScript();

        // Assert
        var contentResult = result as ContentResult;
        Assert.IsNotNull(contentResult);
        Assert.AreEqual("application/javascript", contentResult.ContentType);
        Assert.IsTrue(contentResult.Content.Contains("requireCaptcha: true"));
        Assert.IsTrue(contentResult.Content.Contains("captchaProvider: 'recaptcha'"));
        Assert.IsTrue(contentResult.Content.Contains("recaptcha-site-key"));
    }

    [TestMethod]
    public async Task GetContactScript_WithoutCaptcha_DoesNotRequireCaptcha()
    {
        // Arrange
        await SeedContactApiSettingsNoCaptcha();

        var tokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
        antiforgeryMock.Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokens);

        // Act
        var result = await controller.GetContactScript();

        // Assert
        var contentResult = result as ContentResult;
        Assert.IsTrue(contentResult.Content.Contains("requireCaptcha: false"));
        Assert.IsFalse(contentResult.Content.Contains("captchaSiteKey"));
    }

    [TestMethod]
    public async Task GetContactScript_IncludesAntiforgeryToken()
    {
        // Arrange
        await SeedContactApiSettingsNoCaptcha();

        var tokens = new AntiforgeryTokenSet("my-csrf-token-12345", "cookie", "field", "header");
        antiforgeryMock.Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokens);

        // Act
        var result = await controller.GetContactScript();

        // Assert
        var contentResult = result as ContentResult;
        Assert.IsTrue(contentResult.Content.Contains("my-csrf-token-12345"));
        Assert.IsTrue(contentResult.Content.Contains("antiforgeryToken"));
    }

    [TestMethod]
    public async Task GetContactScript_IncludesConfigurationValues()
    {
        // Arrange
        await SeedContactApiSettings("test@example.com", 10000);

        var tokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
        antiforgeryMock.Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokens);

        // Act
        var result = await controller.GetContactScript();

        // Assert
        var contentResult = result as ContentResult;
        Assert.IsTrue(contentResult.Content.Contains("maxMessageLength: 10000"));
        Assert.IsTrue(contentResult.Content.Contains("submitEndpoint: '/_api/contact/submit'"));
    }

    [TestMethod]
    public async Task GetContactScript_GeneratesValidJavaScript()
    {
        // Arrange
        await SeedContactApiSettingsNoCaptcha();

        var tokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
        antiforgeryMock.Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokens);

        // Act
        var result = await controller.GetContactScript();

        // Assert
        var contentResult = result as ContentResult;
        Assert.IsTrue(contentResult.Content.Contains("SkyCmsContact"));
        Assert.IsTrue(contentResult.Content.Contains("function(window)"));
        Assert.IsTrue(contentResult.Content.Contains("init:"));
        Assert.IsTrue(contentResult.Content.Contains("handleSubmit:"));
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task GetContactScript_WhenExceptionThrown_Returns500WithErrorComment()
    {
        // Arrange - force exception by making antiforgery throw
        antiforgeryMock
            .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
            .Throws(new Exception("Antiforgery error"));

        // Act
        var result = await controller.GetContactScript();

        // Assert
        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(500, objectResult.StatusCode);
    }

    [TestMethod]
    public async Task Submit_WhenExceptionThrown_Returns500()
    {
        // Arrange
        var request = new ContactFormRequest
        {
            Name = "Test",
            Email = "test@example.com",
            Message = "Test message"
        };

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await controller.Submit(request);

        // Assert
        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(500, objectResult.StatusCode);

        var response = objectResult.Value as ContactFormResponse;
        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.Message.Contains("unexpected error"));
    }

    [TestMethod]
    public async Task Submit_WhenMediatorReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        await SeedContactApiSettingsNoCaptcha();

        var request = new ContactFormRequest
        {
            Name = "Test",
            Email = "test@example.com",
            Message = "Test message"
        };

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult<ContactFormResponse>.Failure("Email service unavailable"));

        // Act
        var result = await controller.Submit(request);

        // Assert
        var badRequestResult = result as BadRequestObjectResult;
        Assert.IsNotNull(badRequestResult);

        var response = badRequestResult.Value as ContactFormResponse;
        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.Error.Contains("Email service unavailable"));
    }

    #endregion

    #region CAPTCHA Validation Tests

    [TestMethod]
    public async Task Submit_WithCaptchaRequired_AndMissingToken_ReturnsBadRequest()
    {
        // Arrange
        await SeedCaptchaSettings("turnstile", "site-key", "secret-key");

        var request = new ContactFormRequest
        {
            Name = "Test",
            Email = "test@example.com",
            Message = "Test message",
            CaptchaToken = null // Missing token
        };

        // Act
        var result = await controller.Submit(request);

        // Assert
        var badRequestResult = result as BadRequestObjectResult;
        Assert.IsNotNull(badRequestResult);

        var response = badRequestResult.Value as ContactFormResponse;
        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.Message.Contains("CAPTCHA validation is required"));
        Assert.AreEqual("Missing CAPTCHA token", response.Error);
    }

    [TestMethod]
    public async Task Submit_WithCaptchaRequired_AndInvalidToken_ReturnsBadRequest()
    {
        // Arrange
        await SeedCaptchaSettings("turnstile", "site-key", "secret-key");

        var request = new ContactFormRequest
        {
            Name = "Test",
            Email = "test@example.com",
            Message = "Test message",
            CaptchaToken = "invalid-token"
        };

        // Mediator returns false for CAPTCHA validation
        mediatorMock
            .Setup(x => x.QueryAsync(It.IsAny<ValidateCaptchaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await controller.Submit(request);

        // Assert
        var badRequestResult = result as BadRequestObjectResult;
        Assert.IsNotNull(badRequestResult);

        var response = badRequestResult.Value as ContactFormResponse;
        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.Message.Contains("CAPTCHA validation failed"));
        Assert.AreEqual("Invalid CAPTCHA", response.Error);
    }

    [TestMethod]
    public async Task Submit_WithCaptchaRequired_AndValidToken_Succeeds()
    {
        // Arrange
        await SeedCaptchaSettings("turnstile", "site-key", "secret-key");

        var request = new ContactFormRequest
        {
            Name = "Test",
            Email = "test@example.com",
            Message = "Test message",
            CaptchaToken = "valid-token"
        };

        // Mediator returns true for CAPTCHA validation
        mediatorMock
            .Setup(x => x.QueryAsync(It.IsAny<ValidateCaptchaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var successResponse = new ContactFormResponse
        {
            Success = true,
            Message = "Thank you!"
        };

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult<ContactFormResponse>.Success(successResponse));

        // Act
        var result = await controller.Submit(request);

        // Assert
        var okResult = result as OkObjectResult;
        Assert.IsNotNull(okResult);

        var response = okResult.Value as ContactFormResponse;
        Assert.IsTrue(response.Success);
    }

    #endregion

    #region Configuration Loading Tests

    [TestMethod]
    public async Task LoadContactApiConfig_WithNoSettings_UsesEmailConfigFallback()
    {
        // Arrange - no ContactApi settings in DB
        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ReturnsAsync(new EmailSettings
            {
                SenderEmail = "fallback@example.com",
                IsConfigured = true
            });

        var tokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
        antiforgeryMock.Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokens);

        // Act
        var result = await controller.GetContactScript();

        // Assert
        var contentResult = result as ContentResult;
        Assert.IsNotNull(contentResult);

        // Verify fallback was used
        emailConfigServiceMock.Verify(x => x.GetEmailSettingsAsync(), Times.Once);
    }

    [TestMethod]
    public async Task LoadContactApiConfig_WhenDatabaseFails_UsesAbsoluteDefaults()
    {
        // Arrange - Dispose dbContext to force error
        dbContext.Dispose();

        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ThrowsAsync(new Exception("Email config also failed"));

        var tokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
        antiforgeryMock.Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokens);

        // Act
        var result = await controller.GetContactScript();

        // Assert - Should still return JavaScript with defaults
        var contentResult = result as ContentResult;
        Assert.IsNotNull(contentResult);
        Assert.AreEqual("application/javascript", contentResult.ContentType);
        
        // Verify the script contains default configuration values
        Assert.IsTrue(contentResult.Content.Contains("maxMessageLength: 5000"));
        Assert.IsTrue(contentResult.Content.Contains("requireCaptcha: false"));
        Assert.IsTrue(contentResult.Content.Contains("SkyCmsContact"));
    }

    #endregion

    #region Logging Tests

    [TestMethod]
    public async Task Submit_LogsSuccessfulSubmission()
    {
        // Arrange
        await SeedContactApiSettingsNoCaptcha();

        var request = new ContactFormRequest
        {
            Name = "Test",
            Email = "test@example.com",
            Message = "Test message"
        };

        var successResponse = new ContactFormResponse { Success = true };

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult<ContactFormResponse>.Success(successResponse));

        // Act
        await controller.Submit(request);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("submitted successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Submit_LogsCaptchaFailure()
    {
        // Arrange
        await SeedCaptchaSettings("turnstile", "key", "secret");

        var request = new ContactFormRequest
        {
            Name = "Test",
            Email = "test@example.com",
            Message = "Test",
            CaptchaToken = "invalid"
        };

        mediatorMock
            .Setup(x => x.QueryAsync(It.IsAny<ValidateCaptchaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await controller.Submit(request);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("CAPTCHA validation failed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetContactScript_LogsError_OnException()
    {
        // Arrange
        dbContext.Dispose(); // Force exception

        var tokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
        antiforgeryMock.Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokens);

        // Act
        await controller.GetContactScript();

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Helper Methods

    private async Task SeedContactApiSettings(string adminEmail = "admin@example.com", int maxLength = 5000)
    {
        dbContext.Settings.AddRange(
            new Setting
            {
                Id = Guid.NewGuid(),
                Group = "ContactApi",
                Name = "AdminEmail",
                Value = adminEmail,
                Description = "Admin email",
                IsRequired = true
            },
            new Setting
            {
                Id = Guid.NewGuid(),
                Group = "ContactApi",
                Name = "MaxMessageLength",
                Value = maxLength.ToString(),
                Description = "Max message length",
                IsRequired = false
            });
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedContactApiSettingsNoCaptcha()
    {
        await SeedContactApiSettings();
        // No CAPTCHA settings
    }

    private async Task SeedReCaptchaSettings()
    {
        await SeedContactApiSettings();

        var captchaConfig = System.Text.Json.JsonSerializer.Serialize(new
        {
            RequireCaptcha = true,
            Provider = "recaptcha",
            SiteKey = "recaptcha-site-key",
            SecretKey = "recaptcha-secret-key"
        });

        dbContext.Settings.Add(new Setting
        {
            Id = Guid.NewGuid(),
            Group = "CAPTCHA",
            Name = "Config",
            Value = captchaConfig,
            Description = "CAPTCHA configuration",
            IsRequired = false
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedCaptchaSettings(string provider, string siteKey, string secretKey)
    {
        await SeedContactApiSettings();

        var captchaConfig = System.Text.Json.JsonSerializer.Serialize(new
        {
            RequireCaptcha = true,
            Provider = provider,
            SiteKey = siteKey,
            SecretKey = secretKey
        });

        dbContext.Settings.Add(new Setting
        {
            Id = Guid.NewGuid(),
            Group = "CAPTCHA",
            Name = "Config",
            Value = captchaConfig,
            Description = "CAPTCHA configuration",
            IsRequired = false
        });
        await dbContext.SaveChangesAsync();
    }

    #endregion
}
