// <copyright file="Complete.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sky.Editor.Services.Setup;
using System.Threading.Tasks;

namespace Sky.Editor.Areas.Setup.Pages.Tenant
{
    /// <summary>
    /// Multi-tenant setup completion page.
    /// </summary>
    public class CompleteModel : PageModel
    {
        private readonly IMultiTenantSetupService setupService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompleteModel"/> class.
        /// </summary>
        public CompleteModel(IMultiTenantSetupService setupService)
        {
            this.setupService = setupService;
        }

        /// <summary>
        /// Gets or sets a value indicating whether setup was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the website URL.
        /// </summary>
        public string WebsiteUrl { get; set; }

        /// <summary>
        /// Handles GET requests.
        /// </summary>
        public async Task<IActionResult> OnGetAsync()
        {
            var status = await setupService.GetTenantSetupStatusAsync();

            // Verify all required setup steps are complete
            if (!status.HasAdminAccount || !status.HasLayout)
            {
                return RedirectToPage("/Tenant/Index");
            }

            // Create home page if it doesn't exist
            if (!status.HasHomePage)
            {
                var homePageResult = await setupService.CreateHomePageAsync("Welcome");
                if (!homePageResult.Success)
                {
                    ErrorMessage = homePageResult.Message;
                    Success = false;
                    return Page();
                }
            }

            // Complete the setup
            var result = await setupService.CompleteTenantSetupAsync();
            Success = result.Success;
            ErrorMessage = result.Message;
            WebsiteUrl = status.WebsiteUrl;

            return Page();
        }
    }
}
