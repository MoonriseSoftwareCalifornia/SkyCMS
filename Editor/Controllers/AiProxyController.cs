// <copyright file="AiProxyController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Editor.Controllers;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
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
    private static readonly int[] HistogramUpperBounds = [1000, 2000, 4000, 8000];
    private static readonly ConcurrentDictionary<string, TokenTelemetryAccumulator> TokenTelemetryByDocumentKind = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ICopilotProxyOptionsService copilotProxyOptionsService;
    private readonly IAiProviderModelCatalogService aiProviderModelCatalogService;
    private readonly IAiUserPreferenceService aiUserPreferenceService;
    private readonly IEditorContextPayloadService editorContextPayloadService;
    private readonly IAiDocumentationContextService aiDocumentationContextService;
    private readonly IAiSourceCodeIndexService aiSourceCodeIndexService;
    private readonly IAiHelpQueryContextService aiHelpQueryContextService;
    private readonly IAiLayoutContextService aiLayoutContextService;
    private readonly ILogger<AiProxyController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiProxyController"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="copilotProxyOptionsService">Tenant-aware Copilot proxy options service.</param>
    /// <param name="aiProviderModelCatalogService">Provider model catalog service.</param>
    /// <param name="aiUserPreferenceService">User AI preference service.</param>
    /// <param name="editorContextPayloadService">Editor context payload service.</param>
    /// <param name="aiDocumentationContextService">Documentation context enrichment service.</param>
    /// <param name="aiSourceCodeIndexService">Source code index service.</param>
    /// <param name="aiHelpQueryContextService">Help query context service.</param>
    /// <param name="aiLayoutContextService">Layout context enrichment service.</param>
    /// <param name="logger">Logger instance.</param>
    public AiProxyController(
        IHttpClientFactory httpClientFactory,
        ICopilotProxyOptionsService copilotProxyOptionsService,
        IAiProviderModelCatalogService aiProviderModelCatalogService,
        IAiUserPreferenceService aiUserPreferenceService,
        IEditorContextPayloadService editorContextPayloadService,
        IAiDocumentationContextService aiDocumentationContextService,
        IAiSourceCodeIndexService aiSourceCodeIndexService,
        IAiHelpQueryContextService aiHelpQueryContextService,
        IAiLayoutContextService aiLayoutContextService,
        ILogger<AiProxyController> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.copilotProxyOptionsService = copilotProxyOptionsService;
        this.aiProviderModelCatalogService = aiProviderModelCatalogService;
        this.aiUserPreferenceService = aiUserPreferenceService;
        this.editorContextPayloadService = editorContextPayloadService;
        this.aiDocumentationContextService = aiDocumentationContextService;
        this.aiSourceCodeIndexService = aiSourceCodeIndexService;
        this.aiHelpQueryContextService = aiHelpQueryContextService;
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
    /// Returns staleness/health diagnostics for AI documentation and source-code indices.
    /// This endpoint is intended for dashboard visualization and monitoring.
    /// </summary>
    /// <returns>Index health response.</returns>
    [HttpGet("index-health")]
    public IActionResult IndexHealth()
    {
        var docsSnapshot = AiDocumentationIndexService.GetHealthSnapshot();
        var sourceSnapshot = AiSourceCodeIndexService.GetHealthSnapshot();
        var now = DateTimeOffset.UtcNow;

        var docsThreshold = TimeSpan.FromHours(24);
        var sourceThreshold = TimeSpan.FromDays(7);

        var docsStale = IsStale(docsSnapshot.LastSuccessfulRefreshUtc, docsThreshold, now);
        var sourceStale = IsStale(sourceSnapshot.LastSuccessfulRefreshUtc, sourceThreshold, now);

        if (docsStale)
        {
            this.logger.LogWarning(
                "AI docs index is stale. LastSuccessfulRefreshUtc={LastSuccessfulRefreshUtc}; ThresholdHours={ThresholdHours}",
                docsSnapshot.LastSuccessfulRefreshUtc,
                docsThreshold.TotalHours);
        }

        if (sourceStale)
        {
            this.logger.LogWarning(
                "AI source-code index is stale. LastSuccessfulRefreshUtc={LastSuccessfulRefreshUtc}; ThresholdDays={ThresholdDays}",
                sourceSnapshot.LastSuccessfulRefreshUtc,
                sourceThreshold.TotalDays);
        }

        return this.Ok(new
        {
            generatedAtUtc = now,
            documentation = new
            {
                name = docsSnapshot.IndexName,
                stale = docsStale,
                staleThresholdHours = docsThreshold.TotalHours,
                lastSuccessfulRefreshUtc = docsSnapshot.LastSuccessfulRefreshUtc,
                ageHours = GetAgeHours(docsSnapshot.LastSuccessfulRefreshUtc, now),
                lastAttemptUtc = docsSnapshot.LastAttemptUtc,
                lastIndexedEntryCount = docsSnapshot.LastIndexedEntryCount,
                lastFetchError = docsSnapshot.LastFetchError,
                lastFetchErrorUtc = docsSnapshot.LastFetchErrorUtc,
                lastParseError = docsSnapshot.LastParseError,
                lastParseErrorUtc = docsSnapshot.LastParseErrorUtc,
            },
            sourceCode = new
            {
                name = sourceSnapshot.IndexName,
                stale = sourceStale,
                staleThresholdDays = sourceThreshold.TotalDays,
                lastSuccessfulRefreshUtc = sourceSnapshot.LastSuccessfulRefreshUtc,
                ageHours = GetAgeHours(sourceSnapshot.LastSuccessfulRefreshUtc, now),
                lastAttemptUtc = sourceSnapshot.LastAttemptUtc,
                lastIndexedEntryCount = sourceSnapshot.LastIndexedEntryCount,
                lastFetchError = sourceSnapshot.LastFetchError,
                lastFetchErrorUtc = sourceSnapshot.LastFetchErrorUtc,
                lastParseError = sourceSnapshot.LastParseError,
                lastParseErrorUtc = sourceSnapshot.LastParseErrorUtc,
            },
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
        var editorContextPayload = await this.editorContextPayloadService.BuildPayloadAsync(
            new EditorContextPayloadRequest
            {
                EditorSurface = "monaco",
                DocumentKind = request.DocumentKind,
                SectionKind = request.SectionKind,
                Language = language,
                CurrentField = request.FieldId,
                CurrentFieldValue = request.Prefix,
                Lightweight = true,
            },
            this.HttpContext.RequestAborted);

        var prompt = BuildCompletionPrompt(request, language, editorContextPayload);
        var resolvedModel = AiProviderMetadataResolver.ResolveEffectiveModel(options.Endpoint, options.Model, request.SelectedModel);
        var tokenEstimationStopwatch = Stopwatch.StartNew();
        var baseContextTokens = EstimateTokenCount($"Language:{language};FieldId:{request.FieldId};DocumentKind:{request.DocumentKind};SectionKind:{request.SectionKind}");
        var entityContextTokens = EstimateTokenCount(editorContextPayload);
        var renderingContextTokens = 0;
        var knowledgeContextTokens = 0;
        var responseTokenEstimationMs = 0d;

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

            var promptTokens = EstimateTokenCount(upstreamRequest.Messages);
            tokenEstimationStopwatch.Stop();

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

                var unknownModelFailure = await TryGetUnknownModelFailureAsync(
                    response,
                    request.SelectedModel,
                    resolvedModel,
                    options.Endpoint,
                    options.Model);

                if (unknownModelFailure != null)
                {
                    await this.ClearInvalidModelPreferenceAsync(request.SelectedModel, options.Endpoint, "monaco", request.DocumentKind, linkedCts.Token);

                    if (options.AutoRetryUnknownModel &&
                        !string.IsNullOrWhiteSpace(request.SelectedModel) &&
                        !string.IsNullOrWhiteSpace(unknownModelFailure.FallbackModel) &&
                        !string.Equals(unknownModelFailure.FallbackModel, resolvedModel, StringComparison.OrdinalIgnoreCase))
                    {
                        this.logger.LogInformation(
                            "Retrying completion with fallback model {FallbackModel} after unknown_model for selected model {SelectedModel}.",
                            unknownModelFailure.FallbackModel,
                            request.SelectedModel);

                        return await this.Complete(new CopilotCompletionRequest
                        {
                            Prefix = request.Prefix,
                            Suffix = request.Suffix,
                            Language = request.Language,
                            FieldId = request.FieldId,
                            DocumentKind = request.DocumentKind,
                            SectionKind = request.SectionKind,
                            SelectedModel = null,
                        });
                    }

                    return this.BadRequest(new
                    {
                        error = "Selected model is not available for the configured provider.",
                        attemptedModel = unknownModelFailure.AttemptedModel,
                        selectedModel = unknownModelFailure.SelectedModel,
                        fallbackModel = unknownModelFailure.FallbackModel,
                        upstreamCode = unknownModelFailure.UpstreamCode,
                        upstreamMessage = unknownModelFailure.UpstreamMessage,
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
                this.logger.LogInformation(
                    "Token estimation profile: op={Operation}; model={Model}; elapsedMs={ElapsedMs}",
                    "complete",
                    resolvedModel ?? "default",
                    Math.Round(tokenEstimationStopwatch.Elapsed.TotalMilliseconds, 2));

                this.LogTokenAccounting(
                    operation: "complete",
                    documentKind: request.DocumentKind,
                    baseContextTokens: baseContextTokens,
                    entityContextTokens: entityContextTokens,
                    renderingContextTokens: renderingContextTokens,
                    knowledgeContextTokens: knowledgeContextTokens,
                    promptTokens: promptTokens,
                    responseTokens: 0,
                    model: resolvedModel);

                return this.Ok(new CopilotCompletionResponse());
            }

            var responseTokenStopwatch = Stopwatch.StartNew();
            var responseTokens = EstimateTokenCount(completionText);
            responseTokenStopwatch.Stop();
            responseTokenEstimationMs = responseTokenStopwatch.Elapsed.TotalMilliseconds;

            this.logger.LogInformation(
                "Token estimation profile: op={Operation}; model={Model}; promptEstimationMs={PromptEstimationMs}; responseEstimationMs={ResponseEstimationMs}; totalMs={TotalMs}",
                "complete",
                resolvedModel ?? "default",
                Math.Round(tokenEstimationStopwatch.Elapsed.TotalMilliseconds, 2),
                Math.Round(responseTokenEstimationMs, 2),
                Math.Round(tokenEstimationStopwatch.Elapsed.TotalMilliseconds + responseTokenEstimationMs, 2));

            this.LogTokenAccounting(
                operation: "complete",
                documentKind: request.DocumentKind,
                baseContextTokens: baseContextTokens,
                entityContextTokens: entityContextTokens,
                renderingContextTokens: renderingContextTokens,
                knowledgeContextTokens: knowledgeContextTokens,
                promptTokens: promptTokens,
                responseTokens: responseTokens,
                model: resolvedModel);

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

        var editorContextPayloadTask = this.editorContextPayloadService.BuildPayloadAsync(
            new EditorContextPayloadRequest
            {
                EditorSurface = request.EditorKind,
                DocumentKind = request.DocumentKind,
                SectionKind = request.SectionKind,
                Language = request.Language,
                CurrentField = request.FieldName,
                CurrentFieldValue = request.CurrentCode,
                Selection = request.Selection,
                ArticleNumber = request.ArticleNumber,
                LayoutId = request.LayoutId,
                TemplateId = request.TemplateId,
                Title = request.Title,
                UrlPath = request.UrlPath,
                Lightweight = false,
            },
            this.HttpContext.RequestAborted);

        var docsContextTask = this.aiDocumentationContextService.GetDocumentationContextAsync(contextRequest, this.HttpContext.RequestAborted);
        var layoutContextTask = this.aiLayoutContextService.GetLayoutContextAsync(contextRequest, this.HttpContext.RequestAborted);
        var sourceCodeContextTask = IsHelpChatRequest(request)
            ? this.aiSourceCodeIndexService.SearchSourceCodeAsync(request.Message, this.HttpContext.RequestAborted)
            : Task.FromResult<IReadOnlyList<AiSourceCodeSearchResult>>([]);

        await Task.WhenAll(docsContextTask, layoutContextTask, sourceCodeContextTask, editorContextPayloadTask);

        var documentationContext = docsContextTask.Result.ContextText;
        var layoutContext = layoutContextTask.Result.ContextText;
        var sourceCodeContext = BuildSourceCodeContext(sourceCodeContextTask.Result);
        var editorContextPayload = editorContextPayloadTask.Result;
        var tokenEstimationStopwatch = Stopwatch.StartNew();
        var baseContextTokens = EstimateTokenCount($"EditorKind:{request.EditorKind};Action:{request.Action};Language:{request.Language};Field:{request.FieldName};DocumentKind:{request.DocumentKind};SectionKind:{request.SectionKind};ArticleNumber:{request.ArticleNumber};TemplateId:{request.TemplateId};LayoutId:{request.LayoutId};UrlPath:{request.UrlPath}");
        var entityContextTokens = EstimateTokenCount(editorContextPayload);
        var renderingContextTokens = EstimateTokenCount(layoutContext);
        var knowledgeContextTokens = EstimateTokenCount(documentationContext) + EstimateTokenCount(sourceCodeContext);
        var responseTokenEstimationMs = 0d;

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
                Messages = BuildChatMessages(request, documentationContext, layoutContext, sourceCodeContext, editorContextPayload),
                Temperature = Math.Max(options.Temperature, 0.2d),
                MaxTokens = Math.Clamp(Math.Max(options.MaxTokens, 400), 128, 1200),
                Stream = false,
            };

            var promptTokens = EstimateTokenCount(upstreamRequest.Messages);
            tokenEstimationStopwatch.Stop();

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

                var unknownModelFailure = await TryGetUnknownModelFailureAsync(
                    response,
                    request.SelectedModel,
                    resolvedModel,
                    options.Endpoint,
                    options.Model);

                if (unknownModelFailure != null)
                {
                    await this.ClearInvalidModelPreferenceAsync(request.SelectedModel, options.Endpoint, request.EditorKind, request.DocumentKind, linkedCts.Token);

                    if (options.AutoRetryUnknownModel &&
                        !string.IsNullOrWhiteSpace(request.SelectedModel) &&
                        !string.IsNullOrWhiteSpace(unknownModelFailure.FallbackModel) &&
                        !string.Equals(unknownModelFailure.FallbackModel, resolvedModel, StringComparison.OrdinalIgnoreCase))
                    {
                        this.logger.LogInformation(
                            "Retrying chat with fallback model {FallbackModel} after unknown_model for selected model {SelectedModel}.",
                            unknownModelFailure.FallbackModel,
                            request.SelectedModel);

                        return await this.Chat(new CopilotChatRequest
                        {
                            EditorKind = request.EditorKind,
                            Action = request.Action,
                            Message = request.Message,
                            Selection = request.Selection,
                            CurrentCode = request.CurrentCode,
                            Language = request.Language,
                            FieldName = request.FieldName,
                            Title = request.Title,
                            ArticleNumber = request.ArticleNumber,
                            TemplateId = request.TemplateId,
                            LayoutId = request.LayoutId,
                            UrlPath = request.UrlPath,
                            DocumentKind = request.DocumentKind,
                            SectionKind = request.SectionKind,
                            ChatMode = request.ChatMode,
                            Messages = request.Messages,
                            SelectedModel = null,
                        });
                    }

                    return this.BadRequest(new
                    {
                        error = "Selected model is not available for the configured provider.",
                        attemptedModel = unknownModelFailure.AttemptedModel,
                        selectedModel = unknownModelFailure.SelectedModel,
                        fallbackModel = unknownModelFailure.FallbackModel,
                        upstreamCode = unknownModelFailure.UpstreamCode,
                        upstreamMessage = unknownModelFailure.UpstreamMessage,
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
                this.logger.LogInformation(
                    "Token estimation profile: op={Operation}; model={Model}; elapsedMs={ElapsedMs}",
                    "chat",
                    resolvedModel ?? "default",
                    Math.Round(tokenEstimationStopwatch.Elapsed.TotalMilliseconds, 2));

                this.LogTokenAccounting(
                    operation: "chat",
                    documentKind: request.DocumentKind,
                    baseContextTokens: baseContextTokens,
                    entityContextTokens: entityContextTokens,
                    renderingContextTokens: renderingContextTokens,
                    knowledgeContextTokens: knowledgeContextTokens,
                    promptTokens: promptTokens,
                    responseTokens: 0,
                    model: resolvedModel);

                return this.Ok(new CopilotChatResponse
                {
                    Reply = "I don't have a useful answer for that yet.",
                    Model = resolvedModel,
                });
            }

            var responseTokenStopwatch = Stopwatch.StartNew();
            var responseTokens = EstimateTokenCount(replyText);
            responseTokenStopwatch.Stop();
            responseTokenEstimationMs = responseTokenStopwatch.Elapsed.TotalMilliseconds;

            this.logger.LogInformation(
                "Token estimation profile: op={Operation}; model={Model}; promptEstimationMs={PromptEstimationMs}; responseEstimationMs={ResponseEstimationMs}; totalMs={TotalMs}",
                "chat",
                resolvedModel ?? "default",
                Math.Round(tokenEstimationStopwatch.Elapsed.TotalMilliseconds, 2),
                Math.Round(responseTokenEstimationMs, 2),
                Math.Round(tokenEstimationStopwatch.Elapsed.TotalMilliseconds + responseTokenEstimationMs, 2));

            this.LogTokenAccounting(
                operation: "chat",
                documentKind: request.DocumentKind,
                baseContextTokens: baseContextTokens,
                entityContextTokens: entityContextTokens,
                renderingContextTokens: renderingContextTokens,
                knowledgeContextTokens: knowledgeContextTokens,
                promptTokens: promptTokens,
                responseTokens: responseTokens,
                model: resolvedModel);

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

    /// <summary>
    /// Handles dedicated help-query requests and returns grounded responses with source attribution.
    /// </summary>
    /// <param name="request">Help query request payload.</param>
    /// <returns>Help response with optional source attributions.</returns>
    [HttpPost("/api/ai-help/query")]
    [EnableRateLimiting("copilot-chat")]
    public async Task<IActionResult> HelpQuery([FromBody] AiHelpQueryRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            return this.BadRequest(new { error = "Query is required." });
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

        var contextResult = await this.aiHelpQueryContextService.BuildContextAsync(
            new AiHelpQueryContextRequest
            {
                Query = request.Query,
                ChatMode = request.ChatMode,
                DocumentKind = request.DocumentKind,
                SectionKind = request.SectionKind,
                ArticleNumber = request.ArticleNumber,
                TemplateId = request.TemplateId,
                LayoutId = request.LayoutId,
                UrlPath = request.UrlPath,
            },
            this.HttpContext.RequestAborted);

        var baseContextTokens = EstimateTokenCount($"ChatMode:{request.ChatMode};DocumentKind:{request.DocumentKind};SectionKind:{request.SectionKind};ArticleNumber:{request.ArticleNumber};TemplateId:{request.TemplateId};LayoutId:{request.LayoutId};UrlPath:{request.UrlPath};Query:{request.Query}");
        var entityContextTokens = 0;
        var renderingContextTokens = 0;
        var knowledgeContextTokens = EstimateTokenCount(contextResult.ContextText);
        var tokenEstimationStopwatch = Stopwatch.StartNew();
        var responseTokenEstimationMs = 0d;

        var resolvedModel = AiProviderMetadataResolver.ResolveEffectiveModel(options.Endpoint, options.Model, request.SelectedModel);

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.HttpContext.RequestAborted);
            linkedCts.CancelAfter(Math.Clamp(options.TimeoutMs, 1000, 60000));

            var helpPromptRequest = new CopilotChatRequest
            {
                EditorKind = "help",
                ChatMode = request.ChatMode,
                Message = request.Query,
                DocumentKind = request.DocumentKind,
                SectionKind = request.SectionKind,
                ArticleNumber = request.ArticleNumber,
                TemplateId = request.TemplateId,
                LayoutId = request.LayoutId,
                UrlPath = request.UrlPath,
                Messages = request.Messages,
            };

            var helpMessages = BuildChatMessages(helpPromptRequest, contextResult.ContextText, null, null, null);

            var httpClient = this.httpClientFactory.CreateClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
            ApplyAuthenticationHeaders(httpRequest, options.Endpoint, options.AccessToken);

            var upstreamRequest = new UpstreamChatCompletionRequest
            {
                Model = resolvedModel,
                Messages = helpMessages,
                Temperature = Math.Clamp(Math.Max(options.Temperature, 0.2d), 0, 1),
                MaxTokens = Math.Clamp(Math.Max(options.MaxTokens, 500), 128, 1600),
                Stream = false,
            };

            var promptTokens = EstimateTokenCount(upstreamRequest.Messages);
            tokenEstimationStopwatch.Stop();

            httpRequest.Content = JsonContent.Create(upstreamRequest, options: JsonOptions);

            using var response = await httpClient.SendAsync(httpRequest, linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 429)
                {
                    var retryAfterSeconds = GetRetryAfterSeconds(response);
                    this.logger.LogWarning(
                        "Help query upstream rate-limited (429). Retry-After={RetryAfterSeconds}s.",
                        retryAfterSeconds);

                    return this.StatusCode(429, new
                    {
                        error = "Copilot upstream rate limit reached.",
                        retryAfterSeconds,
                    });
                }

                var unknownModelFailure = await TryGetUnknownModelFailureAsync(
                    response,
                    request.SelectedModel,
                    resolvedModel,
                    options.Endpoint,
                    options.Model);

                if (unknownModelFailure != null)
                {
                    await this.ClearInvalidModelPreferenceAsync(request.SelectedModel, options.Endpoint, "help", request.DocumentKind, linkedCts.Token);

                    if (options.AutoRetryUnknownModel &&
                        !string.IsNullOrWhiteSpace(request.SelectedModel) &&
                        !string.IsNullOrWhiteSpace(unknownModelFailure.FallbackModel) &&
                        !string.Equals(unknownModelFailure.FallbackModel, resolvedModel, StringComparison.OrdinalIgnoreCase))
                    {
                        this.logger.LogInformation(
                            "Retrying help-query with fallback model {FallbackModel} after unknown_model for selected model {SelectedModel}.",
                            unknownModelFailure.FallbackModel,
                            request.SelectedModel);

                        return await this.HelpQuery(new AiHelpQueryRequest
                        {
                            Query = request.Query,
                            ChatMode = request.ChatMode,
                            DocumentKind = request.DocumentKind,
                            SectionKind = request.SectionKind,
                            ArticleNumber = request.ArticleNumber,
                            TemplateId = request.TemplateId,
                            LayoutId = request.LayoutId,
                            UrlPath = request.UrlPath,
                            Messages = request.Messages,
                            SelectedModel = null,
                        });
                    }

                    return this.BadRequest(new
                    {
                        error = "Selected model is not available for the configured provider.",
                        attemptedModel = unknownModelFailure.AttemptedModel,
                        selectedModel = unknownModelFailure.SelectedModel,
                        fallbackModel = unknownModelFailure.FallbackModel,
                        upstreamCode = unknownModelFailure.UpstreamCode,
                        upstreamMessage = unknownModelFailure.UpstreamMessage,
                    });
                }

                this.logger.LogWarning("Help query upstream call failed with status {StatusCode}.", (int)response.StatusCode);
                return this.StatusCode(502, new
                {
                    error = "Copilot upstream provider returned an error.",
                    upstreamStatusCode = (int)response.StatusCode,
                });
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
            var payload = await JsonSerializer.DeserializeAsync<UpstreamChatCompletionResponse>(responseStream, JsonOptions, linkedCts.Token);
            var replyText = payload?.Choices?[0]?.Message?.Content?.Trim();

            var responseTokenStopwatch = Stopwatch.StartNew();
            var responseTokens = EstimateTokenCount(replyText);
            responseTokenStopwatch.Stop();
            responseTokenEstimationMs = responseTokenStopwatch.Elapsed.TotalMilliseconds;

            this.logger.LogInformation(
                "Token estimation profile: op={Operation}; model={Model}; promptEstimationMs={PromptEstimationMs}; responseEstimationMs={ResponseEstimationMs}; totalMs={TotalMs}",
                "help-query",
                resolvedModel ?? "default",
                Math.Round(tokenEstimationStopwatch.Elapsed.TotalMilliseconds, 2),
                Math.Round(responseTokenEstimationMs, 2),
                Math.Round(tokenEstimationStopwatch.Elapsed.TotalMilliseconds + responseTokenEstimationMs, 2));

            this.LogTokenAccounting(
                operation: "help-query",
                documentKind: request.DocumentKind,
                baseContextTokens: baseContextTokens,
                entityContextTokens: entityContextTokens,
                renderingContextTokens: renderingContextTokens,
                knowledgeContextTokens: knowledgeContextTokens,
                promptTokens: promptTokens,
                responseTokens: responseTokens,
                model: resolvedModel);

            return this.Ok(new AiHelpQueryResponse
            {
                Reply = string.IsNullOrWhiteSpace(replyText)
                    ? "I don't have a useful answer for that yet."
                    : replyText,
                Model = resolvedModel,
                Sources = contextResult.Sources,
            });
        }
        catch (OperationCanceledException)
        {
            return this.StatusCode(504, new { error = "Help query request timed out." });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Help query request failed.");
            return this.StatusCode(500, new { error = "Help query request failed." });
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

    private async Task ClearInvalidModelPreferenceAsync(string? selectedModel, string endpoint, string? editorKind, string? documentKind, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(selectedModel))
        {
            return;
        }

        try
        {
            var providerKey = AiProviderMetadataResolver.ResolveProviderKey(endpoint, selectedModel);
            await this.aiUserPreferenceService.SaveSelectedModelAsync(
                this.User,
                providerKey,
                editorKind,
                documentKind,
                selectedModel: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to clear invalid saved model preference after unknown_model response.");
        }
    }

    private static async Task<UnknownModelFailure?> TryGetUnknownModelFailureAsync(
        HttpResponseMessage response,
        string? selectedModel,
        string? attemptedModel,
        string endpoint,
        string configuredModel)
    {
        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            return null;
        }

        var upstreamBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(upstreamBody))
        {
            return null;
        }

        try
        {
            var errorEnvelope = JsonSerializer.Deserialize<UpstreamErrorEnvelope>(upstreamBody, JsonOptions);
            var errorCode = errorEnvelope?.Error?.Code;

            if (!string.Equals(errorCode, "unknown_model", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var fallbackModel = AiProviderMetadataResolver.ResolveEffectiveModel(endpoint, configuredModel, null);
            return new UnknownModelFailure
            {
                SelectedModel = selectedModel,
                AttemptedModel = attemptedModel,
                FallbackModel = fallbackModel,
                UpstreamCode = errorCode,
                UpstreamMessage = errorEnvelope?.Error?.Message,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsStale(DateTimeOffset? lastRefreshUtc, TimeSpan threshold, DateTimeOffset now)
    {
        if (!lastRefreshUtc.HasValue)
        {
            return true;
        }

        return (now - lastRefreshUtc.Value) > threshold;
    }

    private static double? GetAgeHours(DateTimeOffset? lastRefreshUtc, DateTimeOffset now)
    {
        if (!lastRefreshUtc.HasValue)
        {
            return null;
        }

        return Math.Round((now - lastRefreshUtc.Value).TotalHours, 2);
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

    private static string BuildCompletionPrompt(CopilotCompletionRequest request, string language, string? editorContextPayload)
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

        if (!string.IsNullOrWhiteSpace(editorContextPayload))
        {
            sb.AppendLine();
            sb.AppendLine(editorContextPayload);
        }

        sb.AppendLine();
        sb.AppendLine($"Prefix:\n{prefix}");
        sb.AppendLine($"\nSuffix:\n{suffix}");
        sb.AppendLine("\nReturn only inline completion text.");

        return sb.ToString();
    }

    private static List<UpstreamChatMessage> BuildChatMessages(CopilotChatRequest request, string? documentationContext = null, string? layoutContext = null, string? sourceCodeContext = null, string? editorContextPayload = null)
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

        if (!string.IsNullOrWhiteSpace(editorContextPayload))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(editorContextPayload);
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

    private static int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0d));
    }

    private static int EstimateTokenCount(IEnumerable<UpstreamChatMessage> messages)
    {
        if (messages == null)
        {
            return 0;
        }

        return messages.Sum(message => EstimateTokenCount(message.Content));
    }

    private static int GetHistogramBucketIndex(int promptTokens)
    {
        for (var i = 0; i < HistogramUpperBounds.Length; i++)
        {
            if (promptTokens <= HistogramUpperBounds[i])
            {
                return i;
            }
        }

        return HistogramUpperBounds.Length;
    }

    private static string GetHistogramLabel(int index)
    {
        return index switch
        {
            0 => "<=1000",
            1 => "1001-2000",
            2 => "2001-4000",
            3 => "4001-8000",
            _ => ">8000",
        };
    }

    private void LogTokenAccounting(
        string operation,
        string? documentKind,
        int baseContextTokens,
        int entityContextTokens,
        int renderingContextTokens,
        int knowledgeContextTokens,
        int promptTokens,
        int responseTokens,
        string? model)
    {
        var normalizedDocumentKind = string.IsNullOrWhiteSpace(documentKind) ? "unknown" : documentKind.Trim().ToLowerInvariant();
        var contextTokens = baseContextTokens + entityContextTokens + renderingContextTokens + knowledgeContextTokens;
        var responseToPromptRatio = promptTokens > 0
            ? Math.Round((double)responseTokens / promptTokens, 3)
            : 0;

        var telemetry = TokenTelemetryByDocumentKind.GetOrAdd(normalizedDocumentKind, _ => new TokenTelemetryAccumulator());
        var bucketIndex = GetHistogramBucketIndex(promptTokens);
        var snapshot = telemetry.Record(promptTokens, responseTokens, contextTokens, bucketIndex);

        this.logger.LogInformation(
            "AI token accounting: op={Operation}; docKind={DocumentKind}; model={Model}; base={BaseTokens}; entity={EntityTokens}; rendering={RenderingTokens}; knowledge={KnowledgeTokens}; context={ContextTokens}; prompt={PromptTokens}; response={ResponseTokens}; responseToPrompt={ResponseToPromptRatio}; avgPrompt={AvgPromptTokens}; histogram={HistogramLabel}; requests={RequestCount}",
            operation,
            normalizedDocumentKind,
            model ?? "default",
            baseContextTokens,
            entityContextTokens,
            renderingContextTokens,
            knowledgeContextTokens,
            contextTokens,
            promptTokens,
            responseTokens,
            responseToPromptRatio,
            Math.Round(snapshot.averagePromptTokens, 2),
            GetHistogramLabel(bucketIndex),
            snapshot.requestCount);

        if (snapshot.averagePromptTokens > 4000)
        {
            this.logger.LogWarning(
                "AI payload average exceeds budget: docKind={DocumentKind}; avgPrompt={AveragePromptTokens}; histogram[<=1000]={Bucket0}; histogram[1001-2000]={Bucket1}; histogram[2001-4000]={Bucket2}; histogram[4001-8000]={Bucket3}; histogram[>8000]={Bucket4}",
                normalizedDocumentKind,
                Math.Round(snapshot.averagePromptTokens, 2),
                snapshot.histogram[0],
                snapshot.histogram[1],
                snapshot.histogram[2],
                snapshot.histogram[3],
                snapshot.histogram[4]);
        }
    }

    private sealed class TokenTelemetryAccumulator
    {
        private readonly long[] histogram = new long[HistogramUpperBounds.Length + 1];
        private readonly object gate = new();
        private long requestCount;
        private long totalPromptTokens;
        private long totalResponseTokens;
        private long totalContextTokens;

        public TokenTelemetrySnapshot Record(int promptTokens, int responseTokens, int contextTokens, int bucketIndex)
        {
            lock (this.gate)
            {
                this.requestCount++;
                this.totalPromptTokens += promptTokens;
                this.totalResponseTokens += responseTokens;
                this.totalContextTokens += contextTokens;
                this.histogram[bucketIndex]++;

                return new TokenTelemetrySnapshot(
                    this.requestCount,
                    this.requestCount == 0 ? 0 : (double)this.totalPromptTokens / this.requestCount,
                    this.histogram.ToArray());
            }
        }
    }

    private sealed record TokenTelemetrySnapshot(long requestCount, double averagePromptTokens, long[] histogram);

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
    /// Request payload for help chat queries.
    /// </summary>
    public sealed class AiHelpQueryRequest
    {
        /// <summary>
        /// Gets or sets the user query text.
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional chat mode.
        /// </summary>
        public string? ChatMode { get; set; }

        /// <summary>
        /// Gets or sets the document kind context.
        /// </summary>
        public string? DocumentKind { get; set; }

        /// <summary>
        /// Gets or sets the section kind context.
        /// </summary>
        public string? SectionKind { get; set; }

        /// <summary>
        /// Gets or sets the article number context.
        /// </summary>
        public string? ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the template ID context.
        /// </summary>
        public string? TemplateId { get; set; }

        /// <summary>
        /// Gets or sets the layout ID context.
        /// </summary>
        public string? LayoutId { get; set; }

        /// <summary>
        /// Gets or sets the URL path context.
        /// </summary>
        public string? UrlPath { get; set; }

        /// <summary>
        /// Gets or sets conversation history.
        /// </summary>
        public List<CopilotConversationMessage> Messages { get; set; } = [];

        /// <summary>
        /// Gets or sets an optional per-request selected model.
        /// </summary>
        public string? SelectedModel { get; set; }
    }

    /// <summary>
    /// Response payload for help chat queries.
    /// </summary>
    public sealed class AiHelpQueryResponse
    {
        /// <summary>
        /// Gets or sets the assistant reply.
        /// </summary>
        public string Reply { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets resolved model.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets source attributions used by the response.
        /// </summary>
        public List<AiHelpSourceAttribution> Sources { get; set; } = [];
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

    private sealed class UpstreamErrorEnvelope
    {
        public UpstreamError? Error { get; set; }
    }

    private sealed class UpstreamError
    {
        public string? Code { get; set; }

        public string? Message { get; set; }
    }

    private sealed class UnknownModelFailure
    {
        public string? SelectedModel { get; set; }

        public string? AttemptedModel { get; set; }

        public string? FallbackModel { get; set; }

        public string? UpstreamCode { get; set; }

        public string? UpstreamMessage { get; set; }
    }
}