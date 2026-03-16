// <copyright file="SetupService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using Cosmos.Cms.Data;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Editor.Services;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Services.Layouts;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using CommonMediator = Cosmos.Common.Features.Shared.IMediator;

    /// <summary>
    /// Service for setup wizard operations.
    /// Manages draft state (temporary edits) and committed state (persistent settings).
    /// Draft state is stored in Settings table (SETUP/DRAFT_STATE) and deleted upon completion.
    /// Committed state is stored in Settings table with specific group/name combinations.
    /// Audit logs are tracked in Settings table (SETUP/SettingChange) for post-setup changes.
    /// </summary>
    public class SetupService : ISetupService
    {
        /// <summary>
        /// Draft state key for temporary wizard edits (stores entire SetupConfiguration as JSON).
        /// </summary>
        private const string DraftStateKey = "DRAFT_STATE";
        private const string DraftStateGroup = "SETUP";

        /// <summary>
        /// Committed state key (final setup configuration after wizard completion).
        /// </summary>
        private const string CommittedStateKey = "SETUP_WIZARD_STATE";
        private const string CommittedStateGroup = "SYSTEM";

        /// <summary>
        /// Audit log key for tracking post-setup changes.
        /// </summary>
        private const string AuditLogName = "SettingChange";
        private const string AuditLogGroup = "SETUP";

        private readonly IConfiguration configuration;
        private readonly ILogger<SetupService> logger;
        private readonly IMemoryCache memoryCache;
        private readonly ILayoutImportService layoutImportService;
        private readonly CommonMediator mediator;

        private readonly UserManager<IdentityUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly ApplicationDbContext applicationDbContext;
        private readonly IDatabaseConnectionTester databaseConnectionTester;
        private readonly IStorageConnectionTester storageConnectionTester;
        private readonly ISendGridEmailTester sendGridEmailTester;
        private readonly ISmtpEmailTester smtpEmailTester;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupService"/> class.
        /// </summary>
        /// <param name="configuration">Configuration.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="memoryCache">Memory cache.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="roleManager">Role manager.</param>
        /// <param name="applicationDbContext">Database context.</param>
        /// <param name="layoutImportService">Layout import service.</param>
        /// <param name="mediator">Mediator.</param>
        public SetupService(
            IConfiguration configuration,
            ILogger<SetupService> logger,
            IMemoryCache memoryCache,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext applicationDbContext,
            ILayoutImportService layoutImportService,
            CommonMediator mediator)
            : this(
                configuration,
                logger,
                memoryCache,
                userManager,
                roleManager,
                applicationDbContext,
                layoutImportService,
                mediator,
                new DatabaseConnectionTester(),
                new StorageConnectionTester(memoryCache),
                new SendGridEmailTester(),
                new SmtpEmailTester())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupService"/> class.
        /// </summary>
        /// <param name="configuration">Configuration.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="memoryCache">Memory cache.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="roleManager">Role manager.</param>
        /// <param name="applicationDbContext">Database context.</param>
        /// <param name="layoutImportService">Layout import service.</param>
        /// <param name="mediator">Mediator.</param>
        /// <param name="databaseConnectionTester">Database connection tester.</param>
        /// <param name="storageConnectionTester">Storage connection tester.</param>
        /// <param name="sendGridEmailTester">SendGrid tester.</param>
        /// <param name="smtpEmailTester">SMTP tester.</param>
        public SetupService(
            IConfiguration configuration,
            ILogger<SetupService> logger,
            IMemoryCache memoryCache,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext applicationDbContext,
            ILayoutImportService layoutImportService,
            CommonMediator mediator,
            IDatabaseConnectionTester databaseConnectionTester,
            IStorageConnectionTester storageConnectionTester,
            ISendGridEmailTester sendGridEmailTester,
            ISmtpEmailTester smtpEmailTester)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.memoryCache = memoryCache;
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.applicationDbContext = applicationDbContext;
            this.layoutImportService = layoutImportService;
            this.mediator = mediator;
            this.databaseConnectionTester = databaseConnectionTester;
            this.storageConnectionTester = storageConnectionTester;
            this.sendGridEmailTester = sendGridEmailTester;
            this.smtpEmailTester = smtpEmailTester;
        }

        /// <inheritdoc/>
        public async Task<SetupConfiguration> InitializeSetupAsync(bool deleteDatabase = false)
        {
            try
            {
                if (deleteDatabase)
                {
                    await DeleteDraftStateAsync();
                }

                // Check if setup already in progress (draft state exists)
                var existing = await GetDraftStateAsync();
                if (existing != null && !existing.IsComplete)
                {
                    GetEnvironmentVariables(existing);
                    return existing;
                }

                // Create new setup session
                var config = new SetupConfiguration
                {
                    Id = Guid.NewGuid(),
                    TenantMode = "SingleTenant",
                    CreatedAt = DateTime.UtcNow,
                    CurrentStep = 1
                };

                GetEnvironmentVariables(config);
                await SaveDraftStateAsync(config);

                logger.LogInformation("Created new setup session {SetupId}", config.Id);
                return config;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize setup");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<SetupConfiguration> GetCurrentSetupAsync()
        {
            try
            {
                // Get draft state (in-progress wizard)
                var config = await GetDraftStateAsync();

                if (config == null)
                {
                    return null;
                }

                GetEnvironmentVariables(config);

                if (!config.IsComplete)
                {
                    return config;
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get current setup");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task UpdateTenantModeAsync(Guid setupId, string tenantMode)
        {
            try
            {
                var config = await GetDraftStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.TenantMode = tenantMode;
                await SaveDraftStateAsync(config);

                logger.LogInformation("Updated tenant mode to {TenantMode} for setup {SetupId}", tenantMode, setupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update tenant mode");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<TestResult> TestDatabaseConnectionAsync(string connectionString)
        {
            try
            {
                return await databaseConnectionTester.TestConnectionAsync(connectionString);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database connection test failed");
                return new TestResult
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task UpdateDatabaseConfigAsync(Guid setupId, string connectionString)
        {
            try
            {
                var config = await GetDraftStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.DatabaseConnectionString = connectionString;
                await SaveDraftStateAsync(config);

                logger.LogInformation("Updated database configuration for setup {SetupId}", setupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update database configuration");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<TestResult> TestStorageConnectionAsync(string connectionString)
        {
            try
            {
                return await storageConnectionTester.TestConnectionAsync(connectionString);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Storage connection test failed");
                return new TestResult
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task UpdateStorageConfigAsync(Guid setupId, string storageConnectionString, string blobPublicUrl)
        {
            try
            {
                var config = await GetDraftStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.StorageConnectionString = storageConnectionString;
                config.BlobPublicUrl = blobPublicUrl;
                await SaveDraftStateAsync(config);

                logger.LogInformation("Updated storage configuration for setup {SetupId}", setupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update storage configuration");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task UpdateAdminAccountAsync(Guid setupId, string email, string password)
        {
            try
            {
                var config = await GetDraftStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.AdminEmail = email;
                config.SenderEmail = email;
                config.AdminPassword = password; // Will be hashed during completion
                await SaveDraftStateAsync(config);

                logger.LogInformation("Updated admin account for setup {SetupId}", setupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update admin account");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task UpdatePublisherConfigAsync(
            Guid setupId,
            string publisherUrl,
            bool staticWebPages,
            bool requiresAuthentication,
            string allowedFileTypes,
            string microsoftAppId,
            string siteDesignId,
            string title)
        {
            try
            {
                var config = await GetDraftStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.PublisherUrl = publisherUrl;
                config.StaticWebPages = staticWebPages;
                config.CosmosRequiresAuthentication = requiresAuthentication;
                config.AllowedFileTypes = allowedFileTypes;
                config.MicrosoftAppId = microsoftAppId;
                config.SiteDesignId = siteDesignId;
                config.WebsiteTitle = title;

                // If static mode, force BlobPublicUrl to "/"
                if (staticWebPages)
                {
                    config.BlobPublicUrl = "/";
                }

                await SaveDraftStateAsync(config);

                logger.LogInformation("Updated publisher configuration for setup {SetupId}", setupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update publisher configuration");
                throw;
            }
        }

        /// <summary>
        /// Populates setup configuration from environment variables and configuration.
        /// </summary>
        /// <param name="config">Setup configuration to populate.</param>
        private void GetEnvironmentVariables(SetupConfiguration config)
        {
            if (config == null)
            {
                return;
            }

            // Storage Configuration
            var storageConnectionString = configuration.GetConnectionString("StorageConnectionString");
            var blobPublicUrl = configuration.GetValue<string>("AzureBlobStorageEndPoint") ?? configuration.GetValue<string>("BlobPublicUrl");

            if (!string.IsNullOrEmpty(storageConnectionString))
            {
                config.StorageConnectionString = storageConnectionString;
                config.StoragePreConfigured = true;
            }

            if (!string.IsNullOrEmpty(blobPublicUrl))
            {
                config.BlobPublicUrl = blobPublicUrl;
                config.BlobPublicUrlPreConfigured = true;
            }

            // Publisher Configuration
            var publisherUrl = configuration["CosmosPublisherUrl"];
            var staticWebPages = configuration["CosmosStaticWebPages"];
            var cosmosRequiresAuth = configuration["CosmosRequiresAuthentication"];
            var microsoftAppId = configuration["MicrosoftAppId"];
            var allowedFileTypes = configuration["AllowedFileTypes"];

            if (!string.IsNullOrEmpty(publisherUrl))
            {
                config.PublisherUrl = publisherUrl;
                config.PublisherPreConfigured = true;
            }

            if (!string.IsNullOrEmpty(staticWebPages) && bool.TryParse(staticWebPages, out var isStatic))
            {
                config.StaticWebPages = isStatic;
                config.StaticWebPagesPreConfigured = true;
            }

            if (!string.IsNullOrEmpty(cosmosRequiresAuth) && bool.TryParse(cosmosRequiresAuth, out var requiresAuth))
            {
                config.CosmosRequiresAuthentication = requiresAuth;
                config.CosmosRequiresAuthenticationPreConfigured = true;
            }

            if (!string.IsNullOrEmpty(microsoftAppId))
            {
                config.MicrosoftAppId = microsoftAppId;
                config.MicrosoftAppIdPreConfigured = true;
            }

            if (!string.IsNullOrEmpty(allowedFileTypes))
            {
                config.AllowedFileTypes = allowedFileTypes;
                config.AllowedFileTypesPreConfigured = true;
            }

            if (!string.IsNullOrEmpty(publisherUrl))
            {
                logger.LogInformation("Publisher configuration loaded from environment variables");
            }

            // Admin Configuration
            var senderEmail = configuration["AdminEmail"] ?? configuration["SenderEmail"];
            if (!string.IsNullOrEmpty(senderEmail))
            {
                config.SenderEmail = senderEmail;
                config.SenderEmailPreConfigured = true;
            }

            // Database Configuration (optional - usually in appsettings.json)
            var dbConnectionString = configuration.GetConnectionString("ApplicationDbContextConnection");
            if (!string.IsNullOrEmpty(dbConnectionString) && string.IsNullOrEmpty(config.DatabaseConnectionString))
            {
                config.DatabaseConnectionString = dbConnectionString;
            }

            // Email Provider Configuration
            var sendGridApiKey = configuration["CosmosSendGridApiKey"];
            if (!string.IsNullOrEmpty(sendGridApiKey))
            {
                config.SendGridApiKey = sendGridApiKey;
            }

            var smtpHost = configuration["SmtpEmailProviderOptions:Host"]
                  ?? configuration["SmtpEmailProviderOptions__Host"];
            if (!string.IsNullOrEmpty(smtpHost))
            {
                config.SmtpHost = smtpHost;
            }

            var smtpPort = configuration["SmtpEmailProviderOptions:Port"]
                  ?? configuration["SmtpEmailProviderOptions__Port"];
            if (!string.IsNullOrEmpty(smtpPort))
            {
                config.SmtpPort = smtpPort;
            }

            var smtpUsername = configuration["SmtpEmailProviderOptions:UserName"]
                  ?? configuration["SmtpEmailProviderOptions__UserName"];
            if (!string.IsNullOrEmpty(smtpUsername))
            {
                config.SmtpUsername = smtpUsername;
            }

            var smtpPassword = configuration["SmtpEmailProviderOptions:Password"]
                  ?? configuration["SmtpEmailProviderOptions__Password"];
            if (!string.IsNullOrEmpty(smtpPassword))
            {
                config.SmtpPassword = smtpPassword;
            }

            var azureEmailConnectionString = configuration.GetConnectionString("AzureCommunicationConnection");

            if (!string.IsNullOrEmpty(sendGridApiKey)
                || !string.IsNullOrEmpty(smtpHost)
                || !string.IsNullOrEmpty(azureEmailConnectionString))
            {
                config.EmailProviderPreConfigured = true;
                logger.LogInformation("Email configuration loaded from environment variables");
            }

            // CloudFront Configuration
            var cloudFrontData = configuration["CloudFrontConfig"];
            if (!string.IsNullOrEmpty(cloudFrontData))
            {
                try
                {
                    var cloudFrontConfig = JsonConvert.DeserializeObject<CloudFrontConfig>(cloudFrontData);

                    if (cloudFrontConfig != null)
                    {
                        config.CloudFrontDistributionId = cloudFrontConfig.DistributionId;
                        config.CloudFrontAccessKeyId = cloudFrontConfig.AccessKeyId;
                        config.CloudFrontSecretAccessKey = cloudFrontConfig.SecretAccessKey;
                        config.CloudFrontRegion = cloudFrontConfig.Region;
                        config.CloudFrontPreConfigured = true;
                        logger.LogInformation("CloudFront configuration loaded from Secrets Manager");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load CloudFront configuration from Secrets Manager");
                }
            }
        }

        /// <summary>
        /// Retrieves draft setup state from Settings table.
        /// This is temporary data for in-progress wizard sessions.
        /// </summary>
        private async Task<SetupConfiguration> GetDraftStateAsync()
        {
            try
            {
                var setting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == DraftStateGroup && s.Name == DraftStateKey);

                if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<SetupConfiguration>(setting.Value);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to retrieve draft setup state from settings");
                return null;
            }
        }

        /// <summary>
        /// Saves draft setup state to Settings table.
        /// This is temporary data updated as wizard steps are completed.
        /// </summary>
        private async Task SaveDraftStateAsync(SetupConfiguration config)
        {
            try
            {
                var setting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == DraftStateGroup && s.Name == DraftStateKey);

                var json = JsonConvert.SerializeObject(config);

                if (setting == null)
                {
                    setting = new Setting
                    {
                        Group = DraftStateGroup,
                        Name = DraftStateKey,
                        Value = json,
                        Description = "Temporary draft state for setup wizard session",
                        IsRequired = false
                    };
                    applicationDbContext.Settings.Add(setting);
                }
                else
                {
                    setting.Value = json;
                    setting.Description = "Temporary draft state for setup wizard session";
                }

                await applicationDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save draft setup state to settings");
                throw;
            }
        }

        /// <summary>
        /// Deletes draft setup state from Settings table.
        /// Called after setup completion to clean up temporary data.
        /// </summary>
        private async Task DeleteDraftStateAsync()
        {
            try
            {
                var setting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == DraftStateGroup && s.Name == DraftStateKey);

                if (setting != null)
                {
                    applicationDbContext.Settings.Remove(setting);
                    await applicationDbContext.SaveChangesAsync();
                    logger.LogInformation("Deleted draft setup state from settings");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete draft setup state");
            }
        }

        /// <summary>
        /// Retrieves committed setup state from Settings table.
        /// This is the final configuration saved after wizard completion.
        /// </summary>
        private async Task<SetupConfiguration> GetCommittedStateAsync()
        {
            try
            {
                var setting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == CommittedStateGroup && s.Name == CommittedStateKey);

                if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<SetupConfiguration>(setting.Value);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to retrieve committed setup state from settings");
                return null;
            }
        }

        /// <summary>
        /// Saves committed setup state to Settings table.
        /// Called at the end of setup completion to persist final configuration.
        /// </summary>
        private async Task SaveCommittedStateAsync(SetupConfiguration config)
        {
            try
            {
                var setting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == CommittedStateGroup && s.Name == CommittedStateKey);

                var json = JsonConvert.SerializeObject(config);

                if (setting == null)
                {
                    setting = new Setting
                    {
                        Group = CommittedStateGroup,
                        Name = CommittedStateKey,
                        Value = json,
                        Description = "Final setup wizard configuration",
                        IsRequired = false
                    };
                    applicationDbContext.Settings.Add(setting);
                }
                else
                {
                    setting.Value = json;
                    setting.Description = "Final setup wizard configuration";
                }

                await applicationDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save committed setup state to settings");
                throw;
            }
        }

        /// <summary>
        /// Records an audit log entry for post-setup configuration changes.
        /// Multiple entries can exist (one per session), each capturing all changes made in that session.
        /// TODO: Create audit log viewer UI (admin-only) that displays these records in a flexible table format.
        ///       Design the UI to be extensible for other audit logs beyond setup changes.
        /// </summary>
        private async Task LogConfigurationChangeAsync(
            SetupConfiguration oldConfig,
            SetupConfiguration newConfig,
            string initiatedBy,
            string description)
        {
            try
            {
                var changes = CaptureConfigurationChanges(oldConfig, newConfig);

                if (!changes.Any())
                {
                    logger.LogInformation("No configuration changes detected, skipping audit log");
                    return;
                }

                var auditEntry = new SetupAuditLog
                {
                    SessionId = newConfig.Id,
                    Timestamp = DateTime.UtcNow,
                    InitiatedBy = initiatedBy,
                    Description = description,
                    Changes = changes,
                    IsInitialSetup = oldConfig == null || !oldConfig.IsComplete
                };

                var setting = new Setting
                {
                    Id = Guid.NewGuid(),
                    Group = AuditLogGroup,
                    Name = AuditLogName,
                    Value = JsonConvert.SerializeObject(auditEntry),
                    Description = description,
                    IsRequired = false
                };

                applicationDbContext.Settings.Add(setting);
                await applicationDbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Audit log recorded for setup session {SessionId}: {ChangeCount} changes by {User}",
                    newConfig.Id,
                    changes.Count,
                    initiatedBy);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to record configuration change audit log");
                // Don't throw - audit log failure should not block the operation
            }
        }

        /// <summary>
        /// Captures what changed between old and new configurations.
        /// Returns a dictionary of field name -> (old value, new value).
        /// </summary>
        private Dictionary<string, (string OldValue, string NewValue)> CaptureConfigurationChanges(
            SetupConfiguration oldConfig,
            SetupConfiguration newConfig)
        {
            var changes = new Dictionary<string, (string, string)>();

            if (oldConfig == null)
            {
                return changes; // Initial setup, not tracking individual changes
            }

            var properties = typeof(SetupConfiguration).GetProperties();

            foreach (var prop in properties)
            {
                // Skip non-public, sensitive, or metadata properties
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                var oldValue = prop.GetValue(oldConfig)?.ToString() ?? string.Empty;
                var newValue = prop.GetValue(newConfig)?.ToString() ?? string.Empty;

                // Mask sensitive values in audit log
                if (IsSensitiveProperty(prop.Name))
                {
                    oldValue = string.IsNullOrEmpty(oldValue) ? "(empty)" : "(masked)";
                    newValue = string.IsNullOrEmpty(newValue) ? "(empty)" : "(masked)";
                }

                if (oldValue != newValue)
                {
                    changes[prop.Name] = (oldValue, newValue);
                }
            }

            return changes;
        }

        /// <summary>
        /// Determines if a property contains sensitive data that should be masked in audit logs.
        /// </summary>
        private bool IsSensitiveProperty(string propertyName)
        {
            var sensitiveProperties = new[]
            {
                "AdminPassword",
                "SmtpPassword",
                "SendGridApiKey",
                "AzureEmailConnectionString",
                "DatabaseConnectionString",
                "StorageConnectionString",
                "CloudFrontSecretAccessKey",
                "CloudflareApiToken",
                "SucuriApiSecret"
            };

            return sensitiveProperties.Contains(propertyName);
        }

        /// <inheritdoc/>
        public async Task UpdateStepAsync(Guid setupId, int step)
        {
            try
            {
                var config = await GetDraftStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.CurrentStep = step;
                await SaveDraftStateAsync(config);

                logger.LogInformation("Updated current step to {Step} for setup {SetupId}", step, setupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update step");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<TestResult> TestEmailConfigAsync(
            string provider,
            string sendGridApiKey,
            string azureConnectionString,
            string smtpHost,
            string smtpPort,
            string smtpUsername,
            string smtpPassword,
            string senderEmail,
            string testRecipient)
        {
            try
            {
                return provider switch
                {
                    "SendGrid" => await TestSendGridAsync(sendGridApiKey, senderEmail, testRecipient),
                    "AzureCommunication" => await TestAzureEmailAsync(azureConnectionString, senderEmail, testRecipient),
                    "SMTP" => await TestSmtpAsync(smtpHost, smtpPort, smtpUsername, smtpPassword, senderEmail, testRecipient),
                    _ => new TestResult
                    {
                        Success = false,
                        Message = "Unknown email provider"
                    }
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email configuration test failed");
                return new TestResult
                {
                    Success = false,
                    Message = $"Test failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Tests SendGrid configuration.
        /// </summary>
        private async Task<TestResult> TestSendGridAsync(string apiKey, string senderEmail, string recipient)
        {
            try
            {
                return await sendGridEmailTester.TestAsync(apiKey, senderEmail, recipient);
            }
            catch (Exception ex)
            {
                return new TestResult
                {
                    Success = false,
                    Message = $"SendGrid test failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Tests Azure Communication Services email.
        /// </summary>
        private Task<TestResult> TestAzureEmailAsync(string connectionString, string senderEmail, string recipient)
        {
            try
            {
                return Task.FromResult(new TestResult
                {
                    Success = true,
                    Message = "Azure Communication Services configuration saved (test email not implemented)"
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new TestResult
                {
                    Success = false,
                    Message = $"Azure email test failed: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Tests SMTP configuration.
        /// </summary>
        private async Task<TestResult> TestSmtpAsync(
            string host,
            string port,
            string username,
            string password,
            string senderEmail,
            string recipient)
        {
            try
            {
                return await smtpEmailTester.TestAsync(host, port, username, password, senderEmail, recipient);
            }
            catch (Exception ex)
            {
                return new TestResult
                {
                    Success = false,
                    Message = $"SMTP test failed: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task UpdateEmailConfigAsync(
            Guid setupId,
            string provider,
            string sendGridApiKey,
            string azureConnectionString,
            string smtpHost,
            string smtpPort,
            string smtpUsername,
            string smtpPassword)
        {
            try
            {
                var config = await GetDraftStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                if (string.IsNullOrWhiteSpace(provider))
                {
                    config.SendGridApiKey = string.Empty;
                    config.AzureEmailConnectionString = string.Empty;
                    config.SmtpHost = string.Empty;
                    config.SmtpPort = string.Empty;
                    config.SmtpUsername = string.Empty;
                    config.SmtpPassword = string.Empty;
                }
                else
                {
                    config.SendGridApiKey = sendGridApiKey;
                    config.AzureEmailConnectionString = azureConnectionString;
                    config.SmtpHost = smtpHost;
                    config.SmtpPort = smtpPort;
                    config.SmtpUsername = smtpUsername;
                    config.SmtpPassword = smtpPassword;
                }


                await SaveDraftStateAsync(config);

                logger.LogInformation("Updated email configuration for setup {SetupId}", setupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update email configuration");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<SetupCompletionResult> CompleteSetupAsync(Guid setupId)
        {
            try
            {
                logger.LogInformation("Starting setup completion for {SetupId}", setupId);

                logger.LogInformation("Retrieving setup configuration...");
                var config = await GetDraftStateAsync();

                if (config?.Id != setupId)
                {
                    return new SetupCompletionResult
                    {
                        Success = false,
                        Message = "Setup configuration not found"
                    };
                }

                logger.LogInformation("✓ Setup configuration retrieved");

                // Get the main database connection string
                logger.LogInformation("Retrieving main database connection string...");
                var mainDbConnectionString = configuration.GetConnectionString("ApplicationDbContextConnection");
                if (string.IsNullOrEmpty(mainDbConnectionString))
                {
                    return new SetupCompletionResult
                    {
                        Success = false,
                        Message = "Main database connection string not found in configuration"
                    };
                }

                using var mainDbContext = new ApplicationDbContext(mainDbConnectionString);
                logger.LogInformation("✓ Database context created");

                // Validate all required fields
                logger.LogInformation("Validating setup configuration...");
                var validationResult = ValidateSetupConfiguration(config);
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                logger.LogInformation("✓ Configuration validated");

                // Step 1: Create administrator account if none exists
                var adminAccounts = await userManager.GetUsersInRoleAsync(RequiredIdentityRoles.Administrators);

                if (!adminAccounts.Any())
                {
                    logger.LogInformation("Creating administrator account...");
                    var adminResult = await CreateAdminAccountAsync(config);
                    if (!adminResult.Success)
                    {
                        return adminResult;
                    }

                    logger.LogInformation("✓ Administrator account created");
                }
                else
                {
                    logger.LogInformation("✓ Administrator account already exists, skipping creation");
                }

                // Step 2: Save settings to main database
                logger.LogInformation("Saving settings to database...");
                await SaveSettingsToDatabaseAsync(mainDbContext, config);
                logger.LogInformation("✓ Settings saved");

                // Step 3: Create default layout if none exists
                logger.LogInformation("Ensuring default layout exists...");
                await EnsureDefaultLayoutAndHomePageExistsAsync(mainDbContext, config);
                logger.LogInformation("✓ Default layout ensured");

                // Step 4: Mark setup as complete
                config.IsComplete = true;
                config.CompletedAt = DateTime.UtcNow;

                // Clear sensitive data before persisting
                config.AdminPassword = string.Empty;
                config.SendGridApiKey = string.Empty;
                config.AzureEmailConnectionString = string.Empty;
                config.SmtpPassword = string.Empty;
                config.SmtpUsername = string.Empty;
                config.StorageConnectionString = string.Empty;

                // Save committed state
                await SaveCommittedStateAsync(config);
                logger.LogInformation("✓ Committed state saved");

                // Delete draft state to clean up
                await DeleteDraftStateAsync();
                logger.LogInformation("✓ Draft state cleaned up");

                logger.LogInformation("Setup completed successfully for {SetupId}", setupId);

                return new SetupCompletionResult
                {
                    Success = true,
                    Message = "Setup completed successfully. Please login."
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to complete setup");
                return new SetupCompletionResult
                {
                    Success = false,
                    Message = $"Setup failed: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task UpdateCdnConfigAsync(
            Guid setupId,
            string azureSubscriptionId,
            string azureResourceGroup,
            string azureProfileName,
            string azureEndpointName,
            bool azureIsFrontDoor,
            string cloudflareApiToken,
            string cloudflareZoneId,
            string sucuriApiKey,
            string sucuriApiSecret,
            string cloudFrontAccessKeyId,
            string cloudFrontSecretAccessKey,
            string cloudFrontDistributionId,
            string cloudFrontRegion)
        {
            try
            {
                var config = await GetDraftStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.AzureCdnSubscriptionId = azureSubscriptionId ?? string.Empty;
                config.AzureCdnResourceGroup = azureResourceGroup ?? string.Empty;
                config.AzureCdnProfileName = azureProfileName ?? string.Empty;
                config.AzureCdnEndpointName = azureEndpointName ?? string.Empty;
                config.AzureCdnIsFrontDoor = azureIsFrontDoor;

                config.CloudflareApiToken = cloudflareApiToken ?? string.Empty;
                config.CloudflareZoneId = cloudflareZoneId ?? string.Empty;

                config.SucuriApiKey = sucuriApiKey ?? string.Empty;
                config.SucuriApiSecret = sucuriApiSecret ?? string.Empty;

                config.CloudFrontAccessKeyId = cloudFrontAccessKeyId ?? string.Empty;
                config.CloudFrontSecretAccessKey = cloudFrontSecretAccessKey ?? string.Empty;
                config.CloudFrontDistributionId = cloudFrontDistributionId ?? string.Empty;
                config.CloudFrontRegion = cloudFrontRegion ?? string.Empty;

                await SaveDraftStateAsync(config);

                logger.LogInformation("Updated CDN configuration for setup {SetupId}", setupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update CDN configuration");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ShouldSkipStepAsync(Guid setupId, int stepNumber)
        {
            try
            {
                var config = await GetDraftStateAsync();

                if (config?.Id != setupId)
                {
                    return false;
                }

                return stepNumber switch
                {
                    1 => config.StoragePreConfigured,
                    2 => !string.IsNullOrEmpty(config.DatabaseConnectionString),
                    3 => HasAdminAccount(),
                    4 => config.PublisherPreConfigured,
                    _ => false
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check if step should be skipped");
                return false;
            }
        }

        /// <summary>
        /// Checks if an administrator account already exists.
        /// Step 3 (Admin Account) should be skipped if admin exists.
        /// </summary>
        private bool HasAdminAccount()
        {
            try
            {
                var adminAccounts = userManager.GetUsersInRoleAsync(RequiredIdentityRoles.Administrators).Result;
                return adminAccounts.Any();
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task MarkRestartTriggeredAsync(Guid setupId)
        {
            try
            {
                var config = await GetDraftStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.RestartTriggered = true;
                await SaveDraftStateAsync(config);

                logger.LogInformation("Marked restart as triggered for setup {SetupId}", setupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to mark restart as triggered");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsSetupCompleteAsync()
        {
            try
            {
                // Check if AllowSetup is false (setup is complete)
                var allowSetupSetting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == "SYSTEM" && s.Name == "AllowSetup");

                if (allowSetupSetting != null && bool.TryParse(allowSetupSetting.Value, out var allowSetup))
                {
                    return !allowSetup;
                }

                // Fallback: Check committed state
                var committedState = await GetCommittedStateAsync();
                if (committedState?.IsComplete == true)
                {
                    return true;
                }

                // Legacy check: Verify key setup indicators exist
                var adminUserExists = await userManager.GetUsersInRoleAsync("Administrators");
                var layoutExists = await applicationDbContext.Layouts.CountAsync() > 0;
                var homePageExists = await applicationDbContext.Articles
                    .AnyAsync(a => a.UrlPath == "root");

                if (adminUserExists.Count > 0 && layoutExists && homePageExists)
                {
                    // Save state for future checks
                    var newState = new SetupConfiguration
                    {
                        Id = Guid.NewGuid(),
                        IsComplete = true,
                        CompletedAt = DateTime.UtcNow,
                        SenderEmail = adminUserExists.FirstOrDefault()?.Email,
                        CurrentStep = 7
                    };

                    GetEnvironmentVariables(newState);
                    await SaveCommittedStateAsync(newState);

                    logger.LogInformation("Legacy setup detected and state saved for setup {SetupId}", newState.Id);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to check setup completion status, assuming setup is required");
                return false;
            }
        }

        /// <summary>
        /// Validates setup configuration.
        /// </summary>
        private SetupCompletionResult ValidateSetupConfiguration(SetupConfiguration config)
        {
            if (string.IsNullOrEmpty(config.StorageConnectionString))
            {
                return new SetupCompletionResult
                {
                    Success = false,
                    Message = "Storage connection string is required"
                };
            }

            if (string.IsNullOrEmpty(config.SenderEmail))
            {
                return new SetupCompletionResult
                {
                    Success = false,
                    Message = "Administrator email is required"
                };
            }

            if (string.IsNullOrEmpty(config.AdminPassword))
            {
                return new SetupCompletionResult
                {
                    Success = false,
                    Message = "Administrator password is required"
                };
            }

            if (string.IsNullOrEmpty(config.PublisherUrl))
            {
                return new SetupCompletionResult
                {
                    Success = false,
                    Message = "Publisher URL is required"
                };
            }

            return new SetupCompletionResult { Success = true };
        }

        /// <summary>
        /// Creates the administrator account.
        /// </summary>
        private async Task<SetupCompletionResult> CreateAdminAccountAsync(SetupConfiguration config)
        {
            try
            {
                var user = new IdentityUser
                {
                    UserName = config.SenderEmail,
                    Email = config.SenderEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, config.AdminPassword);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return new SetupCompletionResult
                    {
                        Success = false,
                        Message = $"Failed to create admin account: {errors}"
                    };
                }

                var addToRoleResult = await SetupNewAdministrator.Ensure_RolesAndAdmin_Exists(roleManager, userManager, user);

                if (!addToRoleResult)
                {
                    return new SetupCompletionResult
                    {
                        Success = false,
                        Message = "Failed to assign admin role."
                    };
                }

                var roleResult = await userManager.AddToRoleAsync(user, "Administrators");

                if (!roleResult.Succeeded)
                {
                    logger.LogWarning("Failed to add user to Administrators role");
                }

                logger.LogInformation("Admin user {Email} added to Administrators role", config.SenderEmail);

                return new SetupCompletionResult
                {
                    Success = true,
                    Message = "Admin account created successfully"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create admin account");
                return new SetupCompletionResult
                {
                    Success = false,
                    Message = $"Failed to create admin account: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Saves settings to the main database.
        /// </summary>
        private async Task SaveSettingsToDatabaseAsync(ApplicationDbContext context, SetupConfiguration config)
        {
            try
            {
                await SaveOrUpdateSettingAsync(
                    context,
                    "EMAIL",
                    "AdminEmail",
                    config.SenderEmail,
                    "Administrator email address for system emails");

                if (!config.StoragePreConfigured)
                {
                    await SaveOrUpdateSettingAsync(
                        context,
                        "STORAGE",
                        "StorageConnectionString",
                        config.StorageConnectionString,
                        "Cloud storage connection string");
                }

                await SaveOrUpdateSettingAsync(
                    context,
                    "STORAGE",
                    "BlobPublicUrl",
                    config.BlobPublicUrl,
                    "Public URL for static assets");

                await SaveOrUpdateSettingAsync(
                    context,
                    "PUBLISHER",
                    "PublisherUrl",
                    config.PublisherUrl,
                    "Publisher website URL");

                await SaveOrUpdateSettingAsync(
                    context,
                    "PUBLISHER",
                    "StaticWebPages",
                    config.StaticWebPages.ToString(),
                    "Enable static website mode");

                await SaveOrUpdateSettingAsync(
                    context,
                    "PUBLISHER",
                    "CosmosRequiresAuthentication",
                    config.CosmosRequiresAuthentication.ToString(),
                    "Website requires authentication");

                await SaveOrUpdateSettingAsync(
                    context,
                    "PUBLISHER",
                    "AllowedFileTypes",
                    config.AllowedFileTypes,
                    "Allowed file types for upload");

                if (!string.IsNullOrEmpty(config.MicrosoftAppId))
                {
                    await SaveOrUpdateSettingAsync(
                        context,
                        "OAUTH",
                        "MicrosoftAppId",
                        config.MicrosoftAppId,
                        "Microsoft OAuth Application ID");
                }

                if (!string.IsNullOrEmpty(config.SendGridApiKey))
                {
                    await SaveOrUpdateSettingAsync(
                        context,
                        "EMAIL",
                        "SendGridApiKey",
                        config.SendGridApiKey,
                        "SendGrid API Key");
                }

                if (!string.IsNullOrEmpty(config.AzureEmailConnectionString))
                {
                    await SaveOrUpdateSettingAsync(
                        context,
                        "EMAIL",
                        "AzureEmailConnectionString",
                        config.AzureEmailConnectionString,
                        "Azure Communication Services connection string");
                }

                if (!string.IsNullOrEmpty(config.SmtpHost))
                {
                    await SaveOrUpdateSettingAsync(
                        context,
                        "EMAIL",
                        "SmtpHost",
                        config.SmtpHost,
                        "SMTP server host");

                    await SaveOrUpdateSettingAsync(
                        context,
                        "EMAIL",
                        "SmtpPort",
                        config.SmtpPort.ToString(),
                        "SMTP server port");

                    await SaveOrUpdateSettingAsync(
                        context,
                        "EMAIL",
                        "SmtpUsername",
                        config.SmtpUsername,
                        "SMTP username");

                    await SaveOrUpdateSettingAsync(
                        context,
                        "EMAIL",
                        "SmtpPassword",
                        config.SmtpPassword,
                        "SMTP password");
                }

                // Save CDN settings if fully configured
                if (!string.IsNullOrEmpty(config.AzureCdnSubscriptionId) &&
                    !string.IsNullOrEmpty(config.AzureCdnResourceGroup) &&
                    !string.IsNullOrEmpty(config.AzureCdnProfileName) &&
                    !string.IsNullOrEmpty(config.AzureCdnEndpointName))
                {
                    var azureCdnConfig = new Sky.Editor.Services.CDN.AzureCdnConfig
                    {
                        IsFrontDoor = config.AzureCdnIsFrontDoor,
                        SubscriptionId = config.AzureCdnSubscriptionId,
                        ResourceGroup = config.AzureCdnResourceGroup,
                        ProfileName = config.AzureCdnProfileName,
                        EndpointName = config.AzureCdnEndpointName
                    };

                    var azureCdnSetting = new Sky.Editor.Services.CDN.CdnSetting
                    {
                        CdnProvider = config.AzureCdnIsFrontDoor
                            ? Sky.Editor.Services.CDN.CdnProviderEnum.AzureFrontdoor
                            : Sky.Editor.Services.CDN.CdnProviderEnum.AzureCDN,
                        Value = JsonConvert.SerializeObject(azureCdnConfig)
                    };

                    await SaveOrUpdateSettingAsync(
                        context,
                        "CDN",
                        config.AzureCdnIsFrontDoor ? "AzureFrontDoor" : "AzureCDN",
                        JsonConvert.SerializeObject(azureCdnSetting),
                        config.AzureCdnIsFrontDoor ? "Azure Front Door CDN" : "Azure CDN");
                }

                if (!string.IsNullOrEmpty(config.CloudflareApiToken) &&
                    !string.IsNullOrEmpty(config.CloudflareZoneId))
                {
                    var cloudflareCdnConfig = new Sky.Editor.Services.CDN.CloudflareCdnConfig
                    {
                        ApiToken = config.CloudflareApiToken,
                        ZoneId = config.CloudflareZoneId
                    };

                    var cloudflareCdnSetting = new Sky.Editor.Services.CDN.CdnSetting
                    {
                        CdnProvider = Sky.Editor.Services.CDN.CdnProviderEnum.Cloudflare,
                        Value = JsonConvert.SerializeObject(cloudflareCdnConfig)
                    };

                    await SaveOrUpdateSettingAsync(
                        context,
                        "CDN",
                        "Cloudflare",
                        JsonConvert.SerializeObject(cloudflareCdnSetting),
                        "Cloudflare CDN");
                }

                if (!string.IsNullOrEmpty(config.SucuriApiKey) &&
                    !string.IsNullOrEmpty(config.SucuriApiSecret))
                {
                    var sucuriCdnConfig = new Sky.Editor.Services.CDN.SucuriCdnConfig
                    {
                        ApiKey = config.SucuriApiKey,
                        ApiSecret = config.SucuriApiSecret
                    };

                    var sucuriCdnSetting = new Sky.Editor.Services.CDN.CdnSetting
                    {
                        CdnProvider = Sky.Editor.Services.CDN.CdnProviderEnum.Sucuri,
                        Value = JsonConvert.SerializeObject(sucuriCdnConfig)
                    };

                    await SaveOrUpdateSettingAsync(
                        context,
                        "CDN",
                        "Sucuri",
                        JsonConvert.SerializeObject(sucuriCdnSetting),
                        "Sucuri CDN/Firewall");
                }

                // Mark setup as complete
                await SaveOrUpdateSettingAsync(
                    context,
                    "SYSTEM",
                    "AllowSetup",
                    "false",
                    "Allow setup mode");

                await context.SaveChangesAsync();

                logger.LogInformation("Settings saved to database successfully");

                // Warn about partial Azure CDN configuration
                var azureFields = new[] { config.AzureCdnSubscriptionId, config.AzureCdnResourceGroup, config.AzureCdnProfileName, config.AzureCdnEndpointName };
                var nonEmptyCount = azureFields.Count(f => !string.IsNullOrEmpty(f));
                if (nonEmptyCount > 0 && nonEmptyCount < 4)
                {
                    logger.LogWarning("Partial Azure CDN configuration detected ({Count}/4 fields populated) - Azure CDN will not be saved", nonEmptyCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save settings to database");
                throw;
            }
        }

        /// <summary>
        /// Saves or updates a setting in the database.
        /// </summary>
        private async Task SaveOrUpdateSettingAsync(
            ApplicationDbContext context,
            string group,
            string name,
            string value,
            string description)
        {
            var setting = await context.Settings
                .FirstOrDefaultAsync(s => s.Group == group && s.Name == name);

            if (setting == null)
            {
                setting = new Setting
                {
                    Id = Guid.NewGuid(),
                    Group = group,
                    Name = name,
                    Value = value,
                    Description = description,
                    IsRequired = false
                };
                context.Settings.Add(setting);
            }
            else
            {
                setting.Value = value;
                setting.Description = description;
            }

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Ensures a default layout exists.
        /// </summary>
        private async Task EnsureDefaultLayoutAndHomePageExistsAsync(ApplicationDbContext dbContext, SetupConfiguration config)
        {
            try
            {
                if (config.SiteDesignId == null)
                {
                    var layout = new Layout
                    {
                        Id = Guid.NewGuid(),
                        LayoutName = "Default Layout",
                        IsDefault = true,
                        Notes = "Default layout created by setup wizard",
                        Head = "<!-- Add your HEAD content here -->",
                        HtmlHeader = "<header>\n  <h1>Welcome to SkyCMS</h1>\n</header>",
                        FooterHtmlContent = "<footer>\n  <p>&copy; 2024 Your Company</p>\n</footer>",
                        Version = 1,
                        Published = DateTimeOffset.UtcNow,
                        LastModified = DateTimeOffset.UtcNow
                    };

                    dbContext.Layouts.Add(layout);
                    await dbContext.SaveChangesAsync();

                    logger.LogInformation("Created default layout");
                }
                else
                {
                    var layoutId = config.SiteDesignId?.ToString();

                    var layout = await layoutImportService.GetCommunityLayoutAsync(layoutId, true);
                    var communityPages = await layoutImportService.GetCommunityTemplatePagesAsync(layoutId);
                    var userId = await dbContext.Users.Select(u => u.Id).FirstOrDefaultAsync();

                    if (!await Cosmos.Common.Data.Logic.LayoutHelper.HasDefaultLayoutAsync(dbContext))
                    {
                        layout.Version = 1;
                        layout.IsDefault = true;
                        layout.Published = DateTimeOffset.UtcNow;
                        layout.LastModified = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        layout.Version = (await dbContext.Layouts.CountAsync()) + 1;
                        layout.IsDefault = false;
                    }

                    dbContext.Layouts.Add(layout);
                    await dbContext.SaveChangesAsync();

                    foreach (var page in communityPages)
                    {
                        page.LayoutId = layout.Id;
                    }

                    dbContext.Templates.AddRange(communityPages);
                    await dbContext.SaveChangesAsync();

                    var template = await dbContext.Templates.FirstOrDefaultAsync(f => f.Title.ToLower() == "home page");
                    if (template != null)
                    {
                        await SaveOrUpdateSettingAsync(
                            dbContext,
                            "SETUP",
                            "HomePageTemplateId",
                            template.Id.ToString(),
                            "Template ID for home page");
                    }

                    var createCommand = new CreateArticleCommand
                    {
                        Title = config.WebsiteTitle,
                        TemplateId = template.Id,
                        UserId = Guid.Parse(userId),
                        ArticleType = Cosmos.Cms.Common.ArticleType.General,
                        BlogKey = string.Empty,
                        Published = DateTimeOffset.UtcNow,
                        StatusCode = Cosmos.Common.Data.Logic.StatusCodeEnum.Active,
                        UrlPathOverride = "root"
                    };

                    var result = await mediator.SendAsync(createCommand);

                    if (!result.IsSuccess)
                    {
                        var errorMessage = result.ErrorMessage ??
                            string.Join(", ", result.Errors?.SelectMany(e => e.Value) ?? Array.Empty<string>());

                        logger.LogError("Failed to create home page: {Error}", errorMessage);
                        throw new InvalidOperationException($"Failed to create home page: {errorMessage}");
                    }

                    logger.LogInformation("Home page created successfully with article number {ArticleNumber}", result.Data.ArticleNumber);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create default layout");
            }
        }

        /// <summary>
        /// CloudFront configuration from Secrets Manager.
        /// </summary>
        private class CloudFrontConfig
        {
            [JsonProperty("AccessKeyId")]
            public string AccessKeyId { get; set; }

            [JsonProperty("SecretAccessKey")]
            public string SecretAccessKey { get; set; }

            [JsonProperty("DistributionId")]
            public string DistributionId { get; set; }

            [JsonProperty("Region")]
            public string Region { get; set; }
        }
    }
}
