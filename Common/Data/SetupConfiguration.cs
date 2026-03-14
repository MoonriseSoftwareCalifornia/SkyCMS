// <copyright file="SetupConfiguration.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Cosmos.Cms.Common.Constants;

    /// <summary>
    /// Setup configuration stored in SQLite database during installation.
    /// After successful setup, these values are persisted to the main database.
    /// </summary>
    public class SetupConfiguration
    {
        /// <summary>
        /// Gets or sets unique setup session ID.
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the tenant mode: SingleTenant or MultiTenant.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string TenantMode { get; set; } = TenantConstants.SingleTenant; // Default.

        /// <summary>
        /// Gets or sets the publisher URL.
        /// </summary>
        [Required]
        [Url]
        [Display(Name = "Publisher URL")]
        public string PublisherUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether publisher requires authentication.
        /// </summary>
        [Display(Name = "Requires Authentication")]
        public bool CosmosRequiresAuthentication { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether static web pages are enabled.
        /// </summary>
        [Display(Name = "Static Website Mode")]
        public bool StaticWebPages { get; set; } = true;

        /// <summary>
        /// Gets or sets the blob public URL (for static assets).
        /// </summary>
        [Display(Name = "Static Assets URL")]
        public string BlobPublicUrl { get; set; } = TenantConstants.DefaultBlobPublicUrl;

        /// <summary>
        /// Gets or sets the allowed file types for upload.
        /// </summary>
        [Display(Name = "Allowed File Types")]
        public string AllowedFileTypes { get; set; } = FileTypeConstants.DefaultAllowedFileTypes;

        /// <summary>
        /// Gets or sets the Microsoft App ID (for OAuth).
        /// </summary>
        [Display(Name = "Microsoft App ID")]
        public string MicrosoftAppId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the email address used for system emails.
        /// </summary>
        [Required]
        [EmailAddress]
        [Display(Name = "System Email")]
        public string SenderEmail { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the administrator email address.
        /// </summary>
        [Required]
        [EmailAddress]
        [Display(Name = "Administrator Email")]
        public string AdminEmail { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the administrator password (hashed).
        /// </summary>
        [Display(Name = "Administrator Password")]
        public string AdminPassword { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the database connection string (stored in secrets after setup).
        /// </summary>
        [Required]
        [Display(Name = "Database Connection String")]
        public string DatabaseConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the storage connection string.
        /// </summary>
        [Required]
        [Display(Name = "Storage Connection String")]
        public string StorageConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SendGrid API key (optional).
        /// </summary>
        [Display(Name = "SendGrid API Key")]
        public string SendGridApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Azure Communication Services connection string (optional).
        /// </summary>
        [Display(Name = "Azure Communication Services Connection String")]
        public string AzureEmailConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SMTP host (optional).
        /// </summary>
        [Display(Name = "SMTP Host")]
        public string SmtpHost { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SMTP port (optional).
        /// </summary>
        [Display(Name = "SMTP Port")]
        public string SmtpPort { get; set; } = "587";

        /// <summary>
        /// Gets or sets the SMTP username (optional).
        /// </summary>
        [Display(Name = "SMTP Username")]
        public string SmtpUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SMTP password (optional).
        /// </summary>
        [Display(Name = "SMTP Password")]
        public string SmtpPassword { get; set; } = string.Empty;

        // ============================================
        // CDN Configuration (Optional)
        // ============================================

        /// <summary>
        /// Gets or sets the Azure CDN/Front Door subscription ID (optional).
        /// </summary>
        [Display(Name = "Azure Subscription ID")]
        public string AzureCdnSubscriptionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Azure CDN/Front Door resource group (optional).
        /// </summary>
        [Display(Name = "Azure Resource Group")]
        public string AzureCdnResourceGroup { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Azure CDN/Front Door profile name (optional).
        /// </summary>
        [Display(Name = "Azure Profile Name")]
        public string AzureCdnProfileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Azure CDN/Front Door endpoint name (optional).
        /// </summary>
        [Display(Name = "Azure Endpoint Name")]
        public string AzureCdnEndpointName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether Azure Front Door is used (vs Azure CDN).
        /// </summary>
        [Display(Name = "Use Azure Front Door")]
        public bool AzureCdnIsFrontDoor { get; set; } = false;

        /// <summary>
        /// Gets or sets the Cloudflare API token (optional).
        /// </summary>
        [Display(Name = "Cloudflare API Token")]
        public string CloudflareApiToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Cloudflare zone ID (optional).
        /// </summary>
        [Display(Name = "Cloudflare Zone ID")]
        public string CloudflareZoneId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Sucuri API key (optional).
        /// </summary>
        [Display(Name = "Sucuri API Key")]
        public string SucuriApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Sucuri API secret (optional).
        /// </summary>
        [Display(Name = "Sucuri API Secret")]
        public string SucuriApiSecret { get; set; } = string.Empty;

        // ============================================
        // AWS CloudFront Configuration (Optional)
        // ============================================

        /// <summary>
        /// Gets or sets the CloudFront distribution ID (optional).
        /// </summary>
        [Display(Name = "CloudFront Distribution ID")]
        public string CloudFrontDistributionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CloudFront access key ID (optional).
        /// </summary>
        [Display(Name = "CloudFront Access Key ID")]
        public string CloudFrontAccessKeyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CloudFront secret access key (optional).
        /// </summary>
        [Display(Name = "CloudFront Secret Access Key")]
        public string CloudFrontSecretAccessKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CloudFront region (optional).
        /// </summary>
        [Display(Name = "CloudFront Region")]
        public string CloudFrontRegion { get; set; } = "us-east-1";

        /// <summary>
        /// Gets or sets a value indicating whether CloudFront was pre-configured via environment variables.
        /// </summary>
        public bool CloudFrontPreConfigured { get; set; } = false;

        /// <summary>
        /// Gets or sets the current wizard step (for resuming setup).
        /// </summary>
        public int CurrentStep { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating whether setup is complete.
        /// </summary>
        public bool IsComplete { get; set; } = false;

        /// <summary>
        /// Gets or sets the timestamp when setup started.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the timestamp when setup was completed.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the layout ID for community sites (if applicable).
        /// </summary>
        public Guid? CommunityLayoutId { get; set; }

        /// <summary>
        /// Gets or sets the site design ID for community sites (if applicable).
        /// </summary>
        public string SiteDesignId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the website title.
        /// </summary>
        public string WebsiteTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether storage was pre-configured via environment variables.
        /// </summary>
        public bool StoragePreConfigured { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether publisher URL were pre-configured via environment variables.
        /// </summary>
        public bool PublisherPreConfigured { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether system sender email account was pre-configured via environment variables.
        /// </summary>
        public bool SenderEmailPreConfigured { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether email provider was pre-configured via environment variables.
        /// </summary>
        public bool EmailProviderPreConfigured { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether blob public URL was pre-configured via environment variables.
        /// </summary>
        public bool BlobPublicUrlPreConfigured { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether CosmosRequiresAuthentication was pre-configured via environment variables.
        /// </summary>
        public bool CosmosRequiresAuthenticationPreConfigured { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether MicrosoftAppId was pre-configured via environment variables.
        /// </summary>
        public bool MicrosoftAppIdPreConfigured { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether StaticWebPages was pre-configured via environment variables.
        /// </summary>
        public bool StaticWebPagesPreConfigured { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether AllowedFileTypes was pre-configured via environment variables.
        /// </summary>
        public bool AllowedFileTypesPreConfigured { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether restart has been triggered.
        /// </summary>
        public bool RestartTriggered { get; set; }
    }
}
