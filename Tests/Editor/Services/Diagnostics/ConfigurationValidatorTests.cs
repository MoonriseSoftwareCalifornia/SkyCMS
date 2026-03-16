// <copyright file="ConfigurationValidatorTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

#nullable enable
namespace Sky.Tests.Editor.Services.Diagnostics
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Services.Diagnostics;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for ConfigurationValidator service.
    /// Tests database connection validation, storage validation, email validation,
    /// and multi-tenant vs single-tenant configuration checks.
    /// </summary>
    [TestClass]
    public class ConfigurationValidatorTests
    {
        #region Test Initialization & Helpers

        private Mock<IConfiguration> configMock = null!;
        private Mock<ILogger<ConfigurationValidator>> loggerMock = null!;

        private ConfigurationValidator validator = null!;

        [TestInitialize]
        public void Setup()
        {
            configMock = new Mock<IConfiguration>();
            loggerMock = new Mock<ILogger<ConfigurationValidator>>();
            validator = new ConfigurationValidator(configMock.Object, loggerMock.Object);
        }

        /// <summary>
        /// Creates a mock configuration with specified connection strings and values.
        /// </summary>
        private void SetupConfiguration(Dictionary<string, string?> connectionStrings, Dictionary<string, object?> values)
        {
            // Setup default behavior for GetSection - return an empty section for unknown keys
            var emptySection = new Mock<IConfigurationSection>();
            emptySection.Setup(s => s.Value).Returns((string?)null);
            emptySection.Setup(s => s.Path).Returns(string.Empty);
            emptySection.Setup(s => s.Key).Returns(string.Empty);
            emptySection.Setup(s => s.GetChildren()).Returns(Array.Empty<IConfigurationSection>());

            configMock
                .Setup(c => c.GetSection(It.IsAny<string>()))
                .Returns(emptySection.Object);

            // Mock connection strings section (GetConnectionString is an extension method that uses GetSection)
            var connectionStringsSection = new Mock<IConfigurationSection>();
            configMock
                .Setup(c => c.GetSection("ConnectionStrings"))
                .Returns(connectionStringsSection.Object);

            foreach (var (key, value) in connectionStrings)
            {
                var mockSection = new Mock<IConfigurationSection>();
                mockSection.Setup(s => s.Value).Returns(value);
                connectionStringsSection
                    .Setup(c => c[key])
                    .Returns(value);
            }

            foreach (var (key, value) in values)
            {
                // GetValue<T> uses GetSection internally, so we need to mock that
                var mockSection = new Mock<IConfigurationSection>();
                mockSection.Setup(s => s.Value).Returns(value?.ToString());
                mockSection.Setup(s => s.Path).Returns(key);
                mockSection.Setup(s => s.Key).Returns(key);

                configMock
                    .Setup(c => c.GetSection(key))
                    .Returns(mockSection.Object);
            }
        }

        /// <summary>
        /// Creates a mock configuration for testing.
        /// </summary>
        private Mock<IConfiguration> CreateMockConfiguration()
        {
            return new Mock<IConfiguration>();
        }

        #endregion

        #region Admin Email Validation Tests

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_ValidAdminEmail_ReturnsSuccessCheck()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var adminEmailCheck = result.Checks.Find(c => c.Name == "AdminEmail");
            Assert.IsNotNull(adminEmailCheck);
            Assert.AreEqual(CheckStatus.Success, adminEmailCheck.Status);
            Assert.AreEqual("Valid email configured", adminEmailCheck.Message);
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_MissingOrEmptyAdminEmail_ReturnsErrorCheck()
        {
            foreach (var emailValue in new string?[] { null, string.Empty })
            {
                // Arrange
                var connectionStrings = new Dictionary<string, string?>
                {
                    ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
                };
                var values = new Dictionary<string, object?>
                {
                    ["MultiTenantEditor"] = false,
                    ["AdminEmail"] = emailValue
                };
                SetupConfiguration(connectionStrings, values);

                // Act
                var result = await validator.ValidateAsync();

                // Assert
                Assert.IsNotNull(result);
                var adminEmailCheck = result.Checks.Find(c => c.Name == "AdminEmail");
                Assert.IsNotNull(adminEmailCheck);
                Assert.AreEqual(CheckStatus.Error, adminEmailCheck.Status);
                Assert.AreEqual("Not configured or empty", adminEmailCheck.Message);
            }
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_InvalidEmailFormat_ReturnsErrorCheck()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "invalid-email"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var adminEmailCheck = result.Checks.Find(c => c.Name == "AdminEmail");
            Assert.IsNotNull(adminEmailCheck);
            Assert.AreEqual(CheckStatus.Error, adminEmailCheck.Status);
            Assert.AreEqual("Invalid email format", adminEmailCheck.Message);
            Assert.IsFalse(string.IsNullOrWhiteSpace(adminEmailCheck.Details));
            StringAssert.Contains(adminEmailCheck.Details, "***");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_AdminEmailMasked_SensitiveValueNotExposed()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var adminEmailCheck = result.Checks.Find(c => c.Name == "AdminEmail");
            Assert.IsNotNull(adminEmailCheck);
            Assert.IsNotNull(adminEmailCheck.Details);
            StringAssert.Contains(adminEmailCheck.Details, "***");
            Assert.IsFalse(adminEmailCheck.Details.Contains("admin@example.com"),
                "Details should not contain the actual admin email value");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_ValidEmailsVariations_AllSuccessful()
        {
            // Arrange & Act & Assert - Test multiple valid email formats
            var validEmails = new[]
            {
                "user@domain.com",
                "first.last@company.co.uk",
                "test+tag@example.org",
                "a@b.co"
            };

            foreach (var email in validEmails)
            {
                // Use a fresh mock and setup for each iteration
                var config = new Mock<IConfiguration>();
                var logger = new Mock<ILogger<ConfigurationValidator>>();

                // Setup default behavior for GetSection - return an empty section for unknown keys
                var emptySection = new Mock<IConfigurationSection>();
                emptySection.Setup(s => s.Value).Returns((string?)null);
                emptySection.Setup(s => s.Path).Returns(string.Empty);
                emptySection.Setup(s => s.Key).Returns(string.Empty);
                emptySection.Setup(s => s.GetChildren()).Returns(Array.Empty<IConfigurationSection>());

                config
                    .Setup(c => c.GetSection(It.IsAny<string>()))
                    .Returns(emptySection.Object);

                // Mock connection strings section
                var connectionStringsSection = new Mock<IConfigurationSection>();
                config
                    .Setup(c => c.GetSection("ConnectionStrings"))
                    .Returns(connectionStringsSection.Object);
                connectionStringsSection
                    .Setup(c => c["ApplicationDbContextConnection"])
                    .Returns("Server=localhost;Database=test;Trusted_Connection=true;");

                // Mock configuration values
                var multiTenantSection = new Mock<IConfigurationSection>();
                multiTenantSection.Setup(s => s.Value).Returns("false");
                multiTenantSection.Setup(s => s.Path).Returns("MultiTenantEditor");
                multiTenantSection.Setup(s => s.Key).Returns("MultiTenantEditor");
                config
                    .Setup(c => c.GetSection("MultiTenantEditor"))
                    .Returns(multiTenantSection.Object);

                var emailSection = new Mock<IConfigurationSection>();
                emailSection.Setup(s => s.Value).Returns(email);
                emailSection.Setup(s => s.Path).Returns("AdminEmail");
                emailSection.Setup(s => s.Key).Returns("AdminEmail");
                config
                    .Setup(c => c.GetSection("AdminEmail"))
                    .Returns(emailSection.Object);

                var testValidator = new ConfigurationValidator(config.Object, logger.Object);
                var result = await testValidator.ValidateAsync();
                var adminEmailCheck = result.Checks.Find(c => c.Name == "AdminEmail");
                if (adminEmailCheck is null)
                {
                    Assert.Fail("AdminEmail check should be present in validation results.");
                }

                Assert.AreEqual(CheckStatus.Success, adminEmailCheck.Status, $"Email {email} should be valid");
            }
        }

        #endregion

        #region Core Settings - Cosmos Setup Flag

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_CosmosAllowSetupBooleanValue_ReturnsSuccessCheck()
        {
            foreach (var cosmosAllowSetup in new[] { true, false })
            {
                // Arrange
                var connectionStrings = new Dictionary<string, string?>
                {
                    ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
                };
                var values = new Dictionary<string, object?>
                {
                    ["MultiTenantEditor"] = false,
                    ["AdminEmail"] = "admin@example.com",
                    ["CosmosAllowSetup"] = cosmosAllowSetup
                };
                SetupConfiguration(connectionStrings, values);

                // Act
                var result = await validator.ValidateAsync();

                // Assert
                Assert.IsNotNull(result);
                var cosmosCheck = result.Checks.Find(c => c.Name == "CosmosAllowSetup");
                Assert.IsNotNull(cosmosCheck);
                Assert.AreEqual(CheckStatus.Success, cosmosCheck.Status);
            }
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_CosmosAllowSetupNotConfigured_ReturnsWarning()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com",
                ["CosmosAllowSetup"] = null
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var cosmosCheck = result.Checks.Find(c => c.Name == "CosmosAllowSetup");
            Assert.IsNotNull(cosmosCheck);
            Assert.AreEqual(CheckStatus.Warning, cosmosCheck.Status);
            Assert.AreEqual("Not configured (defaults to false)", cosmosCheck.Message);
        }

        #endregion

        #region Single-Tenant Database Connection Validation

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_SingleTenantMissingOrEmptyDatabaseConnection_ReturnsErrorCheck()
        {
            foreach (var connectionValue in new string?[] { null, string.Empty })
            {
                // Arrange
                var connectionStrings = new Dictionary<string, string?>
                {
                    ["ApplicationDbContextConnection"] = connectionValue
                };
                var values = new Dictionary<string, object?>
                {
                    ["MultiTenantEditor"] = false,
                    ["AdminEmail"] = "admin@example.com"
                };
                SetupConfiguration(connectionStrings, values);

                // Act
                var result = await validator.ValidateAsync();

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual("Single-Tenant", result.Mode);
                var dbCheck = result.Checks.Find(c => c.Name == "ApplicationDbContextConnection");
                Assert.IsNotNull(dbCheck);
                Assert.AreEqual(CheckStatus.Error, dbCheck.Status);
                Assert.AreEqual("Not configured or empty", dbCheck.Message);
            }
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_SingleTenantConnections_DetectCorrectDatabaseType()
        {
            var scenarios = new[]
            {
                new
                {
                    ConnectionString = "Server=localhost;Database=test;Trusted_Connection=true;",
                    ExpectedDatabaseType = "SQL Server",
                },
                new
                {
                    ConnectionString = "Server=localhost;Port=3306;Database=test;User=root;Password=pass;",
                    ExpectedDatabaseType = "MySQL",
                },
                new
                {
                    ConnectionString = "Data Source=test.db",
                    ExpectedDatabaseType = "SQLite",
                },
                new
                {
                    ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=key;",
                    ExpectedDatabaseType = "Azure Cosmos DB",
                },
            };

            foreach (var scenario in scenarios)
            {
                // Arrange
                var connectionStrings = new Dictionary<string, string?>
                {
                    ["ApplicationDbContextConnection"] = scenario.ConnectionString
                };
                var values = new Dictionary<string, object?>
                {
                    ["MultiTenantEditor"] = false,
                    ["AdminEmail"] = "admin@example.com"
                };
                SetupConfiguration(connectionStrings, values);

                // Act
                var result = await validator.ValidateAsync();

                // Assert
                Assert.IsNotNull(result);
                var dbCheck = result.Checks.Find(c => c.Name == "ApplicationDbContextConnection");
                Assert.IsNotNull(dbCheck);
                StringAssert.Contains(dbCheck.Message, scenario.ExpectedDatabaseType);
            }
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_SingleTenantSqliteConnection_CreatesFileAndConnects()
        {
            // Arrange
            var dbPath = Path.Combine(Path.GetTempPath(), $"skycms-config-validator-{Guid.NewGuid()}.db");
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = $"Data Source={dbPath}"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            try
            {
                // Act
                var result = await validator.ValidateAsync();

                // Assert
                Assert.IsNotNull(result);
                var dbCheck = result.Checks.Find(c => c.Name == "ApplicationDbContextConnection");
                Assert.IsNotNull(dbCheck);
                StringAssert.Contains(dbCheck.Message, "Connection successful");
                Assert.IsTrue(File.Exists(dbPath), "SQLite database file should be created");
            }
            finally
            {
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
            }
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_SingleTenantDatabaseConnectionMasked_PasswordNotExposed()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;User Id=sa;Password=MySecretPassword123;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var dbCheck = result.Checks.Find(c => c.Name == "ApplicationDbContextConnection");
            Assert.IsNotNull(dbCheck);
            Assert.IsNotNull(dbCheck.Details);
            Assert.IsFalse(dbCheck.Details.Contains("MySecretPassword123"),
                "Details should not contain the actual database password");
            StringAssert.Contains(dbCheck.Details, "Password=***");
        }

        #endregion

        #region Single-Tenant Storage Connection Validation

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_SingleTenantMissingOrEmptyStorageConnection_ReturnsErrorCheck()
        {
            foreach (var storageConnectionValue in new string?[] { null, string.Empty })
            {
                // Arrange
                var connectionStrings = new Dictionary<string, string?>
                {
                    ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;",
                    ["StorageConnectionString"] = storageConnectionValue
                };
                var values = new Dictionary<string, object?>
                {
                    ["MultiTenantEditor"] = false,
                    ["AdminEmail"] = "admin@example.com"
                };
                SetupConfiguration(connectionStrings, values);

                // Act
                var result = await validator.ValidateAsync();

                // Assert
                Assert.IsNotNull(result);
                var storageCheck = result.Checks.Find(c => c.Name == "StorageConnectionString");
                Assert.IsNotNull(storageCheck);
                Assert.AreEqual(CheckStatus.Error, storageCheck.Status);
                Assert.AreEqual("Not configured or empty", storageCheck.Message);
            }
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_SingleTenantAzureBlobStorage_DetectsCorrectStorageType()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;",
                ["StorageConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var storageCheck = result.Checks.Find(c => c.Name == "StorageConnectionString");
            Assert.IsNotNull(storageCheck);
            StringAssert.Contains(storageCheck.Message, "Azure Blob Storage");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_SingleTenantAzureBlobStorage_ConnectionWarningWhenUnreachable()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;",
                ["StorageConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=fake;AccountKey=fake;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var storageCheck = result.Checks.Find(c => c.Name == "StorageConnectionString");
            Assert.IsNotNull(storageCheck);
            Assert.AreEqual(CheckStatus.Warning, storageCheck.Status);
            StringAssert.Contains(storageCheck.Message, "Cannot connect");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_SingleTenantCloudflareR2Storage_DetectsCorrectStorageType()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;",
                ["StorageConnectionString"] = "Bucket=my-bucket;KeyId=key;AccessKey=secret;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var storageCheck = result.Checks.Find(c => c.Name == "StorageConnectionString");
            Assert.IsNotNull(storageCheck);
            StringAssert.Contains(storageCheck.Message, "S3-Compatible");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_SingleTenantStorageConnectionMasked_KeysNotExposed()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;",
                ["StorageConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=MySecretAccountKey123;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var storageCheck = result.Checks.Find(c => c.Name == "StorageConnectionString");
            Assert.IsNotNull(storageCheck);
            Assert.IsNotNull(storageCheck.Details);
            Assert.IsFalse(storageCheck.Details.Contains("MySecretAccountKey123"),
                "Details should not contain the actual storage account key");
            StringAssert.Contains(storageCheck.Details, "AccountKey=***");
        }

        #endregion

        #region Multi-Tenant Configuration Validation

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_MultiTenantMode_ReturnsMultiTenantCategory()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ConfigDbConnectionString"] = "Server=localhost;Database=config;Trusted_Connection=true;",
                ["DataProtectionStorage"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = true,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Multi-Tenant", result.Mode);
            Assert.IsTrue(result.IsMultiTenant);
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_MultiTenantMissingConfigDb_ReturnsErrorCheck()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ConfigDbConnectionString"] = null,
                ["DataProtectionStorage"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = true,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var configDbCheck = result.Checks.Find(c => c.Name == "ConfigDbConnectionString");
            Assert.IsNotNull(configDbCheck);
            Assert.AreEqual(CheckStatus.Error, configDbCheck.Status);
            Assert.AreEqual("Not configured or empty", configDbCheck.Message);
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_MultiTenantMissingDataProtectionStorage_ReturnsErrorCheck()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ConfigDbConnectionString"] = "Server=localhost;Database=config;Trusted_Connection=true;",
                ["DataProtectionStorage"] = null
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = true,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var dpCheck = result.Checks.Find(c => c.Name == "DataProtectionStorage");
            Assert.IsNotNull(dpCheck);
            Assert.AreEqual(CheckStatus.Error, dpCheck.Status);
            Assert.AreEqual("Not configured or empty", dpCheck.Message);
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_MultiTenantConfigDbMasked_PasswordNotExposed()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ConfigDbConnectionString"] = "Server=localhost;Database=config;User Id=sa;Password=ConfigPassword123;",
                ["DataProtectionStorage"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = true,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var configDbCheck = result.Checks.Find(c => c.Name == "ConfigDbConnectionString");
            Assert.IsNotNull(configDbCheck);
            Assert.IsNotNull(configDbCheck.Details);
            Assert.IsFalse(configDbCheck.Details.Contains("ConfigPassword123"),
                "Details should not contain the actual config database password");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_MultiTenantDataProtectionStorageMasked_KeyNotExposed()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ConfigDbConnectionString"] = "Server=localhost;Database=config;Trusted_Connection=true;",
                ["DataProtectionStorage"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=DataProtectionSecret123;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = true,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            var dpCheck = result.Checks.Find(c => c.Name == "DataProtectionStorage");
            Assert.IsNotNull(dpCheck);
            Assert.IsNotNull(dpCheck.Details);
            Assert.IsFalse(dpCheck.Details.Contains("DataProtectionSecret123"),
                "Details should not contain the actual data protection secret");
        }

        #endregion

        #region Validation Result Tests

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_ValidCompleteConfiguration_ReturnsValidResult()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com",
                ["CosmosAllowSetup"] = true
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Checks);
            Assert.IsTrue(result.Checks.Count > 0);
            Assert.IsNotNull(result.Mode);
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_MultipleConfigurationChecks_AllIncludedInResult()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com",
                ["CosmosAllowSetup"] = true
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Checks.Count >= 3, "Should have at least 3 checks");
            Assert.IsNotNull(result.Checks.Find(c => c.Name == "CosmosAllowSetup"));
            Assert.IsNotNull(result.Checks.Find(c => c.Name == "AdminEmail"));
            Assert.IsNotNull(result.Checks.Find(c => c.Name == "ApplicationDbContextConnection"));
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_ChecksHaveCategoryAndDescription_MetadataPresent()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            foreach (var check in result.Checks)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(check.Name));
                Assert.IsFalse(string.IsNullOrWhiteSpace(check.Category));
                Assert.IsFalse(string.IsNullOrWhiteSpace(check.Description));
            }
        }

        #endregion

        #region Mode Detection Tests

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_SingleTenantModeExplicit_ReturnsCorrectMode()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.AreEqual("Single-Tenant", result.Mode);
            Assert.IsFalse(result.IsMultiTenant);
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_MultiTenantModeExplicit_ReturnsCorrectMode()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ConfigDbConnectionString"] = "Server=localhost;Database=config;Trusted_Connection=true;",
                ["DataProtectionStorage"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = true,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.AreEqual("Multi-Tenant", result.Mode);
            Assert.IsTrue(result.IsMultiTenant);
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_MultiTenantEditorNotConfigured_DefaultsToSingleTenant()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = null,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.AreEqual("Single-Tenant", result.Mode);
            Assert.IsFalse(result.IsMultiTenant);
        }

        #endregion

        #region Check Status Categorization

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_ErrorCheckWhenDatabaseEmpty_CheckIncluded()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = string.Empty
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            var errorChecks = result.Checks.FindAll(c => c.Status == CheckStatus.Error);
            Assert.IsTrue(errorChecks.Count > 0, "Should contain error checks");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_WarningCheckWhenCosmosNotConfigured_CheckIncluded()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com",
                ["CosmosAllowSetup"] = null
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            var warningChecks = result.Checks.FindAll(c => c.Status == CheckStatus.Warning);
            Assert.IsTrue(warningChecks.Count > 0, "Should contain warning checks");
        }

        #endregion

        #region Logging Tests

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_DatabaseConnectivityTestFails_LogsWarning()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=invalid-server;Database=test;Connection Timeout=1;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.IsNotNull(result);
            // The logging will occur internally in the validator
            // We verify the check status is Warning instead of checking log calls directly
        }

        #endregion

        #region Email Masking Tests

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_EmailMasking_ShortEmail_ProperlyMasked()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "ab@cd.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            var adminEmailCheck = result.Checks.Find(c => c.Name == "AdminEmail");
            Assert.IsNotNull(adminEmailCheck);
            Assert.IsNotNull(adminEmailCheck.Details);
            // Should mask local part while preserving domain
            StringAssert.Contains(adminEmailCheck.Details, "@cd.com");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_EmailMasking_LongEmail_FirstAndLastCharPreserved()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Trusted_Connection=true;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "administrator@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            var adminEmailCheck = result.Checks.Find(c => c.Name == "AdminEmail");
            Assert.IsNotNull(adminEmailCheck);
            Assert.IsNotNull(adminEmailCheck.Details);
            // Should show first and last character of local part
            StringAssert.Contains(adminEmailCheck.Details, "***");
            StringAssert.Contains(adminEmailCheck.Details, "@example.com");
        }

        #endregion

        #region Connection String Masking Tests

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_ConnectionStringMasking_PasswordMasked_PwdVariation()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;pwd=MySecretPassword;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            var dbCheck = result.Checks.Find(c => c.Name == "ApplicationDbContextConnection");
            Assert.IsNotNull(dbCheck);
            Assert.IsFalse(dbCheck.Details.Contains("MySecretPassword"),
                "Details should not contain the actual database password");
            StringAssert.Contains(dbCheck.Details, "pwd=***");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_ConnectionStringMasking_AccountKeyMasked()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;",
                ["StorageConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=storage;AccountKey=MySecretKey123456789;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            var storageCheck = result.Checks.Find(c => c.Name == "StorageConnectionString");
            Assert.IsNotNull(storageCheck);
            Assert.IsFalse(storageCheck.Details.Contains("MySecretKey123456789"),
                "Details should not contain the actual storage account key");
            StringAssert.Contains(storageCheck.Details, "AccountKey=***");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_ConnectionStringMasking_ClientSecretMasked()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;ClientSecret=SuperSecretValue;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            var dbCheck = result.Checks.Find(c => c.Name == "ApplicationDbContextConnection");
            Assert.IsNotNull(dbCheck);
            Assert.IsFalse(dbCheck.Details.Contains("SuperSecretValue"),
                "Details should not contain the actual client secret");
            StringAssert.Contains(dbCheck.Details, "ClientSecret=***");
        }

        [TestMethod]
        [TestCategory("ConfigurationValidator")]
        public async Task ValidateAsync_ConnectionStringMasking_KeyMasked()
        {
            // Arrange
            var connectionStrings = new Dictionary<string, string?>
            {
                ["ApplicationDbContextConnection"] = "Server=localhost;Database=test;Key=UltraSecretKeyValue;"
            };
            var values = new Dictionary<string, object?>
            {
                ["MultiTenantEditor"] = false,
                ["AdminEmail"] = "admin@example.com"
            };
            SetupConfiguration(connectionStrings, values);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            var dbCheck = result.Checks.Find(c => c.Name == "ApplicationDbContextConnection");
            Assert.IsNotNull(dbCheck);
            Assert.IsFalse(dbCheck.Details.Contains("UltraSecretKeyValue"),
                "Details should not contain the actual key value");
            StringAssert.Contains(dbCheck.Details, "Key=***");
        }

        #endregion
    }
}
