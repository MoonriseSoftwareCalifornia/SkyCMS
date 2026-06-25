// <copyright file="AiProxyController.cs" company="Moonrise Software, LLC">
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
/// Provides server-side proxy completions and chat for editor AI features.
/// </summary>
[Route("api/ai-proxy")]
[Route("api/copilot")]
[ApiController]
[Authorize(Roles = "Reviewers, Administrators, Editors, Authors")]
public sealed class AiProxyController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ICopilotProxyOptionsService copilotProxyOptionsService;
    private readonly IAiProviderModelCatalogService aiProviderModelCatalogService;
    private readonly IAiUserPreferenceService aiUserPreferenceService;
    private readonly IAiDocumentationContextService aiDocumentationContextService;
    private readonly IAiSourceCodeIndexService aiSourceCodeIndexService;
    private readonly IAiLayoutContextService aiLayoutContextService;
    private readonly ILogger<AiProxyController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiProxyController"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="copilotProxyOptionsService">Tenant-aware Copilot proxy options service.</param>
    /// <param name="aiProviderModelCatalogService">Provider model catalog service.</param>
    /// <param name="aiUserPreferenceService">User AI preference service.</param>
    /// <param name="aiDocumentationContextService">Documentation context enrichment service.</param>
    /// <param name="aiSourceCodeIndexService">Source code index service.</param>
    /// <param name="aiLayoutContextService">Layout context enrichment service.</param>
    /// <param name="logger">Logger instance.</param>
    public AiProxyController(
        IHttpClientFactory httpClientFactory,
        ICopilotProxyOptionsService copilotProxyOptionsService,
        IAiProviderModelCatalogService aiProviderModelCatalogService,
        IAiUserPreferenceService aiUserPreferenceService,
        IAiDocumentationContextService aiDocumentationContextService,
        IAiSourceCodeIndexService aiSourceCodeIndexService,
        IAiLayoutContextService aiLayoutContextService,
        ILogger<AiProxyController> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.copilotProxyOptionsService = copilotProxyOptionsService;
        this.aiProviderModelCatalogService = aiProviderModelCatalogService;
        this.aiUserPreferenceService = aiUserPreferenceService;
        this.aiDocumentationContextService = aiDocumentationContextService;
        this.aiSourceCodeIndexService = aiSourceCodeIndexService;
        this.aiLayoutContextService = aiLayoutContextService;
        this.logger = logger;
    }

    /// <summary>
    /// Returns whether the Copilot proxy is available for the current tenant.
    /// </summary>
    /// <returns>Proxy availability status.</returns>
    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] string? editorKind = null, [FromQuery] string? documentKind = null)
    {
        var options = await this.copilotProxyOptionsService.GetOptionsAsync();
        var endpointConfigured = !string.IsNullOrWhiteSpace(options.Endpoint);
        var tokenConfigured = !string.IsNullOrWhiteSpace(options.AccessToken);
        var configured = endpointConfigured && tokenConfigured;
        var providerMetadata = AiProviderMetadataResolver.Describe(options.Endpoint, options.Model);
        var configuredModel = AiProviderMetadataResolver.NormalizeConfiguredModel(options.Model);
        var effectiveModel = AiProviderMetadataResolver.ResolveEffectiveModel(options.Endpoint, options.Model);
        var selectedModel = providerMetadata.SupportsUserModelSelection
            ? await this.aiUserPreferenceService.GetSelectedModelAsync(this.User, providerMetadata.ProviderKey, editorKind, documentKind, this.HttpContext.RequestAborted)
            : null;

        return this.Ok(new CopilotProxyStatusResponse
        {
            Enabled = options.Enabled,
            Configured = configured,
            EndpointConfigured = endpointConfigured,
            Model = effectiveModel ?? configuredModel,
            ConfiguredModel = configuredModel,
            EffectiveModel = effectiveModel,
            ProviderKey = providerMetadata.ProviderKey,
            ProviderDisplayName = providerMetadata.ProviderDisplayName,
            SupportsModelDiscovery = providerMetadata.SupportsModelDiscovery,
            SupportsUserModelSelection = providerMetadata.SupportsUserModelSelection,
            SupportsAutoMode = providerMetadata.SupportsAutoMode,
            DiscoveryState = providerMetadata.DiscoveryState,
            DiscoveryStateMessage = providerMetadata.DiscoveryStateMessage,
            DefaultModeDescription = providerMetadata.DefaultModeDescription,
            DefaultSelectionLabel = AiProviderMetadataResolver.BuildDefaultSelectionLabel(options.Endpoint, options.Model),
            SelectedModel = selectedModel,
        });
    }

    /// <summary>
    /// Returns provider metadata and discoverable model options for the current tenant.
    /// </summary>
    /// <returns>Provider model catalog response.</returns>
    [HttpGet("models")]
    public async Task<IActionResult> Models([FromQuery] string? editorKind = null, [FromQuery] string? documentKind = null, [FromQuery] bool forceRefresh = false)
    {
        var options = await this.copilotProxyOptionsService.GetOptionsAsync();
        var endpointConfigured = !string.IsNullOrWhiteSpace(options.Endpoint);
        var tokenConfigured = !string.IsNullOrWhiteSpace(options.AccessToken);
        var configured = endpointConfigured && tokenConfigured;
        var configuredModel = AiProviderMetadataResolver.NormalizeConfiguredModel(options.Model);
        var effectiveModel = AiProviderMetadataResolver.ResolveEffectiveModel(options.Endpoint, options.Model);
        var catalog = await this.aiProviderModelCatalogService.GetCatalogAsync(options, forceRefresh, this.HttpContext.RequestAborted);
        var selectedModel = catalog.SupportsUserModelSelection
            ? await this.aiUserPreferenceService.GetSelectedModelAsync(this.User, catalog.ProviderKey, editorKind, documentKind, this.HttpContext.RequestAborted)
            : null;

        return this.Ok(new CopilotProxyModelsResponse
        {
            Enabled = options.Enabled,
            Configured = configured,
            EndpointConfigured = endpointConfigured,
            ProviderKey = catalog.ProviderKey,
            ProviderDisplayName = catalog.ProviderDisplayName,
            SupportsModelDiscovery = catalog.SupportsModelDiscovery,
            SupportsUserModelSelection = catalog.SupportsUserModelSelection,
            SupportsAutoMode = catalog.SupportsAutoMode,
            DiscoveryState = catalog.DiscoveryState,
            DiscoveryStateMessage = catalog.DiscoveryStateMessage,
            DefaultModeDescription = catalog.DefaultModeDescription,
            DefaultSelectionLabel = AiProviderMetadataResolver.BuildDefaultSelectionLabel(options.Endpoint, options.Model),
            ConfiguredModel = configuredModel,
            EffectiveModel = effectiveModel,
            DiscoveryError = catalog.DiscoveryError,
            SelectedModel = selectedModel,
            Models = catalog.Models,
        });
    }

    /// <summary>
    /// Saves the current user's selected model for a specific editor/document context.
    /// </summary>
    /// <param name="request">Model preference request.</param>
    /// <returns>Saved preference response.</returns>
    [HttpPost("preferences/model")]
    public async Task<IActionResult> SaveModelPreference([FromBody] CopilotModelPreferenceRequest request)
    {
        if (request == null)
        {
            return this.BadRequest(new { error = "Request body is required." });
        }

        var options = await this.copilotProxyOptionsService.GetOptionsAsync();
        var providerMetadata = AiProviderMetadataResolver.Describe(options.Endpoint, options.Model);
        if (!providerMetadata.SupportsUserModelSelection)
        {
            return this.BadRequest(new { error = "This AI provider does not support user model selection." });
        }

        await this.aiUserPreferenceService.SaveSelectedModelAsync(
            this.User,
            providerMetadata.ProviderKey,
            request.EditorKind,
            request.DocumentKind,
            request.SelectedModel,
            this.HttpContext.RequestAborted);

        return this.Ok(new CopilotModelPreferenceResponse
        {
            ProviderKey = providerMetadata.ProviderKey,
            SelectedModel = string.IsNullOrWhiteSpace(request.SelectedModel) ? null : request.SelectedModel.Trim(),
            EditorKind = request.EditorKind,
            DocumentKind = request.DocumentKind,
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
        var resolvedModel = AiProviderMetadataResolver.ResolveEffectiveModel(options.Endpoint, options.Model, request.SelectedModel);

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.HttpContext.RequestAborted);
            linkedCts.CancelAfter(Math.Clamp(options.TimeoutMs, 1000, 60000));

            var httpClient = this.httpClientFactory.CreateClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
            ApplyAuthenticationHeaders(httpRequest, options.Endpoint, options.AccessToken);

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

        var contextRequest = new AiContextEnrichmentRequest
        {
            DocumentKind = request.DocumentKind,
            SectionKind = request.SectionKind,
            Message = request.Message,
            ArticleNumber = request.ArticleNumber,
            TemplateId = request.TemplateId,
            LayoutId = request.LayoutId,
            UrlPath = request.UrlPath,
        };

        var docsContextTask = this.aiDocumentationContextService.GetDocumentationContextAsync(contextRequest, this.HttpContext.RequestAborted);
        var layoutContextTask = this.aiLayoutContextService.GetLayoutContextAsync(contextRequest, this.HttpContext.RequestAborted);
        var sourceCodeContextTask = IsHelpChatRequest(request)
            ? this.aiSourceCodeIndexService.SearchSourceCodeAsync(request.Message, this.HttpContext.RequestAborted)
            : Task.FromResult<IReadOnlyList<AiSourceCodeSearchResult>>([]);

        await Task.WhenAll(docsContextTask, layoutContextTask, sourceCodeContextTask);

        var documentationContext = docsContextTask.Result.ContextText;
        var layoutContext = layoutContextTask.Result.ContextText;
        var sourceCodeContext = BuildSourceCodeContext(sourceCodeContextTask.Result);

        var resolvedModel = AiProviderMetadataResolver.ResolveEffectiveModel(options.Endpoint, options.Model, request.SelectedModel);

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.HttpContext.RequestAborted);
            linkedCts.CancelAfter(Math.Clamp(options.TimeoutMs, 1000, 60000));

            var httpClient = this.httpClientFactory.CreateClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
            ApplyAuthenticationHeaders(httpRequest, options.Endpoint, options.AccessToken);

            var upstreamRequest = new UpstreamChatCompletionRequest
            {
                Model = resolvedModel,
                Messages = BuildChatMessages(request, documentationContext, layoutContext, sourceCodeContext),
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

    private static void ApplyAuthenticationHeaders(HttpRequestMessage request, string endpoint, string accessToken)
    {
        if (UsesAzureOpenAiApiKey(endpoint))
        {
            request.Headers.TryAddWithoutValidation("api-key", accessToken);
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static bool UsesAzureOpenAiApiKey(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            return false;
        }

        return endpointUri.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCompletionPrompt(CopilotCompletionRequest request, string language)
    {
        var prefix = request.Prefix.Length > 4000 ? request.Prefix[^4000..] : request.Prefix;
        var suffix = string.IsNullOrEmpty(request.Suffix) ? string.Empty : request.Suffix;
        if (suffix.Length > 1000)
        {
            suffix = suffix[..1000];
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Language: {language}");
        sb.AppendLine($"FieldId: {request.FieldId ?? string.Empty}");

        if (!string.IsNullOrWhiteSpace(request.DocumentKind))
        {
            sb.AppendLine($"DocumentKind: {request.DocumentKind}");
        }

        if (!string.IsNullOrWhiteSpace(request.SectionKind))
        {
            sb.AppendLine($"SectionKind: {request.SectionKind}");
        }

        sb.AppendLine();
        sb.AppendLine($"Prefix:\n{prefix}");
        sb.AppendLine($"\nSuffix:\n{suffix}");
        sb.AppendLine("\nReturn only inline completion text.");

        return sb.ToString();
    }

    private static List<UpstreamChatMessage> BuildChatMessages(CopilotChatRequest request, string? documentationContext = null, string? layoutContext = null, string? sourceCodeContext = null)
    {
        var messages = new List<UpstreamChatMessage>
        {
            new ()
            {
                Role = "system",
                Content = BuildChatSystemPrompt(request),
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
        promptBuilder.AppendLine($"EditorKind: {request.EditorKind ?? "monaco"}");
        promptBuilder.AppendLine($"Action: {request.Action ?? "chat"}");

        if (!string.IsNullOrWhiteSpace(request.ChatMode))
        {
            promptBuilder.AppendLine($"ChatMode: {request.ChatMode}");
        }

        promptBuilder.AppendLine($"Language: {request.Language ?? "plaintext"}");
        promptBuilder.AppendLine($"Field: {request.FieldName ?? string.Empty}");
        promptBuilder.AppendLine($"Title: {request.Title ?? string.Empty}");
        promptBuilder.AppendLine($"ArticleNumber: {request.ArticleNumber ?? string.Empty}");

        if (!string.IsNullOrWhiteSpace(request.DocumentKind))
        {
            promptBuilder.AppendLine($"DocumentKind: {request.DocumentKind}");
        }

        if (!string.IsNullOrWhiteSpace(request.SectionKind))
        {
            promptBuilder.AppendLine($"SectionKind: {request.SectionKind}");
        }

        if (!string.IsNullOrWhiteSpace(request.ArticleType))
        {
            promptBuilder.AppendLine($"ArticleType: {request.ArticleType}");
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            promptBuilder.AppendLine($"Category: {request.Category}");
        }

        if (!string.IsNullOrWhiteSpace(request.UrlPath))
        {
            promptBuilder.AppendLine($"UrlPath: {request.UrlPath}");
        }

        if (!string.IsNullOrWhiteSpace(request.Selection))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(IsCkeditorRequest(request) ? "Selected HTML fragment:" : "Selected text:");
            promptBuilder.AppendLine(TrimForPrompt(request.Selection, 4000));
        }

        if (!string.IsNullOrWhiteSpace(request.CurrentCode))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(IsCkeditorRequest(request) ? "Current editor HTML fragment:" : "Current editor content:");
            promptBuilder.AppendLine(TrimForPrompt(request.CurrentCode, 12000));
        }

        if (!string.IsNullOrWhiteSpace(layoutContext))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(layoutContext);
        }

        if (!string.IsNullOrWhiteSpace(documentationContext))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(documentationContext);
        }

        if (!string.IsNullOrWhiteSpace(sourceCodeContext))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(sourceCodeContext);
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

    private static bool IsHelpChatRequest(CopilotChatRequest request)
    {
        return string.Equals(request.EditorKind, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.ChatMode, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Action, "help", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSourceCodeContext(IReadOnlyList<AiSourceCodeSearchResult> results)
    {
        if (results.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Source code context from SkyCMS repository:");

        foreach (var result in results)
        {
            sb.AppendLine($"- {result.SymbolName ?? result.FilePath}");
            if (!string.IsNullOrWhiteSpace(result.Signature))
            {
                sb.AppendLine($"  Signature: {result.Signature}");
            }

            if (!string.IsNullOrWhiteSpace(result.Snippet))
            {
                sb.AppendLine($"  Snippet: {TrimForPrompt(result.Snippet, 1200)}");
            }

            if (!string.IsNullOrWhiteSpace(result.GitHubUrl))
            {
                sb.AppendLine($"  Source: {result.GitHubUrl}");
            }
        }

        return sb.ToString();
    }

    private static string BuildChatSystemPrompt(CopilotChatRequest request)
    {
        var normalizedChatMode = NormalizeChatMode(request.ChatMode);
        if (IsHelpRequest(request, normalizedChatMode))
        {
            return normalizedChatMode == "site-help"
                ? "You are an AI help assistant for SkyCMS website teams. Answer general questions about SkyCMS and practical website development guidance for content editors, authors, and administrators. Prioritize clear explanations over code. Use site and page context from the prompt when available (for example UrlPath, DocumentKind, and SectionKind). Do not claim to know unpublished runtime data you have not been given. If details are missing, ask a focused follow-up question."
                : "You are an AI help assistant for SkyCMS users. Answer general questions about SkyCMS and website development in concise, practical language. This is a non-editor chat experience, so do not assume an active code editor region. Prefer guidance and explanations instead of writing code unless the user explicitly asks for an example snippet.";
        }

        if (IsCkeditorRequest(request))
        {
            var ckBase = "You are an AI writing assistant embedded in the SkyCMS CKEditor experience. The active context is a single rich-text editor region only, not the full page. Help with grammar, tone, clarity, structure, rewriting, summarization, and generating polished copy while preserving the author's intent. Preserve existing HTML structure unless the request explicitly asks to change it. Do not return a full HTML document. When suggesting concrete edits, explain briefly and include the proposed result in a fenced ```html``` block that can be applied directly to the current region, selection, or cursor position.";

            return request.DocumentKind switch
            {
                "blog" => ckBase + " This is a blog post: favour readability, a strong intro, conversational tone, and clear calls to action where appropriate.",
                "article" => ckBase + " This is a general article or page: be informative, clear, and well-structured.",
                _ => ckBase,
            };
        }

        return request.SectionKind switch
        {
            "layout-head" => "You are an AI coding assistant embedded in the SkyCMS Monaco editor. The active field is the layout <head> section. Focus on meta tags, Open Graph tags, structured data (JSON-LD), canonical links, CSS references, and preload hints. Do not emit <body> content. Be concise and preserve existing tags unless explicitly asked to change them. Use markdown when useful.",
            "layout-body-start" => "You are an AI coding assistant embedded in the SkyCMS Monaco editor. The active field is the layout body-start/header region. Focus on navigation markup, skip-nav links, banner HTML, and Bootstrap layout helpers. Do not emit <head> content. Be concise and preserve the existing shell unless asked to redesign it. Use markdown when useful.",
            "layout-body-end" => "You are an AI coding assistant embedded in the SkyCMS Monaco editor. The active field is the layout body-end/footer region. Focus on footer markup, closing scripts, cookie/consent banners, and structured data for the site footer. Keep scripts deferred or at end-of-body. Be concise. Use markdown when useful.",
            "template-content" => "You are an AI coding assistant embedded in the SkyCMS Monaco editor. The active field is a reusable page template. Focus on scaffold HTML, Razor layout directives, placeholder comments, and Bootstrap grid structure. Avoid page-specific copy or hardcoded data. Be concise and use markdown when useful.",
            "article-head-script" => "You are an AI coding assistant embedded in the SkyCMS Monaco editor. The active field is injected into the article's <head>. Focus on page-specific meta tags, JSON-LD structured data, canonical hints, and lightweight CSS overrides. Do not emit <body> markup. Be concise. Use markdown when useful.",
            "article-footer-script" => "You are an AI coding assistant embedded in the SkyCMS Monaco editor. The active field is injected before the article's closing </body>. Focus on deferred scripts, analytics snippets, and page-specific initialisation. Keep scripts minimal. Use markdown when useful.",
            "article-content" or "blog-content" => "You are an AI coding assistant embedded in the SkyCMS Monaco editor. The active field is the main content body of a " + (request.DocumentKind == "blog" ? "blog post" : "page") + ". Help with HTML structure, readability, and rich content markup. Preserve author intent and existing styles. Be concise. Use markdown when useful.",
            _ => "You are an AI coding assistant embedded in the SkyCMS Monaco editor. Help with code explanations, fixes, refactors, and generation. Be concise, practical, and preserve the user's existing architecture unless the request clearly asks for a redesign. When returning code, prefer the smallest relevant snippet. Use markdown when useful.",
        };
    }

    private static bool IsCkeditorRequest(CopilotChatRequest request)
    {
        return string.Equals(request.EditorKind, "ckeditor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHelpRequest(CopilotChatRequest request, string? normalizedChatMode)
    {
        if (normalizedChatMode is "general-help" or "site-help")
        {
            return true;
        }

        return string.Equals(request.EditorKind, "help", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeChatMode(string? chatMode)
    {
        if (string.IsNullOrWhiteSpace(chatMode))
        {
            return null;
        }

        return chatMode.Trim().ToLowerInvariant();
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

        /// <summary>
        /// Gets or sets the document kind (layout, template, article, blog).
        /// </summary>
        public string? DocumentKind { get; set; }

        /// <summary>
        /// Gets or sets the section kind within the document (e.g. layout-head, article-content).
        /// </summary>
        public string? SectionKind { get; set; }

        /// <summary>
        /// Gets or sets an optional per-request selected model.
        /// </summary>
        public string? SelectedModel { get; set; }
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
        /// Gets or sets the editor surface originating the request.
        /// </summary>
        public string? EditorKind { get; set; }

        /// <summary>
        /// Gets or sets the chat mode for non-editor conversations.
        /// </summary>
        public string? ChatMode { get; set; }

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

        /// <summary>
        /// Gets or sets the document kind (layout, template, article, blog).
        /// </summary>
        public string? DocumentKind { get; set; }

        /// <summary>
        /// Gets or sets the section kind within the document (e.g. layout-head, article-content).
        /// </summary>
        public string? SectionKind { get; set; }

        /// <summary>
        /// Gets or sets the article type string (General, BlogPost).
        /// </summary>
        public string? ArticleType { get; set; }

        /// <summary>
        /// Gets or sets the article/blog category.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Gets or sets the URL path of the current document.
        /// </summary>
        public string? UrlPath { get; set; }

        /// <summary>
        /// Gets or sets the template ID, when editing a template.
        /// </summary>
        public string? TemplateId { get; set; }

        /// <summary>
        /// Gets or sets the layout ID, when editing a layout.
        /// </summary>
        public string? LayoutId { get; set; }

        /// <summary>
        /// Gets or sets an optional per-request selected model.
        /// </summary>
        public string? SelectedModel { get; set; }
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
    /// Request payload for saving a user model preference.
    /// </summary>
    public sealed class CopilotModelPreferenceRequest
    {
        /// <summary>
        /// Gets or sets the editor kind.
        /// </summary>
        public string? EditorKind { get; set; }

        /// <summary>
        /// Gets or sets the document kind.
        /// </summary>
        public string? DocumentKind { get; set; }

        /// <summary>
        /// Gets or sets the selected model.
        /// </summary>
        public string? SelectedModel { get; set; }
    }

    /// <summary>
    /// Response payload for saving a user model preference.
    /// </summary>
    public sealed class CopilotModelPreferenceResponse
    {
        /// <summary>
        /// Gets or sets the provider key.
        /// </summary>
        public string ProviderKey { get; set; } = "unknown";

        /// <summary>
        /// Gets or sets the editor kind.
        /// </summary>
        public string? EditorKind { get; set; }

        /// <summary>
        /// Gets or sets the document kind.
        /// </summary>
        public string? DocumentKind { get; set; }

        /// <summary>
        /// Gets or sets the selected model.
        /// </summary>
        public string? SelectedModel { get; set; }
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

        /// <summary>
        /// Gets or sets the raw configured model value.
        /// </summary>
        public string? ConfiguredModel { get; set; }

        /// <summary>
        /// Gets or sets the effective model used when the request is sent.
        /// </summary>
        public string? EffectiveModel { get; set; }

        /// <summary>
        /// Gets or sets the provider key.
        /// </summary>
        public string ProviderKey { get; set; } = "unknown";

        /// <summary>
        /// Gets or sets the provider display name for UI labels.
        /// </summary>
        public string ProviderDisplayName { get; set; } = "AI";

        /// <summary>
        /// Gets or sets a value indicating whether provider model discovery is supported.
        /// </summary>
        public bool SupportsModelDiscovery { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether user model selection is supported.
        /// </summary>
        public bool SupportsUserModelSelection { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether SkyCMS auto mode is supported.
        /// </summary>
        public bool SupportsAutoMode { get; set; }

        /// <summary>
        /// Gets or sets the discovery state.
        /// </summary>
        public string DiscoveryState { get; set; } = AiProviderDiscoveryStates.Unsupported;

        /// <summary>
        /// Gets or sets the discovery state message.
        /// </summary>
        public string DiscoveryStateMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider default mode description.
        /// </summary>
        public string DefaultModeDescription { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default selection label for UI pickers.
        /// </summary>
        public string DefaultSelectionLabel { get; set; } = "Default";

        /// <summary>
        /// Gets or sets the user-selected model for the current editor context.
        /// </summary>
        public string? SelectedModel { get; set; }
    }

    /// <summary>
    /// Response payload for provider model discovery.
    /// </summary>
    public sealed class CopilotProxyModelsResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether AI is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether required proxy settings are configured.
        /// </summary>
        public bool Configured { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the endpoint is configured.
        /// </summary>
        public bool EndpointConfigured { get; set; }

        /// <summary>
        /// Gets or sets the provider key.
        /// </summary>
        public string ProviderKey { get; set; } = "unknown";

        /// <summary>
        /// Gets or sets the provider display name.
        /// </summary>
        public string ProviderDisplayName { get; set; } = "AI";

        /// <summary>
        /// Gets or sets a value indicating whether provider model discovery is supported.
        /// </summary>
        public bool SupportsModelDiscovery { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether user model selection is supported.
        /// </summary>
        public bool SupportsUserModelSelection { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether SkyCMS auto mode is supported.
        /// </summary>
        public bool SupportsAutoMode { get; set; }

        /// <summary>
        /// Gets or sets the discovery state.
        /// </summary>
        public string DiscoveryState { get; set; } = AiProviderDiscoveryStates.Unsupported;

        /// <summary>
        /// Gets or sets the discovery state message.
        /// </summary>
        public string DiscoveryStateMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider default mode description.
        /// </summary>
        public string DefaultModeDescription { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default selection label for UI pickers.
        /// </summary>
        public string DefaultSelectionLabel { get; set; } = "Default";

        /// <summary>
        /// Gets or sets the configured model.
        /// </summary>
        public string? ConfiguredModel { get; set; }

        /// <summary>
        /// Gets or sets the effective model.
        /// </summary>
        public string? EffectiveModel { get; set; }

        /// <summary>
        /// Gets or sets the discovery error text.
        /// </summary>
        public string? DiscoveryError { get; set; }

        /// <summary>
        /// Gets or sets the user-selected model for the current editor context.
        /// </summary>
        public string? SelectedModel { get; set; }

        /// <summary>
        /// Gets or sets the available models.
        /// </summary>
        public List<AiProviderModelOption> Models { get; set; } = [];
    }

    private sealed class UpstreamChatCompletionRequest
    {
        public string? Model { get; set; }

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