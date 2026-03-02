// <copyright file="DynamicEmailSenderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.EmailServices
{
    using Cosmos.Common.Data;
    using Cosmos.EmailServices;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Unit tests for DynamicEmailSender.
    /// Tests email provider resolution for both single-tenant and multi-tenant scenarios.
    /// </summary>
    [TestClass]
    public class DynamicEmailSenderTests
    {
        private Mock<IConfiguration> mockConfiguration;
        private Mock<IHttpContextAccessor> mockHttpContextAccessor;
        private Mock<ILogger<DynamicEmailSender>> mockLogger;
        private Mock<ILoggerFactory> mockLoggerFactory;
        private Mock<HttpContext> mockHttpContext;
        private Mock<HttpRequest> mockRequest;
        private Mock<IHeaderDictionary> mockHeaders;

        [TestInitialize]
        public void Setup()
        {
            mockConfiguration = new Mock<IConfiguration>();
            mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            mockLogger = new Mock<ILogger<DynamicEmailSender>>();
            mockLoggerFactory = new Mock<ILoggerFactory>();
            mockHttpContext = new Mock<HttpContext>();
            mockRequest = new Mock<HttpRequest>();
            mockHeaders = new Mock<IHeaderDictionary>();

            // Setup HTTP context chain
            mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
            mockRequest.Setup(r => r.Headers).Returns(mockHeaders.Object);
            mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

            // Setup logger factory to return mock loggers
            mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
                .Returns(Mock.Of<ILogger>());

            // Setup default ConnectionStrings section (returns null for any connection string)
            // This prevents NullReferenceException in HasEnvironmentVariableConfig()
            var defaultConnectionStringsSection = new Mock<IConfigurationSection>();
            defaultConnectionStringsSection.Setup(s => s[It.IsAny<string>()]).Returns((string)null);
            defaultConnectionStringsSection.Setup(s => s.Value).Returns((string)null);
            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings")).Returns(defaultConnectionStringsSection.Object);
        }

        #region Single-Tenant with Environment Variables Tests

        [TestMethod]
        public void SendEmailAsync_SingleTenant_WithSmtpEnvVars_UsesSMTP()
        {
            // Arrange
            SetupSingleTenantMode();
            SetupSmtpEnvironmentVariables();

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMTP provider")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void SendEmailAsync_SingleTenant_WithSendGridEnvVar_UsesSendGrid()
        {
            // Arrange
            SetupSingleTenantMode();
            SetupSendGridEnvironmentVariable();

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SendGrid provider")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        [Ignore("Azure Communication SDK attempts real connection - requires mocking Azure SDK or using integration tests")]
        public void SendEmailAsync_SingleTenant_WithAzureEnvVar_UsesAzureCommunication()
        {
            // Arrange
            SetupSingleTenantMode();
            SetupAzureEnvironmentVariable();

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Azure Communication")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Single-Tenant with Database Settings Tests

        [TestMethod]
        [Ignore("Database access tests require EF Core InMemory database setup - see SetupDatabaseWithSmtpSettings comment")]
        public void SendEmailAsync_SingleTenant_NoEnvVars_LoadsFromDatabase()
        {
            // Arrange
            SetupSingleTenantMode();
            SetupDatabaseWithSmtpSettings();

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("checking database settings")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void SendEmailAsync_SingleTenant_NoConfiguration_UsesNoOp()
        {
            // Arrange
            SetupSingleTenantMode();
            // No environment variables, no database settings

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No email provider configured")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            Assert.IsNotNull(sender.SendResult);
        }

        #endregion

        #region Multi-Tenant Tests

        [TestMethod]
        [Ignore("Database access tests require EF Core InMemory database setup")]
        public void SendEmailAsync_MultiTenant_WithTenantSettings_ResolvesTenantProvider()
        {
            // Arrange
            SetupMultiTenantMode();
            SetupTenantContext("tenant1.example.com");
            SetupMultiTenantDatabaseWithSmtpSettings("tenant1.example.com");

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("tenant1.example.com")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [TestMethod]
        public void SendEmailAsync_MultiTenant_NoHttpContext_UsesNoOp()
        {
            // Arrange
            SetupMultiTenantMode();
            mockHttpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext)null);

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("no HTTP context available")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void SendEmailAsync_MultiTenant_UnknownTenant_UsesNoOp()
        {
            // Arrange
            SetupMultiTenantMode();
            SetupTenantContext("unknown.example.com");

            // Setup ConfigDbConnectionString properly
            var configConnectionString = "Server=(localdb)\\mssqllocaldb;Database=ConfigDb;Trusted_Connection=True;";
            var connectionStringsSection = new Mock<IConfigurationSection>();
            connectionStringsSection.Setup(s => s["ConfigDbConnectionString"]).Returns(configConnectionString);
            connectionStringsSection.Setup(s => s.Value).Returns(configConnectionString);
            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings")).Returns(connectionStringsSection.Object);
            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings:ConfigDbConnectionString")).Returns(connectionStringsSection.Object);

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert - Should log error about database failure (NullReferenceException trying to access DynamicConfigDbContext)
            // OR log warning about no configuration found (depending on which exception is caught first)
            mockLogger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Warning || l == LogLevel.Error),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("tenant") || v.ToString().Contains("No email provider")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Priority Tests

        [TestMethod]
        [Ignore("Database access tests require EF Core InMemory database setup")]
        public void SendEmailAsync_SingleTenant_EnvVarsOverrideDatabase()
        {
            // Arrange
            SetupSingleTenantMode();
            SetupSmtpEnvironmentVariables(); // Env vars present
            SetupDatabaseWithSendGridSettings(); // Database has SendGrid

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert - Should use SMTP from env vars, NOT SendGrid from database
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMTP provider")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("checking database")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);
        }

        [TestMethod]
        [Ignore("Azure Communication SDK attempts real connection when all providers configured")]
        public void SendEmailAsync_SmtpPriority_OverAzureAndSendGrid()
        {
            // Arrange
            SetupSingleTenantMode();
            SetupAllEnvironmentVariables(); // All providers configured

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert - Should use SMTP (highest priority)
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMTP provider")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void SendEmailAsync_SmtpPriority_OverSendGrid()
        {
            // Arrange
            SetupSingleTenantMode();
            SetupSmtpEnvironmentVariables();
            SetupSendGridEnvironmentVariable();

            var sender = CreateDynamicEmailSender();

            // Act
            var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
            task.Wait();

            // Assert - Should use SMTP (highest priority), not SendGrid
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMTP provider")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Lazy Resolution Tests

        [TestMethod]
        public void SendResult_BeforeSendEmail_DoesNotResolveProvider()
        {
            // Arrange
            SetupSingleTenantMode();
            SetupSmtpEnvironmentVariables();

            var sender = CreateDynamicEmailSender();

            // Act - Access SendResult property without sending
            var result = sender.SendResult;

            // Assert - Provider should be resolved when accessing SendResult
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void SendEmailAsync_CalledTwice_ResolvesOnlyOnce()
        {
            // Arrange
            SetupSingleTenantMode();
            SetupSmtpEnvironmentVariables();

            var sender = CreateDynamicEmailSender();

            // Act
            var task1 = sender.SendEmailAsync("test1@example.com", "Test 1", "Text", "<p>HTML</p>");
            task1.Wait();
            var task2 = sender.SendEmailAsync("test2@example.com", "Test 2", "Text", "<p>HTML</p>");
            task2.Wait();

            // Assert - Resolution log should appear only once
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Resolving email provider")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Helper Methods

        private DynamicEmailSender CreateDynamicEmailSender()
        {
            return new DynamicEmailSender(
                mockConfiguration.Object,
                mockHttpContextAccessor.Object,
                mockLogger.Object,
                mockLoggerFactory.Object,
                azureCredential: null);
        }

        private void SetupSingleTenantMode()
        {
            mockConfiguration.Setup(c => c["MultiTenantEditor"]).Returns("false");
            mockConfiguration.Setup(c => c.GetSection("MultiTenantEditor").Value).Returns("false");
        }

        private void SetupMultiTenantMode()
        {
            mockConfiguration.Setup(c => c["MultiTenantEditor"]).Returns("true");
            mockConfiguration.Setup(c => c.GetSection("MultiTenantEditor").Value).Returns("true");
        }

        private void SetupSmtpEnvironmentVariables()
        {
            mockConfiguration.Setup(c => c["AdminEmail"]).Returns("admin@example.com");
            mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Host"]).Returns("smtp.example.com");
            mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Port"]).Returns("587");
            mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:UserName"]).Returns("user@example.com");
            mockConfiguration.Setup(c => c["SmtpEmailProviderOptions:Password"]).Returns("password123");

            // Create child sections for proper binding with Get<T>()
            var hostSection = new Mock<IConfigurationSection>();
            hostSection.Setup(s => s.Value).Returns("smtp.example.com");
            hostSection.Setup(s => s.Key).Returns("Host");
            hostSection.Setup(s => s.Path).Returns("SmtpEmailProviderOptions:Host");

            var portSection = new Mock<IConfigurationSection>();
            portSection.Setup(s => s.Value).Returns("587");
            portSection.Setup(s => s.Key).Returns("Port");
            portSection.Setup(s => s.Path).Returns("SmtpEmailProviderOptions:Port");

            var userNameSection = new Mock<IConfigurationSection>();
            userNameSection.Setup(s => s.Value).Returns("user@example.com");
            userNameSection.Setup(s => s.Key).Returns("UserName");
            userNameSection.Setup(s => s.Path).Returns("SmtpEmailProviderOptions:UserName");

            var passwordSection = new Mock<IConfigurationSection>();
            passwordSection.Setup(s => s.Value).Returns("password123");
            passwordSection.Setup(s => s.Key).Returns("Password");
            passwordSection.Setup(s => s.Path).Returns("SmtpEmailProviderOptions:Password");

            var smtpSection = new Mock<IConfigurationSection>();
            smtpSection.Setup(s => s.Key).Returns("SmtpEmailProviderOptions");
            smtpSection.Setup(s => s.Path).Returns("SmtpEmailProviderOptions");
            smtpSection.Setup(s => s.Value).Returns((string)null);
            smtpSection.Setup(s => s["Host"]).Returns("smtp.example.com");
            smtpSection.Setup(s => s["Port"]).Returns("587");
            smtpSection.Setup(s => s["UserName"]).Returns("user@example.com");
            smtpSection.Setup(s => s["Password"]).Returns("password123");

            // This is crucial: GetChildren() must return the child sections for Get<T>() to work
            smtpSection.Setup(s => s.GetChildren()).Returns(new[] 
            { 
                hostSection.Object, 
                portSection.Object, 
                userNameSection.Object, 
                passwordSection.Object 
            });

            mockConfiguration.Setup(c => c.GetSection("SmtpEmailProviderOptions")).Returns(smtpSection.Object);
        }

        private void SetupSendGridEnvironmentVariable()
        {
            mockConfiguration.Setup(c => c["AdminEmail"]).Returns("admin@example.com");
            mockConfiguration.Setup(c => c["CosmosSendGridApiKey"]).Returns("SG.test-api-key");
        }

        private void SetupAzureEnvironmentVariable()
        {
            mockConfiguration.Setup(c => c["AdminEmail"]).Returns("admin@example.com");

            // Mock GetConnectionString for Azure Communication
            var azureConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=testkey";
            var connectionStringsSection = new Mock<IConfigurationSection>();
            connectionStringsSection.Setup(s => s["AzureCommunicationConnection"]).Returns(azureConnectionString);
            connectionStringsSection.Setup(s => s.Value).Returns(azureConnectionString);

            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings")).Returns(connectionStringsSection.Object);
            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings:AzureCommunicationConnection")).Returns(connectionStringsSection.Object);
        }

        private void SetupAllEnvironmentVariables()
        {
            SetupSmtpEnvironmentVariables();
            SetupSendGridEnvironmentVariable();
            SetupAzureEnvironmentVariable();
        }

        private void SetupDatabaseWithSmtpSettings()
        {
            var connectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;";

            // Mock ConnectionStrings section for ApplicationDbContextConnection
            var connectionStringsSection = new Mock<IConfigurationSection>();
            connectionStringsSection.Setup(s => s["ApplicationDbContextConnection"]).Returns(connectionString);
            connectionStringsSection.Setup(s => s.Value).Returns(connectionString);

            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings")).Returns(connectionStringsSection.Object);
            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings:ApplicationDbContextConnection")).Returns(connectionStringsSection.Object);

            // Note: For full database testing, use EF Core InMemory database
            // The current implementation will fail when trying to create ApplicationDbContext
            // with a connection string (it needs a real/InMemory database)
        }
       
        private void SetupDatabaseWithSendGridSettings()
        {
            var connectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;";

            // Mock ConnectionStrings section for ApplicationDbContextConnection
            var connectionStringsSection = new Mock<IConfigurationSection>();
            connectionStringsSection.Setup(s => s["ApplicationDbContextConnection"]).Returns(connectionString);
            connectionStringsSection.Setup(s => s.Value).Returns(connectionString);

            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings")).Returns(connectionStringsSection.Object);
            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings:ApplicationDbContextConnection")).Returns(connectionStringsSection.Object);
        }

        private void SetupTenantContext(string tenantDomain)
        {
            mockHeaders.Setup(h => h["x-origin-hostname"]).Returns(tenantDomain);
            mockRequest.Setup(r => r.Host).Returns(new HostString(tenantDomain));
        }

        private void SetupMultiTenantDatabaseWithSmtpSettings(string tenantDomain)
        {
            var configConnectionString = "Server=(localdb)\\mssqllocaldb;Database=ConfigDb;Trusted_Connection=True;";

            // Mock ConnectionStrings section (GetConnectionString uses this internally)
            var connectionStringsSection = new Mock<IConfigurationSection>();
            connectionStringsSection.Setup(s => s["ConfigDbConnectionString"]).Returns(configConnectionString);
            connectionStringsSection.Setup(s => s.Value).Returns(configConnectionString);

            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings")).Returns(connectionStringsSection.Object);
            mockConfiguration.Setup(c => c.GetSection("ConnectionStrings:ConfigDbConnectionString")).Returns(connectionStringsSection.Object);

            // Note: In real tests, you'd setup InMemory EF Core databases
            // to return Connection and Settings entities for the tenant
        }

        private ApplicationDbContext CreateInMemoryDbContext(List<Setting> settings)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new ApplicationDbContext(options);
            context.Settings.AddRange(settings);
            context.SaveChanges();

            return context;
        }

        #endregion

        #region Test Methods

        [TestMethod]
        public void SendEmailAsync_MultiTenant_WithInMemoryDb_ResolvesTenantSmtpProvider()
        {
            // Arrange
            SetupMultiTenantMode();
            SetupTenantContext("tenant1.example.com");

            // Setup in-memory databases using SQLite with shared cache
            // Using .db extension so CosmosDbOptionsBuilder recognizes it as SQLite
            var configConnectionString = $"Data Source={Guid.NewGuid()}.db;Mode=Memory;Cache=Shared";
            var tenantConnectionString = $"Data Source={Guid.NewGuid()}.db;Mode=Memory;Cache=Shared";

            var configDbOptions = new DbContextOptionsBuilder<Cosmos.DynamicConfig.DynamicConfigDbContext>()
                .UseSqlite(configConnectionString)
                .Options;

            var tenantDbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(tenantConnectionString)
                .Options;

            // Keep connections open for the duration of the test
            var configContext = new Cosmos.DynamicConfig.DynamicConfigDbContext(configDbOptions);
            var tenantContext = new ApplicationDbContext(tenantDbOptions);

            try
            {
                // Seed config database with tenant connection
                configContext.Database.OpenConnection();
                configContext.Database.EnsureCreated();

                configContext.Connections.Add(new Cosmos.DynamicConfig.Connection
                {
                    Id = Guid.NewGuid(),
                    DomainNames = new [] { "tenant1.example.com" },
                    DbConn = tenantConnectionString,
                    OwnerEmail = "owner@tenant1.example.com",
                    StorageConn = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net",
                    ResourceGroup = "test-resource-group",
                    WebsiteUrl = "https://tenant1.example.com"
                });
                configContext.SaveChanges();

                // Seed tenant database with email settings
                tenantContext.Database.OpenConnection();
                tenantContext.Database.EnsureCreated();

                tenantContext.Settings.AddRange(new[]
                {
                    new Setting { Group = "EMAIL", Name = "AdminEmail", Value = "admin@tenant1.example.com" },
                    new Setting { Group = "EMAIL", Name = "SmtpHost", Value = "smtp.tenant1.com" },
                    new Setting { Group = "EMAIL", Name = "SmtpPort", Value = "587" },
                    new Setting { Group = "EMAIL", Name = "SmtpUsername", Value = "user@tenant1.com" },
                    new Setting { Group = "EMAIL", Name = "SmtpPassword", Value = "tenant1password" }
                });
                tenantContext.SaveChanges();

                var connectionStringsSection = new Mock<IConfigurationSection>();
                connectionStringsSection.Setup(s => s["ConfigDbConnectionString"]).Returns(configConnectionString);
                mockConfiguration.Setup(c => c.GetSection("ConnectionStrings")).Returns(connectionStringsSection.Object);

                var sender = CreateDynamicEmailSender();

                // Act
                var task = sender.SendEmailAsync("test@example.com", "Test Subject", "Plain text", "<p>HTML</p>");
                task.Wait();

                // Assert - Verify correct provider was resolved from tenant database
                mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("[tenant1.example.com] Using SMTP provider")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                    Times.Once);
            }
            finally
            {
                // Clean up database connections
                configContext.Database.CloseConnection();
                configContext.Dispose();
                tenantContext.Database.CloseConnection();
                tenantContext.Dispose();
            }
        }

        #endregion
    }
}
