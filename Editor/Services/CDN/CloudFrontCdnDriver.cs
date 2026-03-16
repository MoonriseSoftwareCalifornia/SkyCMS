// <copyright file="CloudFrontCdnDriver.cs" company="Moonrise Software, LLC">
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
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Amazon CloudFront CDN Service for cache invalidation management.
    /// </summary>
    public class CloudFrontCdnDriver : ICdnDriver
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly CloudFrontCdnConfig config;
        private readonly ILogger logger;

        /// <summary>
        ///  Initializes a new instance of the <see cref="CloudFrontCdnDriver"/> class.
        /// </summary>
        /// <param name="setting">CDN setting.</param>
        /// <param name="logger">Log service.</param>
        /// <exception cref="ArgumentNullException">Thrown when setting is null.</exception>
        /// <exception cref="JsonException">Thrown when setting.Value contains invalid JSON.</exception>
        public CloudFrontCdnDriver(CdnSetting setting, ILogger logger)
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
                config = JsonConvert.DeserializeObject<CloudFrontCdnConfig>(setting.Value);

                if (config == null)
                {
                    throw new ArgumentException("Failed to deserialize CDN configuration.", nameof(setting));
                }

                // Validate required configuration values
                if (string.IsNullOrWhiteSpace(config.DistributionId))
                {
                    throw new ArgumentException("CloudFront Distribution ID is required.", nameof(setting));
                }

                if (string.IsNullOrWhiteSpace(config.AccessKeyId))
                {
                    throw new ArgumentException("AWS Access Key ID is required.", nameof(setting));
                }

                if (string.IsNullOrWhiteSpace(config.SecretAccessKey))
                {
                    throw new ArgumentException("AWS Secret Access Key is required.", nameof(setting));
                }

                if (string.IsNullOrWhiteSpace(config.Region))
                {
                    throw new ArgumentException("AWS Region is required.", nameof(setting));
                }
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Invalid JSON in CDN setting: {ex.Message}", nameof(setting), ex);
            }

            this.logger = logger;
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string ProviderName
        {
            get { return "CloudFront"; }
        }

        /// <summary>
        ///  Purge all cached content by invalidating all paths.
        /// </summary>
        /// <returns>CDN purge results.</returns>
        public async Task<List<CdnResult>> PurgeCdn()
        {
            return await PurgeCdn(new List<string> { "/*" });
        }

        /// <summary>
        ///  Purge cached content by specific URLs or paths.
        /// </summary>
        /// <param name="purgeUrls">URL or path list.</param>
        /// <returns>CDN purge results.</returns>
        public async Task<List<CdnResult>> PurgeCdn(List<string> purgeUrls)
        {
            var model = new List<CdnResult>();

            if (purgeUrls == null || purgeUrls.Count == 0 || purgeUrls.Any(a => a == "/") || purgeUrls.Any(a => a.Equals("root", StringComparison.CurrentCultureIgnoreCase)))
            {
                purgeUrls = new List<string> { "/*" };
            }

            try
            {
                var invalidationId = await CreateInvalidationAsync(purgeUrls);

                var cdnResult = new CdnResult
                {
                    ClientRequestId = Guid.NewGuid().ToString(),
                    Id = invalidationId,
                    IsSuccessStatusCode = !string.IsNullOrEmpty(invalidationId),
                    Status = !string.IsNullOrEmpty(invalidationId) ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
                    ReasonPhrase = !string.IsNullOrEmpty(invalidationId) ? "Invalidation created successfully" : "Failed to create invalidation",
                    Message = $"Invalidation ID: {invalidationId}",
                    EstimatedFlushDateTime = DateTimeOffset.UtcNow.AddMinutes(5),
                    ProviderName = ProviderName
                };

                model.Add(cdnResult);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Network error during CloudFront invalidation");

                var cdnResult = new CdnResult
                {
                    ClientRequestId = Guid.NewGuid().ToString(),
                    Id = string.Empty,
                    IsSuccessStatusCode = false,
                    Status = HttpStatusCode.InternalServerError,
                    ReasonPhrase = "Network error",
                    Message = ex.Message,
                    EstimatedFlushDateTime = DateTimeOffset.UtcNow,
                    ProviderName = ProviderName
                };

                model.Add(cdnResult);
            }
            catch (TaskCanceledException ex)
            {
                logger.LogError(ex, "CloudFront invalidation request timed out");

                var cdnResult = new CdnResult
                {
                    ClientRequestId = Guid.NewGuid().ToString(),
                    Id = string.Empty,
                    IsSuccessStatusCode = false,
                    Status = HttpStatusCode.RequestTimeout,
                    ReasonPhrase = "Request timed out",
                    Message = ex.Message,
                    EstimatedFlushDateTime = DateTimeOffset.UtcNow,
                    ProviderName = ProviderName
                };

                model.Add(cdnResult);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating CloudFront invalidation");

                var cdnResult = new CdnResult
                {
                    ClientRequestId = Guid.NewGuid().ToString(),
                    Id = string.Empty,
                    IsSuccessStatusCode = false,
                    Status = HttpStatusCode.InternalServerError,
                    ReasonPhrase = "Exception occurred",
                    Message = ex.Message,
                    EstimatedFlushDateTime = DateTimeOffset.UtcNow,
                    ProviderName = ProviderName
                };

                model.Add(cdnResult);
            }

            return model;
        }

        /// <summary>
        /// Creates an invalidation request in CloudFront.
        /// </summary>
        /// <param name="paths">List of paths to invalidate.</param>
        /// <returns>The invalidation ID if successful, otherwise null.</returns>
        private async Task<string> CreateInvalidationAsync(List<string> paths)
        {
            // Capture the current time once to ensure consistency across all usages
            var utcNow = DateTime.UtcNow;
            var timestamp = utcNow.ToString("yyyyMMddTHHmmssZ");
            var dateStamp = utcNow.ToString("yyyyMMdd");
            var amzDate = utcNow.ToString("yyyyMMddTHHmmssZ");
            var callerReference = $"SkyCMS-{timestamp}-{Guid.NewGuid()}";

            // Build the invalidation batch XML
            var pathItems = string.Join(string.Empty, paths.Select(p => $"<Path>{System.Security.SecurityElement.Escape(p)}</Path>"));
            var requestBody = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<InvalidationBatch>
    <Paths>
        <Quantity>{paths.Count}</Quantity>
        <Items>
            {pathItems}
        </Items>
    </Paths>
    <CallerReference>{callerReference}</CallerReference>
</InvalidationBatch>";

            var endpoint = $"https://cloudfront.amazonaws.com/2020-05-31/distribution/{config.DistributionId}/invalidation";

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/xml");

            // Get the actual Content-Type value (includes charset parameter added by StringContent)
            var contentType = request.Content.Headers.ContentType.ToString();

            // AWS Signature Version 4 signing process
            var authorization = CreateAwsSignature(
                request.Method.Method,
                endpoint,
                requestBody,
                amzDate,
                dateStamp,
                contentType);

            request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

            var response = await HttpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Extract invalidation ID from XML response using more robust parsing
                var match = System.Text.RegularExpressions.Regex.Match(responseContent, @"<Id>([^<]+)</Id>");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            else
            {
                logger.LogError($"CloudFront API error: {response.StatusCode} - {responseContent}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Creates AWS Signature Version 4 authorization header.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="endpoint">API endpoint URL.</param>
        /// <param name="requestBody">Request body content.</param>
        /// <param name="amzDate">AMZ date timestamp.</param>
        /// <param name="dateStamp">Date stamp.</param>
        /// <param name="contentType">Content-Type header value.</param>
        /// <returns>Authorization header value.</returns>
        private string CreateAwsSignature(string method, string endpoint, string requestBody, string amzDate, string dateStamp, string contentType)
        {
            var service = "cloudfront";
            var region = config.Region;
            var algorithm = "AWS4-HMAC-SHA256";
            var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";

            // Create canonical request
            var uri = new Uri(endpoint);
            var canonicalUri = uri.AbsolutePath;
            var canonicalQueryString = string.Empty;
            // Headers must be sorted alphabetically and lowercase
            var canonicalHeaders = $"content-type:{contentType}\nhost:{uri.Host}\nx-amz-date:{amzDate}\n";
            var signedHeaders = "content-type;host;x-amz-date";
            var payloadHash = ComputeSha256Hash(requestBody);

            var canonicalRequest = $"{method}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

            // Create string to sign
            var canonicalRequestHash = ComputeSha256Hash(canonicalRequest);
            var stringToSign = $"{algorithm}\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";

            // Calculate signature
            var signingKey = GetSignatureKey(config.SecretAccessKey, dateStamp, region, service);
            var signature = ComputeHmacSha256(signingKey, stringToSign);

            // Build authorization header
            return $"{algorithm} Credential={config.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        }

        /// <summary>
        /// Computes SHA256 hash of the input string.
        /// </summary>
        /// <param name="data">Input data.</param>
        /// <returns>Hex-encoded hash.</returns>
        private string ComputeSha256Hash(string data)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// Computes HMAC-SHA256.
        /// </summary>
        /// <param name="key">Key bytes.</param>
        /// <param name="data">Data to hash.</param>
        /// <returns>Hex-encoded hash.</returns>
        private string ComputeHmacSha256(byte[] key, string data)
        {
            using var hmac = new HMACSHA256(key);
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// Computes HMAC-SHA256 returning raw bytes.
        /// </summary>
        /// <param name="key">Key bytes.</param>
        /// <param name="data">Data to hash.</param>
        /// <returns>Hash bytes.</returns>
        private byte[] ComputeHmacSha256Bytes(byte[] key, string data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Derives the AWS signing key.
        /// </summary>
        /// <param name="key">Secret access key.</param>
        /// <param name="dateStamp">Date stamp.</param>
        /// <param name="regionName">AWS region.</param>
        /// <param name="serviceName">AWS service name.</param>
        /// <returns>Signing key bytes.</returns>
        private byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
        {
            var kSecret = Encoding.UTF8.GetBytes($"AWS4{key}");
            var kDate = ComputeHmacSha256Bytes(kSecret, dateStamp);
            var kRegion = ComputeHmacSha256Bytes(kDate, regionName);
            var kService = ComputeHmacSha256Bytes(kRegion, serviceName);
            var kSigning = ComputeHmacSha256Bytes(kService, "aws4_request");
            return kSigning;
        }
    }
}