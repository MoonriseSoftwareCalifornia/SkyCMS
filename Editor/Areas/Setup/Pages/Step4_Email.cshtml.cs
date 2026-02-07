// <copyright file="Step4_Email.cshtml.cs" company="Moonrise Software, LLC">
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
    /// Setup wizard step 4: Email configuration.
    /// </summary>
    public class Step4_Email : PageModel
    {
        private readonly ISetupService setupService;
        private readonly ISetupCheckService setupCheckService;
        private readonly ILogger<Step4_Email> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Step4_Email"/> class.
        /// </summary>
        /// <param name="setupService">Setup service.</param>
        /// <param name="setupCheckService">Setup check service.</param>
        /// <param name="logger">Logger.</param>
        public Step4_Email(ISetupService setupService, ISetupCheckService setupCheckService, ILogger<Step4_Email> logger)
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
        /// Gets or sets the email provider.
        /// </summary>
        [BindProperty]
        public string EmailProvider { get; set; } = "none";

        /// <summary>
        /// Gets or sets the SendGrid API key.
        /// </summary>
        [BindProperty]
        public string SendGridApiKey { get; set; }

        /// <summary>
        /// Gets or sets the Azure Communication Services connection string.
        /// </summary>
        [BindProperty]
        public string AzureEmailConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the SMTP host.
        /// </summary>
        [BindProperty]
        public string SmtpHost { get; set; }

        /// <summary>
        /// Gets or sets the SMTP port.
        /// </summary>
        [BindProperty]
        public string SmtpPort { get; set; } = "587";

        /// <summary>
        /// Gets or sets the SMTP username.
        /// </summary>
        [BindProperty]
        public string SmtpUsername { get; set; }

        /// <summary>
        /// Gets or sets the SMTP password.
        /// </summary>
        [BindProperty]
        public string SmtpPassword { get; set; }

        /// <summary>
        /// Gets or sets the sender email address.
        /// </summary>
        [BindProperty]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string SenderEmail { get; set; }

        /// <summary>
        /// Gets or sets the test result.
        /// </summary>
        public TestResult TestResult { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the success message.
        /// </summary>
        public string SuccessMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether the email configuration is pre-configured.
        /// </summary>
        public bool IsPreConfigured { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the system email is pre-configured.
        /// </summary>
        public bool SystemEmailPreConfigured { get; private set; }

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
            SendGridApiKey = config.SendGridApiKey;
            AzureEmailConnectionString = config.AzureEmailConnectionString;
            SmtpHost = config.SmtpHost;
            SmtpPort = config.SmtpPort;
            SmtpUsername = config.SmtpUsername;
            SmtpPassword = config.SmtpPassword;
            IsPreConfigured = config.EmailProviderPreConfigured;
            SenderEmail = config.SenderEmail;
            SystemEmailPreConfigured = config.SenderEmailPreConfigured;

            // Infer email provider
            if (!string.IsNullOrWhiteSpace(SendGridApiKey))
            {
                EmailProvider = "SendGrid";
                IsPreConfigured = true;
            }
            else if (!string.IsNullOrWhiteSpace(AzureEmailConnectionString))
            {
                EmailProvider = "AzureCommunication";
                IsPreConfigured = true;
            }
            else if (!string.IsNullOrWhiteSpace(SmtpHost))
            {
                EmailProvider = "SMTP";
                IsPreConfigured = true;
            }
            else
            {
                IsPreConfigured = false;
                EmailProvider = "none";
            }

            return Page();
        }

        /// <summary>
        /// Handles POST requests to test email.
        /// </summary>
        /// <returns>Page result.</returns>
        public async Task<IActionResult> OnPostTestEmailAsync()
        {
            logger.LogInformation("Step4_Email POST TestEmail - EmailProvider: {Provider}, SenderEmail: {Email}", 
                EmailProvider, SenderEmail);

            // Check if setup has been completed
            if (await setupCheckService.IsSetup())
            {
                logger.LogWarning("Step4_Email POST TestEmail - Setup already completed, redirecting to home");
                Response.Redirect("/");
            }

            if (string.IsNullOrEmpty(EmailProvider))
            {
                logger.LogError("Step4_Email POST TestEmail - No email provider selected");
                ErrorMessage = "Please select an email provider";
                return Page();
            }

            if (string.IsNullOrEmpty(SenderEmail))
            {
                logger.LogError("Step4_Email POST TestEmail - No sender email provided");
                ErrorMessage = "Please enter a sender email address to test email configuration";
                return Page();
            }

            try
            {
                var config = await setupService.GetCurrentSetupAsync();

                if (string.IsNullOrWhiteSpace(config.SmtpHost))
                {
                    EmailProvider = "none";
                }
                else if (!string.IsNullOrWhiteSpace(config.SmtpHost))
                {
                    EmailProvider = "SMTP";
                }
                else if (!string.IsNullOrWhiteSpace(config.AzureEmailConnectionString))
                {
                    EmailProvider = "AzureCommunication";
                }
                else if (!string.IsNullOrWhiteSpace(config.SendGridApiKey))
                {
                    EmailProvider = "SendGrid";
                }
                else
                {
                    EmailProvider = "none";
                }

                // Clear previous messages
                ErrorMessage = string.Empty;
                SuccessMessage = string.Empty;

                logger.LogInformation("Step4_Email POST TestEmail - Testing email configuration");
                TestResult = await setupService.TestEmailConfigAsync(
                    EmailProvider,
                    SendGridApiKey,
                    AzureEmailConnectionString,
                    SmtpHost,
                    SmtpPort,
                    SmtpUsername,
                    SmtpPassword,
                    SenderEmail,
                    config.SenderEmail);

                if (TestResult.Success)
                {
                    logger.LogInformation("Step4_Email POST TestEmail - Test email sent successfully");
                    SuccessMessage = "Test email sent successfully! Check your inbox.";
                }
                else
                {
                    logger.LogError("Step4_Email POST TestEmail - Test email failed: {Message}", TestResult.Message);
                }

                return Page();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step4_Email POST TestEmail - Exception during email test");
                TestResult = new TestResult
                {
                    Success = false,
                    Message = $"Email test failed: {ex.Message}"
                };
                return Page();
            }
        }

        /// <summary>
        /// Handles POST requests to proceed to next step.
        /// </summary>
        /// <returns>Redirect to next step.</returns>
        public async Task<IActionResult> OnPostAsync()
        {
            logger.LogInformation("Step4_Email POST - SetupId: {SetupId}, EmailProvider: {Provider}, SenderEmail: {Email}", 
                SetupId, EmailProvider, SenderEmail);

            // Check if setup has been completed
            if (await setupCheckService.IsSetup())
            {
                logger.LogWarning("Step4_Email POST - Setup already completed, redirecting to home");
                Response.Redirect("/");
            }

            try
            {
                var config = await setupService.GetCurrentSetupAsync();

                if (config.EmailProviderPreConfigured)
                {
                    logger.LogInformation("Step4_Email POST - Using pre-configured email settings");
                    SendGridApiKey = config.SendGridApiKey;
                    AzureEmailConnectionString = config.AzureEmailConnectionString;
                    SmtpHost = config.SmtpHost;
                    SmtpPort = config.SmtpPort;
                    SmtpUsername = config.SmtpUsername;
                    SmtpPassword = config.SmtpPassword;
                }

                logger.LogInformation("Step4_Email POST - Saving email configuration");
                await setupService.UpdateEmailConfigAsync(
                    SetupId,
                    EmailProvider,
                    SendGridApiKey,
                    AzureEmailConnectionString,
                    SmtpHost,
                    SmtpPort,
                    SmtpUsername,
                    SmtpPassword);

                await setupService.UpdateStepAsync(SetupId, 4);
                
                logger.LogInformation("Step4_Email POST - Successfully completed Step4, redirecting to Step5");
                return RedirectToPage("./Step5_Cdn");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step4_Email POST - Failed to save email configuration");
                ErrorMessage = $"Failed to save email configuration: {ex.Message}";
                return Page();
            }
        }
    }
}
