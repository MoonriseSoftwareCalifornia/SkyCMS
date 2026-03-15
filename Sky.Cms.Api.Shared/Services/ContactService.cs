// <copyright file="ContactService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Services;

using Cosmos.EmailServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sky.Cms.Api.Shared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

/// <summary>
/// Service for handling contact form submissions.
/// </summary>
public class ContactService : IContactService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ICosmosEmailSender emailSender;
    private readonly ILogger<ContactService> logger;
    private readonly ContactApiConfig config;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="emailSender">Email sender service.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="config">Contact API configuration.</param>
    public ContactService(
        IHttpClientFactory httpClientFactory,
        ICosmosEmailSender emailSender,
        ILogger<ContactService> logger,
        IOptions<ContactApiConfig> config)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(config);

        this.httpClientFactory = httpClientFactory;
        this.emailSender = emailSender;
        this.logger = logger;
        this.config = config.Value;
    }

    /// <inheritdoc/>
    public async Task<ContactFormResponse> SubmitContactFormAsync(ContactFormRequest request, string remoteIpAddress)
    {
        try
        {
            logger.LogInformation("Processing contact form from {Email} (IP: {RemoteIp})", request.Email, remoteIpAddress);

            // Build email content
            var subject = $"Contact Form Submission from {request.Name}";
            var textVersion = BuildTextEmail(request, remoteIpAddress);
            var htmlVersion = BuildHtmlEmail(request, remoteIpAddress);

            // Send email to admin
            await emailSender.SendEmailAsync(
                emailTo: config.AdminEmail,
                subject: subject,
                textVersion: textVersion,
                htmlVersion: htmlVersion,
                emailFrom: request.Email);

            // Check if email was sent successfully
            if (emailSender.SendResult.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Contact form email sent successfully from {Email} to {AdminEmail} (IP: {RemoteIp})",
                    request.Email,
                    config.AdminEmail,
                    remoteIpAddress);

                return new ContactFormResponse
                {
                    Success = true,
                    Message = "Thank you for your message. We'll get back to you soon!"
                };
            }
            else
            {
                logger.LogError(
                    "Failed to send contact form email from {Email}. Status: {StatusCode}, Message: {Message}",
                    request.Email,
                    emailSender.SendResult.StatusCode,
                    emailSender.SendResult.Message);

                return new ContactFormResponse
                {
                    Success = false,
                    Message = "We're sorry, but there was a problem sending your message. Please try again later.",
                    Error = "Email delivery failed"
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing contact form submission from {Email} (IP: {RemoteIp})",
                request.Email, remoteIpAddress);

            return new ContactFormResponse
            {
                Success = false,
                Message = "An unexpected error occurred. Please try again later.",
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateCaptchaAsync(string token, string remoteIpAddress)
    {
        if (!config.RequireCaptcha || string.IsNullOrEmpty(config.CaptchaProvider))
        {
            return true;
        }

        return config.CaptchaProvider.ToLower() switch
        {
            "turnstile" => await ValidateTurnstileAsync(token, remoteIpAddress),
            "recaptcha" => await ValidateReCaptchaAsync(token, remoteIpAddress),
            _ => false
        };
    }

    private async Task<bool> ValidateTurnstileAsync(string token, string remoteIpAddress)
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
                content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Turnstile API returned {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>();

            if (result?.Success == true)
            {
                logger.LogInformation("Turnstile validation successful for IP: {RemoteIp}", remoteIpAddress);
                return true;
            }
            else
            {
                logger.LogWarning("Turnstile validation failed for IP: {RemoteIp}. Errors: {Errors}",
                    remoteIpAddress, string.Join(", ", result?.ErrorCodes ?? Array.Empty<string>()));
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating Turnstile CAPTCHA for IP: {RemoteIp}", remoteIpAddress);
            return false;
        }
    }

    private async Task<bool> ValidateReCaptchaAsync(string token, string remoteIpAddress)
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
                content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("reCAPTCHA API returned {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<ReCaptchaResponse>();

            if (result?.Success == true && result.Score >= 0.5) // Score threshold for reCAPTCHA v3
            {
                logger.LogInformation("reCAPTCHA validation successful for IP: {RemoteIp} with score: {Score}",
                    remoteIpAddress, result.Score);
                return true;
            }
            else
            {
                logger.LogWarning("reCAPTCHA validation failed for IP: {RemoteIp}. Score: {Score}, Errors: {Errors}",
                    remoteIpAddress, result?.Score, string.Join(", ", result?.ErrorCodes ?? Array.Empty<string>()));
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating reCAPTCHA for IP: {RemoteIp}", remoteIpAddress);
            return false;
        }
    }

    private string BuildTextEmail(ContactFormRequest request, string remoteIpAddress)
    {
        var sb = new StringBuilder();
        sb.AppendLine("New Contact Form Submission");
        sb.AppendLine("===========================");
        sb.AppendLine();
        sb.AppendLine($"From: {request.Name}");
        sb.AppendLine($"Email: {request.Email}");
        sb.AppendLine($"IP Address: {remoteIpAddress}");
        sb.AppendLine($"Submitted: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("Message:");
        sb.AppendLine("--------");
        sb.AppendLine(request.Message);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("This email was sent from your website's contact form.");

        return sb.ToString();
    }

    private string BuildHtmlEmail(ContactFormRequest request, string remoteIpAddress)
    {
        var message = System.Web.HttpUtility.HtmlEncode(request.Message).Replace("\n", "<br>");
        var name = System.Web.HttpUtility.HtmlEncode(request.Name);
        var email = System.Web.HttpUtility.HtmlEncode(request.Email);

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Contact Form Submission</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
        }}
        .header {{
            background-color: #f8f9fa;
            border-left: 4px solid #007bff;
            padding: 15px;
            margin-bottom: 20px;
        }}
        .header h1 {{
            margin: 0;
            font-size: 24px;
            color: #007bff;
        }}
        .field {{
            margin-bottom: 15px;
        }}
        .field-label {{
            font-weight: 600;
            color: #555;
            display: block;
            margin-bottom: 5px;
        }}
        .field-value {{
            color: #333;
        }}
        .message-box {{
            background-color: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 4px;
            padding: 15px;
            margin-top: 10px;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 15px;
            border-top: 1px solid #dee2e6;
            font-size: 12px;
            color: #6c757d;
        }}
        .meta-info {{
            font-size: 13px;
            color: #6c757d;
        }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>?? New Contact Form Submission</h1>
    </div>

    <div class=""field"">
        <span class=""field-label"">From:</span>
        <span class=""field-value"">{name}</span>
    </div>

    <div class=""field"">
        <span class=""field-label"">Email:</span>
        <span class=""field-value""><a href=""mailto:{email}"">{email}</a></span>
    </div>

    <div class=""field meta-info"">
        <span class=""field-label"">IP Address:</span>
        <span class=""field-value"">{remoteIpAddress}</span>
    </div>

    <div class=""field meta-info"">
        <span class=""field-label"">Submitted:</span>
        <span class=""field-value"">{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</span>
    </div>

    <div class=""field"">
        <span class=""field-label"">Message:</span>
        <div class=""message-box"">
            {message}
        </div>
    </div>

    <div class=""footer"">
        This email was sent from your website's contact form.
    </div>
</body>
</html>";
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