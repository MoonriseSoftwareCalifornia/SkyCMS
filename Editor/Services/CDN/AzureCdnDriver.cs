// <copyright file="AzureCdnDriver.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.CDN
{
    using Azure;
    using Azure.Identity;
    using Azure.ResourceManager;
    using Azure.ResourceManager.Cdn;
    using Azure.ResourceManager.Cdn.Models;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// The Azure CDN driver.
    /// </summary>
    public class AzureCdnDriver : ICdnDriver
    {
        private readonly CdnSetting setting;
        private readonly AzureCdnConfig config;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureCdnDriver"/> class.
        /// </summary>
        /// <param name="setting">CDN setting.</param>
        /// <param name="logger">Log service.</param>
        public AzureCdnDriver(CdnSetting setting, ILogger logger)
        {
            this.setting = setting;
            config = JsonConvert.DeserializeObject<AzureCdnConfig>(setting.Value);
            this.logger = logger;
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string ProviderName
        {
            get { return config.IsFrontDoor ? "Front Door" : "Azure CDN"; }
        }

        /// <summary>
        /// Purges the specified list of URLs from the CDN.
        /// </summary>
        /// <param name="purgeUrls">List of URLs to purge.</param>
        /// <returns>CDN purge results</returns>
        /// <exception cref="ArgumentNullException">Thrown when purgeUrls is null.</exception>
        public async Task<List<CdnResult>> PurgeCdn(List<string> purgeUrls)
        {
            if (purgeUrls == null)
            {
                throw new ArgumentNullException(nameof(purgeUrls));
            }

            var results = new List<CdnResult>();
            ArmClient client = new ArmClient(new DefaultAzureCredential());

            // Check for Azure Frontdoor, if available use that.
            if (this.setting.CdnProvider == CdnProviderEnum.AzureFrontdoor)
            {
                var frontendEndpointResourceId = FrontDoorEndpointResource.CreateResourceIdentifier(
                    config.SubscriptionId,
                    config.ResourceGroup,
                    config.ProfileName,
                    config.EndpointName);

                var frontDoor = client.GetFrontDoorEndpointResource(frontendEndpointResourceId);

                var purgeContent = new FrontDoorPurgeContent(purgeUrls);

                var result = await frontDoor.PurgeContentAsync(WaitUntil.Started, purgeContent);

                var response = result.GetRawResponse();
                var msg = string.Empty;
                if (response.ContentStream != null)
                {
                    msg = await ReadStream(response.ContentStream);
                }

                var r = new CdnResult
                {
                    Status = (HttpStatusCode)response.Status,
                    ReasonPhrase = response.ReasonPhrase,
                    IsSuccessStatusCode = !response.IsError,
                    ClientRequestId = response.ClientRequestId,
                    Id = Guid.NewGuid().ToString(),
                    EstimatedFlushDateTime = DateTimeOffset.UtcNow.AddMinutes(10),
                    Message = msg,
                    Operation = result,
                    ProviderName = ProviderName
                };

                results.Add(r);

                if (response.IsError)
                {
                    logger.LogError($"Error purging content from Azure Front Door: {r.ReasonPhrase}");
                    logger.LogError($"Error purging content from Azure Front Door: {r.Message}");
                }
            }
            else
            {
                var cdnResource = CdnEndpointResource.CreateResourceIdentifier(
                   config.SubscriptionId,
                   config.ResourceGroup,
                   config.ProfileName,
                   config.EndpointName);

                var cdnEndpoint = client.GetCdnEndpointResource(cdnResource);

                var domains = cdnEndpoint.GetCdnCustomDomains();

                ArmOperation operation = null;

                if (purgeUrls.Count > 100 || purgeUrls.Any(p => p.Equals("/") || p.Equals("/*")))
                {
                    try
                    {
                        operation = await cdnEndpoint.PurgeContentAsync(WaitUntil.Started, new PurgeContent(new string[] { "/*" }));
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e.Message, e);
                    }
                }
                else
                {
                    // 100 paths or less, no need to page or use wildcard
                    var purgeContent = new PurgeContent(purgeUrls);
                    operation = await cdnEndpoint.PurgeContentAsync(WaitUntil.Started, purgeContent);
                }

                try
                {
                    var response = operation.GetRawResponse();
                    var msg = string.Empty;
                    if (response.ContentStream != null)
                    {
                        msg = await ReadStream(response.ContentStream);
                    }

                    var r = new CdnResult
                    {
                        Status = (HttpStatusCode)response.Status,
                        ReasonPhrase = response.ReasonPhrase,
                        IsSuccessStatusCode = !response.IsError,
                        ClientRequestId = response.ClientRequestId,
                        Id = Guid.NewGuid().ToString(),
                        EstimatedFlushDateTime = DateTimeOffset.UtcNow.AddMinutes(10),
                        Message = msg,
                        Operation = operation,
                        ProviderName = ProviderName
                    };
                    results.Add(r);
                    if (response.IsError)
                    {
                        logger.LogError($"Error purging content from Azure CDN: {r.ReasonPhrase}");
                        logger.LogError($"Error purging content from Azure CDN: {r.Message}");
                    }
                }
                catch (Exception e)
                {
                    var d = e; // Debugging.
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
            results = await PurgeCdn(new List<string> { "/*" });
            return results;
        }

        private async Task<string> ReadStream(Stream stream)
        {
            if (stream == null)
            {
                return string.Empty;
            }

            using var streamReader = new StreamReader(stream);
            return await streamReader.ReadToEndAsync();
        }
    }
}
