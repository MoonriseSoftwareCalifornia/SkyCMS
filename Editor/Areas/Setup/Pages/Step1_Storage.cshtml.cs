// <copyright file="Step1_Storage.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Areas.Setup.Pages
{
    using Azure.Storage.Blobs;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Authorization;
    using Sky.Editor.Services.Setup;
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;

    /// <summary>
    /// Setup wizard step 1: Storage configuration.
    /// Allows access during initial setup or for administrators making post-setup changes.
    /// </summary>
    [RequireSetupOrAdmin]
    public class Step1_Storage : PageModel
    {
        private readonly ISetupService setupService;
        private readonly ISetupCheckService setupCheckService;
        private readonly ILogger<Step1_Storage> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Step1_Storage"/> class.
        /// </summary>
        /// <param name="setupService">Setup service.</param>
        /// <param name="setupCheckService">Setup check service.</param>
        /// <param name="logger">Logger.</param>
        public Step1_Storage(ISetupService setupService, ISetupCheckService setupCheckService, ILogger<Step1_Storage> logger)
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
        /// Gets or sets the storage type.
        /// </summary>
        [BindProperty]
        public string StorageType { get; set; }

        /// <summary>
        /// Gets or sets the storage connection string.
        /// </summary>
        [BindProperty]
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the container name (for Azure Blob Storage).
        /// </summary>
        [BindProperty]
        public string ContainerName { get; set; } = "$web";

        /// <summary>
        /// Gets or sets the blob public URL.
        /// </summary>
        [BindProperty]
        [Required(ErrorMessage = "Public URL is required")]
        public string BlobPublicUrl { get; set; } = "/";

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the success message.
        /// </summary>
        public string SuccessMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether storage is pre-configured.
        /// </summary>
        [BindProperty]
        public bool IsPreConfigured { get; private set; }

        /// <summary>
        /// Gets a value indicating whether blob public URL is pre-configured.
        /// </summary>
        public bool BlobPublicUrlPreConfigured { get; private set; }

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

            try
            {
                var config = await setupService.GetCurrentSetupAsync();
                if (config == null)
                {
                    return RedirectToPage("./Index");
                }

                SetupId = config.Id;
                StorageConnectionString = config.StoragePreConfigured ? "**********************" : config.StorageConnectionString;
                BlobPublicUrl = config.BlobPublicUrl;
                IsPreConfigured = config.StoragePreConfigured;
                BlobPublicUrlPreConfigured = config.BlobPublicUrlPreConfigured;

                var testConnectionString = config.StoragePreConfigured
                    ? config.StorageConnectionString
                    : StorageConnectionString;

                // Infer storage type from connection string
                if (!string.IsNullOrEmpty(testConnectionString))
                {
                    StorageType = InferStorageType(testConnectionString);
                }

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error checking setup status: {ex.Message}";
                return Page();
            }
        }

        /// <summary>
        /// Handles POST requests to proceed to next step.
        /// </summary>
        /// <returns>Redirect to next step.</returns>
        public async Task<IActionResult> OnPostAsync()
        {
            logger.LogInformation("Step1_Storage POST - SetupId: {SetupId}, StorageType: {StorageType}, BlobPublicUrl: {BlobPublicUrl}",
                SetupId, StorageType, BlobPublicUrl);

            // Check if setup has been completed
            if (await setupCheckService.IsSetup())
            {
                logger.LogWarning("Step1_Storage POST - Setup already completed, redirecting to home");
                Response.Redirect("/");
            }

            if (!ModelState.IsValid)
            {
                logger.LogWarning("Step1_Storage POST - ModelState validation failed");
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    if (state.Errors.Count > 0)
                    {
                        foreach (var error in state.Errors)
                        {
                            logger.LogError("Step1_Storage POST - Validation error for {Field}: {Error}",
                                key, error.ErrorMessage ?? error.Exception?.Message);
                        }
                    }
                }
                return Page();
            }

            var config = await setupService.GetCurrentSetupAsync();
            if (config == null)
            {
                logger.LogError("Step1_Storage POST - No current setup configuration found");
                ErrorMessage = "Setup configuration not found. Please restart the setup process.";
                return Page();
            }

            SetupId = config.Id;

            if (!config.StoragePreConfigured && string.IsNullOrWhiteSpace(StorageConnectionString))
            {
                logger.LogError("Step1_Storage POST - Storage connection string is required but not provided");
                ModelState.AddModelError(nameof(StorageConnectionString), "Connection string is required.");
                return Page();
            }

            try
            {
                var testConnectionString = config.StoragePreConfigured
                    ? config.StorageConnectionString
                    : StorageConnectionString;

                logger.LogInformation("Step1_Storage POST - Testing storage connection");
                var result = await setupService.TestStorageConnectionAsync(testConnectionString);
                if (!result.Success)
                {
                    logger.LogError("Step1_Storage POST - Storage connection test failed: {Message}", result.Message);
                    ErrorMessage = "Storage connection test failed. Please ensure the connection string is correct.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step1_Storage POST - Exception during storage connection test");
                ErrorMessage = $"Failed to proceed: {ex.Message}";
                return Page();
            }

            try
            {
                // If this is an Azure Blob Storage, ensure container name is set
                var testConnectionString = config.StoragePreConfigured
                    ? config.StorageConnectionString
                    : StorageConnectionString;
                var inferredType = InferStorageType(testConnectionString);

                logger.LogInformation("Step1_Storage POST - Inferred storage type: {Type}", inferredType);

                if (inferredType == "AzureBlob" && string.IsNullOrWhiteSpace(ContainerName))
                {
                    logger.LogInformation("Step1_Storage POST - Creating default Azure Blob container");
                    var blobClient = new BlobServiceClient(testConnectionString);
                    var container = blobClient.GetBlobContainerClient("$web");
                    await container.CreateIfNotExistsAsync();

                    // Enable static website
                    var serviceProperties = await blobClient.GetPropertiesAsync();

                    if (!serviceProperties.Value.StaticWebsite.Enabled)
                    {
                        serviceProperties.Value.StaticWebsite.Enabled = true;
                        serviceProperties.Value.StaticWebsite.IndexDocument = "index.html";
                        serviceProperties.Value.StaticWebsite.ErrorDocument404Path = "404.html";

                        await blobClient.SetPropertiesAsync(serviceProperties.Value);
                        logger.LogInformation("Step1_Storage POST - Enabled static website for Azure Blob");
                    }
                }

                // Use config values if pre-configured, otherwise use form values
                var connectionStringToSave = config.StoragePreConfigured
                    ? config.StorageConnectionString
                    : StorageConnectionString;

                var blobPublicUrlToSave = config.BlobPublicUrlPreConfigured
                    ? config.BlobPublicUrl
                    : BlobPublicUrl;

                logger.LogInformation("Step1_Storage POST - Saving storage configuration");
                await setupService.UpdateStorageConfigAsync(
                    SetupId,
                    connectionStringToSave,
                    blobPublicUrlToSave);

                await setupService.UpdateStepAsync(SetupId, 1);
                logger.LogInformation("Step1_Storage POST - Successfully completed Step1, redirecting to Step2");
                return RedirectToPage("./Step2_AdminAccount");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step1_Storage POST - Failed to save storage configuration");
                ErrorMessage = $"Failed to proceed: {ex.Message}";
                return Page();
            }
        }

        /// <summary>
        /// Infers storage type from connection string.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        /// <returns>Storage type.</returns>
        private string InferStorageType(string connectionString)
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

        /// <summary>
        /// Handles POST requests to go to the next step.
        /// </summary>
        /// <returns>Redirect to next step.</returns>
        public async Task<IActionResult> OnPostNextAsync()
        {
            if (!ModelState.IsValid || SetupId == Guid.Empty)
            {
                return Page();
            }

            var config = await setupService.GetCurrentSetupAsync();
            if (config == null)
            {
                return RedirectToPage("./Index");
            }

            try
            {
                // Save storage configuration
                var connectionStringToSave = config.StoragePreConfigured ? config.StorageConnectionString : StorageConnectionString;
                var blobPublicUrlToSave = config.BlobPublicUrlPreConfigured ? config.BlobPublicUrl : BlobPublicUrl;

                await setupService.UpdateStorageConfigAsync(SetupId, connectionStringToSave, blobPublicUrlToSave);
                await setupService.UpdateStepAsync(SetupId, 2);

                // Check if next step should be skipped
                var shouldSkipStep2 = await setupService.ShouldSkipStepAsync(SetupId, 2);
                var nextPageName = shouldSkipStep2 ? "./Step3_Database" : "./Step2_AdminAccount";

                return RedirectToPage(nextPageName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error proceeding to next step");
                ErrorMessage = $"Error proceeding: {ex.Message}";
                return Page();
            }
        }

        /// <summary>
        /// Handles POST requests to go to the previous step.
        /// </summary>
        /// <returns>Redirect to previous step (Index).</returns>
        public async Task<IActionResult> OnPostBackAsync()
        {
            if (SetupId == Guid.Empty)
            {
                return RedirectToPage("./Index");
            }

            try
            {
                // Save current step before going back
                var config = await setupService.GetCurrentSetupAsync();
                if (config != null)
                {
                    var connectionStringToSave = config.StoragePreConfigured ? config.StorageConnectionString : StorageConnectionString;
                    var blobPublicUrlToSave = config.BlobPublicUrlPreConfigured ? config.BlobPublicUrl : BlobPublicUrl;
                    await setupService.UpdateStorageConfigAsync(SetupId, connectionStringToSave, blobPublicUrlToSave);
                    await setupService.UpdateStepAsync(SetupId, 1);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving state before going back");
            }

            return RedirectToPage("./Index");
        }
    }
}

