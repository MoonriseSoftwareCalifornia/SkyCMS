// <copyright file="MediatorServiceExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Extensions
{
    using Cosmos.Common.Features.Shared;
    using Microsoft.Extensions.DependencyInjection;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Extension methods for registering mediator, command handlers, and query handlers.
    /// </summary>
    public static class MediatorServiceExtensions
    {
        /// <summary>
        /// Registers all command and query handlers from the specified assemblies.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="assemblies">Assemblies to scan for handlers. If none provided, scans calling assembly and Common assembly.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMediatorHandlers(this IServiceCollection services, params Assembly[] assemblies)
        {
            // Default assemblies to scan if none provided
            if (assemblies == null || assemblies.Length == 0)
            {
                assemblies = new[]
                {
                    Assembly.GetExecutingAssembly(), // Sky.Editor
                    typeof(IMediator).Assembly // Cosmos.Common
                };
            }

            // Find and register all ICommandHandler<,> implementations
            var commandHandlerType = typeof(ICommandHandler<,>);
            var commandHandlers = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Select(t => new
                {
                    Implementation = t,
                    Interfaces = t.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == commandHandlerType)
                        .ToList()
                })
                .Where(x => x.Interfaces.Any())
                .ToList();

            foreach (var handler in commandHandlers)
            {
                foreach (var @interface in handler.Interfaces)
                {
                    services.AddScoped(@interface, handler.Implementation);
                }
            }

            // Find and register all IQueryHandler<,> implementations
            var queryHandlerType = typeof(IQueryHandler<,>);
            var queryHandlers = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Select(t => new
                {
                    Implementation = t,
                    Interfaces = t.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == queryHandlerType)
                        .ToList()
                })
                .Where(x => x.Interfaces.Any())
                .ToList();

            foreach (var handler in queryHandlers)
            {
                foreach (var @interface in handler.Interfaces)
                {
                    services.AddScoped(@interface, handler.Implementation);
                }
            }

            return services;
        }

        /// <summary>
        /// Registers the mediator with multi-tenant security decorator.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCosmosMediator(this IServiceCollection services)
        {
            // Register concrete mediator with logger support
            services.AddScoped<Cosmos.Common.Features.Shared.Mediator>(sp =>
                new Cosmos.Common.Features.Shared.Mediator(
                    sp,
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Cosmos.Common.Features.Shared.Mediator>>()));

            // Register interface with multi-tenant wrapper
            services.AddScoped<IMediator>(sp =>
                new Sky.Editor.Features.Shared.MultiTenantMediator(
                    new Cosmos.Common.Features.Shared.Mediator(
                        sp,
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Cosmos.Common.Features.Shared.Mediator>>()),
                    sp.GetRequiredService<Cosmos.Common.Data.ApplicationDbContext>(),
                    sp.GetService<Cosmos.DynamicConfig.IDynamicConfigurationProvider>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Sky.Editor.Features.Shared.MultiTenantMediator>>()));

            return services;
        }
    }
}
