// <copyright file="CopilotController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Editor.Controllers;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Sky.Editor.Models;
using Sky.Editor.Services.Copilot;

/// <summary>
/// Provides server-side proxy completions for Monaco inline suggestions.
/// </summary>
[Route("api/copilot/complete")]
[ApiController]
[EnableRateLimiting("copilot-inline")]
[Authorize(Roles = "Reviewers, Administrators, Editors, Authors")]
public sealed class CopilotController : ControllerBase
{
    private const string DefaultCopilotModel = "gpt-4o-mini";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ICopilotProxyOptionsService copilotProxyOptionsService;
    private readonly ILogger<CopilotController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotController"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="copilotProxyOptionsService">Tenant-aware Copilot proxy options service.</param>
    /// <param name="logger">Logger instance.</param>
    public CopilotController(
        IHttpClientFactory httpClientFactory,
        ICopilotProxyOptionsService copilotProxyOptionsService,
        ILogger<CopilotController> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.copilotProxyOptionsService = copilotProxyOptionsService;
        this.logger = logger;
    }

    /// <summary>
    /// Returns whether the Copilot proxy is available for the current tenant.
    /// </summary>
    /// <returns>Proxy availability status.</returns>
    [HttpGet("/api/copilot/status")]
    public async Task<IActionResult> Status()
    {
        var options = await this.copilotProxyOptionsService.GetOptionsAsync();
        var endpointConfigured = !string.IsNullOrWhiteSpace(options.Endpoint);
        var tokenConfigured = !string.IsNullOrWhiteSpace(options.AccessToken);
        var configured = endpointConfigured && tokenConfigured;
        var resolvedModel = ResolveModel(options.Model);

        return this.Ok(new CopilotProxyStatusResponse
        {
            Enabled = options.Enabled,
            Configured = configured,
            EndpointConfigured = endpointConfigured,
            Model = resolvedModel,
        });
    }

    /// <summary>
    /// Generates inline completion text for the active Monaco cursor position.
    /// </summary>
    /// <param name="request">Completion request payload.</param>
    /// <returns>Inline completion response.</returns>
    [HttpPost]
    public async Task<IActionResult> Complete([FromBody] CopilotCompletionRequest request)
    {
        if (request == null)
        {
            return this.BadRequest(new { error = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Prefix))
        {
            return this.Ok(new CopilotCompletionResponse());
        }

        var options = await this.copilotProxyOptionsService.GetOptionsAsync();
        if (!options.Enabled)
        {
            return this.StatusCode(503, new { error = "Copilot proxy is disabled." });
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.AccessToken))
        {
            this.logger.LogWarning("Copilot proxy endpoint/token is not configured.");
            return this.StatusCode(503, new { error = "Copilot proxy is not configured." });
        }

        var language = string.IsNullOrWhiteSpace(request.Language) ? "plaintext" : request.Language;
        var prompt = BuildPrompt(request, language);
        var resolvedModel = ResolveModel(options.Model);

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.HttpContext.RequestAborted);
            linkedCts.CancelAfter(Math.Clamp(options.TimeoutMs, 1000, 60000));

            var httpClient = this.httpClientFactory.CreateClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);

            var upstreamRequest = new UpstreamChatCompletionRequest
            {
                Model = resolvedModel,
                Messages =
                [
                    new UpstreamChatMessage
                    {
                        Role = "system",
                        Content = "You are an inline coding assistant. Return only the completion text for the cursor position. Do not include markdown fences or explanations.",
                    },
                    new UpstreamChatMessage
                    {
                        Role = "user",
                        Content = prompt,
                    },
                ],
                Temperature = options.Temperature,
                MaxTokens = Math.Clamp(options.MaxTokens, 16, 1024),
                Stream = false,
            };

            httpRequest.Content = JsonContent.Create(upstreamRequest, options: JsonOptions);

            using var response = await httpClient.SendAsync(httpRequest, linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                this.logger.LogWarning("Copilot upstream call failed with status {StatusCode}.", (int)response.StatusCode);
                return this.StatusCode(502, new { error = "Copilot upstream provider returned an error." });
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
            var payload = await JsonSerializer.DeserializeAsync<UpstreamChatCompletionResponse>(responseStream, JsonOptions, linkedCts.Token);
            var completionText = payload?.Choices?[0]?.Message?.Content?.TrimEnd();

            if (string.IsNullOrWhiteSpace(completionText))
            {
                return this.Ok(new CopilotCompletionResponse());
            }

            return this.Ok(new CopilotCompletionResponse
            {
                Completion = completionText,
                Completions = [completionText],
            });
        }
        catch (OperationCanceledException)
        {
            return this.StatusCode(504, new { error = "Copilot completion request timed out." });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Copilot completion failed.");
            return this.StatusCode(500, new { error = "Copilot completion failed." });
        }
    }

    private static string BuildPrompt(CopilotCompletionRequest request, string language)
    {
        var prefix = request.Prefix.Length > 4000 ? request.Prefix[^4000..] : request.Prefix;
        var suffix = string.IsNullOrEmpty(request.Suffix) ? string.Empty : request.Suffix;
        if (suffix.Length > 1000)
        {
            suffix = suffix[..1000];
        }

        return $"Language: {language}\nFieldId: {request.FieldId ?? string.Empty}\n\nPrefix:\n{prefix}\n\nSuffix:\n{suffix}\n\nReturn only inline completion text.";
    }

    private static string ResolveModel(string? configuredModel)
    {
        if (string.IsNullOrWhiteSpace(configuredModel))
        {
            return DefaultCopilotModel;
        }

        return configuredModel.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? DefaultCopilotModel
            : configuredModel;
    }

    /// <summary>
    /// Request payload for inline completion.
    /// </summary>
    public sealed class CopilotCompletionRequest
    {
        /// <summary>
        /// Gets or sets text before the cursor.
        /// </summary>
        public string Prefix { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets text after the cursor.
        /// </summary>
        public string? Suffix { get; set; }

        /// <summary>
        /// Gets or sets Monaco language id.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the active SkyCMS field id.
        /// </summary>
        public string? FieldId { get; set; }

        /// <summary>
        /// Gets or sets the active model URI.
        /// </summary>
        public string? Uri { get; set; }
    }

    /// <summary>
    /// Response payload for inline completion.
    /// </summary>
    public sealed class CopilotCompletionResponse
    {
        /// <summary>
        /// Gets or sets the primary completion.
        /// </summary>
        public string? Completion { get; set; }

        /// <summary>
        /// Gets or sets optional completion alternatives.
        /// </summary>
        public List<string> Completions { get; set; } = [];
    }

    /// <summary>
    /// Response payload for Copilot proxy availability checks.
    /// </summary>
    public sealed class CopilotProxyStatusResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether Copilot is enabled for this tenant.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether required proxy settings are configured.
        /// </summary>
        public bool Configured { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the upstream endpoint is configured.
        /// </summary>
        public bool EndpointConfigured { get; set; }

        /// <summary>
        /// Gets or sets the configured model name.
        /// </summary>
        public string? Model { get; set; }
    }

    private sealed class UpstreamChatCompletionRequest
    {
        public string Model { get; set; } = string.Empty;

        public List<UpstreamChatMessage> Messages { get; set; } = [];

        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        public bool Stream { get; set; }
    }

    private sealed class UpstreamChatMessage
    {
        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }

    private sealed class UpstreamChatCompletionResponse
    {
        public List<UpstreamChoice>? Choices { get; set; }
    }

    private sealed class UpstreamChoice
    {
        public UpstreamChatMessage? Message { get; set; }
    }
}