// <copyright file="Admin.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sky.Editor.Services.Setup;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Sky.Editor.Areas.Setup.Pages.Tenant
{
    /// <summary>
    /// Multi-tenant admin account creation page.
    /// </summary>
    public class AdminModel : PageModel
    {
        private readonly IMultiTenantSetupService setupService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminModel"/> class.
        /// </summary>
        public AdminModel(IMultiTenantSetupService setupService)
        {
            this.setupService = setupService;
        }

        /// <summary>
        /// Gets or sets the email address.
        /// </summary>
        [BindProperty]
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        [MinLength(6)]
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the confirm password.
        /// </summary>
        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Handles GET requests.
        /// </summary>
        public async Task<IActionResult> OnGetAsync()
        {
            var status = await setupService.GetTenantSetupStatusAsync();

            if (!status.SetupRequired || status.HasAdminAccount)
            {
                return RedirectToPage("/Tenant/Index");
            }

            // Pre-populate email if available from connection
            if (!string.IsNullOrEmpty(status.OwnerEmail))
            {
                Email = status.OwnerEmail;
            }

            return Page();
        }

        /// <summary>
        /// Handles POST requests.
        /// </summary>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var result = await setupService.CreateTenantAdminAsync(Email, Password);

            if (!result.Success)
            {
                ErrorMessage = result.Message;
                return Page();
            }

            return RedirectToPage("/Tenant/Index");
        }
    }
}
