// <copyright file="CdnService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.CDN
{
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    ///     Configuration for Azure Front Door, Edgio or Microsoft CDN.
    /// </summary>
    public class CdnService : ICdnDriver
    {
        /// <summary>
        /// CDN group name constant.
        /// </summary>
        public static readonly string CDNGROUPNAME = "CDN";

        private readonly ILogger logger;
        private readonly HttpContext context;
        private readonly List<CdnSetting> settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="CdnService"/> class.
        /// </summary>
        /// <param name="settings">CDN settings.</param>
        /// <param name="logger">Log service.</param>
        /// <param name="context">Access to http request.</param>
        public CdnService(List<CdnSetting> settings, ILogger logger, HttpContext context)
        {
            this.logger = logger;
            this.context = context;
            this.settings = settings;
        }

        /// <summary>
        /// Gets the name of the content delivery network (CDN) provider.
        /// </summary>
        public string ProviderName => "Sky CMD CDN";

        /// <summary>
        /// Gets the CDN service.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="logger">Log service.</param>
        /// <param name="context">HTTP context.</param>
        /// <returns>CdnService.</returns>
        public static async Task<CdnService> GetCdnServiceAsync(ApplicationDbContext dbContext, ILogger logger, HttpContext context)
        {
            var data = await dbContext.Settings
                .Where(f => f.Group == CDNGROUPNAME && f.Value != null && f.Value != "").ToListAsync();

            var cdnSettings = new List<CdnSetting>();

            foreach (var setting in data)
            {
                try
                {
                    var cdnSetting = JsonConvert.DeserializeObject<CdnSetting>(setting.Value);
                    cdnSettings.Add(cdnSetting);
                }
                catch
                {
                    // Invalid JSON, remove setting
                    dbContext.Settings.Remove(setting);
                    await dbContext.SaveChangesAsync();
                }
            }

            return new CdnService(cdnSettings, logger, context);
        }

        /// <summary>
        /// Indicates if CDN integration is configured.
        /// </summary>
        /// <returns>If true then a CDN or Front Door integration is configured.</returns>
        public bool IsConfigured()
        {
            return settings.Any();
        }

        /// <summary>
        /// Checks to see if a particular CDN type is configured.
        /// </summary>
        /// <param name="type">CDN type to check for.</param>
        /// <returns>True of false.</returns>
        public bool IsConfigured(CdnProviderEnum type)
        {
            return settings.Any(a => a.CdnProvider == type);
        }

        /// <summary>
        /// Purges the CDN (or Front Door) if either is configured.
        /// </summary>
        /// <param name="purgeUrls">Purge URL Paths.</param>
        /// <returns>ArmOperation results.</returns>
        public async Task<List<CdnResult>> PurgeCdn(List<string> purgeUrls)
        {
            var results = new List<CdnResult>();

            purgeUrls = purgeUrls.Distinct().ToList();

            foreach (var setting in settings)
            {
                ICdnDriver driver = null;

                switch (setting.CdnProvider)
                {
                    case CdnProviderEnum.AzureFrontdoor:
                    case CdnProviderEnum.AzureCDN:
                        driver = new AzureCdnDriver(setting, logger);
                        break;
                    case CdnProviderEnum.Cloudflare:
                        driver = new CloudflareCdnDriver(setting, logger);
                        break;
                    case CdnProviderEnum.CloudFront:
                        driver = new CloudFrontCdnDriver(setting, logger);
                        break;
                    case CdnProviderEnum.Sucuri:
                        driver = new SucuriCdnService(setting, logger);
                        break;
                    case CdnProviderEnum.Fastly:  // Add this
                        driver = new FastlyCdnDriver(setting, logger);
                        break;
                    default:
                        break;
                }

                if (driver != null)
                {
                    results.AddRange(await driver.PurgeCdn(purgeUrls));
                }
            }

            return results;
        }

        /// <summary>
        /// Purges the entire CDN for the current endpoint.
        /// </summary>
        /// <returns>CDN purge results.</returns>
        public async Task<List<CdnResult>> PurgeCdn()
        {
            var results = new List<CdnResult>();
            foreach (var setting in settings)
            {
                ICdnDriver driver = null;
                switch (setting.CdnProvider)
                {
                    case CdnProviderEnum.AzureFrontdoor:
                    case CdnProviderEnum.AzureCDN:
                        driver = new AzureCdnDriver(setting, logger);
                        break;
                    case CdnProviderEnum.Cloudflare:
                        driver = new CloudflareCdnDriver(setting, logger);
                        break;
                    case CdnProviderEnum.CloudFront:
                        driver = new CloudFrontCdnDriver(setting, logger);
                        break;
                    case CdnProviderEnum.Sucuri:
                        driver = new SucuriCdnService(setting, logger);
                        break;
                    case CdnProviderEnum.Fastly:  // Add this
                        driver = new FastlyCdnDriver(setting, logger);
                        break;
                    default:
                        break;
                }

                if (driver != null)
                {
                    results.AddRange(await driver.PurgeCdn());
                }
            }

            return results;
        }
    }
}
