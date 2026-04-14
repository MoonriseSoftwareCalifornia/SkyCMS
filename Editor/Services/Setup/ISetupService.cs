// <copyright file="ISetupService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;

    /// <summary>
    /// Service interface for setup wizard operations.
    /// </summary>
    public interface ISetupService
    {
        /// <summary>
        /// Initializes a new setup session.
        /// </summary>
        /// <param name="deleteDatabase">Delete the config database to start a fresh install.</param>
        /// <returns>Setup configuration.</returns>
        Task<SetupConfiguration> InitializeSetupAsync(bool deleteDatabase = false);

        /// <summary>
        /// Gets the current setup configuration.
        /// </summary>
        /// <returns>Setup configuration or null.</returns>
        Task<SetupConfiguration> GetCurrentSetupAsync();

        /// <summary>
        /// Checks if the setup process has been completed.
        /// </summary>
        /// <returns>True if setup is complete; otherwise, false.</returns>
        Task<bool> IsSetupCompleteAsync();

        /// <summary>
        /// Updates the tenant mode.
        /// </summary>
        /// <param name="setupId">Setup session ID.</param>
        /// <param name="tenantMode">Tenant mode (SingleTenant or MultiTenant).</param>
        /// <returns>Task.</returns>
        Task UpdateTenantModeAsync(Guid setupId, string tenantMode);

        /// <summary>
        /// Tests database connection.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <returns>Test result.</returns>
        Task<TestResult> TestDatabaseConnectionAsync(string connectionString);

        /// <summary>
        /// Updates database configuration.
        /// </summary>
        /// <param name="setupId">Setup session ID.</param>
        /// <param name="connectionString">Database connection string.</param>
        /// <returns>Task.</returns>
        Task UpdateDatabaseConfigAsync(Guid setupId, string connectionString);

        /// <summary>
        /// Tests storage connection.
        /// </summary>
        /// <param name="connectionString">Storage connection string.</param>
        /// <returns>Test result.</returns>
        Task<TestResult> TestStorageConnectionAsync(string connectionString);

        /// <summary>
        /// Updates storage configuration.
        /// </summary>
        /// <param name="setupId">Setup session ID.</param>
        /// <param name="storageConnectionString">Storage connection string.</param>
        /// <param name="blobPublicUrl">Blob public URL.</param>
        /// <returns>Task.</returns>
        Task UpdateStorageConfigAsync(Guid setupId, string storageConnectionString, string blobPublicUrl);

        /// <summary>
        /// Updates admin account information.
        /// </summary>
        /// <param name="setupId">Setup session ID.</param>
        /// <param name="email">Admin email.</param>
        /// <param name="password">Admin password.</param>
        /// <returns>Task.</returns>
        Task UpdateAdminAccountAsync(Guid setupId, string email, string password);

        /// <summary>
        /// Updates publisher configuration.
        /// </summary>
        /// <param name="setupId">Setup session ID.</param>
        /// <param name="publisherUrl">Publisher URL.</param>
        /// <param name="staticWebPages">Static web pages enabled.</param>
        /// <param name="requiresAuthentication">Requires authentication.</param>
        /// <param name="allowedFileTypes">Allowed file types.</param>
        /// <param name="microsoftAppId">Microsoft App ID.</param>
        /// <param name="siteDesignId">Site design ID.</param>
        /// <param name="title">Website title.</param>
        /// <returns>Task.</returns>
        Task UpdatePublisherConfigAsync(
            Guid setupId,
            string publisherUrl,
            bool staticWebPages,
            bool requiresAuthentication,
            string allowedFileTypes,
            string microsoftAppId,
            string siteDesignId,
            string title);

        /// <summary>
        /// Tests email configuration.
        /// </summary>
        /// <param name="provider">Email provider.</param>
        /// <param name="sendGridApiKey">SendGrid API key.</param>
        /// <param name="azureConnectionString">Azure Communication Services connection string.</param>
        /// <param name="smtpHost">SMTP host.</param>
        /// <param name="smtpPort">SMTP port.</param>
        /// <param name="smtpUsername">SMTP username.</param>
        /// <param name="smtpPassword">SMTP password.</param>
        /// <param name="senderEmail">Sender email.</param>
        /// <param name="testRecipient">Test recipient email.</param>
        /// <returns>Test result.</returns>
        Task<TestResult> TestEmailConfigAsync(
            string provider,
            string sendGridApiKey,
            string azureConnectionString,
            string smtpHost,
            string smtpPort,
            string smtpUsername,
            string smtpPassword,
            string senderEmail,
            string testRecipient);

        /// <summary>
        /// Updates email configuration.
        /// </summary>
        /// <param name="setupId">Setup session ID.</param>
        /// <param name="provider">Email provider.</param>
        /// <param name="sendGridApiKey">SendGrid API key.</param>
        /// <param name="azureConnectionString">Azure Communication Services connection string.</param>
        /// <param name="smtpHost">SMTP host.</param>
        /// <param name="smtpPort">SMTP port.</param>
        /// <param name="smtpUsername">SMTP username.</param>
        /// <param name="smtpPassword">SMTP password.</param>
        /// <returns>Task.</returns>
        Task UpdateEmailConfigAsync(
            Guid setupId,
            string provider,
            string sendGridApiKey,
            string azureConnectionString,
            string smtpHost,
            string smtpPort,
            string smtpUsername,
            string smtpPassword);

        /// <summary>
        /// Updates CDN configuration.
        /// </summary>
        /// <param name="setupId">Setup session ID.</param>
        /// <param name="azureSubscriptionId">Azure subscription ID.</param>
        /// <param name="azureResourceGroup">Azure resource group.</param>
        /// <param name="azureProfileName">Azure profile name.</param>
        /// <param name="azureEndpointName">Azure endpoint name.</param>
        /// <param name="azureIsFrontDoor">Use Azure Front Door.</param>
        /// <param name="cloudflareApiToken">Cloudflare API token.</param>
        /// <param name="cloudflareZoneId">Cloudflare zone ID.</param>
        /// <param name="sucuriApiKey">Sucuri API key.</param>
        /// <param name="sucuriApiSecret">Sucuri API secret.</param>
        /// <param name="cloudFrontAccessKeyId">AWS Access Key ID.</param>
        /// <param name="cloudFrontSecretAccessKey">AWS Access Key secret.</param>
        /// <param name="cloudFrontDistributionId">AWS Distribution ID.</param>
        /// <param name="cloudFrontRegion">AWS Region.</param>
        /// <returns>Task.</returns>
        Task UpdateCdnConfigAsync(
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
            string cloudFrontRegion);

        /// <summary>
        /// Updates the current step.
        /// </summary>
        /// <param name="setupId">Setup session ID.</param>
        /// <param name="step">Step number.</param>
        /// <returns>Task.</returns>
        Task UpdateStepAsync(Guid setupId, int step);

        /// <summary>
        /// Completes the setup process.
        /// </summary>
        /// <param name="setupId">Setup session ID.</param>
        /// <returns>Completion result.</returns>
        Task<SetupCompletionResult> CompleteSetupAsync(Guid setupId);

        /// <summary>
        /// Checks if a specific step should be skipped based on pre-configuration.
        /// </summary>
        /// <param name="setupId">Setup session ID.</param>
        /// <param name="stepNumber">Step number to check.</param>
        /// <returns>True if step should be skipped.</returns>
        Task<bool> ShouldSkipStepAsync(Guid setupId, int stepNumber);

        /// <summary>
        /// Marks that the restart has been triggered for this setup to prevent multiple restart attempts.
        /// </summary>
        /// <param name="setupId">Setup configuration ID.</param>
        /// <returns>Task.</returns>
        Task MarkRestartTriggeredAsync(Guid setupId);
    }

    /// <summary>
    /// Setup completion result.
    /// </summary>
    public class SetupCompletionResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether setup was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the result message.
        /// </summary>
        public string Message { get; set; }
    }
}
