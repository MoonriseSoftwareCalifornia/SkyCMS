// <copyright file="Summary.cshtml.cs" company="Moonrise Software, LLC">
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
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Authorization;
    using Sky.Editor.Services.Setup;

    /// <summary>
    /// Setup wizard summary page.
    /// Displays final configuration with warnings for critical settings.
    /// Allows users to review before completing setup or making post-setup changes.
    /// </summary>
    [RequireSetupOrAdmin]
    public class SummaryModel : PageModel
    {
        private readonly ISetupService setupService;
        private readonly ISetupCheckService setupCheckService;
        private readonly ILogger<SummaryModel> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SummaryModel"/> class.
        /// </summary>
        /// <param name="setupService">Setup service.</param>
        /// <param name="setupCheckService">Setup check service.</param>
        /// <param name="logger">Logger.</param>
        public SummaryModel(ISetupService setupService, ISetupCheckService setupCheckService, ILogger<SummaryModel> logger)
        {
            this.setupService = setupService;
            this.setupCheckService = setupCheckService;
            this.logger = logger;
        }

        /// <summary>
        /// Gets or sets the setup configuration.
        /// </summary>
        [BindProperty]
        public Cosmos.Common.Data.SetupConfiguration SetupConfiguration { get; set; }

        /// <summary>
        /// Gets or sets critical settings for display.
        /// </summary>
        public CriticalSettingsSummary CriticalSettings { get; set; } = new();

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Handles GET requests.
        /// </summary>
        /// <returns>Page result or redirect.</returns>
        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var config = await setupService.GetCurrentSetupAsync();
                if (config == null)
                {
                    return RedirectToPage("./Index");
                }

                SetupConfiguration = config;
                BuildCriticalSettingsSummary(config);

                return Page();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading setup summary");
                ErrorMessage = $"Error loading summary: {ex.Message}";
                return Page();
            }
        }

        /// <summary>
        /// Handles POST requests to complete setup.
        /// </summary>
        /// <returns>Redirect or result.</returns>
        public async Task<IActionResult> OnPostCompleteAsync()
        {
            if (SetupConfiguration?.Id == Guid.Empty)
            {
                ErrorMessage = "Invalid setup session.";
                return Page();
            }

            try
            {
                var result = await setupService.CompleteSetupAsync(SetupConfiguration.Id);
                if (result.Success)
                {
                    logger.LogInformation("Setup completed successfully");
                    return RedirectToPage("/Index");
                }

                ErrorMessage = result.Message;
                return Page();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error completing setup");
                ErrorMessage = $"Error completing setup: {ex.Message}";
                return Page();
            }
        }

        /// <summary>
        /// Handles POST requests to go back and edit settings.
        /// </summary>
        /// <returns>Redirect to previous step.</returns>
        public async Task<IActionResult> OnPostBackAsync()
        {
            try
            {
                if (SetupConfiguration?.Id != Guid.Empty)
                {
                    await setupService.UpdateStepAsync(SetupConfiguration.Id, 6);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving state before going back");
            }

            return RedirectToPage("./Step6_CDN");
        }

        /// <summary>
        /// Builds the critical settings summary from configuration.
        /// </summary>
        /// <param name="config">Setup configuration.</param>
        private void BuildCriticalSettingsSummary(Cosmos.Common.Data.SetupConfiguration config)
        {
            CriticalSettings = new CriticalSettingsSummary
            {
                DatabaseConnection = new CriticalSetting
                {
                    Name = "Database Connection",
                    Value = MaskConnectionString(config.DatabaseConnectionString),
                    IsConfigured = !string.IsNullOrEmpty(config.DatabaseConnectionString),
                    Description = "Connection to primary database - changes can prevent website access"
                },
                StorageConnection = new CriticalSetting
                {
                    Name = "Storage Connection",
                    Value = MaskConnectionString(config.StorageConnectionString),
                    IsConfigured = !string.IsNullOrEmpty(config.StorageConnectionString),
                    Description = "Connection to blob/object storage - changes can break asset loading"
                },
                BlobPublicUrl = new CriticalSetting
                {
                    Name = "Blob Public URL",
                    Value = config.BlobPublicUrl,
                    IsConfigured = !string.IsNullOrEmpty(config.BlobPublicUrl),
                    Description = "URL for static assets - incorrect URLs break styling and images"
                },
                EmailProvider = new CriticalSetting
                {
                    Name = "Email Provider",
                    Value = DetermineEmailProvider(config),
                    IsConfigured = !string.IsNullOrEmpty(config.SendGridApiKey) || 
                                   !string.IsNullOrEmpty(config.AzureEmailConnectionString) ||
                                   !string.IsNullOrEmpty(config.SmtpHost),
                    Description = "Email delivery mechanism - changes can prevent system notifications"
                },
                PublisherUrl = new CriticalSetting
                {
                    Name = "Publisher URL",
                    Value = config.PublisherUrl,
                    IsConfigured = !string.IsNullOrEmpty(config.PublisherUrl),
                    Description = "Publisher website URL - incorrect URL breaks content publishing"
                }
            };
        }

        /// <summary>
        /// Masks a connection string for display (shows first 10 and last 10 characters).
        /// </summary>
        /// <param name="connectionString">Connection string to mask.</param>
        /// <returns>Masked connection string.</returns>
        private string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return "(not configured)";
            }

            if (connectionString.Length <= 20)
            {
                return new string('?', connectionString.Length);
            }

            var first10 = connectionString.Substring(0, 10);
            var last10 = connectionString.Substring(connectionString.Length - 10);
            return $"{first10}...{new string('?', 10)}...{last10}";
        }

        /// <summary>
        /// Determines which email provider is configured.
        /// </summary>
        /// <param name="config">Setup configuration.</param>
        /// <returns>Email provider name or status.</returns>
        private string DetermineEmailProvider(Cosmos.Common.Data.SetupConfiguration config)
        {
            if (!string.IsNullOrEmpty(config.SendGridApiKey))
            {
                return "SendGrid (configured)";
            }

            if (!string.IsNullOrEmpty(config.AzureEmailConnectionString))
            {
                return "Azure Communication Services (configured)";
            }

            if (!string.IsNullOrEmpty(config.SmtpHost))
            {
                return $"SMTP ({config.SmtpHost}:{config.SmtpPort})";
            }

            return "(not configured)";
        }
    }

    /// <summary>
    /// Container for critical settings summary display.
    /// </summary>
    public class CriticalSettingsSummary
    {
        /// <summary>
        /// Gets or sets database connection setting.
        /// </summary>
        public CriticalSetting DatabaseConnection { get; set; }

        /// <summary>
        /// Gets or sets storage connection setting.
        /// </summary>
        public CriticalSetting StorageConnection { get; set; }

        /// <summary>
        /// Gets or sets blob public URL setting.
        /// </summary>
        public CriticalSetting BlobPublicUrl { get; set; }

        /// <summary>
        /// Gets or sets email provider setting.
        /// </summary>
        public CriticalSetting EmailProvider { get; set; }

        /// <summary>
        /// Gets or sets publisher URL setting.
        /// </summary>
        public CriticalSetting PublisherUrl { get; set; }
    }

    /// <summary>
    /// Represents a single critical setting for display.
    /// </summary>
    public class CriticalSetting
    {
        /// <summary>
        /// Gets or sets the setting name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the setting value (masked if sensitive).
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the setting is configured.
        /// </summary>
        public bool IsConfigured { get; set; }

        /// <summary>
        /// Gets or sets the description explaining why this setting is critical.
        /// </summary>
        public string Description { get; set; }
    }
}
