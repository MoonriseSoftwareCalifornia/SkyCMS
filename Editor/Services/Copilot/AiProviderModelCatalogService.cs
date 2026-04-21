// <copyright file="AiProviderModelCatalogService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Sky.Editor.Models;

/// <summary>
/// Retrieves provider model catalogs for supported AI providers.
/// </summary>
public sealed class AiProviderModelCatalogService : IAiProviderModelCatalogService
{
    private const string GitHubApiVersion = "2026-03-10";
    private static readonly TimeSpan LiveCatalogCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InferredCatalogCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<AiProviderModelCatalogService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiProviderModelCatalogService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="memoryCache">Memory cache.</param>
    /// <param name="logger">Logger instance.</param>
    public AiProviderModelCatalogService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<AiProviderModelCatalogService> logger)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<AiProviderModelCatalogResult> GetCatalogAsync(CopilotProxyOptions options, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var cacheKey = BuildCacheKey(options);
        if (!forceRefresh && this.memoryCache.TryGetValue(cacheKey, out AiProviderModelCatalogResult? cachedResult) && cachedResult != null)
        {
            return cachedResult;
        }

        var metadata = AiProviderMetadataResolver.Describe(options.Endpoint, options.Model);
        var result = new AiProviderModelCatalogResult
        {
            ProviderKey = metadata.ProviderKey,
            ProviderDisplayName = metadata.ProviderDisplayName,
            SupportsModelDiscovery = metadata.SupportsModelDiscovery,
            SupportsUserModelSelection = metadata.SupportsUserModelSelection,
            SupportsAutoMode = metadata.SupportsAutoMode,
            DiscoveryState = metadata.DiscoveryState,
            DiscoveryStateMessage = metadata.DiscoveryStateMessage,
            DefaultModeDescription = metadata.DefaultModeDescription,
        };

        if (metadata.ProviderKey == "azure-openai")
        {
            return CacheAndReturn(cacheKey, BuildAzureOpenAiCatalog(options, result));
        }

        if (metadata.ProviderKey == "azure-ai-foundry")
        {
            result.DiscoveryState = AiProviderDiscoveryStates.NeedsAdditionalConfiguration;
            result.DiscoveryStateMessage = metadata.DiscoveryStateMessage;
            result.DiscoveryError = metadata.DiscoveryStateMessage;
            return CacheAndReturn(cacheKey, result);
        }

        if (!metadata.SupportsModelDiscovery)
        {
            result.DiscoveryState = AiProviderDiscoveryStates.Unsupported;
            result.DiscoveryStateMessage = metadata.DiscoveryStateMessage;
            result.DiscoveryError = metadata.DefaultModeDescription;
            return CacheAndReturn(cacheKey, result);
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.AccessToken))
        {
            result.DiscoveryState = AiProviderDiscoveryStates.NeedsAdditionalConfiguration;
            result.DiscoveryStateMessage = "AI provider endpoint and token must be configured before models can be loaded.";
            result.DiscoveryError = "AI provider endpoint and token must be configured before models can be loaded.";
            return CacheAndReturn(cacheKey, result);
        }

        try
        {
            result.Models = metadata.ProviderKey switch
            {
                "openai" => await this.LoadOpenAiModelsAsync(options.AccessToken, cancellationToken).ConfigureAwait(false),
                "github-models" => await this.LoadGitHubModelsAsync(options.AccessToken, cancellationToken).ConfigureAwait(false),
                _ => [],
            };
            result.DiscoveryState = AiProviderDiscoveryStates.LiveCatalog;
            result.DiscoveryStateMessage = $"Loaded the live {metadata.ProviderDisplayName} model catalog.";
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to load AI provider model catalog for provider {ProviderKey}.", metadata.ProviderKey);
            result.DiscoveryState = AiProviderDiscoveryStates.Failed;
            result.DiscoveryStateMessage = "The AI provider model catalog could not be loaded.";
            result.DiscoveryError = "The AI provider model catalog could not be loaded.";
        }

        return CacheAndReturn(cacheKey, result);
    }

    private static string BuildCacheKey(CopilotProxyOptions options)
    {
        var endpoint = options.Endpoint?.Trim() ?? string.Empty;
        var model = options.Model?.Trim() ?? string.Empty;
        var tokenHash = HashText(options.AccessToken?.Trim() ?? string.Empty);
        return $"ai-model-catalog:{endpoint}:{model}:{tokenHash}";
    }

    private static string HashText(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private AiProviderModelCatalogResult CacheAndReturn(string cacheKey, AiProviderModelCatalogResult result)
    {
        var cacheDuration = result.DiscoveryState switch
        {
            AiProviderDiscoveryStates.LiveCatalog => LiveCatalogCacheDuration,
            AiProviderDiscoveryStates.Inferred => InferredCatalogCacheDuration,
            AiProviderDiscoveryStates.Failed => FailureCacheDuration,
            _ => InferredCatalogCacheDuration,
        };

        this.memoryCache.Set(cacheKey, result, cacheDuration);
        return result;
    }

    private static AiProviderModelCatalogResult BuildAzureOpenAiCatalog(CopilotProxyOptions options, AiProviderModelCatalogResult result)
    {
        var deployment = AiProviderMetadataResolver.ExtractAzureOpenAiDeployment(options.Endpoint);
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            result.DiscoveryState = AiProviderDiscoveryStates.NeedsAdditionalConfiguration;
            result.DiscoveryStateMessage = "Provide a deployment-scoped Azure OpenAI chat completions endpoint to infer the active deployment.";
            result.DiscoveryError = result.DiscoveryStateMessage;
            return result;
        }

        if (string.IsNullOrWhiteSpace(deployment))
        {
            result.DiscoveryState = AiProviderDiscoveryStates.NeedsAdditionalConfiguration;
            result.DiscoveryStateMessage = "SkyCMS could not infer an Azure OpenAI deployment from the endpoint. Use a deployment-scoped endpoint URL ending in /openai/deployments/{deployment}/chat/completions.";
            result.DiscoveryError = result.DiscoveryStateMessage;
            return result;
        }

        result.DiscoveryState = AiProviderDiscoveryStates.Inferred;
        result.DiscoveryStateMessage = "SkyCMS inferred the Azure OpenAI deployment from the configured endpoint. Live deployment listing is not available with the current configuration.";
        result.Models =
        [
            new AiProviderModelOption
            {
                Id = deployment,
                DisplayName = deployment,
                Publisher = "Azure OpenAI deployment",
                Recommended = true,
            },
        ];

        return result;
    }

    private async Task<List<AiProviderModelOption>> LoadOpenAiModelsAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var httpClient = this.httpClientFactory.CreateClient();
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<OpenAiModelsResponse>(responseStream, JsonOptions, cancellationToken).ConfigureAwait(false);

        return payload?.Data?
            .Where(model => AiProviderMetadataResolver.IsSupportedOpenAiChatModel(model.Id))
            .OrderBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .Select(model => new AiProviderModelOption
            {
                Id = model.Id,
                DisplayName = model.Id,
                Publisher = model.OwnedBy,
                Recommended = model.Id.Equals(AiProviderMetadataResolver.DefaultAutoModel, StringComparison.OrdinalIgnoreCase),
            })
            .ToList() ?? [];
    }

    private async Task<List<AiProviderModelOption>> LoadGitHubModelsAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://models.github.ai/catalog/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", GitHubApiVersion);

        var httpClient = this.httpClientFactory.CreateClient();
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<List<GitHubCatalogModel>>(responseStream, JsonOptions, cancellationToken).ConfigureAwait(false);

        return payload?
            .Where(model => AiProviderMetadataResolver.SupportsTextOutput(model.SupportedOutputModalities))
            .OrderBy(model => model.Name ?? model.Id, StringComparer.OrdinalIgnoreCase)
            .Select(model => new AiProviderModelOption
            {
                Id = model.Id ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(model.Name) ? model.Id ?? string.Empty : model.Name,
                Publisher = model.Publisher,
                Recommended = string.Equals(model.Id, $"openai/{AiProviderMetadataResolver.DefaultAutoModel}", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(model.Id, AiProviderMetadataResolver.DefaultAutoModel, StringComparison.OrdinalIgnoreCase),
            })
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .ToList() ?? [];
    }

    private sealed class OpenAiModelsResponse
    {
        public List<OpenAiModel>? Data { get; set; }
    }

    private sealed class OpenAiModel
    {
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("owned_by")]
        public string? OwnedBy { get; set; }
    }

    private sealed class GitHubCatalogModel
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Publisher { get; set; }

        [JsonPropertyName("supported_output_modalities")]
        public string[]? SupportedOutputModalities { get; set; }
    }
}