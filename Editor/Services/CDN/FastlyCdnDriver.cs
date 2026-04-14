// <copyright file="FastlyCdnDriver.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.CDN
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Fastly CDN driver implementation using native HTTP APIs.
    /// </summary>
    public class FastlyCdnDriver : ICdnDriver
    {
        private readonly FastlyCdnConfig config;
        private readonly ILogger logger;
        private readonly HttpClient httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="FastlyCdnDriver"/> class.
        /// </summary>
        /// <param name="setting">CDN settings containing Fastly configuration.</param>
        /// <param name="logger">Logger instance.</param>
        public FastlyCdnDriver(CdnSetting setting, ILogger logger)
        {
            this.logger = logger;
            config = JsonSerializer.Deserialize<FastlyCdnConfig>(setting.Value);
            httpClient = new HttpClient();
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string ProviderName => "Fastly";

        /// <summary>
        /// Purges the specified paths from the Fastly CDN.
        /// </summary>
        /// <param name="purgeUrls">List of URL paths to purge.</param>
        /// <returns>List of CDN purge results.</returns>
        public async Task<List<CdnResult>> PurgeCdn(List<string> purgeUrls)
        {
            var results = new List<CdnResult>();

            if (purgeUrls == null || !purgeUrls.Any())
            {
                return results;
            }

            foreach (var url in purgeUrls.Distinct())
            {
                try
                {
                    var result = await PurgeSingleUrl(url);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to purge Fastly URL: {url}");
                    results.Add(new CdnResult
                    {
                        ProviderName = ProviderName,
                        IsSuccessStatusCode = false,
                        Message = $"Error: {ex.Message}",
                        EstimatedFlushDateTime = DateTimeOffset.UtcNow
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Purges all content from the Fastly CDN service.
        /// </summary>
        /// <returns>List containing the purge result.</returns>
        public async Task<List<CdnResult>> PurgeCdn()
        {
            var results = new List<CdnResult>();

            try
            {
                var result = await PurgeAll();
                results.Add(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to purge all Fastly content");
                results.Add(new CdnResult
                {
                    ProviderName = ProviderName,
                    IsSuccessStatusCode = false,
                    Message = $"Error: {ex.Message}",
                    EstimatedFlushDateTime = DateTimeOffset.UtcNow
                });
            }

            return results;
        }

        /// <summary>
        /// Purges a single URL from Fastly.
        /// </summary>
        /// <param name="url">URL path to purge.</param>
        /// <returns>CDN result.</returns>
        private async Task<CdnResult> PurgeSingleUrl(string url)
        {
            // Construct the full URL to purge
            var fullUrl = $"https://{config.Domain}{url}";

            // Fastly purge endpoint using PURGE method
            var request = new HttpRequestMessage(HttpMethod.Post, fullUrl);
            request.Method = new HttpMethod("PURGE");
            request.Headers.Add("Fastly-Key", config.ApiToken);

            // Optionally add soft purge header for stale-while-revalidate behavior
            if (config.SoftPurge)
            {
                request.Headers.Add("Fastly-Soft-Purge", "1");
            }

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            var purgeResponse = JsonSerializer.Deserialize<FastlyPurgeResponse>(responseContent);

            return new CdnResult
            {
                ProviderName = ProviderName,
                IsSuccessStatusCode = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode
                    ? $"Successfully purged: {url}"
                    : $"Failed to purge {url}: {response.ReasonPhrase}",
                Id = purgeResponse?.Id,
                EstimatedFlushDateTime = DateTimeOffset.UtcNow.AddSeconds(5) // Fastly purges are typically very fast (5-150ms)
            };
        }

        /// <summary>
        /// Purges all content for the service.
        /// </summary>
        /// <returns>CDN result.</returns>
        private async Task<CdnResult> PurgeAll()
        {
            // Fastly API endpoint for purging all content
            var url = $"https://api.fastly.com/service/{config.ServiceId}/purge_all";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Fastly-Key", config.ApiToken);
            request.Headers.Add("Accept", "application/json");

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            var purgeResponse = JsonSerializer.Deserialize<FastlyPurgeAllResponse>(responseContent);

            return new CdnResult
            {
                ProviderName = ProviderName,
                IsSuccessStatusCode = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode
                    ? $"Successfully purged all content (status: {purgeResponse?.Status})"
                    : $"Failed to purge all: {response.ReasonPhrase}",
                EstimatedFlushDateTime = DateTimeOffset.UtcNow.AddSeconds(5)
            };
        }

        /// <summary>
        /// Fastly purge response model.
        /// </summary>
        private class FastlyPurgeResponse
        {
            public string Status { get; set; }

            public string Id { get; set; }
        }

        /// <summary>
        /// Fastly purge all response model.
        /// </summary>
        private class FastlyPurgeAllResponse
        {
            public string Status { get; set; }
        }
    }
}