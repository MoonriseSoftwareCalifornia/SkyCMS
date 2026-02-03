// <copyright file="EmailConfigurationServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Editor.Services.Email;

using Cosmos.Common.Data;
using Cosmos.Common.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Sky.Editor.Services.Email;

/// <summary>
/// Tests for <see cref="EmailConfigurationService"/>.
/// </summary>
[TestClass]
public class EmailConfigurationServiceTests
{
    private Mock<IConfiguration> mockConfiguration = null!;
    private Mock<ApplicationDbContext> mockDbContext = null!;
    private Mock<ILogger<EmailConfigurationService>> mockLogger = null!;
    private EmailConfigurationService service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        mockConfiguration = new Mock<IConfiguration>();
        mockDbContext = new Mock<ApplicationDbContext>();
        mockLogger = new Mock<ILogger<EmailConfigurationService>>();
        service = new EmailConfigurationService(mockConfiguration.Object, mockDbContext.Object, mockLogger.Object);
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    private static void SetupMockDbSet<T>(Mock<DbSet<T>> mockDbSet, List<T> data)
        where T : class
    {
        var asyncEnumerable = ToAsyncEnumerable(data);
        var queryable = data.AsQueryable();

        mockDbSet.As<IAsyncEnumerable<T>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(asyncEnumerable.GetAsyncEnumerator());
        mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
    }

    #region SendGrid Configuration Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithSendGridApiKeyInEnvironment_ReturnsSendGridProvider()
    {
        // Arrange
        const string sendGridKey = "sg-test-key-123";
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns(sendGridKey);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(sendGridKey, settings.SendGridApiKey);
        Assert.AreEqual("SendGrid", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithOnlySendGridInDatabase_ReturnsSendGridProvider()
    {
        // Arrange
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-db-key" }
        };

        var mockDbSet = new Mock<DbSet<Setting>>();
        SetupMockDbSet(mockDbSet, dbSettings);

        mockDbContext.Setup(db => db.Settings).Returns(mockDbSet.Object);
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("sg-db-key", settings.SendGridApiKey);
        Assert.AreEqual("SendGrid", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    #endregion

    #region Azure Communication Configuration Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAzureConnectionStringInEnvironment_ReturnsAzureCommunicationProvider()
    {
        // Arrange
        const string azureConnString = "endpoint=https://cosmos.communication.azure.com/;accesskey=test123";
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns(azureConnString);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(azureConnString, settings.AzureEmailConnectionString);
        Assert.AreEqual("AzureCommunication", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAzureConnectionStringInDatabase_ReturnsAzureCommunicationProvider()
    {
        // Arrange
        const string azureConnString = "endpoint=https://cosmos.communication.azure.com/;accesskey=test123";
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "AzureEmailConnectionString", Value = azureConnString }
        };

        var mockDbSet = new Mock<DbSet<Setting>>();
        SetupMockDbSet(mockDbSet, dbSettings);
        mockDbContext.Setup(db => db.Settings).Returns(mockDbSet.Object);

        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(azureConnString, settings.AzureEmailConnectionString);
        Assert.AreEqual("AzureCommunication", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    #endregion

    #region SMTP Configuration Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithSmtpSettingsInEnvironment_ReturnsSmtpProvider()
    {
        // Arrange
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns("smtp.gmail.com");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Port"]).Returns("587");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Port"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:UserName"]).Returns("user@example.com");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__UserName"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Password"]).Returns("password123");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Password"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("smtp.gmail.com", settings.SmtpHost);
        Assert.AreEqual(587, settings.SmtpPort);
        Assert.AreEqual("user@example.com", settings.SmtpUsername);
        Assert.AreEqual("password123", settings.SmtpPassword);
        Assert.AreEqual("SMTP", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithSmtpInEnvironmentUsingUnderscoreSyntax_ReturnsSmtpProvider()
    {
        // Arrange - Test underscore syntax (alternative configuration format)
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null); // Colon syntax returns null
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns("smtp.sendgrid.net"); // Underscore syntax has value
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Port"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Port"]).Returns("465");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:UserName"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__UserName"]).Returns("sendgrid_user");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Password"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Password"]).Returns("pass456");

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("smtp.sendgrid.net", settings.SmtpHost);
        Assert.AreEqual(465, settings.SmtpPort);
        Assert.AreEqual("sendgrid_user", settings.SmtpUsername);
        Assert.AreEqual("pass456", settings.SmtpPassword);
        Assert.AreEqual("SMTP", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithSmtpPortInvalid_UsesDefaultPort587()
    {
        // Arrange
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns("smtp.example.com");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Port"]).Returns("invalid-port");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Port"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:UserName"]).Returns("user@example.com");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__UserName"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Password"]).Returns("pass");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Password"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(587, settings.SmtpPort); // Default port
        Assert.AreEqual("SMTP", settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithSmtpInDatabasePortInvalid_UsesDefaultPort587()
    {
        // Arrange
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpHost", Value = "smtp.db.com" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpPort", Value = "not-a-port" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpUsername", Value = "user" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpPassword", Value = "pass" }
        };

        var mockDbSet = new Mock<DbSet<Setting>>();
        SetupMockDbSet(mockDbSet, dbSettings);
        mockDbContext.Setup(db => db.Settings).Returns(mockDbSet.Object);

        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(587, settings.SmtpPort); // Default port
    }

    #endregion

    #region Provider Priority Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithMultipleProvidersConfigured_PrioritizesSendGrid()
    {
        // Arrange - SendGrid has highest priority
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns("sg-key");
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns("azure-conn");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns("smtp.example.com");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("SendGrid", settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAzureAndSmtp_PrioritizesAzure()
    {
        // Arrange - Azure has priority over SMTP
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns("azure-conn");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns("smtp.example.com");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("AzureCommunication", settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithOnlySmtp_SelectsSmtp()
    {
        // Arrange
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns("smtp.example.com");
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("SMTP", settings.Provider);
    }

    #endregion

    #region Email Address Configuration Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAdminEmailInEnvironment_SetsSenderEmail()
    {
        // Arrange
        const string adminEmail = "admin@example.com";
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns("sg-key");
        mockConfiguration.Setup(c => c["AdminEmail"]).Returns(adminEmail);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(adminEmail, settings.SenderEmail);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAdminEmailInDatabase_SetsSenderEmail()
    {
        // Arrange
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-key" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "AdminEmail", Value = "db-admin@example.com" }
        };

        var mockDbSet = new Mock<DbSet<Setting>>();
        SetupMockDbSet(mockDbSet, dbSettings);
        mockDbContext.Setup(db => db.Settings).Returns(mockDbSet.Object);

        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("db-admin@example.com", settings.SenderEmail);
    }

    #endregion

    #region Database Fallback Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithNoEnvironmentSettings_ChecksDatabase()
    {
        // Arrange
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-db-key" }
        };

        var mockDbSet = new Mock<DbSet<Setting>>();
        SetupMockDbSet(mockDbSet, dbSettings);
        mockDbContext.Setup(db => db.Settings).Returns(mockDbSet.Object);

        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert - Verify database was queried
        mockDbContext.Verify(db => db.Settings, Times.AtLeastOnce);
        Assert.AreEqual("SendGrid", settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithEnvironmentSettingPresent_DoesNotQueryDatabase()
    {
        // Arrange - Environment has a value, so database should not be queried
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns("sg-key");
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert - Database should not be queried
        mockDbContext.Verify(db => db.Settings, Times.Never);
        Assert.AreEqual("SendGrid", settings.Provider);
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithDatabaseException_ReturnsEmptySettings()
    {
        // Arrange
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);
        
        mockDbContext.Setup(db => db.Settings).Throws(new InvalidOperationException("Database error"));

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.IsNull(settings.SendGridApiKey);
        Assert.IsNull(settings.AzureEmailConnectionString);
        Assert.IsNull(settings.SmtpHost);
        Assert.IsFalse(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithDatabaseException_LogsError()
    {
        // Arrange
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);
        
        var exception = new InvalidOperationException("Database error");
        mockDbContext.Setup(db => db.Settings).Throws(exception);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to load email settings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithNoDatabaseSettings_ReturnsUnconfigured()
    {
        // Arrange
        var emptyDbSettings = new List<Setting>();

        var mockDbSet = new Mock<DbSet<Setting>>();
        SetupMockDbSet(mockDbSet, emptyDbSettings);
        mockDbContext.Setup(db => db.Settings).Returns(mockDbSet.Object);

        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.IsFalse(settings.IsConfigured);
        Assert.IsNull(settings.Provider);
    }

    #endregion

    #region Null/Empty Handling Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAllNullSettings_ReturnsUnconfigured()
    {
        // Arrange
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        var emptyDbSettings = new List<Setting>();
        var mockDbSet = new Mock<DbSet<Setting>>();
        SetupMockDbSet(mockDbSet, emptyDbSettings);
        mockDbContext.Setup(db => db.Settings).Returns(mockDbSet.Object);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.IsFalse(settings.IsConfigured);
        Assert.IsNull(settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithEmptyStringSettings_TreatsAsNull()
    {
        // Arrange
        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns(string.Empty);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns(string.Empty);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns(string.Empty);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns(string.Empty);

        var emptyDbSettings = new List<Setting>();
        var mockDbSet = new Mock<DbSet<Setting>>();
        SetupMockDbSet(mockDbSet, emptyDbSettings);
        mockDbContext.Setup(db => db.Settings).Returns(mockDbSet.Object);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.IsFalse(settings.IsConfigured);
    }

    #endregion

    #region Database Setting Filtering Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_FiltersDatabaseSettingsByEmailGroup()
    {
        // Arrange - Only EMAIL group settings should be used
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-key" },
            new Setting { Id = Guid.NewGuid(), Group = "OTHER_GROUP", Name = "SendGridApiKey", Value = "ignore-this" }
        };

        var mockDbSet = new Mock<DbSet<Setting>>();
        SetupMockDbSet(mockDbSet, dbSettings);
        mockDbContext.Setup(db => db.Settings).Returns(mockDbSet.Object);

        mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns((string)null);
        mockConfiguration.Setup(c => c.GetConnectionString("AzureCommunicationConnection")).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns((string)null);
        mockConfiguration.Setup(c => c["SmtpEmailProviderOptions__Host"]).Returns((string)null);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("sg-key", settings.SendGridApiKey); // Correct value from EMAIL group
        Assert.AreEqual("SendGrid", settings.Provider);
    }

    #endregion
}
