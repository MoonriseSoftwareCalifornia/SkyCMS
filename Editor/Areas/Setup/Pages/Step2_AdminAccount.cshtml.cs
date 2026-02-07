// <copyright file="Step2_AdminAccount.cshtml.cs" company="Moonrise Software, LLC">
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
    using Cosmos.Cms.Data;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Setup;

    /// <summary>
    /// Setup wizard step 3: Admin account creation.
    /// </summary>
    public class Step2_AdminAccount : PageModel
    {
        private readonly ISetupService setupService;
        private readonly IServiceProvider services;
        private readonly ISetupCheckService setupCheckService;
        private readonly ILogger<Step2_AdminAccount> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Step2_AdminAccount"/> class.
        /// </summary>
        /// <param name="setupService">Setup service.</param>
        /// <param name="services">App services.</param>
        /// <param name="setupCheckService">Setup check service.</param>
        /// <param name="logger">Logger.</param>
        public Step2_AdminAccount(
    ISetupService setupService,
    IServiceProvider services,
    ISetupCheckService setupCheckService,
    ILogger<Step2_AdminAccount> logger)
        {
            this.setupService = setupService;
            this.services = services;
            this.setupCheckService = setupCheckService;
            this.logger = logger;
        }

        private UserManager<IdentityUser> UserManager
        {
            get
            {
                var manager = services.GetService<UserManager<IdentityUser>>();
                return manager;
            }
        }

        /// <summary>
        /// Gets or sets the setup session ID.
        /// </summary>
        [BindProperty]
        public Guid SetupId { get; set; }

        /// <summary>
        /// Gets or sets the admin email.
        /// </summary>
        [BindProperty]
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Administrator Email")]
        public string AdminEmail { get; set; }

        /// <summary>
        /// Gets or sets the admin password.
        /// </summary>
        [BindProperty]
        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*]).{8,}$",
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character")]
        [Display(Name = "Password")]
        public string AdminPassword { get; set; }

        /// <summary>
        /// Gets or sets the password confirmation.
        /// </summary>
        [BindProperty]
        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare("AdminPassword", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to create sample content.
        /// </summary>
        [BindProperty]
        public bool CreateSampleContent { get; set; } = true;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

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

            // Is there already an admin account?
            // This may happen if the setup is run again.
            var admin = await UserManager.GetUsersInRoleAsync(RequiredIdentityRoles.Administrators);

            SetupId = config.Id;
            AdminEmail = config.AdminEmail;

            return Page();
        }

        /// <summary>
        /// Handles POST requests.
        /// </summary>
        /// <returns>Redirect to next step.</returns>
        public async Task<IActionResult> OnPostAsync()
        {
            logger.LogInformation("Step2_AdminAccount POST - SetupId: {SetupId}, AdminEmail: {Email}",
                SetupId, AdminEmail);

            // Check if setup has been completed
            if (await setupCheckService.IsSetup())
            {
                logger.LogWarning("Step2_AdminAccount POST - Setup already completed, redirecting to home");
                Response.Redirect("/");
            }

            if (!ModelState.IsValid)
            {
                logger.LogWarning("Step2_AdminAccount POST - ModelState validation failed");
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    if (state.Errors.Count > 0)
                    {
                        foreach (var error in state.Errors)
                        {
                            logger.LogError("Step2_AdminAccount POST - Validation error for {Field}: {Error}",
                                key, error.ErrorMessage ?? error.Exception?.Message);
                        }
                    }
                }
                return Page();
            }

            try
            {
                logger.LogInformation("Step2_AdminAccount POST - Updating admin account");
                await setupService.UpdateAdminAccountAsync(SetupId, AdminEmail, AdminPassword);
                await setupService.UpdateStepAsync(SetupId, 2);

                logger.LogInformation("Step2_AdminAccount POST - Successfully completed Step2, redirecting to Step3");
                return RedirectToPage("./Step3_Publisher");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step2_AdminAccount POST - Failed to save admin account");
                ErrorMessage = $"Failed to save admin account: {ex.Message}";
                return Page();
            }
        }
    }
}
