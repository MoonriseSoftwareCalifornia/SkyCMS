// <copyright file="EditorSettings.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.EditorSettings
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Sky.Cms.Services;
    using Sky.Editor.Models;

    /// <summary>
    ///   Logic for managing settings in the application.
    /// </summary>
    public class EditorSettings : IEditorSettings
    {
        /// <summary>
        /// Editor settings group name.
        /// </summary>
        public static readonly string EDITORSETGROUPNAME = "EDITORSETTINGS";

        private readonly ApplicationDbContext dbContext;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IMemoryCache memoryCache;
        private readonly IConfiguration configuration;
        private readonly bool isMultiTenantEditor;
        private EditorConfig editorConfig;
        private readonly string backupStorageConnectionString;
        private readonly IDynamicConfigurationProvider dynamicConfigurationProvider;
        private readonly SemaphoreSlim _configSemaphore = new SemaphoreSlim(1, 1);
        private readonly string domainName;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorSettings"/> class.
        /// </summary>
        /// <param name="configuration">Web app configuration.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="httpContextAccessor">Http context accessor.</param>
        /// <param name="memoryCache">Memory cache.</param>
        /// <param name="serviceProvider">Service provider.</param>
        /// <param name="domainName">Optional domain name for multi-tenant operations.</param>
        public EditorSettings(
            IConfiguration configuration,
            ApplicationDbContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache memoryCache,
            IServiceProvider serviceProvider,
            string domainName = "")
        {
            this.dbContext = dbContext;
            this.httpContextAccessor = httpContextAccessor;
            this.memoryCache = memoryCache;
            this.configuration = configuration;
            backupStorageConnectionString = this.configuration.GetConnectionString("BackupStorageConnectionString") ?? null;
            this.domainName = domainName ?? string.Empty;
            isMultiTenantEditor = this.configuration.GetValue<bool?>("MultiTenantEditor") ?? false;
            
            if (isMultiTenantEditor)
            {
                dynamicConfigurationProvider = serviceProvider.GetService<IDynamicConfigurationProvider>();
                
                if (dynamicConfigurationProvider == null)
                {
                    throw new InvalidOperationException(
                        "MultiTenantEditor is enabled but IDynamicConfigurationProvider is not registered in the service container. " +
                        "Please add the following registration in Program.cs: " +
                        "builder.Services.AddSingleton<IDynamicConfigurationProvider, DynamicConfigurationProvider>();");
                }
            }

            // Configuration will be lazy-loaded on first access
        }

        /// <summary>
        /// Gets allowed file types for the file uploader.
        /// </summary>
        public string AllowedFileTypes
        {
            get
            {
                EnsureConfigLoaded();
                return editorConfig.AllowedFileTypes ?? ".js,.css,.htm,.html,.htm,.mov,.webm,.avi,.mp4,.mpeg,.ts,.svg,.json";
            }
        }

        /// <summary>
        /// Gets a value indicating whether the website is allowed to perform setup tasks.
        /// </summary>
        public bool AllowSetup
        {
            get
            {
                EnsureConfigLoaded();
                return editorConfig.AllowSetup;
            }
        }

        /// <summary>
        /// Gets the static website URL.
        /// </summary>
        public string BlobPublicUrl
        {
            get
            {
                EnsureConfigLoaded();
                return editorConfig.BlobPublicUrl ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the backup storage connection string.
        /// </summary>
        public string BackupStorageConnectionString
        {
            get
            {
                return backupStorageConnectionString;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the publisher requires authentication.
        /// </summary>
        public bool CosmosRequiresAuthentication
        {
            get
            {
                EnsureConfigLoaded();
                return editorConfig.CosmosRequiresAuthentication;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the editor is a multi-tenant editor.
        /// </summary>
        public bool IsMultiTenantEditor
        {
            get
            {
                return isMultiTenantEditor;
            }
        }

        /// <summary>
        /// Gets the Microsoft application ID used for application verification.
        /// </summary>
        public string MicrosoftAppId
        {
            get
            {
                EnsureConfigLoaded();
                return editorConfig.MicrosoftAppId;
            }
        }

        /// <summary>
        /// Gets the publisher or website URL.
        /// </summary>
        public string PublisherUrl
        {
            get
            {
                EnsureConfigLoaded();
                return editorConfig.PublisherUrl;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the publisher is a static website.
        /// </summary>
        public bool StaticWebPages
        {
            get
            {
                EnsureConfigLoaded();
                return editorConfig.StaticWebPages;
            }
        }

        /// <summary>
        ///  Gets or sets the static page parallelism level for static website publishing. If null, defaults to 4.
        /// </summary>
        public int? StaticPageParallelism { get; set; } = null;

        /// <summary>
        /// Gets the blob absolute URL.
        /// </summary>
        /// <returns>Uri.</returns>
        public Uri GetBlobAbsoluteUrl()
        {
            EnsureConfigLoaded();  // Ensure config is loaded BEFORE accessing properties
            
            var htmlUtilities = new HtmlUtilities();
            var blobUrl = BlobPublicUrl;
            var publisherUrl = PublisherUrl;

            if (htmlUtilities.IsAbsoluteUri(blobUrl))
            {
                return new Uri(blobUrl);
            }
            else
            {
                // Ensure we have a valid publisher URL before constructing the combined URL
                if (string.IsNullOrWhiteSpace(publisherUrl))
                {
                    // If no publisher URL is configured, return a relative URI
                    return new Uri(blobUrl, UriKind.Relative);
                }
                
                return new Uri(publisherUrl.TrimEnd('/') + "/" + blobUrl.TrimStart('/'));
            }
        }

        /// <summary>
        /// Gets the editor configuration settings asynchronously.
        /// </summary>
        /// <returns>Editor configuration.</returns>
        public async Task<EditorConfig> GetEditorConfigAsync()
        {
            // Check cache first with the correct domain name
            if (memoryCache.TryGetValue<EditorConfig>(GetNormalizedKeyName(domainName), out var cachedConfig))
            {
                return cachedConfig;
            }

            EditorConfig newConfig;

            if (!isMultiTenantEditor)
            {
                // ✅ Single-tenant: Environment variables/secrets OVERRIDE database settings
                newConfig = await LoadConfigWithPriorityAsync();
            }
            else
            {
                // ✅ Multi-tenant: Load from dynamic config database
                string scopedDomainName = domainName;
                if (string.IsNullOrWhiteSpace(domainName))
                {
                    if (httpContextAccessor.HttpContext == null)
                    {
                        throw new InvalidOperationException("No HttpContext available to determine tenant domain name, and the domain name not given.");
                    }

                    scopedDomainName = dynamicConfigurationProvider.GetTenantDomainNameFromRequest();
                }

                var connection = await dynamicConfigurationProvider.GetTenantConnectionAsync(scopedDomainName);

                if (connection == null)
                {
                    throw new InvalidOperationException($"No tenant connection found for domain: {scopedDomainName}");
                }

                newConfig = new EditorConfig()
                {
                    AllowSetup = connection.AllowSetup,
                    IsMultiTenantEditor = true,
                    BlobPublicUrl = connection.BlobPublicUrl,
                    CosmosRequiresAuthentication = connection.PublisherRequiresAuthentication,
                    MicrosoftAppId = connection.MicrosoftAppId,
                    PublisherUrl = connection.WebsiteUrl,
                    StaticWebPages = connection.PublisherMode == "Static",
                    AllowedFileTypes = ".js,.css,.htm,.html,.mov,.webm,.avi,.mp4,.mpeg,.ts,.svg,.json"
                };
            }

            // Cache for 5 minutes
            memoryCache.Set(GetNormalizedKeyName(domainName), newConfig, TimeSpan.FromMinutes(5));

            return newConfig;
        }

        /// <summary>
        /// Loads configuration with priority: IConfiguration (env vars/secrets) > Database > Defaults.
        /// </summary>
        /// <returns>EditorConfig with merged settings.</returns>
        private async Task<EditorConfig> LoadConfigWithPriorityAsync()
        {
            // ✅ Load from database first (baseline)
            var dbConfig = await LoadConfigFromDatabaseAsync();

            // ✅ Load from IConfiguration (environment variables, secrets, appsettings.json)
            var configFromFiles = LoadConfigFromConfiguration();

            // ✅ Merge with correct priority: IConfiguration > Database > Defaults
            return MergeConfigurations(configFromFiles, dbConfig);
        }

        /// <summary>
        /// Loads configuration from IConfiguration (appsettings.json, user secrets, environment variables).
        /// Returns nullable values to indicate "not set" vs "explicitly set to false".
        /// </summary>
        /// <returns>EditorConfig from configuration sources.</returns>
        private ConfigurationSource LoadConfigFromConfiguration()
        {
            return new ConfigurationSource
            {
                AllowSetup = configuration.GetValue<bool?>("CosmosAllowSetup"),
                BlobPublicUrl = configuration.GetValue<string>("AzureBlobStorageEndPoint") ?? configuration.GetValue<string>("BlobPublicUrl") ?? "/",
                CosmosRequiresAuthentication = configuration.GetValue<bool?>("CosmosRequiresAuthentication"),
                MicrosoftAppId = configuration.GetValue<string>("MicrosoftAppId"),
                PublisherUrl = configuration.GetValue<string>("CosmosPublisherUrl"),
                StaticWebPages = configuration.GetValue<bool?>("CosmosStaticWebPages"),
                AllowedFileTypes = configuration.GetValue<string>("AllowedFileTypes")
            };
        }

        /// <summary>
        /// Merges configurations with strict priority: IConfiguration > Database > Defaults.
        /// </summary>
        /// <param name="configSource">Configuration from IConfiguration (highest priority).</param>
        /// <param name="dbSource">Configuration from database (medium priority).</param>
        /// <returns>Merged EditorConfig.</returns>
        private EditorConfig MergeConfigurations(ConfigurationSource configSource, EditorConfig dbSource)
        {
            return new EditorConfig
            {
                // ✅ CORRECT PRIORITY: IConfiguration > Database > Default
                // If IConfiguration has a value (even false), use it. Otherwise fall back to database, then default.
                AllowSetup = configSource.AllowSetup 
                    ?? dbSource?.AllowSetup 
                    ?? false,

                IsMultiTenantEditor = isMultiTenantEditor,

                BlobPublicUrl = configSource.BlobPublicUrl 
                    ?? dbSource?.BlobPublicUrl 
                    ?? "/",

                CosmosRequiresAuthentication = configSource.CosmosRequiresAuthentication 
                    ?? dbSource?.CosmosRequiresAuthentication 
                    ?? false,

                MicrosoftAppId = configSource.MicrosoftAppId 
                    ?? dbSource?.MicrosoftAppId 
                    ?? string.Empty,

                PublisherUrl = configSource.PublisherUrl 
                    ?? dbSource?.PublisherUrl 
                    ?? string.Empty,

                StaticWebPages = configSource.StaticWebPages 
                    ?? dbSource?.StaticWebPages 
                    ?? true,

                AllowedFileTypes = configSource.AllowedFileTypes 
                    ?? dbSource?.AllowedFileTypes 
                    ?? ".js,.css,.htm,.html,.mov,.webm,.avi,.mp4,.mpeg,.ts,.svg,.json"
            };
        }

        /// <summary>
        /// Loads configuration from the database Settings table.
        /// </summary>
        /// <returns>EditorConfig if found, null otherwise.</returns>
        private async Task<EditorConfig> LoadConfigFromDatabaseAsync()
        {
            try
            {
                // Check if the database is accessible
                if (!await dbContext.Database.CanConnectAsync())
                {
                    return null;
                }

                // ✅ Check if setup has been completed
                var allowSetupSetting = await dbContext.Settings
                    .Where(s => s.Group == "SYSTEM" && s.Name == "AllowSetup")
                    .FirstOrDefaultAsync();

                // If AllowSetup is not "false", setup hasn't been completed yet
                if (allowSetupSetting == null || !allowSetupSetting.Value.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    return null; // Fall back to configuration files
                }

                // ✅ Load all settings from database
                var settings = await dbContext.Settings
                    .Where(s => s.Group == "STORAGE" || s.Group == "PUBLISHER" || s.Group == "SYSTEM" || s.Group == "OAUTH")
                    .ToListAsync();

                if (!settings.Any())
                {
                    return null; // No settings in database yet
                }

                // ✅ Build EditorConfig from database settings
                var config = new EditorConfig
                {
                    AllowSetup = false, // Setup is complete if we're here
                    IsMultiTenantEditor = isMultiTenantEditor,
                    BlobPublicUrl = GetSettingValue(settings, "STORAGE", "BlobPublicUrl"),
                    CosmosRequiresAuthentication = GetSettingValueAsBool(settings, "PUBLISHER", "CosmosRequiresAuthentication") ?? false,
                    MicrosoftAppId = GetSettingValue(settings, "OAUTH", "MicrosoftAppId"),
                    PublisherUrl = GetSettingValue(settings, "PUBLISHER", "PublisherUrl"),
                    StaticWebPages = GetSettingValueAsBool(settings, "PUBLISHER", "StaticWebPages") ?? true,
                    AllowedFileTypes = GetSettingValue(settings, "PUBLISHER", "AllowedFileTypes")
                };

                return config;
            }
            catch (Exception)
            {
                // If database query fails, return null to fall back to configuration
                return null;
            }
        }

        /// <summary>
        /// Gets a setting value from the settings collection.
        /// </summary>
        private string GetSettingValue(System.Collections.Generic.List<Setting> settings, string group, string name)
        {
            return settings
                .FirstOrDefault(s => s.Group == group && s.Name == name)
                ?.Value;
        }

        /// <summary>
        /// Gets a setting value as a boolean.
        /// </summary>
        private bool? GetSettingValueAsBool(System.Collections.Generic.List<Setting> settings, string group, string name)
        {
            var value = GetSettingValue(settings, group, name);
            
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (bool.TryParse(value, out var result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Ensures the editor configuration is loaded before accessing it (synchronous version).
        /// </summary>
        private void EnsureConfigLoaded()
        {
            if (editorConfig == null)
            {
                _configSemaphore.Wait();
                try
                {
                    if (editorConfig == null)
                    {
                        editorConfig = GetEditorConfigAsync().GetAwaiter().GetResult();
                    }
                }
                finally
                {
                    _configSemaphore.Release();
                }
            }
        }

        private string GetNormalizedKeyName(string domainName)
        {
            return $"edsetting-{domainName.ToLower()}";
        }

        /// <summary>
        /// Helper class to hold configuration values from IConfiguration with proper nullable handling.
        /// </summary>
        private class ConfigurationSource
        {
            public bool? AllowSetup { get; set; }
            public string BlobPublicUrl { get; set; }
            public bool? CosmosRequiresAuthentication { get; set; }
            public string MicrosoftAppId { get; set; }
            public string PublisherUrl { get; set; }
            public bool? StaticWebPages { get; set; }
            public string AllowedFileTypes { get; set; }
        }
    }
}
