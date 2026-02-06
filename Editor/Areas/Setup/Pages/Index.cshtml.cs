// <copyright file="Index.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Areas.Setup.Pages
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.Extensions.Configuration;
    using Sky.Editor.Services.Setup;

    /// <summary>
    /// Setup wizard welcome page.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly ISetupService setupService;
        private readonly IConfiguration configuration;
        private readonly ISetupCheckService setupCheckService;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexModel"/> class.
        /// </summary>
        /// <param name="setupService">Setup service.</param>
        /// <param name="configuration">Configuration.</param>
        /// <param name="setupCheckService">Setup check service.</param>
        public IndexModel(ISetupService setupService, IConfiguration configuration, ISetupCheckService setupCheckService)
        {
            this.setupService = setupService;
            this.configuration = configuration;
            this.setupCheckService = setupCheckService;
        }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the database status.
        /// </summary>
        public DbStatus? DbStatus { get; set; }

        /// <summary>
        /// Gets a value indicating whether storage is pre-configured.
        /// </summary>
        [BindProperty]
        public bool StorageIsPreConfigured { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the publisher URL is pre-configured.
        /// </summary>
        public bool PublisherIsPreConfigured { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the email configuration is pre-configured.
        /// </summary>
        public bool EmailIsPreConfigured { get; private set; }

        /// <summary>
        /// Gets a value indicating whether any CDN configuration is pre-configured.
        /// </summary>
        public bool CdnIsPreconfigured { get; private set; }

        /// <summary>
        /// Handles GET requests.
        /// </summary>
        /// <returns>Page result or redirect.</returns>
        public async Task<IActionResult> OnGetAsync()
        {
            // Check if setup has been completed
            if (await setupCheckService.IsSetup())
            {
                // Redirect to setup page
                Response.Redirect("/");
            }

            // When the welcome page is access, start with a clean initialization state.
            await setupService.InitializeSetupAsync(true);

            var config = await setupService.GetCurrentSetupAsync();

            StorageIsPreConfigured = config.StoragePreConfigured;
            PublisherIsPreConfigured = config.PublisherPreConfigured;
            EmailIsPreConfigured = config.EmailProviderPreConfigured;

            var azurePreConfigured = false;
            var sucuriPreConfigured = false;
            var cloudflarePreConfigured = false;
            var cloudFrontPreConfigured = false;

            // Load existing CDN configuration if any
            if (!string.IsNullOrEmpty(config.AzureCdnSubscriptionId) &&
                !string.IsNullOrEmpty(config.AzureCdnResourceGroup) &&
                !string.IsNullOrEmpty(config.AzureCdnProfileName) &&
                !string.IsNullOrEmpty(config.AzureCdnEndpointName))
            {
                azurePreConfigured = true;
            }
            else if (!string.IsNullOrEmpty(config.CloudflareApiToken))
            {
                cloudflarePreConfigured = true;
            }
            else if (!string.IsNullOrEmpty(config.SucuriApiKey))
            {
                sucuriPreConfigured = true;
            }
            else if (!string.IsNullOrEmpty(config.CloudFrontDistributionId) &&
                     !string.IsNullOrEmpty(config.CloudFrontAccessKeyId) &&
                     !string.IsNullOrEmpty(config.CloudFrontSecretAccessKey) &&
                     !string.IsNullOrEmpty(config.CloudFrontRegion))
            {
                cloudFrontPreConfigured = true;
            }

            CdnIsPreconfigured = azurePreConfigured || sucuriPreConfigured || cloudflarePreConfigured || cloudFrontPreConfigured;

            // Check if setup is allowed
            var allowSetup = configuration.GetValue<bool?>("CosmosAllowSetup") ?? false;
            
            if (!allowSetup)
            {
                return RedirectToPage("/Index", new { area = "" });
            }

            // Check if setup is already complete
            var existingConfig = await setupService.GetCurrentSetupAsync();
            if (existingConfig?.IsComplete == true)
            {
                return RedirectToPage("/Index", new { area = "" });
            }

            return Page();
        }

        /// <summary>
        /// Handles POST requests to start setup.
        /// </summary>
        /// <returns>Redirect to next step.</returns>
        public async Task<IActionResult> OnPostAsync()
        {
            // Check if setup has been completed
            if (await setupCheckService.IsSetup())
            {
                // Redirect to setup page
                Response.Redirect("/");
            }

            try
            {
                // ✅ Validate database connection FIRST
                var dbConnectionString = configuration.GetConnectionString("ApplicationDbContextConnection");
                if (string.IsNullOrEmpty(dbConnectionString))
                {
                    ErrorMessage = "Database connection string not found. Please configure 'ApplicationDbContextConnection' in appsettings.json or user secrets.";
                    return Page();
                }

                // Initialize a new setup session
                await setupService.InitializeSetupAsync();
                
                return RedirectToPage("./Step1_Storage");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"There was an error initializing the setup, restarting.";
                await setupService.InitializeSetupAsync(true);
                return Page();
            }
        }
    }
}
