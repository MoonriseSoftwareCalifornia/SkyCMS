// <copyright file="Step5a_Cdn.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Areas.Setup.Pages
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Setup;

    /// <summary>
    /// Setup wizard step 5a: CDN configuration (optional).
    /// </summary>
    public class Step5_Cdn : PageModel
    {
        private readonly ISetupService setupService;
        private readonly ISetupCheckService setupCheckService;
        private readonly ILogger<Step5_Cdn> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Step5_Cdn"/> class.
        /// </summary>
        /// <param name="setupService">Setup service.</param>
        /// <param name="setupCheckService">Setup check service.</param>
        /// <param name="logger">Logger.</param>
        public Step5_Cdn(ISetupService setupService, ISetupCheckService setupCheckService, ILogger<Step5_Cdn> logger)
        {
            this.setupService = setupService;
            this.setupCheckService = setupCheckService;
            this.logger = logger;
        }

        /// <summary>
        /// Gets or sets the setup session ID.
        /// </summary>
        [BindProperty]
        public Guid SetupId { get; set; }

        /// <summary>
        /// Gets or sets the selected CDN provider.
        /// </summary>
        [BindProperty]
        public string SelectedProvider { get; set; } = "None";

        /// <summary>
        /// Gets or sets the Azure subscription ID.
        /// </summary>
        [BindProperty]
        [Display(Name = "Azure Subscription ID")]
        public string AzureSubscriptionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Azure resource group name.
        /// </summary>
        [BindProperty]
        [Display(Name = "Resource Group")]
        public string AzureResourceGroup { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Azure CDN profile name.
        /// </summary>
        [BindProperty]
        [Display(Name = "Profile Name")]
        public string AzureProfileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Azure CDN endpoint name.
        /// </summary>
        [BindProperty]
        [Display(Name = "Endpoint Name")]
        public string AzureEndpointName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to use Azure Front Door instead of Azure CDN.
        /// </summary>
        [BindProperty]
        [Display(Name = "Use Front Door (instead of Azure CDN)")]
        public bool AzureIsFrontDoor { get; set; } = false;

        /// <summary>
        /// Gets or sets the Cloudflare API token.
        /// </summary>
        [BindProperty]
        [Display(Name = "API Token")]
        public string CloudflareApiToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Cloudflare zone ID.
        /// </summary>
        [BindProperty]
        [Display(Name = "Zone ID")]
        public string CloudflareZoneId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AWS access key ID.
        /// </summary>
        [BindProperty]
        [Display(Name = "Access ID")]
        public string CloudFrontAccessKeyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AWS secret access key.
        /// </summary>
        [BindProperty]
        [Display(Name = "Access Key")]
        public string CloudFrontSecretAccessKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CloudFront distribution ID.
        /// </summary>
        [BindProperty]
        [Display(Name = "Distribution ID")]
        public string CloudFrontDistributionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AWS region (e.g., us-east-1).
        /// </summary>
        [BindProperty]
        [Display(Name = "AWS Region")]
        public string CloudFrontRegion { get; set; } = "us-east-1";

        /// <summary>
        /// Gets or sets the Sucuri API key.
        /// </summary>
        [BindProperty]
        [Display(Name = "API Key")]
        public string SucuriApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Sucuri API secret.
        /// </summary>
        [BindProperty]
        [Display(Name = "API Secret")]
        public string SucuriApiSecret { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the success message.
        /// </summary>
        public string SuccessMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether any CDN configuration is pre-configured.
        /// </summary>
        public bool IsPreconfigured => AzurePreConfigured || CloudflarePreConfigured || SucuriPreConfigured || CloudFrontPreConfigured;

        /// <summary>
        /// Gets a value indicating whether Azure CDN configuration is pre-configured.
        /// </summary>
        public bool AzurePreConfigured { get; private set; } = false;

        /// <summary>
        /// Gets a value indicating whether Cloudflare has been pre-configured for this instance.
        /// </summary>
        public bool CloudflarePreConfigured { get; private set; } = false;

        /// <summary>
        /// Gets a value indicating whether Sucuri has been pre-configured for this instance.
        /// </summary>
        public bool SucuriPreConfigured { get; private set; } = false;

        /// <summary>
        /// Gets a value indicating whether CloudFront has been pre-configured for this instance.
        /// </summary>
        public bool CloudFrontPreConfigured { get; private set; } = false;

        /// <summary>
        /// Handles GET requests.
        /// </summary>
        /// <returns>Page result.</returns>
        public async Task<IActionResult> OnGetAsync()
        {
            // Check if setup has been completed
            if (await setupCheckService.IsSetup())
            {
                // Redirect to setup page
                Response.Redirect("/");
            }

            var config = await setupService.GetCurrentSetupAsync();
            if (config == null)
            {
                return RedirectToPage("./Index");
            }

            SetupId = config.Id;

            // Load existing CDN configuration if any
            if (!string.IsNullOrEmpty(config.AzureCdnSubscriptionId) &&
                !string.IsNullOrEmpty(config.AzureCdnResourceGroup) &&
                !string.IsNullOrEmpty(config.AzureCdnProfileName) &&
                !string.IsNullOrEmpty(config.AzureCdnEndpointName))
            {
                SelectedProvider = "Azure";
                AzureSubscriptionId = config.AzureCdnSubscriptionId;
                AzureResourceGroup = config.AzureCdnResourceGroup;
                AzureProfileName = config.AzureCdnProfileName;
                AzureEndpointName = config.AzureCdnEndpointName;
                AzureIsFrontDoor = config.AzureCdnIsFrontDoor;
                AzurePreConfigured = true;
            }
            else if (!string.IsNullOrEmpty(config.CloudflareApiToken))
            {
                SelectedProvider = "Cloudflare";
                CloudflareApiToken = config.CloudflareApiToken;
                CloudflareZoneId = config.CloudflareZoneId;
                CloudflarePreConfigured = true;
            }
            else if (!string.IsNullOrEmpty(config.SucuriApiKey))
            {
                SelectedProvider = "Sucuri";
                SucuriApiKey = config.SucuriApiKey;
                SucuriApiSecret = config.SucuriApiSecret;
                SucuriPreConfigured = true;
            }
            else if (!string.IsNullOrEmpty(config.CloudFrontDistributionId) &&
                     !string.IsNullOrEmpty(config.CloudFrontAccessKeyId) &&
                     !string.IsNullOrEmpty(config.CloudFrontSecretAccessKey) &&
                     !string.IsNullOrEmpty(config.CloudFrontRegion))
            {
                SelectedProvider = "Cloudfront";
                CloudFrontAccessKeyId = config.CloudFrontAccessKeyId;
                CloudFrontSecretAccessKey = config.CloudFrontSecretAccessKey;
                CloudFrontDistributionId = config.CloudFrontDistributionId;
                CloudFrontRegion = config.CloudFrontRegion;
                CloudFrontPreConfigured = true;
            }

            return Page();
        }

        /// <summary>
        /// Handles POST requests to save and continue.
        /// </summary>
        /// <returns>Redirect to next step.</returns>
        public async Task<IActionResult> OnPostAsync()
        {
            logger.LogInformation("Step5_Cdn POST - SetupId: {SetupId}, SelectedProvider: {Provider}", 
                SetupId, SelectedProvider);

            // Check if setup has been completed
            if (await setupCheckService.IsSetup())
            {
                logger.LogWarning("Step5_Cdn POST - Setup already completed, redirecting to home");
                Response.Redirect("/");
            }

            try
            {
                // Validate based on selected provider
                if (SelectedProvider == "Azure")
                {
                    if (string.IsNullOrEmpty(AzureSubscriptionId) ||
                        string.IsNullOrEmpty(AzureResourceGroup) ||
                        string.IsNullOrEmpty(AzureProfileName) ||
                        string.IsNullOrEmpty(AzureEndpointName))
                    {
                        logger.LogError("Step5_Cdn POST - Azure CDN validation failed - missing required fields");
                        ErrorMessage = "All Azure CDN fields are required when Azure is selected.";
                        return Page();
                    }
                }
                else if (SelectedProvider == "Cloudflare")
                {
                    if (string.IsNullOrEmpty(CloudflareApiToken) ||
                        string.IsNullOrEmpty(CloudflareZoneId))
                    {
                        logger.LogError("Step5_Cdn POST - Cloudflare validation failed - missing required fields");
                        ErrorMessage = "Both Cloudflare API Token and Zone ID are required.";
                        return Page();
                    }
                }
                else if (SelectedProvider == "Cloudfront")
                {
                    if (string.IsNullOrEmpty(CloudFrontAccessKeyId) ||
                        string.IsNullOrEmpty(CloudFrontSecretAccessKey) ||
                        string.IsNullOrEmpty(CloudFrontDistributionId) ||
                        string.IsNullOrEmpty(CloudFrontRegion))
                    {
                        logger.LogError("Step5_Cdn POST - CloudFront validation failed - missing required fields");
                        ErrorMessage = "All CloudFront fields are required when CloudFront is selected.";
                        return Page();
                    }
                }
                else if (SelectedProvider == "Sucuri")
                {
                    if (string.IsNullOrEmpty(SucuriApiKey) ||
                        string.IsNullOrEmpty(SucuriApiSecret))
                    {
                        logger.LogError("Step5_Cdn POST - Sucuri validation failed - missing required fields");
                        ErrorMessage = "Both Sucuri API Key and API Secret are required.";
                        return Page();
                    }
                }

                logger.LogInformation("Step5_Cdn POST - Saving CDN configuration for provider: {Provider}", SelectedProvider);
                await setupService.UpdateCdnConfigAsync(
                    SetupId,
                    SelectedProvider == "Azure" ? AzureSubscriptionId : string.Empty,
                    SelectedProvider == "Azure" ? AzureResourceGroup : string.Empty,
                    SelectedProvider == "Azure" ? AzureProfileName : string.Empty,
                    SelectedProvider == "Azure" ? AzureEndpointName : string.Empty,
                    SelectedProvider == "Azure" && AzureIsFrontDoor,
                    SelectedProvider == "Cloudflare" ? CloudflareApiToken : string.Empty,
                    SelectedProvider == "Cloudflare" ? CloudflareZoneId : string.Empty,
                    SelectedProvider == "Sucuri" ? SucuriApiKey : string.Empty,
                    SelectedProvider == "Sucuri" ? SucuriApiSecret : string.Empty,
                    SelectedProvider == "Cloudfront" ? CloudFrontAccessKeyId : string.Empty,
                    SelectedProvider == "Cloudfront" ? CloudFrontSecretAccessKey : string.Empty,
                    SelectedProvider == "Cloudfront" ? CloudFrontDistributionId : string.Empty,
                    SelectedProvider == "Cloudfront" ? CloudFrontRegion : string.Empty);

                await setupService.UpdateStepAsync(SetupId, 5);
                
                logger.LogInformation("Step5_Cdn POST - Successfully completed Step5, redirecting to Step6");
                return RedirectToPage("./Step6_Review");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step5_Cdn POST - Failed to save CDN configuration");
                ErrorMessage = $"Failed to save CDN configuration: {ex.Message}";
                return Page();
            }
        }

        /// <summary>
        /// Handles POST requests to skip CDN configuration.
        /// </summary>
        /// <returns>Redirect to next step.</returns>
        public async Task<IActionResult> OnPostSkipAsync()
        {
            logger.LogInformation("Step5_Cdn POST Skip - Skipping CDN configuration");

            try
            {
                var config = await setupService.GetCurrentSetupAsync();
                if (config == null)
                {
                    logger.LogError("Step5_Cdn POST Skip - No current setup configuration found");
                    return RedirectToPage("./Index");
                }

                SetupId = config.Id;

                logger.LogInformation("Step5_Cdn POST Skip - Clearing CDN configuration");
                await setupService.UpdateCdnConfigAsync(
                    SetupId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);

                await setupService.UpdateStepAsync(SetupId, 5);
                
                logger.LogInformation("Step5_Cdn POST Skip - Successfully skipped Step5, redirecting to Step6");
                return RedirectToPage("./Step6_Review");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step5_Cdn POST Skip - Failed to skip CDN configuration");
                ErrorMessage = $"Failed to skip CDN configuration: {ex.Message}";
                return Page();
            }
        }
    }
}
