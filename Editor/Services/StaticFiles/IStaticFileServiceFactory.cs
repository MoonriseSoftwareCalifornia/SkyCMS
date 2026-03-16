// <copyright file="IStaticFileServiceFactory.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.StaticFiles
{
    /// <summary>
    /// Factory for creating scoped instances of <see cref="IStaticFileService"/>.
    /// </summary>
    /// <remarks>
    /// This factory is used during parallel static file generation to ensure each parallel task
    /// has its own scoped database context and dependencies, preventing concurrency issues.
    /// This is safer than injecting IServiceProvider directly (Service Locator anti-pattern).
    /// </remarks>
    public interface IStaticFileServiceFactory
    {
        /// <summary>
        /// Creates a new scoped instance of <see cref="IStaticFileService"/>.
        /// </summary>
        /// <returns>A new scoped static file service instance.</returns>
        /// <remarks>
        /// The caller is responsible for disposing the returned service and its scope.
        /// Use with 'await using' pattern to ensure proper cleanup.
        /// </remarks>
        IStaticFileService CreateScoped();
    }
}
