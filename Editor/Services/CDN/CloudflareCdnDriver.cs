// <copyright file="CloudflareCdnDriver.cs" company="Moonrise Software, LLC">
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
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Cloudflare CDN Service for cache management.
    /// </summary>
    public class CloudflareCdnDriver : ICdnDriver
    {
        private readonly CloudflareCdnConfig config;

        /// <summary>
        ///  Initializes a new instance of the <see cref="CloudflareCdnDriver"/> class.
        /// </summary>
        /// <param name="setting">CDN setting.</param>
        /// <param name="logger">Log service.</param>
        /// <exception cref="ArgumentNullException">Thrown when setting is null.</exception>
        /// <exception cref="JsonException">Thrown when setting.Value contains invalid JSON.</exception>
        public CloudflareCdnDriver(CdnSetting setting, ILogger logger)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            if (string.IsNullOrWhiteSpace(setting.Value))
            {
                throw new ArgumentException("CDN setting value cannot be null or empty.", nameof(setting));
            }

            try
            {
                config = JsonConvert.DeserializeObject<CloudflareCdnConfig>(setting.Value);

                if (config == null)
                {
                    throw new ArgumentException("Failed to deserialize CDN configuration.", nameof(setting));
                }
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Invalid JSON in CDN setting: {ex.Message}", nameof(setting), ex);
            }
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string ProviderName
        {
            get { return "Cloudflare"; }
        }

        /// <summary>
        ///  Purge all cached content.
        /// </summary>
        /// <returns>Success indicator.</returns>
        public async Task<List<CdnResult>> PurgeCdn()
        {
            var requestBody = new { purge_everything = true };
            var json = $"{{ \"purge_everything\": true }}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await PurgeAsync(content);
            return response;
        }

        /// <summary>
        ///  Purge cached content by specific URLs.
        /// </summary>
        /// <param name="purgeUrls">URL list.</param>
        /// <returns>Success indicator.</returns>
        public async Task<List<CdnResult>> PurgeCdn(List<string> purgeUrls)
        {
            var model = new List<CdnResult>();
            if (purgeUrls == null || purgeUrls.Count == 0 || purgeUrls.Any(a => a == "/") || purgeUrls.Any(a => a.Equals("root", StringComparison.CurrentCultureIgnoreCase)))
            {
                return await PurgeCdn(); // Nothing to purge
            }

            // Build the request.
            var json = JsonConvert.SerializeObject(new { files = purgeUrls });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            return await PurgeAsync(content);
        }

        /// <summary>
        /// Sends a request to purge the cache for the specified zone.
        /// </summary>
        /// <remarks>This method sends an HTTP POST request to the purge cache endpoint of the Cloudflare
        /// API.  If the request fails or an exception occurs, the method returns <see langword="false"/>.</remarks>
        /// <param name="content">An object representing the request payload to be sent to the purge cache endpoint.  The structure of this
        /// object must conform to the API's expected format.</param>
        /// <returns><see langword="true"/> if the cache purge request was successful; otherwise, <see langword="false"/>.</returns>
        private async Task<List<CdnResult>> PurgeAsync(StringContent content)
        {
            var model = new List<CdnResult>();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", config.ApiToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var url = $"https://api.cloudflare.com/client/v4/zones/{config.ZoneId}/purge_cache";
            var response = await httpClient.PostAsync(url, content);

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<CloudflareResponse>(responseContent);

            var cdnResult = new CdnResult
            {
                ClientRequestId = Guid.NewGuid().ToString(),
                Id = Guid.NewGuid().ToString(),
                IsSuccessStatusCode = result.Success,
                Status = result.Success ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.BadRequest,
                ReasonPhrase = "None given.",
                Message = string.Empty,
                EstimatedFlushDateTime = DateTimeOffset.UtcNow.AddSeconds(30),
                ProviderName = ProviderName
            };

            model.Add(cdnResult);
            return model;
        }

        /// <summary>
        /// Represents result information metadata returned by the Cloudflare API.
        /// </summary>
        private class CloudflareResponse
        {
            /// <summary>
            /// Gets or sets the purge result.
            /// </summary>
            [JsonProperty("result")]
            public Result Result { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether it was successful.
            /// </summary>
            [JsonProperty("success")]
            public bool Success { get; set; }

            /// <summary>
            /// Gets or sets the error list.
            /// </summary>
            [JsonProperty("errors")]
            public List<object> Errors { get; set; }

            /// <summary>
            /// Gets or sets the messages from the API.
            /// </summary>
            [JsonProperty("messages")]
            public List<string> Messages { get; set; }
        }

        /// <summary>
        ///  Result identifier.
        /// </summary>
        private class Result
        {
            /// <summary>
            /// Gets or sets the unique identifier for the object.
            /// </summary>
            [JsonProperty("id")]
            public string Id { get; set; }
        }
    }
}
