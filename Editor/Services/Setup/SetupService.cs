// <copyright file="SetupService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System;
    using System.Linq;
    using System.Net.Mail;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Cms.Data;
    using Cosmos.Common.Data;
    using Cosmos.Editor.Services;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Articles.Save;
    using Sky.Editor.Features.Shared;
    using Sky.Editor.Services.Layouts;

    /// <summary>
    /// Service for setup wizard operations.
    /// </summary>
    public class SetupService : ISetupService
    {
        private const string SetupStateKey = "SETUP_WIZARD_STATE";
        private const string SetupStateGroup = "SYSTEM";

        private readonly IConfiguration configuration;
        private readonly ILogger<SetupService> logger;
        private readonly IMemoryCache memoryCache;
        private readonly ILayoutImportService layoutImportService;
        private readonly IMediator mediator;

        private readonly UserManager<IdentityUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly ApplicationDbContext applicationDbContext;
        private readonly ArticleEditLogic articleEditLogic;

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
        /// <param name="articleEditLogic">Article edit logic.</param>
        /// <param name="mediator">Mediator.</param>
        public SetupService(
            IConfiguration configuration,
            ILogger<SetupService> logger,
            IMemoryCache memoryCache,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext applicationDbContext,
            ILayoutImportService layoutImportService,
            ArticleEditLogic articleEditLogic,
            IMediator mediator)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.memoryCache = memoryCache;
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.applicationDbContext = applicationDbContext;
            this.layoutImportService = layoutImportService;
            this.articleEditLogic = articleEditLogic;
            this.mediator = mediator;
        }

        /// <inheritdoc/>
        public async Task<SetupConfiguration> InitializeSetupAsync(bool deleteDatabase = false)
        {
            try
            {
                if (deleteDatabase)
                {
                    await DeleteSetupStateAsync();
                }

                // Check if setup already in progress
                var existing = await GetSetupStateAsync();
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
                await SaveSetupStateAsync(config);

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
                var config = await GetSetupStateAsync();

                if (config == null)
                {
                    config = new SetupConfiguration();
                }

                GetEnvironmentVariables(config);

                if (config != null && !config.IsComplete)
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
                var config = await GetSetupStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.TenantMode = tenantMode;
                await SaveSetupStateAsync(config);

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
                // Test connection by creating a temporary context
                using var context = new ApplicationDbContext(connectionString);
                var canConnect = await context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    return new TestResult
                    {
                        Success = false,
                        Message = "Unable to connect to database"
                    };
                }

                var dbStatus = ApplicationDbContext.EnsureDatabaseExists(connectionString);

                return new TestResult
                {
                    Success = dbStatus == DbStatus.ExistsWithNoUsers, // Expecting a new database with no users.
                    Message = $"Database connection successful. Status: {dbStatus}",
                    Status = dbStatus
                };

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
                var config = await GetSetupStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.DatabaseConnectionString = connectionString;
                await SaveSetupStateAsync(config);

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
                var storageContext = new StorageContext(connectionString, memoryCache);

                // Enable static website to ensure proper configuration for Azure, AWS S3, etc. does nothing.
                await storageContext.EnableAzureStaticWebsite();

                // Test by listing root directory
                var result = await storageContext.GetFilesAndDirectories("/");

                if (result == null)
                {
                    return new TestResult
                    {
                        Success = false,
                        Message = "Unable to connect to storage"
                    };
                }

                return new TestResult
                {
                    Success = true,
                    Message = $"Storage connection successful. Found {result.Count} items in root."
                };
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
                var config = await GetSetupStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.StorageConnectionString = storageConnectionString;
                config.BlobPublicUrl = blobPublicUrl;
                await SaveSetupStateAsync(config);

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
                var config = await GetSetupStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.SenderEmail = email;
                config.AdminPassword = password; // Will be hashed during completion
                await SaveSetupStateAsync(config);

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
                var config = await GetSetupStateAsync();
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

                await SaveSetupStateAsync(config);

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

            // Storage connection.
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
            // Only overwrite if not already set to preserve values from UpdateDatabaseConfigAsync
            var dbConnectionString = configuration.GetConnectionString("ApplicationDbContextConnection");
            if (!string.IsNullOrEmpty(dbConnectionString) && string.IsNullOrEmpty(config.DatabaseConnectionString))
            {
                config.DatabaseConnectionString = dbConnectionString;
            }

            // Detect if one of an Email provider is preconfigured.
            // Only overwrite if environment variable is set to preserve database values
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
            // Pull CDN configuration from Environment Variables
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
        /// Retrieves setup state from Settings table.
        /// </summary>
        private async Task<SetupConfiguration> GetSetupStateAsync()
        {
            try
            {
                var setting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == SetupStateGroup && s.Name == SetupStateKey);

                if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<SetupConfiguration>(setting.Value);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to retrieve setup state from settings");
                return null;
            }
        }

        /// <summary>
        /// Saves setup state to Settings table.
        /// </summary>
        private async Task SaveSetupStateAsync(SetupConfiguration config)
        {
            try
            {
                var setting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == SetupStateGroup && s.Name == SetupStateKey);

                var json = JsonConvert.SerializeObject(config);

                if (setting == null)
                {
                    setting = new Setting
                    {
                        Group = SetupStateGroup,
                        Name = SetupStateKey,
                        Value = json
                    };
                    applicationDbContext.Settings.Add(setting);
                }
                else
                {
                    setting.Value = json;
                    applicationDbContext.Settings.Update(setting);
                }

                await applicationDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save setup state to settings");
                throw;
            }
        }

        /// <summary>
        /// Deletes setup state from Settings table.
        /// </summary>
        private async Task DeleteSetupStateAsync()
        {
            try
            {
                var setting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == SetupStateGroup && s.Name == SetupStateKey);

                if (setting != null)
                {
                    applicationDbContext.Settings.Remove(setting);
                    await applicationDbContext.SaveChangesAsync();
                    logger.LogInformation("Deleted setup state from settings");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete setup state");
            }
        }

        /// <inheritdoc/>
        public async Task UpdateStepAsync(Guid setupId, int step)
        {
            try
            {
                var config = await GetSetupStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.CurrentStep = step;
                await SaveSetupStateAsync(config);

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
                switch (provider)
                {
                    case "SendGrid":
                        return await TestSendGridAsync(sendGridApiKey, senderEmail, testRecipient);

                    case "AzureCommunication":
                        return await TestAzureEmailAsync(azureConnectionString, senderEmail, testRecipient);

                    case "SMTP":
                        return await TestSmtpAsync(smtpHost, smtpPort, smtpUsername, smtpPassword, senderEmail, testRecipient);

                    default:
                        return new TestResult
                        {
                            Success = false,
                            Message = "Unknown email provider"
                        };
                }
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
                var client = new SendGrid.SendGridClient(apiKey);
                var from = new SendGrid.Helpers.Mail.EmailAddress(senderEmail, "SkyCMS Setup");
                var to = new SendGrid.Helpers.Mail.EmailAddress(recipient);
                var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(
                    from,
                    to,
                    "SkyCMS Setup Test Email",
                    "This is a test email from SkyCMS setup wizard.",
                    "<p>This is a test email from SkyCMS setup wizard.</p>");

                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    return new TestResult
                    {
                        Success = true,
                        Message = $"Test email sent successfully to {recipient}"
                    };
                }

                var body = await response.Body.ReadAsStringAsync();
                return new TestResult
                {
                    Success = false,
                    Message = $"SendGrid returned status {response.StatusCode}: {body}"
                };
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
                // Azure Communication Services email testing
                // Note: This requires Azure.Communication.Email package
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
                using var client = new SmtpClient(host, int.Parse(port));
                client.EnableSsl = port == "587" || port == "465";
                client.UseDefaultCredentials = false;
                client.Credentials = new System.Net.NetworkCredential(username, password);

                var message = new MailMessage(senderEmail, recipient)
                {
                    Subject = "SkyCMS Setup Test Email",
                    Body = "This is a test email from SkyCMS setup wizard.",
                    IsBodyHtml = false
                };

                await client.SendMailAsync(message);

                return new TestResult
                {
                    Success = true,
                    Message = $"Test email sent successfully to {recipient}"
                };
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
                var config = await GetSetupStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.SendGridApiKey = sendGridApiKey;
                config.AzureEmailConnectionString = azureConnectionString;
                config.SmtpHost = smtpHost;
                config.SmtpPort = smtpPort;
                config.SmtpUsername = smtpUsername;
                config.SmtpPassword = smtpPassword;

                await SaveSetupStateAsync(config);

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
                var config = await GetSetupStateAsync();

                if (config?.Id != setupId)
                {
                    return new SetupCompletionResult
                    {
                        Success = false,
                        Message = "Setup configuration not found"
                    };
                }

                logger.LogInformation("\t Successful.");

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

                // Create main database context
                using var mainDbContext = new ApplicationDbContext(mainDbConnectionString);

                // Database schema is already initialized during application startup by SingleTenant.Configure() or MultiTenant.Configure()
                logger.LogInformation("Database schema already initialized during application startup");

                logger.LogInformation("\t Successful.");

                // Validate all required fields
                logger.LogInformation("Validating setup configuration...");
                var validationResult = ValidateSetupConfiguration(config);
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                logger.LogInformation("\t Successful.");


                // Step 1: Create administrator account--if one does not exist yet.
                var adminAccounts = await userManager.GetUsersInRoleAsync(RequiredIdentityRoles.Administrators);

                if (!adminAccounts.Any())
                {
                    logger.LogInformation("Creating administrator account");
                    var adminResult = await CreateAdminAccountAsync(config);
                    if (!adminResult.Success)
                    {
                        return adminResult;
                    }
                }
                else
                {
                    logger.LogInformation("Administrator account already exists, skipping creation");
                }

                // Step 2: Save settings to main database
                logger.LogInformation("Saving settings to database...");
                await SaveSettingsToDatabaseAsync(mainDbContext, config);

                logger.LogInformation("\t Successful.");

                // Step 3: Create default layout if none exists
                logger.LogInformation("Ensuring default layout exists");
                await EnsureDefaultLayoutAndHomePageExistsAsync(mainDbContext, config);

                logger.LogInformation("\t Successful.");

                // Step 4: Mark setup as complete
                config.IsComplete = true;
                config.CompletedAt = DateTime.UtcNow;

                // Clear sensitive data
                config.AdminPassword = string.Empty;
                config.SendGridApiKey = string.Empty;
                config.AzureEmailConnectionString = string.Empty;
                config.SmtpPassword = string.Empty;
                config.SmtpUsername = string.Empty;
                config.StorageConnectionString = string.Empty;

                await SaveSetupStateAsync(config);

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
                var config = await GetSetupStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                // Update Azure CDN/Front Door settings
                config.AzureCdnSubscriptionId = azureSubscriptionId ?? string.Empty;
                config.AzureCdnResourceGroup = azureResourceGroup ?? string.Empty;
                config.AzureCdnProfileName = azureProfileName ?? string.Empty;
                config.AzureCdnEndpointName = azureEndpointName ?? string.Empty;
                config.AzureCdnIsFrontDoor = azureIsFrontDoor;

                // Update Cloudflare settings
                config.CloudflareApiToken = cloudflareApiToken ?? string.Empty;
                config.CloudflareZoneId = cloudflareZoneId ?? string.Empty;

                // Update Sucuri settings
                config.SucuriApiKey = sucuriApiKey ?? string.Empty;
                config.SucuriApiSecret = sucuriApiSecret ?? string.Empty;

                // Update CloudFront settings
                config.CloudFrontAccessKeyId = cloudFrontAccessKeyId ?? string.Empty;
                config.CloudFrontSecretAccessKey = cloudFrontSecretAccessKey ?? string.Empty;
                config.CloudFrontDistributionId = cloudFrontDistributionId ?? string.Empty;
                config.CloudFrontRegion = cloudFrontRegion ?? string.Empty;

                await SaveSetupStateAsync(config);

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
                var config = await GetSetupStateAsync();

                if (config?.Id != setupId)
                {
                    return false;
                }

                return stepNumber switch
                {
                    1 => config.StoragePreConfigured, // Skip Step 1 (Storage) if pre-configured
                    2 => !string.IsNullOrEmpty(config.DatabaseConnectionString), // Skip Step 2 (Database) if pre-configured
                    3 => config.SenderEmailPreConfigured, // Skip Step 3 (Admin) if pre-configured
                    4 => config.PublisherPreConfigured, // Skip Step 4 (Publisher) if pre-configured
                    _ => false // Never skip email, CDN, or completion steps
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check if step should be skipped");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task MarkRestartTriggeredAsync(Guid setupId)
        {
            try
            {
                var config = await GetSetupStateAsync();
                if (config?.Id != setupId)
                {
                    throw new InvalidOperationException($"Setup configuration {setupId} not found");
                }

                config.RestartTriggered = true;
                await SaveSetupStateAsync(config);

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
                // Check if the AllowSetup setting is false (which means setup is complete)
                var allowSetupSetting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == "SYSTEM" && s.Name == "AllowSetup");

                if (allowSetupSetting != null && bool.TryParse(allowSetupSetting.Value, out var allowSetup))
                {
                    // If AllowSetup is false, setup is complete
                    return !allowSetup;
                }

                // Fallback: Check if setup state exists and is marked as complete
                var setupState = await GetSetupStateAsync();

                if (setupState != null)
                {
                    return setupState.IsComplete;
                }

                // For legacy setups where no state exists, check if any settings exist that would indicate setup has been completed.
                // Check for a administrator account.
                var adminUserExists = await userManager.GetUsersInRoleAsync("Administrators");
                var layoutExists = await applicationDbContext.Layouts.CountAsync();
                var homePageExists = await applicationDbContext.Articles.FirstOrDefaultAsync(a => a.UrlPath == "root");
                var publishedPagesExist = await applicationDbContext.Pages.FirstOrDefaultAsync(p => p.UrlPath == "root");

                // If these exist, we can assume setup is complete.
                if (adminUserExists.Count > 0 && layoutExists > 0 && (homePageExists != null || publishedPagesExist != null))
                {
                    // Save setup state for future checks.
                    var newState = new SetupConfiguration
                    {
                        Id = Guid.NewGuid(),
                        IsComplete = true,
                        CompletedAt = DateTime.UtcNow,
                        SenderEmail = adminUserExists.FirstOrDefault()?.Email,
                        CurrentStep = 7 // Final step
                    };

                    // Populate from environment variables
                    GetEnvironmentVariables(newState);

                    // Save the state
                    await SaveSetupStateAsync(newState);

                    logger.LogInformation("Legacy setup detected and state saved for setup {SetupId}", newState.Id);

                    return true;
                }

                // No setup state found - assume setup is required
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
                // Create user
                var user = new IdentityUser
                {
                    UserName = config.SenderEmail,
                    Email = config.SenderEmail,
                    EmailConfirmed = true // Auto-confirm for first admin
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

                // Add to Administrators role
                var addToRoleResult = await SetupNewAdministrator.Ensure_RolesAndAdmin_Exists(roleManager, userManager, user);

                if (!addToRoleResult)
                {
                    return new SetupCompletionResult
                    {
                        Success = false,
                        Message = $"Failed to assign admin role."
                    };
                }
                else
                {
                    logger.LogInformation("Admin user {Email} assigned to Administrators role", config.SenderEmail);
                }

                // Add user to Administrators role
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
                // ✅ NEW: Save AdminEmail for email services
                await SaveOrUpdateSettingAsync(
                    context,
                    "EMAIL",
                    "AdminEmail",
                    config.SenderEmail,
                    "Administrator email address for system emails");

                // Save storage connection string
                if (!config.StoragePreConfigured)
                {
                    await SaveOrUpdateSettingAsync(
                        context,
                        "STORAGE",
                        "StorageConnectionString",
                        config.StorageConnectionString,
                        "Cloud storage connection string");
                }

                // Save blob public URL
                await SaveOrUpdateSettingAsync(
                    context,
                    "STORAGE",
                    "BlobPublicUrl",
                    config.BlobPublicUrl,
                    "Public URL for static assets");

                // Save publisher URL
                await SaveOrUpdateSettingAsync(
                    context,
                    "PUBLISHER",
                    "PublisherUrl",
                    config.PublisherUrl,
                    "Publisher website URL");

                // Save static web pages flag
                await SaveOrUpdateSettingAsync(
                    context,
                    "PUBLISHER",
                    "StaticWebPages",
                    config.StaticWebPages.ToString(),
                    "Enable static website mode");

                // Save requires authentication flag
                await SaveOrUpdateSettingAsync(
                    context,
                    "PUBLISHER",
                    "CosmosRequiresAuthentication",
                    config.CosmosRequiresAuthentication.ToString(),
                    "Website requires authentication");

                // Save allowed file types
                await SaveOrUpdateSettingAsync(
                    context,
                    "PUBLISHER",
                    "AllowedFileTypes",
                    config.AllowedFileTypes,
                    "Allowed file types for upload");

                // Save Microsoft App ID if provided
                if (!string.IsNullOrEmpty(config.MicrosoftAppId))
                {
                    await SaveOrUpdateSettingAsync(
                        context,
                        "OAUTH",
                        "MicrosoftAppId",
                        config.MicrosoftAppId,
                        "Microsoft OAuth Application ID");
                }

                // Save email settings if configured
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

                // Save CDN settings if configured
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

                // IMPORTANT: Set AllowSetup to false
                await SaveOrUpdateSettingAsync(
                    context,
                    "SYSTEM",
                    "AllowSetup",
                    "false",
                    "Allow setup mode");

                await context.SaveChangesAsync();

                logger.LogInformation("Settings saved to database successfully");

                // Check for partial Azure CDN configuration
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

            // Save changes.
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

                    // Create the home page
                    // REFACTORED: Use CreateArticleCommand instead of CreateArticle + SaveArticle
                    var createCommand = new CreateArticleCommand
                    {
                        Title = config.WebsiteTitle,
                        TemplateId = template.Id,
                        UserId = Guid.Parse(userId),
                        ArticleType = Cosmos.Cms.Common.ArticleType.General,
                        BlogKey = string.Empty,
                        
                        // Special home page properties
                        Published = DateTimeOffset.UtcNow, // Auto-publish
                        StatusCode = Cosmos.Common.Data.Logic.StatusCodeEnum.Active,     // Active status
                        UrlPathOverride = "root" // Home page must be "root"
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

                    logger.LogInformation($"Using site design: '{layout.LayoutName}'. Home page created successfully.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create default layout");

                // Don't throw - this is not critical for setup completion
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
