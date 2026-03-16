// <copyright file="Index.cshtml.cs" company="Moonrise Software, LLC">
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
    /// Multi-tenant setup index page.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly IMultiTenantSetupService setupService;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexModel"/> class.
        /// </summary>
        public IndexModel(IMultiTenantSetupService setupService)
        {
            this.setupService = setupService;
        }

        /// <summary>
        /// Gets or sets the tenant setup status.
        /// </summary>
        public TenantSetupStatus Status { get; set; }

        /// <summary>
        /// Handles GET requests.
        /// </summary>
        public async Task<IActionResult> OnGetAsync()
        {
            Status = await setupService.GetTenantSetupStatusAsync();

            if (!Status.SetupRequired)
            {
                return RedirectToPage("/Index", new { area = "" });
            }

            return Page();
        }
    }
}
