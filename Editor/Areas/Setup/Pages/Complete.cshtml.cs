// <copyright file="Complete.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Areas.Setup.Pages
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Services.Caching;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Setup;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Setup completion page.
    /// </summary>
    public class CompleteModel : PageModel
    {
        private readonly ISetupService setupService;
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly IWebHostEnvironment webHostEnvironment;
        private readonly ILogger<CompleteModel> logger;
        private readonly ISetupCheckService setupCheckService;
        private readonly ApplicationDbContext dbContext;
        private readonly ICacheService<bool> setupCache;

        private const string SETUP_CACHE_KEY_PREFIX = "SetupComplete";
        private const string HEADER_ORIGIN_HOSTNAME = "x-origin-hostname";

        /// <summary>
        /// Initializes a new instance of the <see cref="CompleteModel"/> class.
        /// </summary>
        /// <param name="setupService">Setup service.</param>
        /// <param name="hostApplicationLifetime">Host application lifetime.</param>
        /// <param name="webHostEnvironment">Web host environment.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="setupCheckService">Setup check service.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="setupCache">Tenant-aware setup status cache.</param>
        public CompleteModel(
                ISetupService setupService,
                IHostApplicationLifetime hostApplicationLifetime,
                IWebHostEnvironment webHostEnvironment,
                ILogger<CompleteModel> logger,
                ISetupCheckService setupCheckService,
                ApplicationDbContext dbContext,
                ICacheService<bool> setupCache)
        {
            this.setupService = setupService;
            this.hostApplicationLifetime = hostApplicationLifetime;
            this.webHostEnvironment = webHostEnvironment;
            this.logger = logger;
            this.setupCheckService = setupCheckService;
            this.dbContext = dbContext;
            this.setupCache = setupCache;
        }

        /// <summary>
        /// Gets or sets the admin email.
        /// </summary>
        public string AdminEmail { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether email is configured.
        /// </summary>
        public bool EmailConfigured { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether CDN is configured.
        /// </summary>
        public bool CdnConfigured { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether running in Docker.
        /// </summary>
        public bool IsDocker { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether restart has been triggered.
        /// </summary>
        public bool RestartTriggered { get; set; }

        /// <summary>
        /// Handles GET requests.
        /// </summary>
        /// <returns>Page result.</returns>
        public async Task OnGetAsync()
        {
            var userCount = await dbContext.Users.CountAsync();
            if (userCount > 1)
            {
                // If more than one user exists, setup is already complete - redirect to home
                Response.Redirect("/");
                return;
            }

            var config = await setupService.GetCurrentSetupAsync();
            if (config != null)
            {
                AdminEmail = config.AdminEmail;
                EmailConfigured = !string.IsNullOrEmpty(config.SendGridApiKey) ||
                                  !string.IsNullOrEmpty(config.AzureEmailConnectionString) ||
                                  !string.IsNullOrEmpty(config.SmtpHost);

                CdnConfigured = !string.IsNullOrEmpty(config.AzureCdnSubscriptionId) ||
                                !string.IsNullOrEmpty(config.CloudflareApiToken) ||
                                !string.IsNullOrEmpty(config.SucuriApiKey);

                // Check if restart has already been triggered
                RestartTriggered = config.RestartTriggered;
            }

            // Detect if running in Docker
            IsDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        }

        /// <summary>
        /// Handles POST requests to commit setup completion and redirect to login.
        /// </summary>
        /// <returns>Redirect result.</returns>
        public IActionResult OnPostCompleteSetup()
        {
            InvalidateSetupCache();
            logger.LogInformation("Setup committed by user, redirecting to login page");
            return Redirect("/Identity/Account/Login");
        }

        /// <summary>
        /// Invalidates the setup completion cache for the current hostname.
        /// </summary>
        private void InvalidateSetupCache()
        {
            var hostname = Request.Headers[HEADER_ORIGIN_HOSTNAME].ToString().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(hostname))
            {
                hostname = Request.Host.Host.ToLowerInvariant();
            }

            var cacheKey = $"{SETUP_CACHE_KEY_PREFIX}:{hostname}";
            setupCache.Remove(cacheKey);

            logger.LogInformation("Invalidated setup cache for hostname: {Hostname}", hostname);
        }
    }
}
