// <copyright file="CloudflareCdnService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/MoonriseSoftwareCalifornia/CosmosCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class CloudflareCdnService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiToken;
        private readonly string _zoneId;
        private const string BaseUrl = "https://api.cloudflare.com/v4";

        public CloudflareCdnService(HttpClient httpClient, string apiToken, string zoneId)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiToken = apiToken ?? throw new ArgumentNullException(nameof(apiToken));
            _zoneId = zoneId ?? throw new ArgumentNullException(nameof(zoneId));
            
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiToken);
        }

        public async Task<bool> PurgeAllAsync()
        {
            var requestBody = new { purge_everything = true };
            return await PurgeAsync(requestBody);
        }

        public async Task<bool> PurgeByUrlsAsync(params string[] urls)
        {
            if (urls == null || urls.Length == 0)
                throw new ArgumentException("At least one URL must be provided", nameof(urls));

            var requestBody = new { files = urls };
            return await PurgeAsync(requestBody);
        }

        public async Task<bool> PurgeByTagsAsync(params string[] tags)
        {
            if (tags == null || tags.Length == 0)
                throw new ArgumentException("At least one tag must be provided", nameof(tags));

            var requestBody = new { tags = tags };
            return await PurgeAsync(requestBody);
        }

        public async Task<bool> PurgeByHostsAsync(params string[] hosts)
        {
            if (hosts == null || hosts.Length == 0)
                throw new ArgumentException("At least one host must be provided", nameof(hosts));

            var requestBody = new { hosts = hosts };
            return await PurgeAsync(requestBody);
        }

        private async Task<bool> PurgeAsync(object requestBody)
        {
            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{BaseUrl}/zones/{_zoneId}/purge_cache", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<CloudflareResponse>(responseContent);
                    return result?.Success == true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        private class CloudflareResponse
        {
            public bool Success { get; set; }
        }
    }
}
