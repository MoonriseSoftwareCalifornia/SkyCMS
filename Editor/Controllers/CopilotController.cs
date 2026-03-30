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
using System.Text;
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
/// Provides server-side proxy completions and chat for Monaco-powered editing.
/// </summary>
[Route("api/copilot")]
[ApiController]
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
    [HttpGet("status")]
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
    [HttpPost("complete")]
    [EnableRateLimiting("copilot-inline")]
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
        var prompt = BuildCompletionPrompt(request, language);
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
                if ((int)response.StatusCode == 429)
                {
                    var retryAfterSeconds = GetRetryAfterSeconds(response);
                    this.logger.LogWarning(
                        "Copilot upstream rate-limited (429). Retry-After={RetryAfterSeconds}s.",
                        retryAfterSeconds);

                    return this.StatusCode(429, new
                    {
                        error = "Copilot upstream rate limit reached.",
                        retryAfterSeconds,
                    });
                }

                this.logger.LogWarning("Copilot upstream call failed with status {StatusCode}.", (int)response.StatusCode);
                return this.StatusCode(502, new
                {
                    error = "Copilot upstream provider returned an error.",
                    upstreamStatusCode = (int)response.StatusCode,
                });
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

    /// <summary>
    /// Sends an explicit chat-style coding request for the active Monaco editor context.
    /// </summary>
    /// <param name="request">Chat request payload.</param>
    /// <returns>Assistant reply.</returns>
    [HttpPost("chat")]
    [EnableRateLimiting("copilot-chat")]
    public async Task<IActionResult> Chat([FromBody] CopilotChatRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return this.BadRequest(new { error = "A chat message is required." });
        }

        var options = await this.copilotProxyOptionsService.GetOptionsAsync();
        if (!options.Enabled)
        {
            return this.StatusCode(503, new { error = "Copilot proxy is disabled." });
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.AccessToken))
        {
            this.logger.LogWarning("Copilot proxy endpoint/token is not configured for chat.");
            return this.StatusCode(503, new { error = "Copilot proxy is not configured." });
        }

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
                Messages = BuildChatMessages(request),
                Temperature = Math.Max(options.Temperature, 0.2d),
                MaxTokens = Math.Clamp(Math.Max(options.MaxTokens, 400), 128, 1200),
                Stream = false,
            };

            httpRequest.Content = JsonContent.Create(upstreamRequest, options: JsonOptions);

            using var response = await httpClient.SendAsync(httpRequest, linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 429)
                {
                    var retryAfterSeconds = GetRetryAfterSeconds(response);
                    this.logger.LogWarning(
                        "Copilot chat upstream rate-limited (429). Retry-After={RetryAfterSeconds}s.",
                        retryAfterSeconds);

                    return this.StatusCode(429, new
                    {
                        error = "Copilot chat rate limit reached.",
                        retryAfterSeconds,
                    });
                }

                this.logger.LogWarning("Copilot chat upstream call failed with status {StatusCode}.", (int)response.StatusCode);
                return this.StatusCode(502, new
                {
                    error = "Copilot upstream provider returned a chat error.",
                    upstreamStatusCode = (int)response.StatusCode,
                });
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
            var payload = await JsonSerializer.DeserializeAsync<UpstreamChatCompletionResponse>(responseStream, JsonOptions, linkedCts.Token);
            var replyText = payload?.Choices?[0]?.Message?.Content?.Trim();

            if (string.IsNullOrWhiteSpace(replyText))
            {
                return this.Ok(new CopilotChatResponse
                {
                    Reply = "I don't have a useful answer for that yet.",
                    Model = resolvedModel,
                });
            }

            return this.Ok(new CopilotChatResponse
            {
                Reply = replyText,
                Model = resolvedModel,
            });
        }
        catch (OperationCanceledException)
        {
            return this.StatusCode(504, new { error = "Copilot chat request timed out." });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Copilot chat request failed.");
            return this.StatusCode(500, new { error = "Copilot chat request failed." });
        }
    }

    private static int GetRetryAfterSeconds(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta.HasValue == true)
        {
            var seconds = (int)Math.Ceiling(retryAfter.Delta.Value.TotalSeconds);
            if (seconds > 0)
            {
                return seconds;
            }
        }

        if (retryAfter?.Date.HasValue == true)
        {
            var remaining = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
            if (seconds > 0)
            {
                return seconds;
            }
        }

        return 5;
    }

    private static string BuildCompletionPrompt(CopilotCompletionRequest request, string language)
    {
        var prefix = request.Prefix.Length > 4000 ? request.Prefix[^4000..] : request.Prefix;
        var suffix = string.IsNullOrEmpty(request.Suffix) ? string.Empty : request.Suffix;
        if (suffix.Length > 1000)
        {
            suffix = suffix[..1000];
        }

        return $"Language: {language}\nFieldId: {request.FieldId ?? string.Empty}\n\nPrefix:\n{prefix}\n\nSuffix:\n{suffix}\n\nReturn only inline completion text.";
    }

    private static List<UpstreamChatMessage> BuildChatMessages(CopilotChatRequest request)
    {
        var messages = new List<UpstreamChatMessage>
        {
            new()
            {
                Role = "system",
                Content = "You are an AI coding assistant embedded in the SkyCMS Monaco editor. Help with code explanations, fixes, refactors, and generation. Be concise, practical, and preserve the user's existing architecture unless the request clearly asks for a redesign. When returning code, prefer the smallest relevant snippet. Use markdown when useful.",
            },
        };

        if (request.Messages != null)
        {
            foreach (var message in request.Messages)
            {
                if (message == null || string.IsNullOrWhiteSpace(message.Content))
                {
                    continue;
                }

                var normalizedRole = NormalizeConversationRole(message.Role);
                if (normalizedRole == null)
                {
                    continue;
                }

                messages.Add(new UpstreamChatMessage
                {
                    Role = normalizedRole,
                    Content = TrimForPrompt(message.Content, 3000),
                });
            }
        }

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine($"Action: {request.Action ?? "chat"}");
        promptBuilder.AppendLine($"Language: {request.Language ?? "plaintext"}");
        promptBuilder.AppendLine($"Field: {request.FieldName ?? string.Empty}");
        promptBuilder.AppendLine($"Title: {request.Title ?? string.Empty}");
        promptBuilder.AppendLine($"ArticleNumber: {request.ArticleNumber ?? string.Empty}");

        if (!string.IsNullOrWhiteSpace(request.Selection))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Selected text:");
            promptBuilder.AppendLine(TrimForPrompt(request.Selection, 4000));
        }

        if (!string.IsNullOrWhiteSpace(request.CurrentCode))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Current editor content:");
            promptBuilder.AppendLine(TrimForPrompt(request.CurrentCode, 12000));
        }

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("User request:");
        promptBuilder.AppendLine(TrimForPrompt(request.Message, 3000));

        messages.Add(new UpstreamChatMessage
        {
            Role = "user",
            Content = promptBuilder.ToString(),
        });

        return messages;
    }

    private static string? NormalizeConversationRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
            ? "assistant"
            : role.Equals("user", StringComparison.OrdinalIgnoreCase)
                ? "user"
                : null;
    }

    private static string TrimForPrompt(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        return text[^maxLength..];
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
    /// Request payload for editor chat.
    /// </summary>
    public sealed class CopilotChatRequest
    {
        /// <summary>
        /// Gets or sets the explicit action invoked from the UI.
        /// </summary>
        public string? Action { get; set; }

        /// <summary>
        /// Gets or sets the chat message from the user.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the active selection, if any.
        /// </summary>
        public string? Selection { get; set; }

        /// <summary>
        /// Gets or sets the current editor content.
        /// </summary>
        public string? CurrentCode { get; set; }

        /// <summary>
        /// Gets or sets the language of the active editor model.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the active field name.
        /// </summary>
        public string? FieldName { get; set; }

        /// <summary>
        /// Gets or sets the current document title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the active article number.
        /// </summary>
        public string? ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the recent chat history.
        /// </summary>
        public List<CopilotConversationMessage> Messages { get; set; } = [];
    }

    /// <summary>
    /// Request/response conversation message.
    /// </summary>
    public sealed class CopilotConversationMessage
    {
        /// <summary>
        /// Gets or sets the role.
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// Gets or sets the content.
        /// </summary>
        public string? Content { get; set; }
    }

    /// <summary>
    /// Response payload for editor chat.
    /// </summary>
    public sealed class CopilotChatResponse
    {
        /// <summary>
        /// Gets or sets the assistant reply.
        /// </summary>
        public string Reply { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolved model.
        /// </summary>
        public string? Model { get; set; }
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