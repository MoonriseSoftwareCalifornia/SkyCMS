using Cosmos.BlobService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Handlers;

namespace SkyCMS.Drivers.ElFinder;

/// <summary>
/// Dependency injection extension methods for the elFinder driver.
/// Registers command handlers and adapter implementations without requiring MediatR.
/// </summary>
public static class ElFinderServiceCollectionExtensions
{
    /// <summary>
    /// Adds elFinder driver services to the DI container.
    /// Registers all command handlers, the dispatcher, and storage adapter implementations.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddElFinderDriver(this IServiceCollection services)
    {
        // Register the dispatcher that routes commands to their handlers.
        services.TryAddScoped<IElFinderDispatcher, ElFinderDispatcher>();

        // Register all elFinder command handlers.
        services.TryAddScoped<IElFinderHandler<DimCommand>, DimCommandHandler>();
        services.TryAddScoped<IElFinderHandler<DuplicateCommand>, DuplicateCommandHandler>();
        services.TryAddScoped<IElFinderHandler<FileCommand>, FileCommandHandler>();
        services.TryAddScoped<IElFinderHandler<GetCommand>, GetCommandHandler>();
        services.TryAddScoped<IElFinderHandler<InfoCommand>, InfoCommandHandler>();
        services.TryAddScoped<IElFinderHandler<LsCommand>, LsCommandHandler>();
        services.TryAddScoped<IElFinderHandler<MkdirCommand>, MkdirCommandHandler>();
        services.TryAddScoped<IElFinderHandler<MkfileCommand>, MkfileCommandHandler>();
        services.TryAddScoped<IElFinderHandler<OpenCommand>, OpenCommandHandler>();
        services.TryAddScoped<IElFinderHandler<ParentsCommand>, ParentsCommandHandler>();
        services.TryAddScoped<IElFinderHandler<PasteCommand>, PasteCommandHandler>();
        services.TryAddScoped<IElFinderHandler<PutCommand>, PutCommandHandler>();
        services.TryAddScoped<IElFinderHandler<RenameCommand>, RenameCommandHandler>();
        services.TryAddScoped<IElFinderHandler<ResizeCommand>, ResizeCommandHandler>();
        services.TryAddScoped<IElFinderHandler<RmCommand>, RmCommandHandler>();
        services.TryAddScoped<IElFinderHandler<SearchCommand>, SearchCommandHandler>();
        services.TryAddScoped<IElFinderHandler<SizeCommand>, SizeCommandHandler>();
        services.TryAddScoped<IElFinderHandler<TmbCommand>, TmbCommandHandler>();
        services.TryAddScoped<IElFinderHandler<TreeCommand>, TreeCommandHandler>();
        services.TryAddScoped<IElFinderHandler<UploadCommand>, UploadCommandHandler>();
        services.TryAddScoped<IElFinderHandler<UrlCommand>, UrlCommandHandler>();

        // Register storage adapter (keep first registration if app overrides in tests/startup).
        services.TryAddScoped<IElFinderStorageAdapter, ElFinderStorageAdapter>();

        // Required path services for ElFinderStorageAdapter.
        services.AddSingleton<IPathNormalizer, PathNormalizer>();
        services.AddSingleton<IPathValidator, PathValidator>();

        return services;
    }
}
