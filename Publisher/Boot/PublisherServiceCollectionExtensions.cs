// <copyright file="PublisherServiceCollectionExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Cosmos.BlobService;
using Cosmos.Cms.Common.Services.Configurations;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Articles.Queries;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services;
using Cosmos.Publisher.Configuration;
using Cosmos.Publisher.Services;
using Cosmos.MicrosoftGraph;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring Publisher services in the dependency injection container.
    /// Centralizes DI setup to reduce duplication and improve maintainability.
    /// </summary>
    public static class PublisherServiceCollectionExtensions
    {
        /// <summary>
        /// Adds core Publisher services to the dependency injection container.
        /// Includes caching, HTTP context access, and custom providers.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherCoreServices(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            services.AddScoped<IRequestContextProvider, RequestContextProvider>();

            return services;
        }

        /// <summary>
        /// Adds Cosmos database context to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration provider.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(PublisherConfigurationKeys.ApplicationDbContextConnection);
            if (string.IsNullOrEmpty(connectionString))
            {
                var keys = configuration.AsEnumerable()
                    .Select(k => k.Key)
                    .Where(k => k.StartsWith("ConnectionStrings", StringComparison.CurrentCultureIgnoreCase))
                    .ToArray();
                var keyString = string.Join(", ", keys);
                throw new InvalidOperationException($"Connection string is missing. Found keys: {keyString}");
            }

            var cosmosIdentityDbName = configuration.GetValue<string>(PublisherConfigurationKeys.CosmosIdentityDbName)
                ?? PublisherConfigurationKeys.DefaultCosmosIdentityDbName;

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseCosmos(connectionString, cosmosIdentityDbName);
            });

            return services;
        }

        /// <summary>
        /// Adds CQRS mediator and query handlers to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherMediator(this IServiceCollection services)
        {
            services.AddScoped<IMediator, Mediator>();
            services.AddScoped<IQueryHandler<GetPublishedPageByUrlQuery, Cosmos.Common.Models.ArticleViewModel>, GetPublishedPageByUrlQueryHandler>();
            services.AddScoped<IQueryHandler<GetPublishedPageHeaderByUrlQuery, Cosmos.Common.Models.ArticleViewModel>, GetPublishedPageHeaderByUrlQueryHandler>();
            services.AddScoped<IQueryHandler<GetTableOfContentsQuery, Cosmos.Common.Models.TableOfContents>, GetTableOfContentsQueryHandler>();
            services.AddScoped<IQueryHandler<SearchPublishedArticlesQuery, List<Cosmos.Common.Models.TableOfContentsItem>>, SearchPublishedArticlesQueryHandler>();

            return services;
        }

        /// <summary>
        /// Adds CORS policy configuration to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration provider.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherCors(this IServiceCollection services, IConfiguration configuration)
        {
            var corsOrigins = configuration.GetValue<string>(PublisherConfigurationKeys.CorsAllowedOrigins);
            if (string.IsNullOrEmpty(corsOrigins))
            {
                services.AddCors();
            }
            else
            {
                var origins = corsOrigins.Split(',');
                services.AddCors(options =>
                {
                    options.AddPolicy(
                        name: PublisherConfigurationKeys.CorsPolicyName,
                        policy =>
                        {
                            policy.WithOrigins(origins);
                        });
                });
            }

            return services;
        }

        /// <summary>
        /// Adds rate limiting to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(_ => _
                .AddFixedWindowLimiter(policyName: PublisherConfigurationKeys.RateLimitingPolicyName, options =>
                {
                    options.PermitLimit = PublisherConfigurationKeys.RateLimiter.DefaultPermitLimit;
                    options.Window = PublisherConfigurationKeys.RateLimiter.DefaultWindow;
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = PublisherConfigurationKeys.RateLimiter.DefaultQueueLimit;
                }));

            return services;
        }

        /// <summary>
        /// Adds JSON serialization configuration to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherJsonSerialization(this IServiceCollection services)
        {
            services.AddMvc()
                .AddNewtonsoftJson(options =>
                    options.SerializerSettings.ContractResolver = new DefaultContractResolver());

            return services;
        }

        /// <summary>
        /// Adds response caching to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="isStaticSite">Whether this is a static website configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherResponseCaching(this IServiceCollection services, bool isStaticSite = false)
        {
            services.AddResponseCaching(options =>
            {
                options.MaximumBodySize = PublisherConfigurationKeys.ResponseCaching.MaximumBodySize;
                options.UseCaseSensitivePaths = PublisherConfigurationKeys.ResponseCaching.UseCaseSensitivePaths;
            });

            return services;
        }

        /// <summary>
        /// Adds distributed Cosmos cache to the dependency injection container (dynamic publisher only).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration provider.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherDistributedCache(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(PublisherConfigurationKeys.ApplicationDbContextConnection)
                ?? throw new InvalidOperationException("Connection string is required for distributed cache");

            var cosmosIdentityDbName = configuration.GetValue<string>(PublisherConfigurationKeys.CosmosIdentityDbName)
                ?? PublisherConfigurationKeys.DefaultCosmosIdentityDbName;

            services.AddCosmosCache((cacheOptions) =>
            {
                cacheOptions.ContainerName = PublisherConfigurationKeys.CacheContainerName;
                cacheOptions.DatabaseName = cosmosIdentityDbName;
                cacheOptions.ClientBuilder = new CosmosClientBuilder(connectionString);
                cacheOptions.CreateIfNotExists = true;
            });

            return services;
        }

        /// <summary>
        /// Adds Cosmos storage context to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration provider.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherStorageContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddCosmosStorageContext(configuration);
            return services;
        }

        /// <summary>
        /// Adds forwarded headers configuration to the dependency injection container.
        /// Used when deploying behind a proxy (e.g., Docker, load balancer).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherForwardedHeaders(this IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                           ForwardedHeaders.XForwardedProto;

                // Allow all proxies (configured explicitly for Docker/load balancer scenarios)
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            return services;
        }

        /// <summary>
        /// Adds Publisher-specific configuration options to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration provider.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SiteSettings>(settings =>
            {
                settings.CosmosRequiresAuthentication = configuration.GetValue<bool?>(PublisherConfigurationKeys.CosmosRequiresAuthentication) ?? false;
                settings.AllowLocalAccounts = configuration.GetValue<bool?>(PublisherConfigurationKeys.AllowLocalAccounts) ?? true;
            });

            return services;
        }

        /// <summary>
        /// Adds optional Graph integration service to the dependency injection container.
        /// This service is available only if the MsGraphService is registered (i.e., if Microsoft OAuth is configured).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherGraphIntegration(this IServiceCollection services)
        {
            services.AddScoped<IGraphIntegrationService>(provider =>
            {
                var msGraphService = provider.GetService<MsGraphService>();
                var logger = provider.GetRequiredService<ILogger<GraphIntegrationService>>();
                return new GraphIntegrationService(msGraphService, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds caching services to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPublisherCaching(this IServiceCollection services)
        {
            services.AddScoped(typeof(ICacheService<>), typeof(CacheService<>));
            services.AddScoped<ICacheKeyProvider, CacheKeyProvider>();

            return services;
        }
    }
}
