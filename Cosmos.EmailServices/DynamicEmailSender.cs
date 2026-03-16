// <copyright file="DynamicEmailSender.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/MoonriseSoftwareCalifornia/CosmosCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.EmailServices
{
    using AspNetCore.Identity.FlexDb;
    using Azure.Identity;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// Dynamic email sender that resolves the appropriate email provider at runtime.
    /// Supports both single-tenant and multi-tenant scenarios with automatic fallback.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// 1. Multi-tenant: Load from tenant's database Settings table
    /// 2. Single-tenant with env vars: Load from IConfiguration (environment variables)
    /// 3. Single-tenant without env vars: Load from database Settings table
    /// 4. Fallback: NoOp sender (logs warning)
    /// 
    /// Registered as SCOPED to support per-request tenant resolution in multi-tenant mode.
    /// </remarks>
    public class DynamicEmailSender : ICosmosEmailSender
    {
        private readonly IConfiguration configuration;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly ILogger<DynamicEmailSender> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly DefaultAzureCredential? azureCredential;
        private ICosmosEmailSender? resolvedSender;
        private bool isResolved = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicEmailSender"/> class.
        /// </summary>
        /// <param name="configuration">Configuration.</param>
        /// <param name="httpContextAccessor">HTTP context accessor.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="loggerFactory">Logger service factory.</param>
        /// <param name="azureCredential">Azure credential (optional).</param>
        public DynamicEmailSender(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<DynamicEmailSender> logger,
            ILoggerFactory loggerFactory,
            DefaultAzureCredential? azureCredential = null)
        {
            this.configuration = configuration;
            this.httpContextAccessor = httpContextAccessor;
            this.logger = logger;
            this.loggerFactory = loggerFactory;
            this.azureCredential = azureCredential;
        }

        /// <inheritdoc/>
        public SendResult SendResult
        {
            get
            {
                EnsureResolved();
                return resolvedSender?.SendResult ?? new SendResult
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    Message = "Email sender not initialized"
                };
            }
        }

        /// <inheritdoc/>
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            EnsureResolved();
            await resolvedSender!.SendEmailAsync(email, subject, htmlMessage);
        }

        /// <inheritdoc/>
        public async Task SendEmailAsync(
            string emailTo,
            string subject,
            string textVersion,
            string htmlVersion,
            string? emailFrom = null)
        {
            EnsureResolved();
            await resolvedSender!.SendEmailAsync(emailTo, subject, textVersion, htmlVersion, emailFrom);
        }

        /// <summary>
        /// Ensures the email sender is resolved for the current request/tenant.
        /// Called lazily on first send operation.
        /// </summary>
        private void EnsureResolved()
        {
            if (isResolved)
            {
                return;
            }

            try
            {
                logger.LogInformation("Resolving email provider...");

                // Check if multi-tenant mode
                var isMultiTenant = configuration.GetValue<bool?>("MultiTenantEditor") ?? false;

                if (isMultiTenant)
                {
                    logger.LogInformation("Multi-tenant mode: Resolving tenant-specific email configuration");
                    resolvedSender = ResolveMultiTenantProvider();
                }
                else
                {
                    logger.LogInformation("Single-tenant mode");

                    // Check environment variables first
                    if (HasEnvironmentVariableConfig())
                    {
                        logger.LogInformation("Using email configuration from environment variables");
                        resolvedSender = ResolveFromEnvironmentVariables();
                    }
                    else
                    {
                        logger.LogInformation("No environment variables - checking database settings");
                        resolvedSender = ResolveFromDatabase();
                    }
                }

                // Fallback to NoOp if no provider resolved
                if (resolvedSender == null)
                {
                    logger.LogWarning("⚠️ No email provider configured - using NoOp sender (emails will NOT be sent)");
                    resolvedSender = new CosmosNoOpEmailSender();
                }

                isResolved = true;
                logger.LogInformation("✅ Email provider resolved: {ProviderType}", resolvedSender.GetType().Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Failed to resolve email provider - falling back to NoOp sender");
                resolvedSender = new CosmosNoOpEmailSender();
                isResolved = true;
            }
        }

        private bool HasEnvironmentVariableConfig()
        {
            var smtpHost = configuration["SmtpEmailProviderOptions:Host"];
            var azureConnection = configuration.GetConnectionString("AzureCommunicationConnection");
            var sendGridKey = configuration["CosmosSendGridApiKey"];

            return !string.IsNullOrEmpty(smtpHost)
                || !string.IsNullOrEmpty(azureConnection)
                || !string.IsNullOrEmpty(sendGridKey);
        }

        private ICosmosEmailSender? ResolveFromEnvironmentVariables()
        {
            var adminEmail = configuration["AdminEmail"];
            if (string.IsNullOrEmpty(adminEmail))
            {
                logger.LogWarning("AdminEmail not configured");
                return null;
            }

            // Try SMTP
            try
            {
                var smtpSection = configuration.GetSection("SmtpEmailProviderOptions");
                var smtpHost = smtpSection["Host"];
                var smtpPort = smtpSection["Port"];
                var smtpUserName = smtpSection["UserName"];
                var smtpPassword = smtpSection["Password"];

                if (!string.IsNullOrEmpty(smtpHost)
                    && !string.IsNullOrEmpty(smtpUserName)
                    && !string.IsNullOrEmpty(smtpPassword)
                    && int.TryParse(smtpPort, out var port) && port > 0)
                {
                    var smtpConfig = new SmtpEmailProviderOptions
                    {
                        Host = smtpHost,
                        Port = port,
                        UserName = smtpUserName,
                        Password = smtpPassword,
                        DefaultFromEmailAddress = adminEmail,
                        UsesSsl = true
                    };
                    logger.LogInformation("Using SMTP provider: {Host}:{Port}", smtpConfig.Host, smtpConfig.Port);
                    return new SmtpEmailSender(Options.Create(smtpConfig));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to configure SMTP from environment variables");
            }

            // Try Azure Communication
            var azureConnection = configuration.GetConnectionString("AzureCommunicationConnection");
            if (!string.IsNullOrEmpty(azureConnection))
            {
                logger.LogInformation("Using Azure Communication Services");
                return new AzureCommunicationEmailSender(
                    Options.Create(new AzureCommunicationEmailProviderOptions
                    {
                        ConnectionString = azureConnection,
                        DefaultFromEmailAddress = adminEmail
                    }),
                    loggerFactory.CreateLogger<AzureCommunicationEmailSender>(),
                    azureCredential ?? new DefaultAzureCredential());
            }

            // Try SendGrid
            var sendGridKey = configuration["CosmosSendGridApiKey"];
            if (!string.IsNullOrEmpty(sendGridKey))
            {
                logger.LogInformation("Using SendGrid provider");
                return new SendGridEmailSender(
                    Options.Create(new SendGridEmailProviderOptions(sendGridKey, adminEmail)),
                    loggerFactory.CreateLogger<SendGridEmailSender>());
            }

            return null;
        }

        private ICosmosEmailSender? ResolveFromDatabase()
        {
            try
            {
                var dbConnectionString = configuration.GetConnectionString("ApplicationDbContextConnection");
                if (string.IsNullOrEmpty(dbConnectionString))
                {
                    logger.LogWarning("No database connection string configured");
                    return null;
                }

                using var context = new ApplicationDbContext(dbConnectionString);
                var emailSettings = context.Settings
                    .Where(s => s.Group == "EMAIL")
                    .ToDictionary(s => s.Name, s => s.Value);

                if (!emailSettings.Any())
                {
                    logger.LogInformation("No email settings found in database");
                    return null;
                }

                var adminEmail = emailSettings.GetValueOrDefault("AdminEmail");
                if (string.IsNullOrEmpty(adminEmail))
                {
                    logger.LogWarning("AdminEmail not found in database settings");
                    return null;
                }

                logger.LogInformation("Loading email configuration from database");
                return ResolveProviderFromSettings(emailSettings, adminEmail, "single-tenant");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load email configuration from database");
                return null;
            }
        }

        private ICosmosEmailSender? ResolveMultiTenantProvider()
        {
            try
            {
                if (httpContextAccessor.HttpContext == null)
                {
                    logger.LogWarning("Multi-tenant mode but no HTTP context available");
                    return null;
                }

                // Get tenant domain
                var tenantDomain = httpContextAccessor.HttpContext.Request.Headers["x-origin-hostname"].ToString();
                if (string.IsNullOrWhiteSpace(tenantDomain))
                {
                    tenantDomain = httpContextAccessor.HttpContext.Request.Host.Host;
                }

                if (string.IsNullOrWhiteSpace(tenantDomain))
                {
                    logger.LogWarning("Cannot determine tenant domain");
                    return null;
                }

                logger.LogInformation("Resolving email configuration for tenant: {TenantDomain}", tenantDomain);

                var configDbConnectionString = configuration.GetConnectionString("ConfigDbConnectionString");
                if (string.IsNullOrEmpty(configDbConnectionString))
                {
                    logger.LogError("ConfigDbConnectionString not configured");
                    return null;
                }

                // Load tenant connection
                using var configContext = new Cosmos.DynamicConfig.DynamicConfigDbContext(
                    CosmosDbOptionsBuilder.GetDbOptions<Cosmos.DynamicConfig.DynamicConfigDbContext>(configDbConnectionString));

                var connection = configContext.Connections
                    .FirstOrDefault(c => c.DomainNames.Contains(tenantDomain));

                if (connection == null)
                {
                    logger.LogWarning("No tenant configuration found for: {TenantDomain}", tenantDomain);
                    return null;
                }

                if (string.IsNullOrEmpty(connection.DbConn))
                {
                    logger.LogWarning("Tenant {TenantDomain} has no database connection", tenantDomain);
                    return null;
                }

                // Load email settings from tenant's database
                using var tenantDbContext = new Cosmos.Common.Data.ApplicationDbContext(connection.DbConn);
                var emailSettings = tenantDbContext.Settings
                    .Where(s => s.Group == "EMAIL")
                    .ToDictionary(s => s.Name, s => s.Value);

                if (!emailSettings.Any())
                {
                    logger.LogInformation("No email settings found for tenant: {TenantDomain}", tenantDomain);
                    return null;
                }

                var adminEmail = emailSettings.GetValueOrDefault("AdminEmail") ?? connection.OwnerEmail;
                if (string.IsNullOrEmpty(adminEmail))
                {
                    logger.LogWarning("No admin email configured for tenant: {TenantDomain}", tenantDomain);
                    return null;
                }

                return ResolveProviderFromSettings(emailSettings, adminEmail, tenantDomain);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve multi-tenant email configuration");
                return null;
            }
        }

        private ICosmosEmailSender? ResolveProviderFromSettings(
            Dictionary<string, string> settings,
            string adminEmail,
            string tenantName)
        {
            // Try SMTP
            if (settings.TryGetValue("SmtpHost", out var smtpHost) && !string.IsNullOrEmpty(smtpHost))
            {
                var smtpConfig = new SmtpEmailProviderOptions
                {
                    Host = smtpHost,
                    Port = int.TryParse(settings.GetValueOrDefault("SmtpPort"), out var port) ? port : 587,
                    UserName = settings.GetValueOrDefault("SmtpUsername"),
                    Password = settings.GetValueOrDefault("SmtpPassword"),
                    DefaultFromEmailAddress = adminEmail,
                    UsesSsl = true
                };

                if (!string.IsNullOrEmpty(smtpConfig.UserName) && !string.IsNullOrEmpty(smtpConfig.Password))
                {
                    logger.LogInformation("[{TenantName}] Using SMTP provider: {Host}:{Port}",
                        tenantName, smtpConfig.Host, smtpConfig.Port);
                    return new SmtpEmailSender(Options.Create(smtpConfig));
                }
            }

            // Try Azure Communication
            if (settings.TryGetValue("AzureEmailConnectionString", out var azureConn) && !string.IsNullOrEmpty(azureConn))
            {
                logger.LogInformation("[{TenantName}] Using Azure Communication Services", tenantName);
                return new AzureCommunicationEmailSender(
                    Options.Create(new AzureCommunicationEmailProviderOptions
                    {
                        ConnectionString = azureConn,
                        DefaultFromEmailAddress = adminEmail
                    }),
                    loggerFactory.CreateLogger<AzureCommunicationEmailSender>(),
                    azureCredential ?? new DefaultAzureCredential());
            }

            // Try SendGrid
            if (settings.TryGetValue("SendGridApiKey", out var sendGridKey) && !string.IsNullOrEmpty(sendGridKey))
            {
                logger.LogInformation("[{TenantName}] Using SendGrid provider", tenantName);
                return new SendGridEmailSender(
                    Options.Create(new SendGridEmailProviderOptions(sendGridKey, adminEmail)),
                    loggerFactory.CreateLogger<SendGridEmailSender>());
            }

            return null;
        }
    }
}