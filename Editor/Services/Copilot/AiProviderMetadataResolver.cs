// <copyright file="AiProviderMetadataResolver.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;
using System.Collections.Concurrent;
using System.Linq;

/// <summary>
/// Resolves provider metadata and effective model selection for AI proxy requests.
/// </summary>
public static class AiProviderMetadataResolver
{
    /// <summary>
    /// Gets the default model used for explicit SkyCMS auto selection.
    /// </summary>
    public const string DefaultAutoModel = "gpt-4o-mini";

    private static readonly AiProviderMetadata GitHubModelsMetadata = new()
    {
        ProviderKey = "github-models",
        ProviderDisplayName = "GitHub Models",
        SupportsModelDiscovery = true,
        SupportsUserModelSelection = true,
        SupportsAutoMode = true,
        DiscoveryState = AiProviderDiscoveryStates.LiveCatalog,
        DiscoveryStateMessage = "SkyCMS can load the live GitHub Models catalog for this provider.",
        DefaultModeDescription = $"SkyCMS auto resolves to {DefaultAutoModel}.",
    };

    private static readonly AiProviderMetadata OpenAiMetadata = new()
    {
        ProviderKey = "openai",
        ProviderDisplayName = "OpenAI",
        SupportsModelDiscovery = true,
        SupportsUserModelSelection = true,
        SupportsAutoMode = true,
        DiscoveryState = AiProviderDiscoveryStates.LiveCatalog,
        DiscoveryStateMessage = "SkyCMS can load the live OpenAI model catalog for this provider.",
        DefaultModeDescription = $"SkyCMS auto resolves to {DefaultAutoModel}.",
    };

    private static readonly AiProviderMetadata AzureOpenAiMetadata = new()
    {
        ProviderKey = "azure-openai",
        ProviderDisplayName = "Azure OpenAI",
        SupportsModelDiscovery = true,
        SupportsUserModelSelection = false,
        SupportsAutoMode = false,
        DiscoveryState = AiProviderDiscoveryStates.Inferred,
        DiscoveryStateMessage = "SkyCMS can infer the active Azure OpenAI deployment from a deployment-scoped endpoint, but it cannot list deployments with the current configuration.",
        DefaultModeDescription = "The deployment in the endpoint URL is the default model.",
    };

    private static readonly AiProviderMetadata AzureAiFoundryMetadata = new()
    {
        ProviderKey = "azure-ai-foundry",
        ProviderDisplayName = "Azure AI Foundry",
        SupportsModelDiscovery = false,
        SupportsUserModelSelection = false,
        SupportsAutoMode = false,
        DiscoveryState = AiProviderDiscoveryStates.NeedsAdditionalConfiguration,
        DiscoveryStateMessage = "Azure AI Foundry model discovery needs additional project or management metadata beyond the current endpoint and token.",
        DefaultModeDescription = "The endpoint default model is used when no model is sent.",
    };

    private static readonly AiProviderMetadata LocalMetadata = new()
    {
        ProviderKey = "local",
        ProviderDisplayName = "Local AI",
        SupportsModelDiscovery = false,
        SupportsUserModelSelection = false,
        SupportsAutoMode = false,
        DiscoveryState = AiProviderDiscoveryStates.Unsupported,
        DiscoveryStateMessage = "SkyCMS cannot discover local model catalogs automatically for this provider.",
        DefaultModeDescription = "Configure a local model explicitly if the provider requires one.",
    };

    private static readonly AiProviderMetadata AnthropicMetadata = new()
    {
        ProviderKey = "anthropic",
        ProviderDisplayName = "Claude",
        SupportsModelDiscovery = false,
        SupportsUserModelSelection = false,
        SupportsAutoMode = false,
        DiscoveryState = AiProviderDiscoveryStates.Unsupported,
        DiscoveryStateMessage = "SkyCMS cannot discover Claude models automatically from the current configuration.",
        DefaultModeDescription = "Configure an explicit Claude model.",
    };

    private static readonly AiProviderMetadata UnknownMetadata = new()
    {
        ProviderKey = "unknown",
        ProviderDisplayName = "AI",
        SupportsModelDiscovery = false,
        SupportsUserModelSelection = false,
        SupportsAutoMode = false,
        DiscoveryState = AiProviderDiscoveryStates.Unsupported,
        DiscoveryStateMessage = "This provider does not expose a supported discovery flow in SkyCMS yet.",
        DefaultModeDescription = "Use the configured model or provider default.",
    };

    private static readonly ConcurrentDictionary<string, AiProviderMetadata> UnknownProviderMetadataCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates provider metadata for the configured endpoint.
    /// </summary>
    /// <param name="endpoint">Configured endpoint.</param>
    /// <param name="configuredModel">Configured model.</param>
    /// <returns>Provider metadata.</returns>
    public static AiProviderMetadata Describe(string? endpoint, string? configuredModel)
    {
        return ResolveProviderMetadata(ResolveProviderKey(endpoint, configuredModel));
    }

    private static AiProviderMetadata ResolveProviderMetadata(string providerKey)
    {
        return providerKey switch
        {
            "github-models" => GitHubModelsMetadata,
            "openai" => OpenAiMetadata,
            "azure-openai" => AzureOpenAiMetadata,
            "azure-ai-foundry" => AzureAiFoundryMetadata,
            "local" => LocalMetadata,
            "anthropic" => AnthropicMetadata,
            _ => UnknownProviderMetadataCache.GetOrAdd(providerKey, CreateUnknownMetadata),
        };
    }

    private static AiProviderMetadata CreateUnknownMetadata(string providerKey)
    {
        return new AiProviderMetadata
        {
            ProviderKey = providerKey,
            ProviderDisplayName = UnknownMetadata.ProviderDisplayName,
            SupportsModelDiscovery = UnknownMetadata.SupportsModelDiscovery,
            SupportsUserModelSelection = UnknownMetadata.SupportsUserModelSelection,
            SupportsAutoMode = UnknownMetadata.SupportsAutoMode,
            DiscoveryState = UnknownMetadata.DiscoveryState,
            DiscoveryStateMessage = UnknownMetadata.DiscoveryStateMessage,
            DefaultModeDescription = UnknownMetadata.DefaultModeDescription,
        };
    }

    /// <summary>
    /// Resolves the configured model without applying provider defaults.
    /// </summary>
    /// <param name="configuredModel">Stored configured model.</param>
    /// <returns>The normalized configured model, or null.</returns>
    public static string? NormalizeConfiguredModel(string? configuredModel)
    {
        return string.IsNullOrWhiteSpace(configuredModel) ? null : configuredModel.Trim();
    }

    /// <summary>
    /// Resolves the effective model to use for a request.
    /// </summary>
    /// <param name="endpoint">Configured endpoint.</param>
    /// <param name="configuredModel">Stored configured model.</param>
    /// <param name="requestedModel">Optional per-request model override.</param>
    /// <returns>The effective model identifier, or null when the provider default should be used.</returns>
    public static string? ResolveEffectiveModel(string? endpoint, string? configuredModel, string? requestedModel = null)
    {
        var metadata = Describe(endpoint, configuredModel);
        var explicitModel = NormalizeConfiguredModel(requestedModel);
        if (!string.IsNullOrWhiteSpace(explicitModel) && metadata.SupportsUserModelSelection)
        {
            return explicitModel;
        }

        var normalizedConfiguredModel = NormalizeConfiguredModel(configuredModel);
        if (!string.IsNullOrWhiteSpace(normalizedConfiguredModel) &&
            !normalizedConfiguredModel.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedConfiguredModel;
        }

        return metadata.ProviderKey switch
        {
            "github-models" => DefaultAutoModel,
            "openai" => DefaultAutoModel,
            "azure-openai" => ExtractAzureOpenAiDeployment(endpoint),
            "azure-ai-foundry" => null,
            "local" => null,
            "anthropic" => null,
            _ => null,
        };
    }

    /// <summary>
    /// Resolves the provider key from endpoint and model hints.
    /// </summary>
    /// <param name="endpoint">Configured endpoint.</param>
    /// <param name="configuredModel">Configured model.</param>
    /// <returns>Provider key.</returns>
    public static string ResolveProviderKey(string? endpoint, string? configuredModel)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            if (host.Contains("models.inference.ai.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return "github-models";
            }

            if (host.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase))
            {
                return "openai";
            }

            if (host.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return "azure-openai";
            }

            if (host.Contains("services.ai.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return "azure-ai-foundry";
            }

            if (host.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                host.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                return "local";
            }
        }

        if (!string.IsNullOrWhiteSpace(configuredModel) &&
            configuredModel.Contains("claude", StringComparison.OrdinalIgnoreCase))
        {
            return "anthropic";
        }

        return "unknown";
    }

    /// <summary>
    /// Extracts the Azure OpenAI deployment name from a deployment-scoped endpoint.
    /// </summary>
    /// <param name="endpoint">Configured endpoint.</param>
    /// <returns>The deployment name, or null.</returns>
    public static string? ExtractAzureOpenAiDeployment(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var deploymentIndex = Array.FindIndex(segments, s => s.Equals("deployments", StringComparison.OrdinalIgnoreCase));
        if (deploymentIndex < 0 || deploymentIndex + 1 >= segments.Length)
        {
            return null;
        }

        return segments[deploymentIndex + 1];
    }

    /// <summary>
    /// Creates a human-readable default option label for UI selectors.
    /// </summary>
    /// <param name="endpoint">Configured endpoint.</param>
    /// <param name="configuredModel">Configured model.</param>
    /// <returns>Selector label.</returns>
    public static string BuildDefaultSelectionLabel(string? endpoint, string? configuredModel)
    {
        var metadata = Describe(endpoint, configuredModel);
        var effectiveModel = ResolveEffectiveModel(endpoint, configuredModel);
        if (metadata.SupportsAutoMode)
        {
            return effectiveModel == null
                ? "Auto"
                : $"Auto ({effectiveModel})";
        }

        return effectiveModel == null
            ? $"Default ({metadata.DefaultModeDescription})"
            : $"Default ({effectiveModel})";
    }

    /// <summary>
    /// Determines whether a GitHub Models catalog entry is suitable for text chat or coding.
    /// </summary>
    /// <param name="supportedOutputModalities">Output modalities from the catalog.</param>
    /// <returns>True when the entry can be used for text output.</returns>
    public static bool SupportsTextOutput(string[]? supportedOutputModalities)
    {
        return supportedOutputModalities == null ||
               supportedOutputModalities.Length == 0 ||
               supportedOutputModalities.Contains("text", StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether an OpenAI model id is suitable for chat or coding UI selection.
    /// </summary>
    /// <param name="modelId">Model identifier.</param>
    /// <returns>True when the model looks usable for text generation.</returns>
    public static bool IsSupportedOpenAiChatModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        var normalized = modelId.Trim();
        if (normalized.Contains("embedding", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("whisper", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("tts", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("moderation", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("dall-e", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("transcribe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("audio", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalized.StartsWith("gpt", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("o", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("codex", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("chatgpt", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Provider metadata used to describe model discovery and default behavior.
/// </summary>
public sealed class AiProviderMetadata
{
    /// <summary>
    /// Gets or sets the provider key.
    /// </summary>
    public string ProviderKey { get; init; } = "unknown";

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string ProviderDisplayName { get; init; } = "AI";

    /// <summary>
    /// Gets or sets a value indicating whether model discovery is supported.
    /// </summary>
    public bool SupportsModelDiscovery { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the UI should offer user model selection.
    /// </summary>
    public bool SupportsUserModelSelection { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether SkyCMS auto mode is supported.
    /// </summary>
    public bool SupportsAutoMode { get; init; }

    /// <summary>
    /// Gets or sets the discovery state.
    /// </summary>
    public string DiscoveryState { get; init; } = AiProviderDiscoveryStates.Unsupported;

    /// <summary>
    /// Gets or sets a human-readable discovery state message.
    /// </summary>
    public string DiscoveryStateMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the default mode description.
    /// </summary>
    public string DefaultModeDescription { get; init; } = string.Empty;
}