// <copyright file="ConfigurationValidator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Diagnostics
{
    using AspNetCore.Identity.FlexDb;
    using Azure.Storage.Blobs;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    /// <summary>
    /// Validates configuration settings for single-tenant and multi-tenant modes.
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<ConfigurationValidator> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationValidator"/> class.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="logger">Logger instance.</param>
        public ConfigurationValidator(IConfiguration configuration, ILogger<ConfigurationValidator> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        /// <summary>
        /// Validates configuration and returns diagnostic results.
        /// </summary>
        /// <returns>Validation result.</returns>
        public async Task<ValidationResult> ValidateAsync()
        {
            var result = new ValidationResult();
            var isMultiTenant = configuration.GetValue<bool?>("MultiTenantEditor") ?? false;

            result.Mode = isMultiTenant ? "Multi-Tenant" : "Single-Tenant";
            result.IsMultiTenant = isMultiTenant;

            // Common validations
            ValidateCosmosAllowSetup(result);
            ValidateAdminEmail(result);

            if (isMultiTenant)
            {
                await ValidateMultiTenantConfigurationAsync(result);
            }
            else
            {
                await ValidateSingleTenantConfigurationAsync(result);
            }

            result.IsValid = result.Checks.All(c => c.Status == CheckStatus.Success);
            return result;
        }

        private void ValidateCosmosAllowSetup(ValidationResult result)
        {
            var check = new ConfigurationCheck
            {
                Category = "Core Settings",
                Name = "CosmosAllowSetup",
                Description = "Determines if setup/diagnostic features are enabled"
            };

            var value = configuration.GetValue<bool?>("CosmosAllowSetup");
            if (value.HasValue)
            {
                check.Status = CheckStatus.Success;
                check.Message = $"Set to: {value.Value}";
            }
            else
            {
                check.Status = CheckStatus.Warning;
                check.Message = "Not configured (defaults to false)";
            }

            result.Checks.Add(check);
        }

        private void ValidateAdminEmail(ValidationResult result)
        {
            var check = new ConfigurationCheck
            {
                Category = "Core Settings",
                Name = "AdminEmail",
                Description = "Administrator email address"
            };

            var email = configuration.GetValue<string>("AdminEmail");
            if (string.IsNullOrWhiteSpace(email))
            {
                check.Status = CheckStatus.Error;
                check.Message = "Not configured or empty";
            }
            else if (!IsValidEmail(email))
            {
                check.Status = CheckStatus.Error;
                check.Message = "Invalid email format";
                check.Details = MaskSensitiveValue(email);
            }
            else
            {
                check.Status = CheckStatus.Success;
                check.Message = "Valid email configured";
                check.Details = MaskEmail(email);
            }

            result.Checks.Add(check);
        }

        private async Task ValidateSingleTenantConfigurationAsync(ValidationResult result)
        {
            // Validate Database Connection
            var dbCheck = new ConfigurationCheck
            {
                Category = "Database",
                Name = "ApplicationDbContextConnection",
                Description = "Primary database connection string"
            };

            var dbConnectionString = configuration.GetConnectionString("ApplicationDbContextConnection");
            if (string.IsNullOrWhiteSpace(dbConnectionString))
            {
                dbCheck.Status = CheckStatus.Error;
                dbCheck.Message = "Not configured or empty";
            }
            else
            {
                dbCheck.Details = MaskConnectionString(dbConnectionString);
                var dbType = DetectDatabaseType(dbConnectionString);
                dbCheck.Message = $"Configured ({dbType})";

                // Test connectivity
                try
                {
                    var canConnect = await TestDatabaseConnectionAsync(dbConnectionString);
                    if (canConnect)
                    {
                        dbCheck.Status = CheckStatus.Success;
                        dbCheck.Message += " - Connection successful";
                    }
                    else
                    {
                        dbCheck.Status = CheckStatus.Warning;
                        dbCheck.Message += " - Cannot connect (database may not exist yet)";
                    }
                }
                catch (Exception ex)
                {
                    dbCheck.Status = CheckStatus.Warning;
                    dbCheck.Message += " - Connectivity test failed";
                    dbCheck.Details += $"\nError: {ex.Message}";
                    logger.LogWarning(ex, "Database connectivity test failed");
                }
            }

            result.Checks.Add(dbCheck);

            // Validate Storage Connection
            var storageCheck = new ConfigurationCheck
            {
                Category = "Storage",
                Name = "StorageConnectionString",
                Description = "Blob storage connection string"
            };

            var storageConnectionString = configuration.GetConnectionString("StorageConnectionString");
            if (string.IsNullOrWhiteSpace(storageConnectionString))
            {
                storageCheck.Status = CheckStatus.Error;
                storageCheck.Message = "Not configured or empty";
            }
            else
            {
                storageCheck.Details = MaskConnectionString(storageConnectionString);
                var storageType = DetectStorageType(storageConnectionString);
                storageCheck.Message = $"Configured ({storageType})";

                // Test connectivity
                try
                {
                    var canConnect = await TestStorageConnectionAsync(storageConnectionString);
                    if (canConnect)
                    {
                        storageCheck.Status = CheckStatus.Success;
                        storageCheck.Message += " - Connection successful";
                    }
                    else
                    {
                        storageCheck.Status = CheckStatus.Warning;
                        storageCheck.Message += " - Cannot connect";
                    }
                }
                catch (Exception ex)
                {
                    storageCheck.Status = CheckStatus.Warning;
                    storageCheck.Message += " - Connectivity test failed";
                    storageCheck.Details += $"\nError: {ex.Message}";
                    logger.LogWarning(ex, "Storage connectivity test failed");
                }
            }

            result.Checks.Add(storageCheck);
        }

        private async Task ValidateMultiTenantConfigurationAsync(ValidationResult result)
        {
            // Validate Config Database Connection
            var configDbCheck = new ConfigurationCheck
            {
                Category = "Multi-Tenant Database",
                Name = "ConfigDbConnectionString",
                Description = "Configuration database connection string"
            };

            var configConnectionString = configuration.GetConnectionString("ConfigDbConnectionString");
            if (string.IsNullOrWhiteSpace(configConnectionString))
            {
                configDbCheck.Status = CheckStatus.Error;
                configDbCheck.Message = "Not configured or empty";
            }
            else
            {
                configDbCheck.Details = MaskConnectionString(configConnectionString);
                var dbType = DetectDatabaseType(configConnectionString);
                configDbCheck.Message = $"Configured ({dbType})";

                // Test connectivity
                try
                {
                    var canConnect = await TestDatabaseConnectionAsync(configConnectionString);
                    if (canConnect)
                    {
                        configDbCheck.Status = CheckStatus.Success;
                        configDbCheck.Message += " - Connection successful";
                    }
                    else
                    {
                        configDbCheck.Status = CheckStatus.Warning;
                        configDbCheck.Message += " - Cannot connect";
                    }
                }
                catch (Exception ex)
                {
                    configDbCheck.Status = CheckStatus.Warning;
                    configDbCheck.Message += " - Connectivity test failed";
                    configDbCheck.Details += $"\nError: {ex.Message}";
                    logger.LogWarning(ex, "Config database connectivity test failed");
                }
            }

            result.Checks.Add(configDbCheck);

            // Validate Data Protection Storage
            var dataProtectionCheck = new ConfigurationCheck
            {
                Category = "Multi-Tenant Storage",
                Name = "DataProtectionStorage",
                Description = "Data protection keys storage connection"
            };

            var dataProtectionStorage = configuration.GetConnectionString("DataProtectionStorage");
            if (string.IsNullOrWhiteSpace(dataProtectionStorage))
            {
                dataProtectionCheck.Status = CheckStatus.Error;
                dataProtectionCheck.Message = "Not configured or empty";
            }
            else
            {
                dataProtectionCheck.Details = MaskConnectionString(dataProtectionStorage);
                dataProtectionCheck.Status = CheckStatus.Success;
                dataProtectionCheck.Message = "Configured";

                // Test connectivity
                try
                {
                    var canConnect = await TestStorageConnectionAsync(dataProtectionStorage);
                    if (canConnect)
                    {
                        dataProtectionCheck.Message += " - Connection successful";
                    }
                    else
                    {
                        dataProtectionCheck.Status = CheckStatus.Warning;
                        dataProtectionCheck.Message += " - Cannot connect";
                    }
                }
                catch (Exception ex)
                {
                    dataProtectionCheck.Status = CheckStatus.Warning;
                    dataProtectionCheck.Message += " - Connectivity test failed";
                    dataProtectionCheck.Details += $"\nError: {ex.Message}";
                    logger.LogWarning(ex, "Data protection storage connectivity test failed");
                }
            }

            result.Checks.Add(dataProtectionCheck);
        }

        private async Task<bool> TestDatabaseConnectionAsync(string connectionString)
        {
            try
            {
                var options = CosmosDbOptionsBuilder.GetDbOptions<ApplicationDbContext>(connectionString);
                using var context = new ApplicationDbContext(options);

                if (context.Database.IsSqlite())
                {
                    // For SQLite, check if the file exists
                    var dataSource = context.Database.GetDbConnection().DataSource;
                    if (!System.IO.File.Exists(dataSource))
                    {
                        // Attempt to create the database file
                        await context.Database.EnsureCreatedAsync();
                    }
                }

                var canConnect = await context.Database.CanConnectAsync();

                // Explicitly close the connection to release file handles (important for SQLite)
                if (context.Database.IsSqlite())
                {
                    var connection = context.Database.GetDbConnection();
                    if (connection.State != System.Data.ConnectionState.Closed)
                    {
                        await connection.CloseAsync();
                    }

                    // Clear the connection pool to ensure the file handle is released
                    Microsoft.Data.Sqlite.SqliteConnection.ClearPool((Microsoft.Data.Sqlite.SqliteConnection)connection);
                }

                return canConnect;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TestStorageConnectionAsync(string connectionString)
        {
            try
            {
                // Azure Blob Storage
                if (connectionString.Contains("DefaultEndpointsProtocol", StringComparison.OrdinalIgnoreCase))
                {
                    var blobServiceClient = new BlobServiceClient(connectionString);
                    var accountInfo = await blobServiceClient.GetAccountInfoAsync();
                    return accountInfo != null;
                }

                // Cloudflare R2 or other S3-compatible storage
                // Just validate format for now
                return connectionString.Contains("Bucket") && connectionString.Contains("KeyId");
            }
            catch
            {
                return false;
            }
        }

        private string DetectDatabaseType(string connectionString)
        {
            if (connectionString.Contains("AccountEndpoint", StringComparison.OrdinalIgnoreCase))
            {
                return "Azure Cosmos DB";
            }

            if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) && connectionString.Contains("Port=3306"))
            {
                return "MySQL";
            }

            if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) && connectionString.Contains(".db"))
            {
                return "SQLite";
            }

            if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            {
                return "SQL Server";
            }

            return "Unknown";
        }

        private string DetectStorageType(string connectionString)
        {
            if (connectionString.Contains("DefaultEndpointsProtocol", StringComparison.OrdinalIgnoreCase))
            {
                return "Azure Blob Storage";
            }

            if (connectionString.Contains("Bucket", StringComparison.OrdinalIgnoreCase) && connectionString.Contains("KeyId"))
            {
                return "S3-Compatible (Cloudflare R2)";
            }

            return "Unknown";
        }

        private bool IsValidEmail(string email)
        {
            return new EmailAddressAttribute().IsValid(email);
        }

        private string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return string.Empty;
            }

            var parts = email.Split('@');
            if (parts.Length != 2)
            {
                return "***@***";
            }

            var localPart = parts[0];
            var maskedLocal = localPart.Length > 2
                ? $"{localPart[0]}***{localPart[^1]}"
                : "***";

            return $"{maskedLocal}@{parts[1]}";
        }

        private string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return string.Empty;
            }

            // Mask sensitive parts of connection strings
            var masked = Regex.Replace(connectionString, @"(Password|pwd|AccountKey|Key|ClientSecret)=([^;]+)", "$1=***", RegexOptions.IgnoreCase);
            return masked;
        }

        private string MaskSensitiveValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= 4)
            {
                return "***";
            }

            return $"{value[..2]}***{value[^2..]}";
        }
    }

    /// <summary>
    /// Validation result containing all configuration checks.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets or sets the deployment mode (Single-Tenant or Multi-Tenant).
        /// </summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the mode is multi-tenant.
        /// </summary>
        public bool IsMultiTenant { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether all checks passed.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets the list of configuration checks performed.
        /// </summary>
        public List<ConfigurationCheck> Checks { get; } = new List<ConfigurationCheck>();
    }

    /// <summary>
    /// Individual configuration check result.
    /// </summary>
    public class ConfigurationCheck
    {
        /// <summary>
        /// Gets or sets the category of the check.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the configuration name being checked.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of what is being validated.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status of the check.
        /// </summary>
        public CheckStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the message describing the result.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional details (masked values, error messages).
        /// </summary>
        public string Details { get; set; } = string.Empty;
    }

    /// <summary>
    /// Status of a configuration check.
    /// </summary>
    public enum CheckStatus
    {
        /// <summary>
        /// Check passed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// Check found a potential issue.
        /// </summary>
        Warning,

        /// <summary>
        /// Check failed - critical issue.
        /// </summary>
        Error
    }
}