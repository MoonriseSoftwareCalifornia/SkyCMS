// <copyright file="SucuriCdnService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.CDN
{
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// Sucuri CDN service API class.
    /// </summary>
    public class SucuriCdnService : ICdnDriver
    {
        private readonly SucuriCdnConfig config;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SucuriCdnService"/> class.
        /// </summary>
        /// <param name="setting">CDN setting.</param>
        /// <param name="logger">Log service.</param>
        public SucuriCdnService(CdnSetting setting, ILogger logger)
        {
            config = JsonConvert.DeserializeObject<SucuriCdnConfig>(setting.Value);
            this.logger = logger;
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string ProviderName
        {
            get { return "Sucuri"; }
        }

        /// <summary>
        /// Purges the specified list of URLs from the CDN.
        /// </summary>
        /// <param name="purgeUrls">List of URLs to purge.</param>
        /// <returns>CDN purge results.</returns>
        public async Task<List<CdnResult>> PurgeCdn(List<string> purgeUrls)
        {
            var responses = new List<CdnResult>();

            if (purgeUrls.Count == 0 || purgeUrls.Count > 20 || purgeUrls[0] == "/")
            {
                responses.Add(await PurgeContentAsync(string.Empty));
            }
            else
            {
                foreach (var path in purgeUrls)
                {
                    responses.Add(await PurgeContentAsync(path));
                }
            }

            return responses;
        }

        /// <summary>
        /// Purges the entire CDN for the current endpoint.
        /// </summary>
        /// <returns>CDN purge results.</returns>
        public async Task<List<CdnResult>> PurgeCdn()
        {
            var responses = new List<CdnResult>
            {
                await PurgeContentAsync(string.Empty)
            };

            return responses;
        }

        private async Task<CdnResult> PurgeContentAsync(string path)
        {
            using var client = new HttpClient();
            var requestUri = $"https://waf.sucuri.net/api?k={config.ApiKey}&s={config.ApiSecret}&a=clearcache";
            string json = string.Empty;
            if (!string.IsNullOrEmpty(path))
            {
                requestUri += $"&file={path}";
            }

            var result = await client.GetAsync(requestUri);

            var response = await result.Content.ReadAsStringAsync();

            return new CdnResult
            {
                ClientRequestId = Guid.NewGuid().ToString(),
                Id = Guid.NewGuid().ToString(),
                IsSuccessStatusCode = result.IsSuccessStatusCode,
                Status = result.StatusCode,
                ReasonPhrase = result.ReasonPhrase,
                EstimatedFlushDateTime = DateTimeOffset.UtcNow.AddMinutes(2),
                ProviderName = ProviderName
            };
        }

    }
}
