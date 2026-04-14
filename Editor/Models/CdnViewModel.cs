// <copyright file="CdnViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// CDN View Model.
    /// </summary>
    public class CdnViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CdnViewModel"/> class.
        /// </summary>
        public CdnViewModel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CdnViewModel"/> class.
        /// </summary>
        /// <param name="settings">CDN Settings List.</param>
        public CdnViewModel(List<CdnSetting> settings)
        {
            if (settings == null)
            {
                return;
            }

            foreach (var setting in settings)
            {
                // Skip settings with null or empty values
                if (string.IsNullOrWhiteSpace(setting.Value))
                {
                    continue;
                }

                switch (setting.CdnProvider)
                {
                    case CdnProviderEnum.AzureCDN:
                    case CdnProviderEnum.AzureFrontdoor:
                        AzureCdn = JsonConvert.DeserializeObject<AzureCdnConfig>(setting.Value);
                        break;
                    case CdnProviderEnum.Cloudflare:
                        Cloudflare = JsonConvert.DeserializeObject<CloudflareCdnConfig>(setting.Value);
                        break;
                    case CdnProviderEnum.CloudFront:
                        CloudFront = JsonConvert.DeserializeObject<CloudFrontCdnConfig>(setting.Value);
                        break;
                    case CdnProviderEnum.Sucuri:
                        Sucuri = JsonConvert.DeserializeObject<SucuriCdnConfig>(setting.Value);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Gets or sets the configuration settings for the Azure Content Delivery Network (CDN).
        /// </summary>
        public AzureCdnConfig AzureCdn { get; set; } = new AzureCdnConfig();

        /// <summary>
        /// Gets or sets the configuration settings for the Cloudflare CDN.
        /// </summary>
        public CloudflareCdnConfig Cloudflare { get; set; } = new CloudflareCdnConfig();

        /// <summary>
        /// Gets or sets the configuration settings for the Amazon CloudFront CDN.
        /// </summary>
        public CloudFrontCdnConfig CloudFront { get; set; } = new CloudFrontCdnConfig();

        /// <summary>
        /// Gets or sets the configuration settings for the Sucuri CDN.
        /// </summary>
        public SucuriCdnConfig Sucuri { get; set; } = new SucuriCdnConfig();
    }
}
