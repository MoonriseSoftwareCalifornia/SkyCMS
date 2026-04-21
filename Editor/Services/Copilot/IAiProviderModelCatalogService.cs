// <copyright file="IAiProviderModelCatalogService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sky.Editor.Models;

/// <summary>
/// Retrieves provider model catalogs for supported AI providers.
/// </summary>
public interface IAiProviderModelCatalogService
{
    /// <summary>
    /// Gets provider catalog metadata and model options for the configured tenant.
    /// </summary>
    /// <param name="options">Configured proxy options.</param>
    /// <param name="forceRefresh">True to bypass cached results and reload the catalog.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Catalog response.</returns>
    Task<AiProviderModelCatalogResult> GetCatalogAsync(CopilotProxyOptions options, bool forceRefresh = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Well-known AI provider discovery states.
/// </summary>
public static class AiProviderDiscoveryStates
{
    /// <summary>
    /// Provider does not support discovery.
    /// </summary>
    public const string Unsupported = "unsupported";

    /// <summary>
    /// Model information was inferred from configuration.
    /// </summary>
    public const string Inferred = "inferred";

    /// <summary>
    /// A live catalog was loaded from the upstream provider.
    /// </summary>
    public const string LiveCatalog = "live-catalog";

    /// <summary>
    /// Additional configuration is required before discovery can proceed.
    /// </summary>
    public const string NeedsAdditionalConfiguration = "needs-additional-configuration";

    /// <summary>
    /// Discovery was attempted but failed.
    /// </summary>
    public const string Failed = "failed";
}

/// <summary>
/// Provider model catalog result.
/// </summary>
public sealed class AiProviderModelCatalogResult
{
    /// <summary>
    /// Gets or sets the provider key.
    /// </summary>
    public string ProviderKey { get; set; } = "unknown";

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string ProviderDisplayName { get; set; } = "AI";

    /// <summary>
    /// Gets or sets a value indicating whether discovery is supported.
    /// </summary>
    public bool SupportsModelDiscovery { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether user model selection is supported.
    /// </summary>
    public bool SupportsUserModelSelection { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether auto mode is supported.
    /// </summary>
    public bool SupportsAutoMode { get; set; }

    /// <summary>
    /// Gets or sets the discovery state.
    /// </summary>
    public string DiscoveryState { get; set; } = AiProviderDiscoveryStates.Unsupported;

    /// <summary>
    /// Gets or sets a human-readable discovery state message.
    /// </summary>
    public string DiscoveryStateMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default mode description.
    /// </summary>
    public string DefaultModeDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the discovery error.
    /// </summary>
    public string? DiscoveryError { get; set; }

    /// <summary>
    /// Gets or sets the available models.
    /// </summary>
    public List<AiProviderModelOption> Models { get; set; } = [];
}

/// <summary>
/// A selectable provider model option.
/// </summary>
public sealed class AiProviderModelOption
{
    /// <summary>
    /// Gets or sets the model id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the publisher.
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this option is recommended.
    /// </summary>
    public bool Recommended { get; set; }
}