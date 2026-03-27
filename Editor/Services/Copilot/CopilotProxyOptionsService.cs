// <copyright file="CopilotProxyOptionsService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using Cosmos.Common.Data;
using Cosmos.Common.Services.Caching;
using Cosmos.DynamicConfig;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Sky.Editor.Models;
using System;
using System.Threading.Tasks;

/// <summary>
/// Resolves Copilot proxy options from the tenant-specific settings table.
/// </summary>
public class CopilotProxyOptionsService : ICopilotProxyOptionsService
{
    private const string CopilotProxySettingsGroupName = "COPILOTPROXYSETTINGS";
    private const string CacheKeyPrefix = "COPILOT_PROXY_OPTIONS";

    private readonly IApplicationDbContext dbContext;
    private readonly ICacheService<CopilotProxyOptions> cacheService;
    private readonly IDynamicConfigurationProvider dynamicConfigurationProvider;
    private readonly ILogger<CopilotProxyOptionsService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotProxyOptionsService"/> class.
    /// </summary>
    /// <param name="dbContext">Tenant-aware application database context.</param>
    /// <param name="cacheService">Cache service for Copilot options.</param>
    /// <param name="dynamicConfigurationProvider">Tenant context provider.</param>
    /// <param name="logger">Logger instance.</param>
    public CopilotProxyOptionsService(
        IApplicationDbContext dbContext,
        ICacheService<CopilotProxyOptions> cacheService,
        IDynamicConfigurationProvider dynamicConfigurationProvider,
        ILogger<CopilotProxyOptionsService> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        this.dynamicConfigurationProvider = dynamicConfigurationProvider ?? throw new ArgumentNullException(nameof(dynamicConfigurationProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<CopilotProxyOptions> GetOptionsAsync()
    {
        var cacheKey = this.GetCacheKey();
        if (cacheService.TryGet(cacheKey, out var cachedOptions) && cachedOptions != null)
        {
            return cachedOptions;
        }

        var result = await LoadFromDatabaseAsync().ConfigureAwait(false);
        cacheService.Set(cacheKey, result, TimeSpan.FromSeconds(30));

        return result;
    }

    private string GetCacheKey()
    {
        var tenantDomain = dynamicConfigurationProvider.GetTenantDomainNameFromRequest();
        if (string.IsNullOrWhiteSpace(tenantDomain))
        {
            tenantDomain = "default";
        }

        return $"{CacheKeyPrefix}:{tenantDomain.ToLowerInvariant()}";
    }

    private async Task<CopilotProxyOptions> LoadFromDatabaseAsync()
    {
        var setting = await dbContext.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Group == CopilotProxySettingsGroupName)
            .ConfigureAwait(false);

        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return new CopilotProxyOptions();
        }

        try
        {
            return JsonConvert.DeserializeObject<CopilotProxyOptions>(setting.Value) ?? new CopilotProxyOptions();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize Copilot proxy options for setting ID: {SettingId}", setting.Id);
            return new CopilotProxyOptions();
        }
    }
}
