// <copyright file="SubmitContactFormHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.ContactForm;

using Cosmos.Common.Data;
using Cosmos.Common.Models;
using Cosmos.Common.Services.Email;
using Cosmos.EmailServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Cms.Api.Shared.Features.ContactForm.Submit;
using Sky.Cms.Api.Shared.Models;

/// <summary>
/// Unit tests for SubmitContactFormHandler.
/// </summary>
[TestClass]
public class SubmitContactFormHandlerTests
{
    private Mock<ICosmosEmailSender> emailSenderMock;
    private ApplicationDbContext dbContext;
    private Mock<ILogger<SubmitContactFormHandler>> loggerMock;
    private Mock<IEmailConfigurationService> emailConfigServiceMock;
    private SubmitContactFormHandler handler;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"SubmitContactFormTest_{Guid.NewGuid()}")
            .Options;
        dbContext = new ApplicationDbContext(options);

        emailSenderMock = new Mock<ICosmosEmailSender>();
        loggerMock = new Mock<ILogger<SubmitContactFormHandler>>();
        emailConfigServiceMock = new Mock<IEmailConfigurationService>();

        handler = new SubmitContactFormHandler(
            emailSenderMock.Object,
            dbContext,
            loggerMock.Object,
            emailConfigServiceMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        dbContext?.Dispose();
    }

    /// <summary>
    /// Tests that HandleAsync_ShouldSendEmail_AndReturnSuccess.
    /// </summary>
    [TestMethod]
    public async Task HandleAsync_ShouldSendEmail_AndReturnSuccess()
    {
        // Arrange
        await SeedSettings();

        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ReturnsAsync(new EmailSettings
            {
                SenderEmail = "admin@test.com",
                Provider = "SendGrid",
                IsConfigured = true
            });

        var command = new SubmitContactFormCommand
        {
            Request = new ContactFormRequest
            {
                Name = "John Doe",
                Email = "john@example.com",
                Message = "This is a test message"
            },
            RemoteIpAddress = "192.168.1.1"
        };

        emailSenderMock
            .SetupGet(x => x.SendResult)
            .Returns(new SendResult
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Message = "Email sent successfully"
            });

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Data);
        Assert.IsTrue(result.Data.Success);
        Assert.AreEqual("Thank you for your message. We'll get back to you soon!", result.Data.Message);

        // Verify email was sent
        emailSenderMock.Verify(x => x.SendEmailAsync(
            It.Is<string>(email => email == "admin@test.com"),
            It.Is<string>(subject => subject.Contains("John Doe")),
            It.IsAny<string>(), // text body
            It.IsAny<string>(), // HTML body
            It.IsAny<string>()), // from email
            Times.Once);
    }

    /// <summary>
    /// Tests that HandleAsync_ShouldIncludeRemoteIpInEmail.
    /// </summary>
    [TestMethod]
    public async Task HandleAsync_ShouldIncludeRemoteIpInEmail()
    {
        // Arrange
        await SeedSettings();

        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ReturnsAsync(new EmailSettings
            {
                SenderEmail = "admin@test.com",
                Provider = "SendGrid",
                IsConfigured = true
            });

        var command = new SubmitContactFormCommand
        {
            Request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test"
            },
            RemoteIpAddress = "203.0.113.42"
        };

        string capturedTextBody = string.Empty;
        string capturedHtmlBody = string.Empty;

        emailSenderMock
            .Setup(x => x.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<string, string, string, string, string>((to, subj, text, html, from) =>
            {
                capturedTextBody = text;
                capturedHtmlBody = html;
            })
            .Returns(Task.CompletedTask);

        emailSenderMock
            .SetupGet(x => x.SendResult)
            .Returns(new SendResult { StatusCode = System.Net.HttpStatusCode.OK });

        // Act
        await handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(capturedTextBody.Contains("203.0.113.42"));
        Assert.IsTrue(capturedHtmlBody.Contains("203.0.113.42"));
    }

    /// <summary>
    /// Tests that HandleAsync_ShouldUseFallbackEmail_WhenAdminEmailNotConfigured.
    /// </summary>
    [TestMethod]
    public async Task HandleAsync_ShouldUseFallbackEmail_WhenAdminEmailNotConfigured()
    {
        // Arrange - Don't seed ContactApi settings
        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ReturnsAsync(new EmailSettings
            {
                SenderEmail = "fallback@example.com",
                Provider = "SendGrid",
                IsConfigured = true
            });

        emailSenderMock
            .SetupGet(x => x.SendResult)
            .Returns(new SendResult { StatusCode = System.Net.HttpStatusCode.OK });

        var command = new SubmitContactFormCommand
        {
            Request = new ContactFormRequest
            {
                Name = "Test",
                Email = "test@example.com",
                Message = "Test"
            },
            RemoteIpAddress = "192.168.1.1"
        };

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);

        emailSenderMock.Verify(x => x.SendEmailAsync(
            It.Is<string>(email => email == "fallback@example.com"),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);

        emailConfigServiceMock.Verify(x => x.GetEmailSettingsAsync(), Times.Once);
    }

    /// <summary>
    /// Tests that HandleAsync_ShouldReturnFailure_WhenEmailSendFails.
    /// </summary>
    [TestMethod]
    public async Task HandleAsync_ShouldReturnFailure_WhenEmailSendFails()
    {
        // Arrange
        await SeedSettings();

        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ReturnsAsync(new EmailSettings
            {
                SenderEmail = "admin@test.com",
                Provider = "SendGrid",
                IsConfigured = true
            });

        var command = new SubmitContactFormCommand
        {
            Request = new ContactFormRequest
            {
                Name = "Test",
                Email = "test@example.com",
                Message = "Test"
            },
            RemoteIpAddress = "192.168.1.1"
        };

        emailSenderMock
            .SetupGet(x => x.SendResult)
            .Returns(new SendResult
            {
                StatusCode = System.Net.HttpStatusCode.InternalServerError,
                Message = "SMTP server error"
            });

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.ErrorMessage.Contains("Failed to send your message"));
    }

    /// <summary>
    /// Tests that HandleAsync_ShouldEscapeHtmlInMessage.
    /// </summary>
    [TestMethod]
    public async Task HandleAsync_ShouldEscapeHtmlInMessage()
    {
        // Arrange
        await SeedSettings();

        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ReturnsAsync(new EmailSettings
            {
                SenderEmail = "admin@test.com",
                Provider = "SendGrid",
                IsConfigured = true
            });

        var command = new SubmitContactFormCommand
        {
            Request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "<script>alert('xss')</script>Test message"
            },
            RemoteIpAddress = "192.168.1.1"
        };

        string capturedHtmlBody = string.Empty;

        emailSenderMock
            .Setup(x => x.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<string, string, string, string, string>((to, subj, text, html, from) =>
            {
                capturedHtmlBody = html;
            })
            .Returns(Task.CompletedTask);

        emailSenderMock
            .SetupGet(x => x.SendResult)
            .Returns(new SendResult { StatusCode = System.Net.HttpStatusCode.OK });

        // Act
        await handler.HandleAsync(command);

        // Assert
        // HTML should be escaped in the email body
        Assert.IsFalse(capturedHtmlBody.Contains("<script>"));
        Assert.IsTrue(capturedHtmlBody.Contains("&lt;script&gt;"));
    }

    /// <summary>
    /// Tests that HandleAsync_ShouldFormatEmailProperly.
    /// </summary>
    [TestMethod]
    public async Task HandleAsync_ShouldFormatEmailProperly()
    {
        // Arrange
        await SeedSettings();

        emailConfigServiceMock
            .Setup(x => x.GetEmailSettingsAsync())
            .ReturnsAsync(new EmailSettings
            {
                SenderEmail = "admin@test.com",
                Provider = "SendGrid",
                IsConfigured = true
            });

        var command = new SubmitContactFormCommand
        {
            Request = new ContactFormRequest
            {
                Name = "Jane Smith",
                Email = "jane@example.com",
                Message = "I need help with my account"
            },
            RemoteIpAddress = "192.168.1.1"
        };

        string capturedSubject = string.Empty;
        string capturedTextBody = string.Empty;

        emailSenderMock
            .Setup(x => x.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<string, string, string, string, string>((to, subj, text, html, from) =>
            {
                capturedSubject = subj;
                capturedTextBody = text;
            })
            .Returns(Task.CompletedTask);

        emailSenderMock
            .SetupGet(x => x.SendResult)
            .Returns(new SendResult { StatusCode = System.Net.HttpStatusCode.OK });

        // Act
        await handler.HandleAsync(command);

        // Assert
        Assert.AreEqual("Contact Form Submission from Jane Smith", capturedSubject);
        Assert.IsTrue(capturedTextBody.Contains("Jane Smith"));
        Assert.IsTrue(capturedTextBody.Contains("jane@example.com"));
        Assert.IsTrue(capturedTextBody.Contains("I need help with my account"));
    }

    private async Task SeedSettings()
    {
        dbContext.Settings.Add(new Setting
        {
            Id = Guid.NewGuid(),
            Group = "ContactApi",
            Name = "AdminEmail",
            Value = "admin@test.com",
            Description = "Admin email",
            IsRequired = true
        });

        await dbContext.SaveChangesAsync();
    }
}
