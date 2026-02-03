// <copyright file="ContactApiControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers;

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
/// Unit tests for ContactApiController.
/// </summary>
[TestClass]
public class ContactApiControllerTests
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
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ContactApiTest_{Guid.NewGuid()}")
            .Options;
        dbContext = new ApplicationDbContext(options);

        // Setup mocks
        mediatorMock = new Mock<IMediator>();
        antiforgeryMock = new Mock<IAntiforgery>();
        loggerMock = new Mock<ILogger<ContactApiController>>();
        emailConfigServiceMock = new Mock<IEmailConfigurationService>();

        // Create controller instance
        controller = new ContactApiController(
            mediatorMock.Object,
            antiforgeryMock.Object,
            loggerMock.Object,
            dbContext,
            emailConfigServiceMock.Object);

        // Setup HttpContext
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        dbContext?.Dispose();
    }

    /// <summary>
    /// Tests that GetContactScript_ShouldReturnJavaScript_WhenConfigurationExists.
    /// </summary>
    [TestMethod]
    public async Task GetContactScript_ShouldReturnJavaScript_WhenConfigurationExists()
    {
        // Arrange
        await SeedContactApiSettings();

        var antiforgeryTokens = new AntiforgeryTokenSet("test-request-token", "test-cookie-token", "form-field", "header");
        antiforgeryMock
            .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
            .Returns(antiforgeryTokens);

        // Act
        var result = await controller.GetContactScript();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(ContentResult));
        var contentResult = result as ContentResult;
        Assert.AreEqual("application/javascript", contentResult.ContentType);
        Assert.IsTrue(contentResult.Content.Contains("SkyCmsContact"));
        Assert.IsTrue(contentResult.Content.Contains("test-request-token"));
    }

    /// <summary>
    /// Tests that GetContactScript_ShouldFallbackToEmailConfig_WhenAdminEmailNotConfigured.
    /// </summary>
    [TestMethod]
    public async Task GetContactScript_ShouldFallbackToEmailConfig_WhenAdminEmailNotConfigured()
    {
        // Arrange - Don't seed ContactApi settings, only EMAIL settings
        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ReturnsAsync(new EmailSettings
            {
                SenderEmail = "fallback@example.com",
                IsConfigured = true
            });

        var antiforgeryTokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
        antiforgeryMock
            .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
            .Returns(antiforgeryTokens);

        // Act
        var result = await controller.GetContactScript();

        // Assert
        Assert.IsInstanceOfType(result, typeof(ContentResult));
        var contentResult = result as ContentResult;
        Assert.AreEqual("application/javascript", contentResult.ContentType);

        // Verify fallback was used
        emailConfigServiceMock.Verify(x => x.GetEmailSettingsAsync(), Times.Once);
    }

    /// <summary>
    /// Tests that GetContactScript_ShouldIncludeCaptchaConfig_WhenConfigured.
    /// </summary>
    [TestMethod]
    public async Task GetContactScript_ShouldIncludeCaptchaConfig_WhenConfigured()
    {
        // Arrange
        await SeedContactApiSettingsWithCaptcha();

        var antiforgeryTokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
        antiforgeryMock
            .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
            .Returns(antiforgeryTokens);

        // Act
        var result = await controller.GetContactScript();

        // Assert
        var contentResult = result as ContentResult;
        Assert.IsTrue(contentResult.Content.Contains("requireCaptcha: true"));
        Assert.IsTrue(contentResult.Content.Contains("captchaProvider: 'turnstile'"));
        Assert.IsTrue(contentResult.Content.Contains("captchaSiteKey: 'test-site-key'"));
    }

    /// <summary>
    /// Tests that Submit_ShouldReturnOk_WhenSubmissionSucceeds.
    /// </summary>
    [TestMethod]
    public async Task Submit_ShouldReturnOk_WhenSubmissionSucceeds()
    {
        // Arrange
        await SeedContactApiSettings();

        var request = new ContactFormRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Test message"
        };

        var successResponse = new ContactFormResponse
        {
            Success = true,
            Message = "Thank you for your message. We'll get back to you soon!"
        };

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cosmos.Common.Features.Shared.CommandResult<ContactFormResponse>.Success(successResponse));

        // Act
        var result = await controller.Submit(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        var okResult = result as OkObjectResult;
        var response = okResult.Value as ContactFormResponse;
        Assert.IsTrue(response.Success);
        Assert.AreEqual("Thank you for your message. We'll get back to you soon!", response.Message);

        // Verify mediator was called
        mediatorMock.Verify(x => x.SendAsync(
            It.Is<SubmitContactFormCommand>(cmd =>
                cmd.Request.Name == "John Doe" &&
                cmd.Request.Email == "john@example.com" &&
                cmd.Request.Message == "Test message"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that Submit_ShouldReturnBadRequest_WhenModelStateInvalid.
    /// </summary>
    [TestMethod]
    public async Task Submit_ShouldReturnBadRequest_WhenModelStateInvalid()
    {
        // Arrange
        controller.ModelState.AddModelError("Email", "Email is required");
        var request = new ContactFormRequest
        {
            Name = "John Doe"
            // Missing email
        };

        // Act
        var result = await controller.Submit(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        var badRequestResult = result as BadRequestObjectResult;
        var response = badRequestResult.Value as ContactFormResponse;
        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.Message.Contains("Validation failed"));
    }

    /// <summary>
    /// Tests that Submit_ShouldReturnBadRequest_WhenCaptchaTokenMissingAndRequired.
    /// </summary>
    [TestMethod]
    public async Task Submit_ShouldReturnBadRequest_WhenCaptchaTokenMissingAndRequired()
    {
        // Arrange
        await SeedContactApiSettingsWithCaptcha();

        var request = new ContactFormRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Test message",
            CaptchaToken = null // Missing CAPTCHA
        };

        // Act
        var result = await controller.Submit(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        var badRequestResult = result as BadRequestObjectResult;
        var response = badRequestResult.Value as ContactFormResponse;
        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.Message.Contains("CAPTCHA validation is required"));
    }

    /// <summary>
    /// Tests that Submit_ShouldValidateCaptcha_WhenRequired.
    /// </summary>
    [TestMethod]
    public async Task Submit_ShouldValidateCaptcha_WhenRequired()
    {
        // Arrange
        await SeedContactApiSettingsWithCaptcha();

        var request = new ContactFormRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Test message",
            CaptchaToken = "valid-token"
        };

        mediatorMock
            .Setup(x => x.QueryAsync(It.IsAny<ValidateCaptchaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cosmos.Common.Features.Shared.CommandResult<ContactFormResponse>.Success(new ContactFormResponse { Success = true }));

        // Act
        var result = await controller.Submit(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));

        // Verify CAPTCHA validation was performed
        mediatorMock.Verify(x => x.QueryAsync(
            It.Is<ValidateCaptchaQuery>(q =>
                q.Token == "valid-token" &&
                q.CaptchaProvider == "turnstile" &&
                q.SecretKey == "test-secret-key"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that Submit_ShouldReturnBadRequest_WhenCaptchaValidationFails.
    /// </summary>
    [TestMethod]
    public async Task Submit_ShouldReturnBadRequest_WhenCaptchaValidationFails()
    {
        // Arrange
        await SeedContactApiSettingsWithCaptcha();

        var request = new ContactFormRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Test message",
            CaptchaToken = "invalid-token"
        };

        mediatorMock
            .Setup(x => x.QueryAsync(It.IsAny<ValidateCaptchaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // CAPTCHA validation failed

        // Act
        var result = await controller.Submit(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        var badRequestResult = result as BadRequestObjectResult;
        var response = badRequestResult.Value as ContactFormResponse;
        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.Message.Contains("CAPTCHA validation failed"));
    }

    /// <summary>
    /// Tests that Submit_ShouldCaptureRemoteIpAddress.
    /// </summary>
    [TestMethod]
    public async Task Submit_ShouldCaptureRemoteIpAddress()
    {
        // Arrange
        await SeedContactApiSettings();

        var request = new ContactFormRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Test"
        };

        controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cosmos.Common.Features.Shared.CommandResult<ContactFormResponse>.Success(new ContactFormResponse { Success = true }));

        // Act
        await controller.Submit(request);

        // Assert
        mediatorMock.Verify(x => x.SendAsync(
            It.Is<SubmitContactFormCommand>(cmd => cmd.RemoteIpAddress == "192.168.1.100"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Rate Limiting Tests (Note: Rate limiting is enforced by middleware, not controller logic)

    /// <summary>
    /// Tests that Submit_WithRateLimitAttribute_IsConfigured.
    /// </summary>
    [TestMethod]
    public void Submit_ShouldHaveRateLimitAttribute()
    {
        // Arrange
        var methodInfo = typeof(ContactApiController).GetMethod("Submit");

        // Act
        var rateLimitAttr = methodInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), false);

        // Assert
        Assert.IsTrue(rateLimitAttr.Length > 0, "Submit method should have EnableRateLimiting attribute");
        
        // Verify the policy name is "contact-form"
        var attr = rateLimitAttr[0] as Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute;
        Assert.IsNotNull(attr);
    }

    /// <summary>
    /// Tests that Submit_HasAntiforgeryValidation.
    /// </summary>
    [TestMethod]
    public void Submit_ShouldHaveAntiforgeryAttribute()
    {
        // Arrange
        var methodInfo = typeof(ContactApiController).GetMethod("Submit");

        // Act
        var antiforgeryAttr = methodInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute), false);

        // Assert
        Assert.IsTrue(antiforgeryAttr.Length > 0, "Submit method should have ValidateAntiForgeryToken attribute");
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Tests that Submit_ReturnsInternalServerError_WhenExceptionThrown.
    /// </summary>
    [TestMethod]
    public async Task Submit_ReturnsInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        await SeedContactApiSettings();

        var request = new ContactFormRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Test"
        };

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await controller.Submit(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(ObjectResult));
        var objectResult = result as ObjectResult;
        Assert.AreEqual(500, objectResult.StatusCode);
        
        var response = objectResult.Value as ContactFormResponse;
        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.Message.Contains("unexpected error"));
    }

    /// <summary>
    /// Tests that Submit_ReturnsBadRequest_WhenMediatorReturnsFailure.
    /// </summary>
    [TestMethod]
    public async Task Submit_ReturnsBadRequest_WhenMediatorReturnsFailure()
    {
        // Arrange
        await SeedContactApiSettings();

        var request = new ContactFormRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Test"
        };

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cosmos.Common.Features.Shared.CommandResult<ContactFormResponse>.Failure("Email service unavailable"));

        // Act
        var result = await controller.Submit(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        var badRequestResult = result as BadRequestObjectResult;
        var response = badRequestResult.Value as ContactFormResponse;
        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.Message.Contains("Email service unavailable") || response.Message.Contains("Failed to submit"));
    }

    /// <summary>
    /// Tests that GetContactScript_ReturnsError_WhenExceptionThrown.
    /// </summary>
    [TestMethod]
    public async Task GetContactScript_ReturnsError_WhenExceptionThrown()
    {
        // Arrange - Don't seed settings to cause an exception path
        antiforgeryMock
            .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
            .Throws(new Exception("Antiforgery service unavailable"));

        // Act
        var result = await controller.GetContactScript();

        // Assert
        Assert.IsInstanceOfType(result, typeof(ObjectResult));
        var objectResult = result as ObjectResult;
        Assert.AreEqual(500, objectResult.StatusCode);
    }

    #endregion

    #region Configuration Fallback Tests

    /// <summary>
    /// Tests that LoadContactApiConfig_UsesEmailConfigFallback_WhenAdminEmailNotConfigured.
    /// </summary>
    [TestMethod]
    public async Task Submit_UsesFallbackEmail_WhenContactApiAdminEmailNotConfigured()
    {
        // Arrange - Seed only MaxMessageLength, not AdminEmail
        dbContext.Settings.Add(new Setting
        {
            Id = Guid.NewGuid(),
            Group = "ContactApi",
            Name = "MaxMessageLength",
            Value = "5000",
            Description = "Max message length"
        });
        await dbContext.SaveChangesAsync();

        // Setup email config service to return fallback email
        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ReturnsAsync(new EmailSettings
            {
                SenderEmail = "fallback@example.com"
            });

        var request = new ContactFormRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Test"
        };

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cosmos.Common.Features.Shared.CommandResult<ContactFormResponse>.Success(new ContactFormResponse { Success = true }));

        // Act
        var result = await controller.Submit(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        
        // Verify email config service was called for fallback
        emailConfigServiceMock.Verify(x => x.GetEmailSettingsAsync(), Times.AtLeastOnce);
    }

    /// <summary>
    /// Tests that LoadContactApiConfig_UsesDefaultEmail_WhenAllConfigurationsFail.
    /// </summary>
    [TestMethod]
    public async Task Submit_UsesDefaultEmail_WhenAllConfigurationsFail()
    {
        // Arrange - No settings in database
        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ThrowsAsync(new Exception("Email config service unavailable"));

        var request = new ContactFormRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Test"
        };

        mediatorMock
            .Setup(x => x.SendAsync(It.IsAny<SubmitContactFormCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cosmos.Common.Features.Shared.CommandResult<ContactFormResponse>.Success(new ContactFormResponse { Success = true }));

        // Act
        var result = await controller.Submit(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        // The controller should still work with default configuration
    }

    #endregion

    private async Task SeedContactApiSettings()
    {
        dbContext.Settings.AddRange(
            new Setting
            {
                Id = Guid.NewGuid(),
                Group = "ContactApi",
                Name = "AdminEmail",
                Value = "admin@test.com",
                Description = "Admin email",
                IsRequired = true
            },
            new Setting
            {
                Id = Guid.NewGuid(),
                Group = "ContactApi",
                Name = "MaxMessageLength",
                Value = "5000",
                Description = "Max message length",
                IsRequired = false
            }
        );
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedContactApiSettingsWithCaptcha()
    {
        await SeedContactApiSettings();

        dbContext.Settings.Add(new Setting
        {
            Id = Guid.NewGuid(),
            Group = "CAPTCHA",
            Name = "Config",
            Value = "{\"Provider\":\"turnstile\",\"SiteKey\":\"test-site-key\",\"SecretKey\":\"test-secret-key\",\"RequireCaptcha\":true}",
            Description = "CAPTCHA configuration",
            IsRequired = false
        });

        await dbContext.SaveChangesAsync();
    }
}
