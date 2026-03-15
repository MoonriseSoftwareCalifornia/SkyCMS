// <copyright file="Step4_Publisher.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Areas.Setup.Pages
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Layouts;
    using Sky.Editor.Services.Setup;
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Setup wizard step 3: Publisher configuration.
    /// </summary>
    public class Step3_Publisher : PageModel
    {
        private readonly ISetupService setupService;
        private readonly ILayoutImportService layoutImportService;
        private readonly ISetupCheckService setupCheckService;
        private readonly ILogger<Step3_Publisher> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Step3_Publisher"/> class.
        /// </summary>
        /// <param name="setupService">Setup service.</param>
        /// <param name="layoutImportService">Layout import service.</param>
        /// <param name="setupCheckService">Setup check service.</param>
        /// <param name="logger">Logger.</param>
        public Step3_Publisher(
            ISetupService setupService,
            ILayoutImportService layoutImportService,
            ISetupCheckService setupCheckService,
            ILogger<Step3_Publisher> logger)
        {
            this.setupService = setupService;
            this.layoutImportService = layoutImportService;
            this.setupCheckService = setupCheckService;
            this.logger = logger;
        }

        /// <summary>
        /// Gets or sets the setup session ID.
        /// </summary>
        [BindProperty]
        public Guid SetupId { get; set; }

        /// <summary>
        /// Gets or sets the publisher URL.
        /// </summary>
        [BindProperty]
        [Url(ErrorMessage = "Please enter a valid URL")]
        [Display(Name = "Website URL")]
        public string PublisherUrl { get; set; }

        /// <summary>
        /// Gets or sets the website title.
        /// </summary>
        [BindProperty]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Website title is required")]
        [Display(Name = "Website Title")]
        public string WebsiteTitle { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether static web pages are enabled.
        /// </summary>
        [BindProperty]
        [Display(Name = "Static Website Mode")]
        public bool StaticWebPages { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether authentication is required.
        /// </summary>
        [BindProperty]
        [Display(Name = "Require Authentication")]
        public bool CosmosRequiresAuthentication { get; set; } = false;

        /// <summary>
        /// Gets or sets the allowed file types.
        /// </summary>
        [BindProperty]
        [Required(ErrorMessage = "Allowed file types are required")]
        [Display(Name = "Allowed File Types")]
        public string AllowedFileTypes { get; set; } = ".js,.css,.htm,.html,.mov,.webm,.avi,.mp4,.mpeg,.ts,.svg,.json";

        /// <summary>
        /// Gets or sets the Microsoft App ID.
        /// </summary>
        [BindProperty]
        [Display(Name = "Microsoft Application ID")]
        public string MicrosoftAppId { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the site design ID.
        /// </summary>
        [BindProperty]
        public string SiteDesignId { get; set; }

        /// <summary>
        /// Gets a value indicating whether the publisher URL is pre-configured.
        /// </summary>
        public bool IsPreConfigured { get; private set; }

        /// <summary>
        /// Gets a value indicating whether CosmosRequiresAuthentication is pre-configured.
        /// </summary>
        public bool CosmosRequiresAuthenticationPreConfigured { get; private set; }

        /// <summary>
        /// Gets a value indicating whether MicrosoftAppId is pre-configured.
        /// </summary>
        public bool MicrosoftAppIdPreConfigured { get; private set; }

        /// <summary>
        /// Gets a value indicating whether StaticWebPages is pre-configured.
        /// </summary>
        public bool StaticWebPagesPreConfigured { get; private set; }

        /// <summary>
        /// Gets a value indicating whether AllowedFileTypes is pre-configured.
        /// </summary>
        public bool AllowedFileTypesPreConfigured { get; private set; }

        /// <summary>
        /// Handles GET requests.
        /// </summary>
        /// <returns>Page result.</returns>
        public async Task<IActionResult> OnGetAsync()
        {
            if (await setupCheckService.IsSetup())
            {
                // Redirect to home page if setup is already completed
                return RedirectToPage("/Index", new { area = "" });
            }

            var config = await setupService.GetCurrentSetupAsync();
            if (config == null)
            {
                return RedirectToPage("./Index");
            }

            await GetSiteDesignOptions();

            SetupId = config.Id;
            SiteDesignId = config.SiteDesignId;
            PublisherUrl = config.PublisherUrl;
            StaticWebPages = config.StaticWebPages;
            CosmosRequiresAuthentication = config.CosmosRequiresAuthentication;
            MicrosoftAppId = config.MicrosoftAppId;
            WebsiteTitle = config.WebsiteTitle;
            IsPreConfigured = config.PublisherPreConfigured;
            CosmosRequiresAuthenticationPreConfigured = config.CosmosRequiresAuthenticationPreConfigured;
            MicrosoftAppIdPreConfigured = config.MicrosoftAppIdPreConfigured;
            StaticWebPagesPreConfigured = config.StaticWebPagesPreConfigured;
            AllowedFileTypesPreConfigured = config.AllowedFileTypesPreConfigured;

            return Page();
        }

        /// <summary>
        /// Handles POST requests.
        /// </summary>
        /// <returns>Redirect to next step.</returns>
        public async Task<IActionResult> OnPostAsync()
        {
            logger.LogInformation("Step3_Publisher POST - SetupId: {SetupId}, PublisherUrl: {Url}, WebsiteTitle: {Title}, SiteDesignId: {DesignId}",
                SetupId, PublisherUrl, WebsiteTitle, SiteDesignId);

            // Check if setup has been completed
            if (await setupCheckService.IsSetup())
            {
                logger.LogWarning("Step3_Publisher POST - Setup already completed, redirecting to home");
                Response.Redirect("/");
            }

            if (!ModelState.IsValid)
            {
                logger.LogWarning("Step3_Publisher POST - ModelState validation failed");
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    if (state.Errors.Count > 0)
                    {
                        foreach (var error in state.Errors)
                        {
                            logger.LogError("Step3_Publisher POST - Validation error for {Field}: {Error}",
                                key, error.ErrorMessage ?? error.Exception?.Message);
                        }
                    }
                }

                await GetSiteDesignOptions();
                return Page();
            }

            try
            {
                var config = await setupService.GetCurrentSetupAsync();

                // Use config values if pre-configured, otherwise use form values
                var publisherUrlToSave = config.PublisherPreConfigured
                    ? config.PublisherUrl
                    : PublisherUrl?.TrimEnd('/');

                var cosmosRequiresAuthToSave = config.CosmosRequiresAuthenticationPreConfigured
                    ? config.CosmosRequiresAuthentication
                    : CosmosRequiresAuthentication;

                var allowedFileTypesToSave = config.AllowedFileTypesPreConfigured
                    ? config.AllowedFileTypes
                    : AllowedFileTypes;

                var microsoftAppIdToSave = config.MicrosoftAppIdPreConfigured
                    ? config.MicrosoftAppId
                    : MicrosoftAppId;

                logger.LogInformation("Step3_Publisher POST - Saving publisher configuration");
                await setupService.UpdatePublisherConfigAsync(
                    SetupId,
                    publisherUrlToSave,
                    true,
                    cosmosRequiresAuthToSave,
                    allowedFileTypesToSave,
                    microsoftAppIdToSave,
                    SiteDesignId,
                    WebsiteTitle);

                await setupService.UpdateStepAsync(SetupId, 3);

                logger.LogInformation("Step3_Publisher POST - Successfully completed Step3, redirecting to Step4");
                return RedirectToPage("./Step4_Email");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step3_Publisher POST - Failed to save publisher configuration");
                ErrorMessage = $"Failed to save publisher configuration: {ex.Message}";
                await GetSiteDesignOptions();
                return Page();
            }
        }

        private async Task GetSiteDesignOptions()
        {
            var catalog = await layoutImportService.GetCommunityCatalogAsync();
            ViewData["AvailableLayouts"] = catalog.LayoutCatalog.Select(layout => new SiteDesignOption()
            {
                Id = layout.Id,
                Title = layout.Name,
                Description = layout.Description
            }).ToList();
        }

        /// <summary>
        /// Site design option for layout selection.
        /// </summary>
        public class SiteDesignOption
        {
            /// <summary>
            /// Gets or sets the layout ID.
            /// </summary>
            public string Id { get; internal set; }

            /// <summary>
            /// Gets or sets the layout title.
            /// </summary>
            public string Title { get; internal set; }

            /// <summary>
            /// Gets or sets the layout description.
            /// </summary>
            public string Description { get; internal set; }
        }
    }
}
