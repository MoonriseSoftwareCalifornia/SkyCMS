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
    private IConfiguration configuration = null!;
    private ApplicationDbContext dbContext = null!;
    private Mock<ILogger<EmailConfigurationService>> mockLogger = null!;
    private EmailConfigurationService service = null!;
    private Dictionary<string, string?> configValues = null!;

    /// <summary>
    /// Initializes test dependencies before each test.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        // Initialize configuration values dictionary
        configValues = new Dictionary<string, string?>();

        // Setup in-memory database with unique name per test
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"EmailConfigTest_{Guid.NewGuid()}")
            .Options;
        dbContext = new ApplicationDbContext(options);

        mockLogger = new Mock<ILogger<EmailConfigurationService>>();
    }

    /// <summary>
    /// Builds the configuration and service with current config values.
    /// </summary>
    private void BuildService()
    {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(configValues!);
        configuration = configurationBuilder.Build();
        service = new EmailConfigurationService(configuration, dbContext, mockLogger.Object);
    }

    /// <summary>
    /// Cleanup resources after each test.
    /// </summary>
    [TestCleanup]
    public void TestCleanup()
    {
        dbContext?.Dispose();
    }

    /// <summary>
    /// Helper method to seed database with email settings.
    /// </summary>
    /// <param name="settings">List of settings to add to database.</param>
    private void SeedDatabaseSettings(List<Setting> settings)
    {
        dbContext.Settings.AddRange(settings);
        dbContext.SaveChanges();
    }

    #region Step 1: Basic Environment Variable Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithSendGridApiKeyInEnvironment_ReturnsSendGridProvider()
    {
        // Arrange
        const string sendGridKey = "sg-test-key-123";
        configValues["CosmosSendGridApiKey"] = sendGridKey;
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(sendGridKey, settings.SendGridApiKey);
        Assert.AreEqual("SendGrid", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAzureConnectionStringInEnvironment_ReturnsAzureCommunicationProvider()
    {
        // Arrange
        const string azureConnString = "endpoint=https://cosmos.communication.azure.com/;accesskey=test123";
        configValues["ConnectionStrings:AzureCommunicationConnection"] = azureConnString;
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(azureConnString, settings.AzureEmailConnectionString);
        Assert.AreEqual("AzureCommunication", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithSmtpSettingsInEnvironment_ReturnsSmtpProvider()
    {
        // Arrange
        configValues["SmtpEmailProviderOptions:Host"] = "smtp.gmail.com";
        configValues["SmtpEmailProviderOptions:Port"] = "587";
        configValues["SmtpEmailProviderOptions:UserName"] = "user@example.com";
        configValues["SmtpEmailProviderOptions:Password"] = "password123";
        BuildService();

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
        configValues["SmtpEmailProviderOptions__Host"] = "smtp.sendgrid.net";
        configValues["SmtpEmailProviderOptions__Port"] = "465";
        configValues["SmtpEmailProviderOptions__UserName"] = "sendgrid_user";
        configValues["SmtpEmailProviderOptions__Password"] = "pass456";
        BuildService();

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
    public async Task GetEmailSettingsAsync_WithAdminEmailInEnvironment_SetsSenderEmail()
    {
        // Arrange
        const string adminEmail = "admin@example.com";
        configValues["CosmosSendGridApiKey"] = "sg-key";
        configValues["AdminEmail"] = adminEmail;
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(adminEmail, settings.SenderEmail);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithNoConfiguration_ReturnsUnconfigured()
    {
        // Arrange - Empty configuration
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.IsFalse(settings.IsConfigured);
        Assert.AreEqual(string.Empty, settings.Provider);
        Assert.IsNull(settings.SendGridApiKey);
        Assert.IsNull(settings.AzureEmailConnectionString);
        Assert.IsNull(settings.SmtpHost);
    }

    #endregion

    #region Step 2: Database Fallback Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithSendGridInDatabase_ReturnsSendGridProvider()
    {
        // Arrange
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-db-key-456" }
        };
        SeedDatabaseSettings(dbSettings);
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("sg-db-key-456", settings.SendGridApiKey);
        Assert.AreEqual("SendGrid", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAzureInDatabase_ReturnsAzureCommunicationProvider()
    {
        // Arrange
        var dbSettings = new List<Setting>
        {
            new Setting 
            { 
                Id = Guid.NewGuid(), 
                Group = "EMAIL", 
                Name = "AzureEmailConnectionString", 
                Value = "endpoint=https://db.communication.azure.com/;accesskey=dbkey" 
            }
        };
        SeedDatabaseSettings(dbSettings);
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("endpoint=https://db.communication.azure.com/;accesskey=dbkey", settings.AzureEmailConnectionString);
        Assert.AreEqual("AzureCommunication", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithCompleteSmtpInDatabase_ReturnsAllSmtpSettings()
    {
        // Arrange
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpHost", Value = "smtp.database.com" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpPort", Value = "465" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpUsername", Value = "dbuser@example.com" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpPassword", Value = "dbpass123" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "AdminEmail", Value = "admin@db.com" }
        };
        SeedDatabaseSettings(dbSettings);
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("smtp.database.com", settings.SmtpHost);
        Assert.AreEqual(465, settings.SmtpPort);
        Assert.AreEqual("dbuser@example.com", settings.SmtpUsername);
        Assert.AreEqual("dbpass123", settings.SmtpPassword);
        Assert.AreEqual("admin@db.com", settings.SenderEmail);
        Assert.AreEqual("SMTP", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithSmtpInDatabaseWithoutPort_UsesDefaultPort()
    {
        // Arrange - Test that missing port defaults to 587
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpHost", Value = "smtp.noport.com" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpUsername", Value = "user@example.com" }
        };
        SeedDatabaseSettings(dbSettings);
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("smtp.noport.com", settings.SmtpHost);
        Assert.AreEqual(587, settings.SmtpPort); // Default port
        Assert.AreEqual("SMTP", settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithSmtpInDatabasePortInvalid_UsesDefaultPort()
    {
        // Arrange - Invalid port should default to 587
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpHost", Value = "smtp.db.com" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpPort", Value = "not-a-port" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpUsername", Value = "user" }
        };
        SeedDatabaseSettings(dbSettings);
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(587, settings.SmtpPort); // Default port when parsing fails
        Assert.AreEqual("SMTP", settings.Provider);
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
        SeedDatabaseSettings(dbSettings);
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("db-admin@example.com", settings.SenderEmail);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithEmptyDatabase_ReturnsUnconfigured()
    {
        // Arrange - No database settings
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.IsFalse(settings.IsConfigured);
        Assert.AreEqual(string.Empty, settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithNonEmailGroupSettings_IgnoresThem()
    {
        // Arrange - Settings with different group should be ignored
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "GENERAL", Name = "SendGridApiKey", Value = "wrong-key" },
            new Setting { Id = Guid.NewGuid(), Group = "SYSTEM", Name = "SmtpHost", Value = "wrong-host" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "correct-key" }
        };
        SeedDatabaseSettings(dbSettings);
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert - Only EMAIL group settings should be used
        Assert.AreEqual("correct-key", settings.SendGridApiKey);
        Assert.AreEqual("SendGrid", settings.Provider);
    }

    #endregion

    #region Step 3: Configuration Syntax & Port Parsing Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithValidPortFromBothSyntax_PrefersColonSyntax()
    {
        // Arrange - Both colon and underscore present, colon takes precedence
        configValues["SmtpEmailProviderOptions:Host"] = "smtp.example.com";
        configValues["SmtpEmailProviderOptions:Port"] = "25";
        configValues["SmtpEmailProviderOptions__Port"] = "465"; // Should be ignored
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(25, settings.SmtpPort); // Colon syntax takes priority
        Assert.AreEqual("SMTP", settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithNullPortInEnvironment_UsesDefaultPort()
    {
        // Arrange - Port is null, should default to 587
        configValues["SmtpEmailProviderOptions:Host"] = "smtp.example.com";
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(587, settings.SmtpPort);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithInvalidPortInEnvironment_UsesDefaultPort()
    {
        // Arrange - Invalid port string
        configValues["SmtpEmailProviderOptions:Host"] = "smtp.example.com";
        configValues["SmtpEmailProviderOptions:Port"] = "invalid-port";
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(587, settings.SmtpPort); // Default port
        Assert.AreEqual("SMTP", settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithEmptyPortString_UsesDefaultPort()
    {
        // Arrange - Empty string port
        configValues["SmtpEmailProviderOptions:Host"] = "smtp.example.com";
        configValues["SmtpEmailProviderOptions:Port"] = string.Empty;
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(587, settings.SmtpPort);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithColonSyntaxPreferredOverUnderscore_UsesColonValue()
    {
        // Arrange - Test that colon syntax is checked first for all SMTP settings
        configValues["SmtpEmailProviderOptions:Host"] = "smtp.colon.com";
        configValues["SmtpEmailProviderOptions__Host"] = "smtp.underscore.com";
        configValues["SmtpEmailProviderOptions:UserName"] = "colon@user.com";
        configValues["SmtpEmailProviderOptions__UserName"] = "underscore@user.com";
        configValues["SmtpEmailProviderOptions:Password"] = "colonpass";
        configValues["SmtpEmailProviderOptions__Password"] = "underscorepass";
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("smtp.colon.com", settings.SmtpHost);
        Assert.AreEqual("colon@user.com", settings.SmtpUsername);
        Assert.AreEqual("colonpass", settings.SmtpPassword);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithZeroPort_UsesZeroPort()
    {
        // Arrange - Valid port of 0
        configValues["SmtpEmailProviderOptions:Host"] = "smtp.example.com";
        configValues["SmtpEmailProviderOptions:Port"] = "0";
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(0, settings.SmtpPort); // 0 is valid
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithLargeValidPort_UsesSpecifiedPort()
    {
        // Arrange - High port number
        configValues["SmtpEmailProviderOptions:Host"] = "smtp.example.com";
        configValues["SmtpEmailProviderOptions:Port"] = "8025";
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual(8025, settings.SmtpPort);
    }

    #endregion

    #region Step 4: Provider Priority & Mixed Scenarios

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithMultipleProvidersConfigured_PrioritizesSendGrid()
    {
        // Arrange - SendGrid has highest priority
        configValues["CosmosSendGridApiKey"] = "sg-key";
        configValues["ConnectionStrings:AzureCommunicationConnection"] = "azure-conn";
        configValues["SmtpEmailProviderOptions:Host"] = "smtp.example.com";
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("SendGrid", settings.Provider);
        Assert.AreEqual("sg-key", settings.SendGridApiKey);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAzureAndSmtp_PrioritizesAzure()
    {
        // Arrange - Azure has priority over SMTP
        configValues["ConnectionStrings:AzureCommunicationConnection"] = "azure-conn";
        configValues["SmtpEmailProviderOptions:Host"] = "smtp.example.com";
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("AzureCommunication", settings.Provider);
        Assert.AreEqual("azure-conn", settings.AzureEmailConnectionString);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithEnvironmentPriorityOverDatabase_UsesEnvironment()
    {
        // Arrange - Environment should take precedence even if database has settings
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-db-key" }
        };
        SeedDatabaseSettings(dbSettings);

        configValues["CosmosSendGridApiKey"] = "sg-env-key";
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("sg-env-key", settings.SendGridApiKey); // Environment wins
        Assert.AreEqual("SendGrid", settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAdminEmailInBothSources_PrefersEnvironment()
    {
        // Arrange - AdminEmail in both environment and database
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-key" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "AdminEmail", Value = "db@example.com" }
        };
        SeedDatabaseSettings(dbSettings);

        configValues["CosmosSendGridApiKey"] = "sg-key";
        configValues["AdminEmail"] = "env@example.com";
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("env@example.com", settings.SenderEmail); // Environment has priority
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithProviderInEnvironmentOnly_DoesNotQueryDatabase()
    {
        // Arrange - Since we have a provider in env, database shouldn't be queried
        configValues["CosmosSendGridApiKey"] = "sg-key";
        BuildService();

        // Seed database (but it shouldn't be accessed)
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpHost", Value = "smtp.db.com" }
        };
        SeedDatabaseSettings(dbSettings);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("SendGrid", settings.Provider);
        Assert.IsNull(settings.SmtpHost); // Database settings should not be loaded
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithAllDatabaseProviderTypes_PrioritizesSendGrid()
    {
        // Arrange - All three providers in database, SendGrid should win
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-key" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "AzureEmailConnectionString", Value = "azure-conn" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SmtpHost", Value = "smtp.host" }
        };
        SeedDatabaseSettings(dbSettings);
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.AreEqual("SendGrid", settings.Provider); // SendGrid has highest priority
        Assert.AreEqual("sg-key", settings.SendGridApiKey);
        Assert.AreEqual("azure-conn", settings.AzureEmailConnectionString);
        Assert.AreEqual("smtp.host", settings.SmtpHost);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithOnlySenderEmailConfigured_ReturnsUnconfigured()
    {
        // Arrange - AdminEmail alone shouldn't make it configured
        configValues["AdminEmail"] = "admin@example.com";
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert
        Assert.IsFalse(settings.IsConfigured);
        Assert.AreEqual("admin@example.com", settings.SenderEmail);
        Assert.AreEqual(string.Empty, settings.Provider);
    }

    #endregion

    #region Step 5: Error Handling & Logging Tests

    [TestMethod]
    public async Task GetEmailSettingsAsync_WhenCheckingDatabase_LogsInformation()
    {
        // Arrange
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-key" }
        };
        SeedDatabaseSettings(dbSettings);
        BuildService();

        // Act
        await service.GetEmailSettingsAsync();

        // Assert - Verify LogInformation was called
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Email settings not found in environment variables, checking database")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithProviderInEnvironment_DoesNotLogInformation()
    {
        // Arrange - Provider in environment, so database won't be checked
        configValues["CosmosSendGridApiKey"] = "sg-key";
        BuildService();

        // Act
        await service.GetEmailSettingsAsync();

        // Assert - Verify LogInformation was NOT called
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Email settings not found in environment variables, checking database")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithDatabaseException_ReturnsEmptySettings()
    {
        // Arrange - Create a disposed context to force exception
        BuildService();
        await dbContext.DisposeAsync();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert - Should return empty settings, not throw
        Assert.IsNull(settings.SendGridApiKey);
        Assert.IsNull(settings.AzureEmailConnectionString);
        Assert.IsNull(settings.SmtpHost);
        Assert.IsFalse(settings.IsConfigured);
        Assert.AreEqual(string.Empty, settings.Provider);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithDatabaseException_LogsError()
    {
        // Arrange - Create a disposed context to force exception
        BuildService();
        await dbContext.DisposeAsync();

        // Act
        await service.GetEmailSettingsAsync();

        // Assert - Verify LogError was called
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to load email settings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithUnknownDatabaseSetting_IgnoresIt()
    {
        // Arrange - Database has unknown setting name (tests switch default case)
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-key" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "UnknownSetting", Value = "ignored-value" },
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "AnotherUnknown", Value = "also-ignored" }
        };
        SeedDatabaseSettings(dbSettings);
        BuildService();

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert - Unknown settings should be ignored, SendGrid should work
        Assert.AreEqual("sg-key", settings.SendGridApiKey);
        Assert.AreEqual("SendGrid", settings.Provider);
        Assert.IsTrue(settings.IsConfigured);
    }

    [TestMethod]
    public async Task GetEmailSettingsAsync_WithEmptyStringSettings_TreatsAsUnconfigured()
    {
        // Arrange - Empty strings should be treated as not configured
        configValues["CosmosSendGridApiKey"] = string.Empty;
        configValues["ConnectionStrings:AzureCommunicationConnection"] = string.Empty;
        configValues["SmtpEmailProviderOptions:Host"] = string.Empty;
        BuildService();

        // Database should be checked since env vars are empty
        var dbSettings = new List<Setting>
        {
            new Setting { Id = Guid.NewGuid(), Group = "EMAIL", Name = "SendGridApiKey", Value = "sg-db-key" }
        };
        SeedDatabaseSettings(dbSettings);

        // Act
        var settings = await service.GetEmailSettingsAsync();

        // Assert - Database should have been queried and used
        Assert.AreEqual("sg-db-key", settings.SendGridApiKey);
        Assert.AreEqual("SendGrid", settings.Provider);
    }

    #endregion
}




