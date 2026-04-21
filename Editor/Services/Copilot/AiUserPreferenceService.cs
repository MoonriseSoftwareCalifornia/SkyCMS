// <copyright file="AiUserPreferenceService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reads and writes per-user AI model preferences in the tenant-scoped settings table.
/// </summary>
public interface IAiUserPreferenceService
{
    /// <summary>
    /// Gets the saved selected model for the current user and editing context.
    /// </summary>
    /// <param name="user">Current user principal.</param>
    /// <param name="providerKey">Resolved AI provider key.</param>
    /// <param name="editorKind">Editor surface, such as monaco or ckeditor.</param>
    /// <param name="documentKind">Document kind, such as article, blog, template, or layout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved selected model, or null.</returns>
    Task<string?> GetSelectedModelAsync(ClaimsPrincipal user, string providerKey, string? editorKind, string? documentKind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or clears the selected model for the current user and editing context.
    /// </summary>
    /// <param name="user">Current user principal.</param>
    /// <param name="providerKey">Resolved AI provider key.</param>
    /// <param name="editorKind">Editor surface, such as monaco or ckeditor.</param>
    /// <param name="documentKind">Document kind, such as article, blog, template, or layout.</param>
    /// <param name="selectedModel">Selected model, or null to clear the saved preference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SaveSelectedModelAsync(ClaimsPrincipal user, string providerKey, string? editorKind, string? documentKind, string? selectedModel, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores per-user AI model preferences in the tenant-scoped settings table.
/// </summary>
public sealed class AiUserPreferenceService : IAiUserPreferenceService
{
    /// <summary>
    /// Settings group name for user AI preferences.
    /// </summary>
    public const string GroupName = "AIUSERSETTINGS";

    private const string SettingNamePrefix = "v1:model";
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<AiUserPreferenceService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiUserPreferenceService"/> class.
    /// </summary>
    /// <param name="dbContext">Application database context.</param>
    /// <param name="logger">Logger instance.</param>
    public AiUserPreferenceService(
        ApplicationDbContext dbContext,
        ILogger<AiUserPreferenceService> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string?> GetSelectedModelAsync(ClaimsPrincipal user, string providerKey, string? editorKind, string? documentKind, CancellationToken cancellationToken = default)
    {
        var userKey = ResolveUserKey(user);
        if (string.IsNullOrWhiteSpace(userKey) || string.IsNullOrWhiteSpace(providerKey))
        {
            return null;
        }

        var settingName = BuildSettingName(userKey, providerKey, editorKind, documentKind);
        var setting = await dbContext.Settings
            .FirstOrDefaultAsync(s => s.Group == GroupName && s.Name == settingName, cancellationToken)
            .ConfigureAwait(false);

        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return null;
        }

        try
        {
            var record = JsonSerializer.Deserialize<AiUserModelPreferenceRecord>(setting.Value);
            return string.IsNullOrWhiteSpace(record?.SelectedModel) ? null : record.SelectedModel.Trim();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize AI user preference setting {SettingName}.", settingName);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SaveSelectedModelAsync(ClaimsPrincipal user, string providerKey, string? editorKind, string? documentKind, string? selectedModel, CancellationToken cancellationToken = default)
    {
        var userKey = ResolveUserKey(user);
        if (string.IsNullOrWhiteSpace(userKey) || string.IsNullOrWhiteSpace(providerKey))
        {
            return;
        }

        var settingName = BuildSettingName(userKey, providerKey, editorKind, documentKind);
        var normalizedSelectedModel = string.IsNullOrWhiteSpace(selectedModel) ? null : selectedModel.Trim();
        var existingSetting = await dbContext.Settings
            .FirstOrDefaultAsync(s => s.Group == GroupName && s.Name == settingName, cancellationToken)
            .ConfigureAwait(false);

        if (normalizedSelectedModel == null)
        {
            if (existingSetting != null)
            {
                dbContext.Settings.Remove(existingSetting);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        var normalizedEditorKind = NormalizeContextPart(editorKind, "editor");
        var normalizedDocumentKind = NormalizeContextPart(documentKind, "default");
        var payload = JsonSerializer.Serialize(new AiUserModelPreferenceRecord
        {
            Version = 1,
            UserKey = userKey,
            ProviderKey = providerKey,
            EditorKind = normalizedEditorKind,
            DocumentKind = normalizedDocumentKind,
            SelectedModel = normalizedSelectedModel,
            UpdatedUtc = DateTime.UtcNow,
        });

        if (existingSetting == null)
        {
            existingSetting = new Setting
            {
                Group = GroupName,
                Name = settingName,
                Description = $"Version 1 AI model preference for user {userKey}, provider {providerKey}, editor {normalizedEditorKind}, document {normalizedDocumentKind}.",
            };
            dbContext.Settings.Add(existingSetting);
        }

        existingSetting.Name = settingName;
        existingSetting.Value = payload;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string BuildSettingName(string userKey, string providerKey, string? editorKind, string? documentKind)
    {
        var normalizedProviderKey = NormalizeContextPart(providerKey, "unknown");
        var normalizedEditorKind = NormalizeContextPart(editorKind, "editor");
        var normalizedDocumentKind = NormalizeContextPart(documentKind, "default");
        var normalizedUserKey = NormalizeContextPart(userKey, "anonymous");
        return $"{SettingNamePrefix}:{normalizedProviderKey}:{normalizedEditorKind}:{normalizedDocumentKind}:{normalizedUserKey}";
    }

    private static string ResolveUserKey(ClaimsPrincipal user)
    {
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim()
            ?? user?.FindFirst("sub")?.Value?.Trim()
            ?? user?.Identity?.Name?.Trim()
            ?? string.Empty;
    }

    private static string NormalizeContextPart(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().Replace(':', '-').Replace('|', '-');
    }

    private sealed class AiUserModelPreferenceRecord
    {
        public int Version { get; set; }

        public string UserKey { get; set; } = string.Empty;

        public string ProviderKey { get; set; } = string.Empty;

        public string EditorKind { get; set; } = string.Empty;

        public string DocumentKind { get; set; } = string.Empty;

        public string SelectedModel { get; set; } = string.Empty;

        public DateTime UpdatedUtc { get; set; }
    }
}