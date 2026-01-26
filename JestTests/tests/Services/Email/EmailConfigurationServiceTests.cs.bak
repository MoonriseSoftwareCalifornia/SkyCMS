// <copyright file="EmailConfigurationServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Email
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Services.Email;

    /// <summary>
    /// Unit tests for the <see cref="EmailConfigurationService"/> class.
    /// </summary>
    [TestClass]
    public class EmailConfigurationServiceTests
    {
        private Mock<IConfiguration> _mockConfiguration;
        private Mock<ILogger<EmailConfigurationService>> _mockLogger;
        private ApplicationDbContext _dbContext;
        private EmailConfigurationService _sut;

        /// <summary>
        /// Initializes the test class before each test method runs.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<EmailConfigurationService>>();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new ApplicationDbContext(options);

            _sut = new EmailConfigurationService(
                _mockConfiguration.Object,
                _dbContext,
                _mockLogger.Object);
        }

        /// <summary>
        /// Cleanup after each test.
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            _dbContext?.Dispose();
        }

        #region SendGrid Configuration Tests

        [TestMethod]
        [TestCategory("SendGridConfiguration")]
        public async Task GetEmailSettingsAsync_WithSendGridInEnvironment_ReturnsSendGridSettings()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["SendGridApiKey"]).Returns("test-sendgrid-key");
            _mockConfiguration.Setup(x => x["AdminEmail"]).Returns("admin@test.com");

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.IsTrue(result.IsConfigured);
            Assert.AreEqual("SendGrid", result.Provider);
            Assert.AreEqual("test-sendgrid-key", result.SendGridApiKey);
            Assert.AreEqual("admin@test.com", result.SenderEmail);
        }

        #endregion

        #region Azure Email Configuration Tests

        [TestMethod]
        [TestCategory("AzureConfiguration")]
        public async Task GetEmailSettingsAsync_WithAzureEmailInEnvironment_ReturnsAzureSettings()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["AzureEmailConnectionString"]).Returns("endpoint=https://test.com/;accesskey=test");
            _mockConfiguration.Setup(x => x["SenderEmail"]).Returns("sender@test.com");

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.IsTrue(result.IsConfigured);
            Assert.AreEqual("AzureCommunication", result.Provider);
            Assert.AreEqual("endpoint=https://test.com/;accesskey=test", result.AzureEmailConnectionString);
            Assert.AreEqual("sender@test.com", result.SenderEmail);
        }

        #endregion

        #region SMTP Configuration Tests

        [TestMethod]
        [TestCategory("SmtpConfiguration")]
        public async Task GetEmailSettingsAsync_WithSmtpInEnvironment_ReturnsSmtpSettings()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["SmtpHost"]).Returns("smtp.test.com");
            _mockConfiguration.Setup(x => x["SmtpPort"]).Returns("587");
            _mockConfiguration.Setup(x => x["SmtpUsername"]).Returns("user@test.com");
            _mockConfiguration.Setup(x => x["SmtpPassword"]).Returns("password");
            _mockConfiguration.Setup(x => x["SenderEmail"]).Returns("sender@test.com");

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.IsTrue(result.IsConfigured);
            Assert.AreEqual("SMTP", result.Provider);
            Assert.AreEqual("smtp.test.com", result.SmtpHost);
            Assert.AreEqual(587, result.SmtpPort);
            Assert.AreEqual("user@test.com", result.SmtpUsername);
            Assert.AreEqual("password", result.SmtpPassword);
        }

        [TestMethod]
        [TestCategory("SmtpConfiguration")]
        public async Task GetEmailSettingsAsync_WithInvalidSmtpPort_UsesDefaultPort587()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["SmtpHost"]).Returns("smtp.test.com");
            _mockConfiguration.Setup(x => x["SmtpPort"]).Returns("invalid");
            _mockConfiguration.Setup(x => x["SmtpUsername"]).Returns("user");
            _mockConfiguration.Setup(x => x["SmtpPassword"]).Returns("pass");

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.AreEqual(587, result.SmtpPort);
        }

        #endregion

        #region Fallback Tests

        [TestMethod]
        [TestCategory("Fallback")]
        public async Task GetEmailSettingsAsync_WithAdminEmailFallback_UsesSenderEmail()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["SendGridApiKey"]).Returns("test-key");
            _mockConfiguration.Setup(x => x["AdminEmail"]).Returns("admin@test.com");
            _mockConfiguration.Setup(x => x["SenderEmail"]).Returns((string)null);

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.AreEqual("admin@test.com", result.SenderEmail);
        }

        #endregion

        #region Database Configuration Tests

        [TestMethod]
        [TestCategory("DatabaseConfiguration")]
        public async Task GetEmailSettingsAsync_WithNoEnvironmentVariables_ChecksDatabase()
        {
            // Arrange
            await SeedDatabaseSettings();

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.IsTrue(result.IsConfigured);
            Assert.AreEqual("SendGrid", result.Provider);
            Assert.AreEqual("db-sendgrid-key", result.SendGridApiKey);
            Assert.AreEqual("dbadmin@test.com", result.SenderEmail);
        }

        [TestMethod]
        [TestCategory("DatabaseConfiguration")]
        public async Task GetEmailSettingsAsync_WithDatabaseSmtpSettings_ReturnsSmtpFromDatabase()
        {
            // Arrange
            await SeedDatabaseSmtpSettings();

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.IsTrue(result.IsConfigured);
            Assert.AreEqual("SMTP", result.Provider);
            Assert.AreEqual("smtp.db.com", result.SmtpHost);
            Assert.AreEqual(2525, result.SmtpPort);
        }

        #endregion

        #region Not Configured Tests

        [TestMethod]
        [TestCategory("NotConfigured")]
        public async Task GetEmailSettingsAsync_WithNoSettingsAnywhere_ReturnsNotConfigured()
        {
            // Arrange - no environment variables, no database settings

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.IsFalse(result.IsConfigured);
            Assert.IsNull(result.Provider);
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        [TestCategory("ErrorHandling")]
        public async Task GetEmailSettingsAsync_WhenDatabaseThrowsException_LogsError()
        {
            // Arrange - Force database to be disposed to cause exception
            _dbContext.Dispose();

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to load email settings")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Provider Priority Tests

        [TestMethod]
        [TestCategory("ProviderPriority")]
        public async Task GetEmailSettingsAsync_WithMultipleProviders_PrioritizesSendGrid()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["SendGridApiKey"]).Returns("sendgrid-key");
            _mockConfiguration.Setup(x => x["AzureEmailConnectionString"]).Returns("azure-connection");
            _mockConfiguration.Setup(x => x["SmtpHost"]).Returns("smtp.test.com");

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.AreEqual("SendGrid", result.Provider);
        }

        [TestMethod]
        [TestCategory("ProviderPriority")]
        public async Task GetEmailSettingsAsync_WithoutSendGridButWithAzure_UsesAzure()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["AzureEmailConnectionString"]).Returns("azure-connection");
            _mockConfiguration.Setup(x => x["SmtpHost"]).Returns("smtp.test.com");

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.AreEqual("AzureCommunication", result.Provider);
        }

        [TestMethod]
        [TestCategory("ProviderPriority")]
        public async Task GetEmailSettingsAsync_WithOnlySmtp_UsesSmtp()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["SmtpHost"]).Returns("smtp.test.com");
            _mockConfiguration.Setup(x => x["SmtpUsername"]).Returns("user");
            _mockConfiguration.Setup(x => x["SmtpPassword"]).Returns("pass");

            // Act
            var result = await _sut.GetEmailSettingsAsync();

            // Assert
            Assert.AreEqual("SMTP", result.Provider);
        }

        #endregion

        #region Helper Methods

        private async Task SeedDatabaseSettings()
        {
            var settings = new List<Setting>
            {
                new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "db-sendgrid-key" },
                new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "AdminEmail", Value = "dbadmin@test.com" }
            };

            _dbContext.Settings.AddRange(settings);
            await _dbContext.SaveChangesAsync();
        }

        private async Task SeedDatabaseSmtpSettings()
        {
            var settings = new List<Setting>
            {
                new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpHost", Value = "smtp.db.com" },
                new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpPort", Value = "2525" },
                new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpUsername", Value = "dbuser" },
                new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpPassword", Value = "dbpass" },
                new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "AdminEmail", Value = "dbadmin@test.com" }
            };

            _dbContext.Settings.AddRange(settings);
            await _dbContext.SaveChangesAsync();
        }

        #endregion
    }
}