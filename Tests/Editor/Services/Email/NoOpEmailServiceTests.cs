// <copyright file="NoOpEmailServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Editor.Services.Email;

using Microsoft.Extensions.Logging;
using Moq;
using Sky.Editor.Services.Email;

/// <summary>
/// Tests for <see cref="NoOpEmailService"/>.
/// </summary>
[TestClass]
public class NoOpEmailServiceTests
{
    private Mock<ILogger<NoOpEmailService>> mockLogger = null!;
    private NoOpEmailService service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        mockLogger = new Mock<ILogger<NoOpEmailService>>();
        service = new NoOpEmailService(mockLogger.Object);
    }

    #region SendEmailAsync(string to, string subject, string htmlMessage, string textMessage) Tests

    [TestMethod]
    public async Task SendEmailAsync_ThreeParametersWithoutFromAddress_ReturnsTrue()
    {
        // Act
        var result = await service.SendEmailAsync("test@example.com", "Test Subject", "<p>HTML content</p>");

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SendEmailAsync_ThreeParametersWithoutFromAddress_LogsWarning()
    {
        // Act
        await service.SendEmailAsync("test@example.com", "Test Subject", "<p>HTML content</p>");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Email service not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendEmailAsync_ThreeParametersWithoutFromAddress_LogsEmailTo()
    {
        // Act
        await service.SendEmailAsync("recipient@example.com", "Subject", "<p>HTML</p>");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("recipient@example.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendEmailAsync_ThreeParametersWithoutFromAddress_LogsSubject()
    {
        // Act
        await service.SendEmailAsync("test@example.com", "Test Subject Line", "<p>HTML</p>");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Test Subject Line")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendEmailAsync_ThreeParametersWithNullTextMessage_ReturnsTrue()
    {
        // Act
        var result = await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>", null);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SendEmailAsync_ThreeParametersWithTextMessage_ReturnsTrue()
    {
        // Act
        var result = await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>", "Plain text");

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SendEmailAsync_ThreeParametersWithTextMessage_LogsWarning()
    {
        // Act
        await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>", "Plain text");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    #endregion

    #region SendEmailAsync(string from, string to, string subject, string htmlMessage, string textMessage) Tests

    [TestMethod]
    public async Task SendEmailAsync_FiveParametersWithFromAddress_ReturnsTrue()
    {
        // Act
        var result = await service.SendEmailAsync("sender@example.com", "recipient@example.com", "Subject", "<p>HTML</p>", "Text");

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SendEmailAsync_FiveParametersWithFromAddress_LogsWarning()
    {
        // Act
        await service.SendEmailAsync("sender@example.com", "recipient@example.com", "Subject", "<p>HTML</p>", "Text");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Email service not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendEmailAsync_FiveParametersWithFromAddress_LogsFromAddress()
    {
        // Act
        await service.SendEmailAsync("sender@example.com", "recipient@example.com", "Subject", "<p>HTML</p>");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("sender@example.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendEmailAsync_FiveParametersWithFromAddress_LogsToAddress()
    {
        // Act
        await service.SendEmailAsync("sender@example.com", "recipient@example.com", "Subject", "<p>HTML</p>");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("recipient@example.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendEmailAsync_FiveParametersWithFromAddress_LogsSubject()
    {
        // Act
        await service.SendEmailAsync("sender@example.com", "recipient@example.com", "Email Subject", "<p>HTML</p>");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Email Subject")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendEmailAsync_FiveParametersWithNullTextMessage_ReturnsTrue()
    {
        // Act
        var result = await service.SendEmailAsync("sender@example.com", "recipient@example.com", "Subject", "<p>HTML</p>", null);

        // Assert
        Assert.IsTrue(result);
    }

    #endregion

    #region SendEmailAsync(string email, string subject, string htmlMessage) Tests - IEmailSender Interface

    [TestMethod]
    public async Task SendEmailAsync_IEmailSenderInterfaceMethod_ReturnsCompletedTask()
    {
        // Act
        var task = service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>");

        // Assert
        Assert.IsNotNull(task);
        await task; // Should complete without throwing
    }

    [TestMethod]
    public async Task SendEmailAsync_IEmailSenderInterfaceMethod_LogsWarning()
    {
        // Act
        await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Email service not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendEmailAsync_IEmailSenderInterfaceMethod_LogsEmailAddress()
    {
        // Act
        await service.SendEmailAsync("user@example.com", "Subject", "<p>HTML</p>");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("user@example.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendEmailAsync_IEmailSenderInterfaceMethod_LogsSubject()
    {
        // Act
        await service.SendEmailAsync("test@example.com", "Password Reset", "<p>HTML</p>");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Password Reset")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    #endregion

    #region No-Op Behavior Tests

    [TestMethod]
    public async Task SendEmailAsync_NeverThrowsException()
    {
        // Act & Assert - Should not throw
        await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>");
        await service.SendEmailAsync("sender@example.com", "recipient@example.com", "Subject", "<p>HTML</p>");
        await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>", "Text");
    }

    [TestMethod]
    public async Task SendEmailAsync_WithInvalidEmail_NeverThrowsException()
    {
        // Act & Assert
        await service.SendEmailAsync("invalid-email", "Subject", "<p>HTML</p>");
    }

    [TestMethod]
    public async Task SendEmailAsync_WithEmptyEmail_NeverThrowsException()
    {
        // Act & Assert
        await service.SendEmailAsync(string.Empty, "Subject", "<p>HTML</p>");
    }

    [TestMethod]
    public async Task SendEmailAsync_WithNullEmail_NeverThrowsException()
    {
        // Act & Assert
        await service.SendEmailAsync(null, "Subject", "<p>HTML</p>");
    }

    [TestMethod]
    public async Task SendEmailAsync_WithNullSubject_NeverThrowsException()
    {
        // Act & Assert
        await service.SendEmailAsync("test@example.com", null, "<p>HTML</p>");
    }

    [TestMethod]
    public async Task SendEmailAsync_WithNullHtmlMessage_NeverThrowsException()
    {
        // Act & Assert
        await service.SendEmailAsync("test@example.com", "Subject", null);
    }

    #endregion

    #region Setup Mode Tests

    [TestMethod]
    public async Task SendEmailAsync_WhenUsedInSetupMode_AlwaysSucceeds()
    {
        // Arrange - Multiple rapid calls simulating setup wizard
        var tasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(service.SendEmailAsync($"test{i}@example.com", "Setup Email", "<p>Test</p>"));
        }

        // Act & Assert - All should complete successfully
        await Task.WhenAll(tasks);
    }

    [TestMethod]
    public async Task SendEmailAsync_SetupModeSafety_NeverBlocksExecution()
    {
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>");
        }
        stopwatch.Stop();

        // Assert - Should complete quickly without hanging
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000); // 5 second threshold for 100 calls
    }

    #endregion

    #region Logging Key Information Tests

    [TestMethod]
    public async Task SendEmailAsync_LogMessageContainsModeIndicator()
    {
        // Act
        await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>");

        // Assert - Message should indicate setup mode
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("setup mode")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendEmailAsync_LogMessageIndicatesEmailNotSent()
    {
        // Act
        await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>");

        // Assert - Message should indicate email not sent
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("not sent")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    #endregion

    #region Multiple Overload Consistency Tests

    [TestMethod]
    public async Task SendEmailAsync_AllOverloadsReturnSuccessfully()
    {
        // Act
        var result1 = await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>");
        var result2 = await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>", "Text");
        var result3 = await service.SendEmailAsync("from@example.com", "to@example.com", "Subject", "<p>HTML</p>");
        var result4 = await service.SendEmailAsync("from@example.com", "to@example.com", "Subject", "<p>HTML</p>", "Text");

        // Assert
        Assert.IsTrue(result1);
        Assert.IsTrue(result2);
        Assert.IsTrue(result3);
        Assert.IsTrue(result4);
    }

    [TestMethod]
    public async Task SendEmailAsync_AllOverloadsLog()
    {
        // Act
        await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>");
        await service.SendEmailAsync("test@example.com", "Subject", "<p>HTML</p>", "Text");
        await service.SendEmailAsync("from@example.com", "to@example.com", "Subject", "<p>HTML</p>");

        // Assert - Should have logged 3 times (once per call)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeast(3));
    }

    #endregion

    #region Constructor Tests

    [TestMethod]
    public void Constructor_WithLogger_InitializesSuccessfully()
    {
        // Act & Assert
        var emailService = new NoOpEmailService(mockLogger.Object);
        Assert.IsNotNull(emailService);
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentNullException>(() => new NoOpEmailService(null));
        Assert.IsNotNull(exception);
    }

    #endregion

    #region Concurrent Call Tests

    [TestMethod]
    public async Task SendEmailAsync_MultipleConcurrentCalls_AllSucceed()
    {
        // Arrange
        var tasks = new List<Task<bool>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(service.SendEmailAsync($"test{i}@example.com", $"Subject{i}", "<p>HTML</p>"));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.AreEqual(10, results.Length);
        Assert.IsTrue(results.All(r => r == true));
    }

    #endregion
}
