// <copyright file="Cosmos___SettingsController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Sky.Editor.Models;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;

    /// <summary>
    /// The settings controller.
    /// </summary>
    [Authorize(Roles = "Administrators")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "The URL must be unique and not have a changes of conflicting with user authored web page URLs.")]
    public class Cosmos___SettingsController : Controller
    {
        /// <summary>
        /// Editor settings group name.
        /// </summary>
        public static readonly string EDITORSETGROUPNAME = "EDITORSETTINGS";

        /// <summary>
        /// Copilot settings group name.
        /// </summary>
        public static readonly string COPILOTPROXYSETGROUPNAME = "COPILOTPROXYSETTINGS";

        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<Cosmos___SettingsController> logger;
        private readonly IEditorSettings settings;
        private readonly ICdnServiceFactory cdnServiceFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cosmos___SettingsController"/> class.
        /// </summary>
        /// <param name="dbContext">Sets the database context.</param>
        /// <param name="logger">Log service.</param>
        /// <param name="settings">Editor settings.</param>
        /// <param name="cdnServiceFactory">CDN service factory.</param>
        public Cosmos___SettingsController(
            ApplicationDbContext dbContext,
            ILogger<Cosmos___SettingsController> logger,
            IEditorSettings settings,
            ICdnServiceFactory cdnServiceFactory)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.cdnServiceFactory = cdnServiceFactory ?? throw new ArgumentNullException(nameof(cdnServiceFactory));
        }

        /// <summary>
        /// Gets the index page.
        /// </summary>
        /// <returns>IActionResult.</returns>
        public async Task<IActionResult> Index()
        {
            var model = await settings.GetEditorConfigAsync();
            return View(model);
        }

        /// <summary>
        /// Updates the index page.
        /// </summary>
        /// <param name="model">Editor config model.</param>
        /// <returns>IActionResult.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(EditorConfig model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if mode is static website, and if so, set the blob URL.
            if (model.StaticWebPages)
            {
                model.BlobPublicUrl = "/";
            }

            var setting = await GetOrCreateEditorSettingAsync();
            setting.Value = JsonConvert.SerializeObject(model);

            await dbContext.SaveChangesAsync();

            return View(model);
        }

        /// <summary>
        /// Gets the CDN configuration page.
        /// </summary>
        /// <returns>IActionResult.</returns>
        public async Task<IActionResult> CDN()
        {
            var model = await LoadCdnViewModelAsync();
            ViewData["Operation"] = null;

            return View(model);
        }

        /// <summary>
        /// Updates the CDN configuration.
        /// </summary>
        /// <param name="model">The CDN configuration.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CDN(CdnViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await ClearCdnSettingsAsync();

            await AddCdnSettingIfValidAsync(model.AzureCdn);
            await AddCdnSettingIfValidAsync(model.Cloudflare);
            await AddCdnSettingIfValidAsync(model.CloudFront);
            await AddCdnSettingIfValidAsync(model.Sucuri);

            await dbContext.SaveChangesAsync();

            var operation = await TestConnectionAsync();
            ViewData["TestResult"] = operation;

            return View(model);
        }

        /// <summary>
        /// Removes the CDN configuration.
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public async Task<IActionResult> Remove()
        {
            await ClearCdnSettingsAsync();
            await dbContext.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        /// <summary>
        /// Gets the Copilot proxy configuration page.
        /// </summary>
        /// <returns>The Copilot proxy settings view.</returns>
        public async Task<IActionResult> Copilot()
        {
            var model = await LoadCopilotProxyOptionsAsync();
            return View(model);
        }

        /// <summary>
        /// Updates the Copilot proxy configuration.
        /// </summary>
        /// <param name="model">Copilot proxy options.</param>
        /// <returns>The Copilot proxy settings view.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Copilot(CopilotProxyOptions model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var setting = await GetOrCreateCopilotSettingAsync();
            setting.Value = JsonConvert.SerializeObject(model);

            await dbContext.SaveChangesAsync();
            ViewData["Operation"] = "Saved";

            return View(model);
        }

        /// <summary>
        /// Removes the Copilot proxy configuration.
        /// </summary>
        /// <returns>A redirect to the Copilot settings page.</returns>
        public async Task<IActionResult> RemoveCopilot()
        {
            await ClearCopilotSettingsAsync();
            await dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Copilot));
        }

        private async Task<Setting> GetOrCreateEditorSettingAsync()
        {
            var setting = await dbContext.Settings
                .FirstOrDefaultAsync(f => f.Group == EDITORSETGROUPNAME);

            if (setting == null)
            {
                setting = new Setting
                {
                    Group = EDITORSETGROUPNAME,
                    Name = "EditorSettings",
                    Value = string.Empty,
                    Description = "Settings used by the Cosmos Editor",
                };
                dbContext.Settings.Add(setting);
            }

            return setting;
        }

        private async Task<CdnViewModel> LoadCdnViewModelAsync()
        {
            var model = new CdnViewModel();
            var settings = await dbContext.Settings
                .Where(f => f.Group == CdnService.CDNGROUPNAME)
                .ToListAsync();

            foreach (var setting in settings)
            {
                try
                {
                    var cdnSetting = JsonConvert.DeserializeObject<CdnSetting>(setting.Value);
                    if (cdnSetting == null)
                    {
                        continue;
                    }

                    switch (cdnSetting.CdnProvider)
                    {
                        case CdnProviderEnum.AzureCDN:
                        case CdnProviderEnum.AzureFrontdoor:
                            model.AzureCdn = JsonConvert.DeserializeObject<AzureCdnConfig>(cdnSetting.Value) ?? new AzureCdnConfig();
                            break;
                        case CdnProviderEnum.Cloudflare:
                            model.Cloudflare = JsonConvert.DeserializeObject<CloudflareCdnConfig>(cdnSetting.Value) ?? new CloudflareCdnConfig();
                            break;
                        case CdnProviderEnum.CloudFront:
                            model.CloudFront = JsonConvert.DeserializeObject<CloudFrontCdnConfig>(cdnSetting.Value) ?? new CloudFrontCdnConfig();
                            break;
                        case CdnProviderEnum.Sucuri:
                            model.Sucuri = JsonConvert.DeserializeObject<SucuriCdnConfig>(cdnSetting.Value) ?? new SucuriCdnConfig();
                            break;
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize CDN setting with ID: {SettingId}", setting.Id);
                }
            }

            return model;
        }

        private async Task ClearCdnSettingsAsync()
        {
            var cdnSettings = await dbContext.Settings
                .Where(f => f.Group == CdnService.CDNGROUPNAME)
                .ToListAsync();

            if (cdnSettings.Any())
            {
                dbContext.Settings.RemoveRange(cdnSettings);
            }
        }

        private async Task AddCdnSettingIfValidAsync(AzureCdnConfig config)
        {
            if (string.IsNullOrEmpty(config?.ProfileName))
            {
                return;
            }

            var setting = CreateCdnSetting(
                config.IsFrontDoor ? CdnProviderEnum.AzureFrontdoor : CdnProviderEnum.AzureCDN,
                config,
                "Azure CDN or Front Door configuration.");

            dbContext.Settings.Add(setting);
            await Task.CompletedTask;
        }

        private async Task AddCdnSettingIfValidAsync(CloudflareCdnConfig config)
        {
            if (string.IsNullOrEmpty(config?.ApiToken))
            {
                return;
            }

            var setting = CreateCdnSetting(
                CdnProviderEnum.Cloudflare,
                config,
                "Cloudflare CDN configuration.");

            dbContext.Settings.Add(setting);
            await Task.CompletedTask;
        }

        private async Task AddCdnSettingIfValidAsync(CloudFrontCdnConfig config)
        {
            if (string.IsNullOrEmpty(config?.DistributionId))
            {
                return;
            }

            var setting = CreateCdnSetting(
                CdnProviderEnum.CloudFront,
                config,
                "Amazon CloudFront CDN configuration.");

            dbContext.Settings.Add(setting);
            await Task.CompletedTask;
        }

        private async Task AddCdnSettingIfValidAsync(SucuriCdnConfig config)
        {
            if (string.IsNullOrEmpty(config?.ApiKey))
            {
                return;
            }

            var setting = CreateCdnSetting(
                CdnProviderEnum.Sucuri,
                config,
                "Sucuri CDN configuration.");

            dbContext.Settings.Add(setting);
            await Task.CompletedTask;
        }

        private Setting CreateCdnSetting<T>(CdnProviderEnum provider, T config, string description)
        {
            return new Setting
            {
                Group = CdnService.CDNGROUPNAME,
                Name = provider.ToString(),
                Value = JsonConvert.SerializeObject(new CdnSetting
                {
                    CdnProvider = provider,
                    Value = JsonConvert.SerializeObject(config),
                }),
                Description = description,
            };
        }

        private async Task<List<CdnResult>> TestConnectionAsync()
        {
            try
            {
                var cdnService = await cdnServiceFactory.CreateCdnServiceAsync(dbContext, logger, HttpContext);
                var result = await cdnService.PurgeCdn(new List<string> { "/" });
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing CDN connection.");
                return new List<CdnResult>
                {
                    new CdnResult
                    {
                        IsSuccessStatusCode = false,
                        Message = ex.Message
                    }
                };
            }
        }

        private async Task<Setting> GetOrCreateCopilotSettingAsync()
        {
            var setting = await dbContext.Settings
                .FirstOrDefaultAsync(f => f.Group == COPILOTPROXYSETGROUPNAME);

            if (setting == null)
            {
                setting = new Setting
                {
                    Group = COPILOTPROXYSETGROUPNAME,
                    Name = nameof(CopilotProxyOptions),
                    Value = string.Empty,
                    Description = "Settings used by the Copilot completion proxy",
                };
                dbContext.Settings.Add(setting);
            }

            return setting;
        }

        private async Task<CopilotProxyOptions> LoadCopilotProxyOptionsAsync()
        {
            var setting = await dbContext.Settings
                .FirstOrDefaultAsync(f => f.Group == COPILOTPROXYSETGROUPNAME);

            if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            {
                return new CopilotProxyOptions();
            }

            try
            {
                return JsonConvert.DeserializeObject<CopilotProxyOptions>(setting.Value) ?? new CopilotProxyOptions();
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize Copilot setting with ID: {SettingId}", setting.Id);
                return new CopilotProxyOptions();
            }
        }

        private async Task ClearCopilotSettingsAsync()
        {
            var copilotSettings = await dbContext.Settings
                .Where(f => f.Group == COPILOTPROXYSETGROUPNAME)
                .ToListAsync();

            if (copilotSettings.Any())
            {
                dbContext.Settings.RemoveRange(copilotSettings);
            }
        }
    }
}
