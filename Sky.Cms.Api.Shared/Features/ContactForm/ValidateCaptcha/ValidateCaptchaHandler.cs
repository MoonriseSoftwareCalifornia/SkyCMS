// <copyright file="ValidateCaptchaHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Features.ContactForm.ValidateCaptcha;

using Cosmos.Common.Features.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sky.Cms.Api.Shared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Handler for validating CAPTCHA tokens.
/// </summary>
public class ValidateCaptchaHandler : IQueryHandler<ValidateCaptchaQuery, bool>
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<ValidateCaptchaHandler> logger;
    private readonly ContactApiConfig config;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateCaptchaHandler"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="config">Contact API configuration.</param>
    public ValidateCaptchaHandler(
        IHttpClientFactory httpClientFactory,
        ILogger<ValidateCaptchaHandler> logger,
        IOptions<ContactApiConfig> config)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.config = (config ?? throw new ArgumentNullException(nameof(config))).Value;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleAsync(ValidateCaptchaQuery query, CancellationToken cancellationToken = default)
    {
        if (!config.RequireCaptcha || string.IsNullOrEmpty(config.CaptchaProvider))
        {
            logger.LogDebug("CAPTCHA validation skipped - not required");
            return true;
        }

        return config.CaptchaProvider.ToLower() switch
        {
            "turnstile" => await ValidateTurnstileAsync(query.Token, query.RemoteIpAddress, cancellationToken),
            "recaptcha" => await ValidateReCaptchaAsync(query.Token, query.RemoteIpAddress, cancellationToken),
            _ => false
        };
    }

    private async Task<bool> ValidateTurnstileAsync(string token, string remoteIpAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", config.CaptchaSecretKey ?? string.Empty),
                new KeyValuePair<string, string>("response", token),
                new KeyValuePair<string, string>("remoteip", remoteIpAddress)
            });

            var response = await httpClient.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Turnstile API returned {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: cancellationToken);

            if (result?.Success == true)
            {
                logger.LogInformation("Turnstile validation successful for IP: {RemoteIp}", remoteIpAddress);
                return true;
            }
            else
            {
                logger.LogWarning(
                    "Turnstile validation failed for IP: {RemoteIp}. Errors: {Errors}",
                    remoteIpAddress,
                    string.Join(", ", result?.ErrorCodes ?? Array.Empty<string>()));
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating Turnstile CAPTCHA for IP: {RemoteIp}", remoteIpAddress);
            return false;
        }
    }

    private async Task<bool> ValidateReCaptchaAsync(string token, string remoteIpAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", config.CaptchaSecretKey ?? string.Empty),
                new KeyValuePair<string, string>("response", token),
                new KeyValuePair<string, string>("remoteip", remoteIpAddress)
            });

            var response = await httpClient.PostAsync(
                "https://www.google.com/recaptcha/api/siteverify",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("reCAPTCHA API returned {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<ReCaptchaResponse>(cancellationToken: cancellationToken);

            if (result?.Success == true && result.Score >= 0.5) // Score threshold for reCAPTCHA v3
            {
                logger.LogInformation(
                    "reCAPTCHA validation successful for IP: {RemoteIp} with score: {Score}",
                    remoteIpAddress,
                    result.Score);
                return true;
            }
            else
            {
                logger.LogWarning(
                    "reCAPTCHA validation failed for IP: {RemoteIp}. Score: {Score}, Errors: {Errors}",
                    remoteIpAddress,
                    result?.Score,
                    string.Join(", ", result?.ErrorCodes ?? Array.Empty<string>()));
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating reCAPTCHA for IP: {RemoteIp}", remoteIpAddress);
            return false;
        }
    }

    private class TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }

        [JsonPropertyName("challenge_ts")]
        public string? ChallengeTimestamp { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }
    }

    private class ReCaptchaResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("challenge_ts")]
        public string? ChallengeTimestamp { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}