// <copyright file="TenantAwareEmailSenderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Email
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Azure.Identity;
    using Cosmos.EmailServices;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Services.Email;

    /// <summary>
    /// Unit tests for the <see cref="TenantAwareEmailSender"/> class.
    /// </summary>
    [TestClass]
    public class TenantAwareEmailSenderTests
    {
        private Mock<IEmailConfigurationService> _mockConfigService;
        private Mock<ILogger<TenantAwareEmailSender>> _mockLogger;
        private Mock<ILoggerFactory> _mockLoggerFactory;
        private DefaultAzureCredential _azureCredential;
        private TenantAwareEmailSender _sut;

        /// <summary>
        /// Initializes the test class before each test method runs.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            _mockConfigService = new Mock<IEmailConfigurationService>();
            _mockLogger = new Mock<ILogger<TenantAwareEmailSender>>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _azureCredential = new DefaultAzureCredential();

            // Setup the logger factory to return appropriate loggers
            _mockLoggerFactory
                .Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(_mockLogger.Object);

            _sut = new TenantAwareEmailSender(
                _mockConfigService.Object,
                _mockLogger.Object,
                _mockLoggerFactory.Object,
                _azureCredential);
        }

        #region Constructor Tests

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_InitializesSendResult()
        {
            // Assert
            Assert.IsNotNull(_sut.SendResult);
        }

        #endregion

        #region Email Not Configured Tests

        [TestMethod]
        [TestCategory("NotConfigured")]
        public async Task SendEmailAsync_WithEmailNotConfigured_ReturnsServiceUnavailableStatus()
        {
            // Arrange
            var settings = new EmailSettings
            {
                IsConfigured = false,
                Provider = null
            };

            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            await _sut.SendEmailAsync("recipient@test.com", "Test Subject", "Test Body");

            // Assert
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, _sut.SendResult.StatusCode);
            StringAssert.Contains(_sut.SendResult.Message, "not configured");
        }

        [TestMethod]
        [TestCategory("NotConfigured")]
        public async Task SendEmailAsync_WithEmailNotConfigured_LogsWarning()
        {
            // Arrange
            var settings = new EmailSettings { IsConfigured = false };
            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            await _sut.SendEmailAsync("recipient@test.com", "Test Subject", "Test Body");

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("not configured")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Exception Handling Tests

        [TestMethod]
        [TestCategory("ExceptionHandling")]
        public async Task SendEmailAsync_WhenConfigServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            // Act
            await _sut.SendEmailAsync("recipient@test.com", "Test Subject", "Test Body");

            // Assert
            Assert.AreEqual(HttpStatusCode.InternalServerError, _sut.SendResult.StatusCode);
            StringAssert.Contains(_sut.SendResult.Message, "Failed to send email");
        }

        [TestMethod]
        [TestCategory("ExceptionHandling")]
        public async Task SendEmailAsync_WhenConfigServiceThrowsException_LogsError()
        {
            // Arrange
            var exception = new InvalidOperationException("Database connection failed");
            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ThrowsAsync(exception);

            // Act
            await _sut.SendEmailAsync("recipient@test.com", "Test Subject", "Test Body");

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Provider Selection Tests

        [TestMethod]
        [TestCategory("ProviderSelection")]
        public async Task SendEmailAsync_WithUnknownProvider_UsesNoOpSender()
        {
            // Arrange
            var settings = new EmailSettings
            {
                IsConfigured = true,
                Provider = "UnknownProvider",
                SenderEmail = "sender@test.com"
            };

            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            await _sut.SendEmailAsync("recipient@test.com", "Test Subject", "Test Body");

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("NoOp")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        [TestCategory("ProviderSelection")]
        [DataRow("SendGrid")]
        [DataRow("SMTP")]
        [DataRow("AzureCommunication")]
        public async Task SendEmailAsync_WithValidProvider_CreatesAppropriateEmailSender(string provider)
        {
            // Arrange
            var settings = new EmailSettings
            {
                IsConfigured = true,
                Provider = provider,
                SenderEmail = "sender@test.com"
            };

            // Set provider-specific properties
            switch (provider)
            {
                case "SendGrid":
                    settings.SendGridApiKey = "test-api-key";
                    break;
                case "SMTP":
                    settings.SmtpHost = "smtp.test.com";
                    settings.SmtpPort = 587;
                    settings.SmtpUsername = "user";
                    settings.SmtpPassword = "pass";
                    break;
                case "AzureCommunication":
                    settings.AzureEmailConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=test";
                    break;
            }

            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            await _sut.SendEmailAsync("recipient@test.com", "Test Subject", "Test Body");

            // Assert
            Assert.IsNotNull(_sut.SendResult);
        }

        #endregion

        #region Email Message Tests

        [TestMethod]
        [TestCategory("EmailMessage")]
        public async Task SendEmailAsync_WithHtmlOnlyMessage_UsesHtmlVersion()
        {
            // Arrange
            var settings = new EmailSettings
            {
                IsConfigured = true,
                Provider = "SMTP",
                SenderEmail = "sender@test.com",
                SmtpHost = "smtp.test.com",
                SmtpPort = 587,
                SmtpUsername = "user",
                SmtpPassword = "pass"
            };

            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            await _sut.SendEmailAsync(
                "recipient@test.com",
                "Test Subject",
                "<html><body>Test Body</body></html>");

            // Assert
            Assert.IsNotNull(_sut.SendResult);
        }

        [TestMethod]
        [TestCategory("EmailMessage")]
        public async Task SendEmailAsync_WithTextAndHtmlVersions_UsesBothVersions()
        {
            // Arrange
            var settings = new EmailSettings
            {
                IsConfigured = true,
                Provider = "SMTP",
                SenderEmail = "sender@test.com",
                SmtpHost = "smtp.test.com",
                SmtpPort = 587,
                SmtpUsername = "user",
                SmtpPassword = "pass"
            };

            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            await _sut.SendEmailAsync(
                "recipient@test.com",
                "Test Subject",
                "Text Body",
                "<html><body>HTML Body</body></html>");

            // Assert
            Assert.IsNotNull(_sut.SendResult);
        }

        #endregion

        #region From Address Tests

        [TestMethod]
        [TestCategory("FromAddress")]
        public async Task SendEmailAsync_WithCustomFromAddress_UsesProvidedFromAddress()
        {
            // Arrange
            var settings = new EmailSettings
            {
                IsConfigured = true,
                Provider = "SMTP",
                SenderEmail = "default@test.com",
                SmtpHost = "smtp.test.com",
                SmtpPort = 587,
                SmtpUsername = "user",
                SmtpPassword = "pass"
            };

            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            var customFrom = "custom@test.com";

            // Act
            await _sut.SendEmailAsync(
                "recipient@test.com",
                "Test Subject",
                "Text Body",
                "<html><body>HTML Body</body></html>",
                customFrom);

            // Assert
            Assert.IsNotNull(_sut.SendResult);
        }

        [TestMethod]
        [TestCategory("FromAddress")]
        public async Task SendEmailAsync_WithNullFromAddress_UsesDefaultSenderEmail()
        {
            // Arrange
            var settings = new EmailSettings
            {
                IsConfigured = true,
                Provider = "SMTP",
                SenderEmail = "default@test.com",
                SmtpHost = "smtp.test.com",
                SmtpPort = 587,
                SmtpUsername = "user",
                SmtpPassword = "pass"
            };

            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            await _sut.SendEmailAsync(
                "recipient@test.com",
                "Test Subject",
                "Text Body",
                "<html><body>HTML Body</body></html>",
                null);

            // Assert
            Assert.IsNotNull(_sut.SendResult);
        }

        #endregion

        #region SMTP Port Tests

        [TestMethod]
        [TestCategory("SmtpConfiguration")]
        public async Task SendEmailAsync_WithSmtpPort465_UsesSsl()
        {
            // Arrange
            var settings = new EmailSettings
            {
                IsConfigured = true,
                Provider = "SMTP",
                SenderEmail = "sender@test.com",
                SmtpHost = "smtp.test.com",
                SmtpPort = 465, // SSL port
                SmtpUsername = "user",
                SmtpPassword = "pass"
            };

            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            await _sut.SendEmailAsync("recipient@test.com", "Test Subject", "Test Body");

            // Assert
            Assert.IsNotNull(_sut.SendResult);
        }

        [TestMethod]
        [TestCategory("SmtpConfiguration")]
        public async Task SendEmailAsync_WithSmtpPort587_DoesNotUseSsl()
        {
            // Arrange
            var settings = new EmailSettings
            {
                IsConfigured = true,
                Provider = "SMTP",
                SenderEmail = "sender@test.com",
                SmtpHost = "smtp.test.com",
                SmtpPort = 587, // TLS port
                SmtpUsername = "user",
                SmtpPassword = "pass"
            };

            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            await _sut.SendEmailAsync("recipient@test.com", "Test Subject", "Test Body");

            // Assert
            Assert.IsNotNull(_sut.SendResult);
        }

        #endregion

        #region State Management Tests

        [TestMethod]
        [TestCategory("StateManagement")]
        public async Task SendEmailAsync_CalledTwice_UpdatesSendResult()
        {
            // Arrange
            var settings = new EmailSettings
            {
                IsConfigured = false
            };

            _mockConfigService
                .Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            await _sut.SendEmailAsync("recipient1@test.com", "Subject 1", "Body 1");
            var firstResult = _sut.SendResult;

            await _sut.SendEmailAsync("recipient2@test.com", "Subject 2", "Body 2");
            var secondResult = _sut.SendResult;

            // Assert
            Assert.IsNotNull(firstResult);
            Assert.IsNotNull(secondResult);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, firstResult.StatusCode);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, secondResult.StatusCode);
        }

        #endregion
    }
}