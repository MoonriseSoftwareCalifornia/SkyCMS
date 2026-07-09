// <copyright file="MediatorServiceExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Cosmos.Common.Features.Shared;
    using Microsoft.Extensions.DependencyInjection;

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
            if (assemblies == null || assemblies.Length == 0)
            {
                assemblies = new[]
                {
                    Assembly.GetExecutingAssembly(),
                    typeof(IMediator).Assembly,
                };
            }

            var loadableTypes = new List<Type>();
            foreach (var assembly in assemblies)
            {
                loadableTypes.AddRange(GetLoadableTypes(assembly));
            }

            var loadableTypesArray = loadableTypes.ToArray();
            RegisterHandlers(services, loadableTypesArray, typeof(ICommandHandler<,>));
            RegisterHandlers(services, loadableTypesArray, typeof(IQueryHandler<,>));

            return services;
        }

        /// <summary>
        /// Registers the mediator with multi-tenant security decorator.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCosmosMediator(this IServiceCollection services)
        {
            services.AddScoped<Cosmos.Common.Features.Shared.Mediator>(sp =>
                new Cosmos.Common.Features.Shared.Mediator(
                    sp,
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Cosmos.Common.Features.Shared.Mediator>>()));

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

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type is not null).Select(type => type!).ToArray();
            }
        }

        private static void RegisterHandlers(IServiceCollection services, Type[] types, Type openGenericHandlerType)
        {
            foreach (var implementation in types)
            {
                if (implementation.IsAbstract || implementation.IsInterface)
                {
                    continue;
                }

                foreach (var handlerInterface in implementation.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericHandlerType))
                {
                    services.AddScoped(handlerInterface, implementation);
                }
            }
        }
    }
}
