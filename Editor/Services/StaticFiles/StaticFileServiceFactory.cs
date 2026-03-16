// <copyright file="StaticFileServiceFactory.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.StaticFiles
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Factory implementation for creating scoped instances of <see cref="IStaticFileService"/>.
    /// </summary>
    public class StaticFileServiceFactory : IStaticFileServiceFactory
    {
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticFileServiceFactory"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider for creating scoped services.</param>
        public StaticFileServiceFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public IStaticFileService CreateScoped()
        {
            var scope = serviceProvider.CreateAsyncScope();
            return scope.ServiceProvider.GetRequiredService<IStaticFileService>();
        }
    }
}
