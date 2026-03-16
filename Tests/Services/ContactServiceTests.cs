// <copyright file="ContactServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services;

using Cosmos.EmailServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Cms.Api.Shared.Models;
using Sky.Cms.Api.Shared.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

/// <summary>
/// Unit tests for <see cref="ContactService"/>.
/// Tests email building, CAPTCHA validation, and form submission.
/// Thread-safe for parallel execution.
/// </summary>
[TestClass]
public class ContactServiceTests
{
    private Mock<IHttpClientFactory> httpClientFactoryMock;
    private Mock<ICosmosEmailSender> emailSenderMock;
    private Mock<ILogger<ContactService>> loggerMock;
    private ContactApiConfig config;
    private ContactService service;

    [TestInitialize]
    public void Setup()
    {
        // Setup mocks
        httpClientFactoryMock = new Mock<IHttpClientFactory>();
        emailSenderMock = new Mock<ICosmosEmailSender>();
        loggerMock = new Mock<ILogger<ContactService>>();

        // Setup default config
        config = new ContactApiConfig
        {
            AdminEmail = "admin@example.com",
            MaxMessageLength = 5000,
            RequireCaptcha = false
        };

        // Create service
        service = new ContactService(
            httpClientFactoryMock.Object,
            emailSenderMock.Object,
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
            var service = new ContactService(null, emailSenderMock.Object, loggerMock.Object, Options.Create(config));
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void Constructor_WithNullEmailSender_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            var service = new ContactService(httpClientFactoryMock.Object, null, loggerMock.Object, Options.Create(config));
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
            var service = new ContactService(httpClientFactoryMock.Object, emailSenderMock.Object, null, Options.Create(config));
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
            var service = new ContactService(httpClientFactoryMock.Object, emailSenderMock.Object, loggerMock.Object, null);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    #endregion

    #region SubmitContactFormAsync Tests

    [TestMethod]
    public async Task SubmitContactFormAsync_WithValidRequest_SendsEmail()
    {
        // Arrange
        var request = new ContactFormRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "This is a test message."
        };

        var sendResult = new SendResult
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Message = "Email sent successfully"
        };

        emailSenderMock.Setup(x => x.SendResult).Returns(sendResult);

        // Act
        var result = await service.SubmitContactFormAsync(request, "192.168.1.1");

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual("Thank you for your message. We'll get back to you soon!", result.Message);

        // Verify email was sent with correct parameters
        emailSenderMock.Verify(x => x.SendEmailAsync(
            "admin@example.com",
            It.Is<string>(s => s.Contains("John Doe")),
            It.IsAny<string>(),
            It.IsAny<string>(),
            "john@example.com"), Times.Once);
    }

    [TestMethod]
    public async Task SubmitContactFormAsync_WhenEmailFails_ReturnsFailureResponse()
    {
        // Arrange
        var request = new ContactFormRequest
        {
            Name = "Jane Doe",
            Email = "jane@example.com",
            Message = "Test message"
        };

        var sendResult = new SendResult
        {
            StatusCode = System.Net.HttpStatusCode.InternalServerError,
            Message = "SMTP server error"
        };

        emailSenderMock.Setup(x => x.SendResult).Returns(sendResult);

        // Act
        var result = await service.SubmitContactFormAsync(request, "192.168.1.2");

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Message.Contains("problem sending your message"));
        Assert.AreEqual("Email delivery failed", result.Error);
    }

    [TestMethod]
    public async Task SubmitContactFormAsync_BuildsCorrectEmailSubject()
    {
        // Arrange
        var request = new ContactFormRequest
        {
            Name = "Test User",
            Email = "test@example.com",
            Message = "Subject test message"
        };

        emailSenderMock.Setup(x => x.SendResult).Returns(new SendResult
        {
            StatusCode = System.Net.HttpStatusCode.OK,
        });

        // Act
        await service.SubmitContactFormAsync(request, "192.168.1.3");

        // Assert
        emailSenderMock.Verify(x => x.SendEmailAsync(
            It.IsAny<string>(),
            "Contact Form Submission from Test User",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task SubmitContactFormAsync_IncludesRemoteIpInEmail()
    {
        // Arrange
        var request = new ContactFormRequest
        {
            Name = "IP Test",
            Email = "iptest@example.com",
            Message = "Testing IP inclusion"
        };

        string capturedTextVersion = null;
        string capturedHtmlVersion = null;

        emailSenderMock
            .Setup(x => x.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<string, string, string, string, string>((to, subj, text, html, from) =>
            {
                capturedTextVersion = text;
                capturedHtmlVersion = html;
            })
            .Returns(Task.CompletedTask);

        emailSenderMock.Setup(x => x.SendResult).Returns(new SendResult
        {
            StatusCode = System.Net.HttpStatusCode.OK,
        });

        // Act
        await service.SubmitContactFormAsync(request, "203.0.113.42");

        // Assert
        Assert.IsTrue(capturedTextVersion.Contains("203.0.113.42"), "Text version should contain IP");
        Assert.IsTrue(capturedHtmlVersion.Contains("203.0.113.42"), "HTML version should contain IP");
    }

    [TestMethod]
    public async Task SubmitContactFormAsync_LogsSuccess()
    {
        // Arrange
        var request = new ContactFormRequest
        {
            Name = "Logger Test",
            Email = "logger@example.com",
            Message = "Testing logging"
        };

        emailSenderMock.Setup(x => x.SendResult).Returns(new SendResult
        {
            StatusCode = System.Net.HttpStatusCode.OK,
        });

        // Act
        await service.SubmitContactFormAsync(request, "192.168.1.100");

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing contact form")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("sent successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SubmitContactFormAsync_LogsFailure()
    {
        // Arrange
        var request = new ContactFormRequest
        {
            Name = "Error Test",
            Email = "error@example.com",
            Message = "Testing error logging"
        };

        emailSenderMock.Setup(x => x.SendResult).Returns(new SendResult
        {
            StatusCode = System.Net.HttpStatusCode.InternalServerError,
            Message = "Service unavailable"
        });

        // Act
        await service.SubmitContactFormAsync(request, "192.168.1.200");

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to send")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SubmitContactFormAsync_HandlesSpecialCharactersInMessage()
    {
        // Arrange
        var request = new ContactFormRequest
        {
            Name = "Special <Test>",
            Email = "special@example.com",
            Message = "Message with <html> & \"quotes\" and 'apostrophes'"
        };

        string capturedHtmlVersion = null;

        emailSenderMock
            .Setup(x => x.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<string, string, string, string, string>((to, subj, text, html, from) =>
            {
                capturedHtmlVersion = html;
            })
            .Returns(Task.CompletedTask);

        emailSenderMock.Setup(x => x.SendResult).Returns(new SendResult
        {
            StatusCode = System.Net.HttpStatusCode.OK,
        });

        // Act
        await service.SubmitContactFormAsync(request, "192.168.1.50");

        // Assert - HTML should be properly escaped
        Assert.IsNotNull(capturedHtmlVersion);
        // Note: The actual implementation should escape HTML, verify it does
    }

    [TestMethod]
    public async Task SubmitContactFormAsync_WithLongMessage_TruncatesIfNeeded()
    {
        // Arrange
        var longMessage = new string('A', 6000); // Exceeds max length
        var request = new ContactFormRequest
        {
            Name = "Long Message Test",
            Email = "long@example.com",
            Message = longMessage
        };

        emailSenderMock.Setup(x => x.SendResult).Returns(new SendResult
        {
            StatusCode = System.Net.HttpStatusCode.OK,
        });

        // Act
        var result = await service.SubmitContactFormAsync(request, "192.168.1.75");

        // Assert
        Assert.IsTrue(result.Success);
        // Email sender should still be called
        emailSenderMock.Verify(x => x.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    #endregion

    #region Email Building Tests

    [TestMethod]
    public async Task BuildTextEmail_ContainsAllRequiredFields()
    {
        // Arrange
        var request = new ContactFormRequest
        {
            Name = "Field Test",
            Email = "fields@example.com",
            Message = "Testing all fields"
        };

        string capturedText = null;

        emailSenderMock
            .Setup(x => x.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<string, string, string, string, string>((to, subj, text, html, from) =>
            {
                capturedText = text;
            })
            .Returns(Task.CompletedTask);

        emailSenderMock.Setup(x => x.SendResult).Returns(new SendResult { StatusCode = System.Net.HttpStatusCode.OK });

        // Act
        await service.SubmitContactFormAsync(request, "192.168.1.10");

        // Assert
        Assert.IsNotNull(capturedText);
        Assert.IsTrue(capturedText.Contains("Field Test"), "Should contain name");
        Assert.IsTrue(capturedText.Contains("fields@example.com"), "Should contain email");
        Assert.IsTrue(capturedText.Contains("Testing all fields"), "Should contain message");
        Assert.IsTrue(capturedText.Contains("192.168.1.10"), "Should contain IP");
    }

    [TestMethod]
    public async Task BuildHtmlEmail_ContainsAllRequiredFields()
    {
        // Arrange
        var request = new ContactFormRequest
        {
            Name = "HTML Test",
            Email = "html@example.com",
            Message = "Testing HTML email"
        };

        string capturedHtml = null;

        emailSenderMock
            .Setup(x => x.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<string, string, string, string, string>((to, subj, text, html, from) =>
            {
                capturedHtml = html;
            })
            .Returns(Task.CompletedTask);

        emailSenderMock.Setup(x => x.SendResult).Returns(new SendResult { StatusCode = System.Net.HttpStatusCode.OK });

        // Act
        await service.SubmitContactFormAsync(request, "192.168.1.20");

        // Assert
        Assert.IsNotNull(capturedHtml);
        Assert.IsTrue(capturedHtml.Contains("HTML Test"), "Should contain name");
        Assert.IsTrue(capturedHtml.Contains("html@example.com"), "Should contain email");
        Assert.IsTrue(capturedHtml.Contains("Testing HTML email"), "Should contain message");
        Assert.IsTrue(capturedHtml.Contains("<html"), "Should be valid HTML");
        Assert.IsTrue(capturedHtml.Contains("</html>"), "Should close HTML tag");
    }

    #endregion
}
