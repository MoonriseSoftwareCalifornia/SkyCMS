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