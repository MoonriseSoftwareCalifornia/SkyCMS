using Cosmos.BlobService;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkyCMS.Drivers.ElFinder.Adapters;

namespace SkyCMS.Drivers.ElFinder;

/// <summary>
/// Dependency injection extension methods for elFinder CQRS driver.
/// Registers MediatR handlers and adapter implementations.
/// </summary>
public static class ElFinderServiceCollectionExtensions
{
    /// <summary>
    /// Adds elFinder driver services to the DI container.
    /// Registers MediatR with all command handlers and the storage adapter implementation.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddElFinderDriver(this IServiceCollection services)
    {
        // Register MediatR handlers from this driver assembly.
        // Safe to call in app startup; duplicate registrations are tolerated by MediatR.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ElFinderServiceCollectionExtensions).Assembly);
        });

        // Register storage adapter (keep first registration if app overrides in tests/startup).
        services.TryAddScoped<IElFinderStorageAdapter, ElFinderStorageAdapter>();

        // Required path services for ElFinderStorageAdapter
        services.AddSingleton<IPathNormalizer, PathNormalizer>();
        services.AddSingleton<IPathValidator, PathValidator>();

        return services;
    }
}
