// <copyright file="Step6_Review.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Areas.Setup.Pages
{
    using System;
    using System.Threading.Tasks;
    using AspNetCore.Identity.FlexDb;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Layouts;
    using Sky.Editor.Services.Setup;

    /// <summary>
    /// Setup wizard step 6: Review and complete setup.
    /// </summary>
    public class Step6_Review : PageModel
    {
        private readonly ISetupService setupService;
        private readonly ILogger<Step6_Review> logger;
        private readonly ISetupCheckService setupCheckService;
        private readonly ILayoutImportService layoutImportService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Step6_Review"/> class.
        /// </summary>
        /// <param name="setupService">Setup service.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="setupCheckService">Setup check service.</param>
        /// <param name="layoutImportService">Layout import service.</param>
        public Step6_Review(ISetupService setupService, ILogger<Step6_Review> logger, ISetupCheckService setupCheckService, ILayoutImportService layoutImportService)
        {
            this.setupService = setupService;
            this.logger = logger;
            this.setupCheckService = setupCheckService;
            this.layoutImportService = layoutImportService;
        }

        /// <summary>
        /// Gets or sets the setup session ID.
        /// </summary>
        [BindProperty]
        public Guid SetupId { get; set; }

        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        public SetupConfiguration Config { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets the storage type.
        /// </summary>
        public string StorageType => InferStorageType(Config?.StorageConnectionString);

        /// <summary>
        /// Gets the database type.
        /// </summary>
        public string DatabaseType => Utilities.InferDatabaseProviderShortName(Config?.DatabaseConnectionString);

        /// <summary>
        /// Gets the site design name based on the selected layout in configuration. If no layout is selected, returns "None selected".
        /// </summary>
        public string SiteDesignName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Config?.SiteDesignId))
                {
                    return "None selected";
                }

                var layout = layoutImportService.GetCommunityLayoutAsync(Config.SiteDesignId, true).GetAwaiter().GetResult();
                return layout.LayoutName;
            }
        }

        /// <summary>
        /// Gets the Email provider based on configuration. Checks for SendGrid, then Azure Communication Services, then SMTP, otherwise None.
        /// </summary>
        public string EmailProvider
        {
            get
            {
                if (Config?.EmailProviderPreConfigured == false)
                {
                    return "None";
                }

                if (string.IsNullOrEmpty(Config?.SendGridApiKey))
                {
                    return "SendGrid";
                }
                else if (!string.IsNullOrEmpty(Config?.AzureEmailConnectionString))
                {
                    return "Azure ACS";
                }
                else if (!string.IsNullOrEmpty(Config?.SmtpHost))
                {
                    return "SMTP";
                }

                return "None";
            }
        }

        /// <summary>
        /// Gets the CDN provider based on configuration. Checks for Azure Front Door, then CloudFlare, then CloudFront, then Sucuri, otherwise None.
        /// </summary>
        public string CdnProvider
        {
            get
            {
                var hasCdn = !string.IsNullOrEmpty(Config.AzureCdnSubscriptionId) ||
                            !string.IsNullOrEmpty(Config.CloudflareApiToken) ||
                            !string.IsNullOrEmpty(Config.SucuriApiKey);

                if (!hasCdn)
                {
                    return "None";
                }

                if (!string.IsNullOrEmpty(Config?.AzureCdnSubscriptionId))
                {
                    return "Azure Front Door";
                }

                if (!string.IsNullOrEmpty(Config?.CloudflareApiToken))
                {
                    return "CloudFlare";
                }

                if (!string.IsNullOrEmpty(Config?.CloudFrontSecretAccessKey))
                {
                    return "Cloud Front";
                }

                if (!string.IsNullOrEmpty(Config?.SucuriApiSecret))
                {
                    return "Sucuri";
                }

                return "None";
            }
        }

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

            Config = await setupService.GetCurrentSetupAsync();
            if (Config == null)
            {
                return RedirectToPage("./Index");
            }

            SetupId = Config.Id;

            if (string.IsNullOrEmpty(Config.StorageConnectionString))
            {
                ErrorMessage = "Storage connection string is missing. Please go back and configure storage.";
                return Page();
            }

            if (string.IsNullOrEmpty(Config.SenderEmail) || string.IsNullOrEmpty(Config.AdminPassword))
            {
                ErrorMessage = "Administrator account is incomplete. Please go back and create the admin account.";
                return Page();
            }

            if (string.IsNullOrEmpty(Config.PublisherUrl))
            {
                ErrorMessage = "Publisher URL is missing. Please go back and configure the publisher.";
                return Page();
            }

            return Page();
        }

        /// <summary>
        /// Handles POST requests to complete setup.
        /// </summary>
        /// <returns>Redirect to login page.</returns>
        public async Task<IActionResult> OnPostAsync()
        {
            logger.LogInformation("Step6_Review POST - SetupId: {SetupId}", SetupId);

            // Check if setup has been completed
            if (await setupCheckService.IsSetup())
            {
                logger.LogWarning("Step6_Review POST - Setup already completed, redirecting to home");
                Response.Redirect("/");
            }

            try
            {
                logger.LogInformation("Step6_Review POST - Starting setup completion process for setup ID: {SetupId}", SetupId);

                // Complete the setup process
                var result = await setupService.CompleteSetupAsync(SetupId);

                if (!result.Success)
                {
                    logger.LogError("Step6_Review POST - Setup completion failed: {Message}", result.Message);
                    ErrorMessage = result.Message;
                    Config = await setupService.GetCurrentSetupAsync();
                    return Page();
                }

                logger.LogInformation("Step6_Review POST - Setup completed successfully. Setup ID: {SetupId}", SetupId);

                // Redirect to completion success page
                return RedirectToPage("./Complete");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step6_Review POST - Failed to complete setup. Setup ID: {SetupId}", SetupId);
                ErrorMessage = $"Failed to complete setup: {ex.Message}";
                Config = await setupService.GetCurrentSetupAsync();
                return Page();
            }
        }


        /// <summary>
        /// Infers storage type from connection string.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        /// <returns>Storage type.</returns>
        public string InferStorageType(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return string.Empty;
            }

            if (connectionString.Contains("DefaultEndpointsProtocol=", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("AccountName=", StringComparison.OrdinalIgnoreCase))
            {
                return "AzureBlob";
            }
            else if (connectionString.Contains("Bucket=", StringComparison.OrdinalIgnoreCase) &&
                     connectionString.Contains("Region=", StringComparison.OrdinalIgnoreCase))
            {
                return "AmazonS3";
            }
            else if (connectionString.Contains("AccountId=", StringComparison.OrdinalIgnoreCase) &&
                     connectionString.Contains("Bucket=", StringComparison.OrdinalIgnoreCase))
            {
                return "CloudflareR2";
            }

            return string.Empty;
        }
    }
}
