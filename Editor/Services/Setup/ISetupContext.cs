// <copyright file="ISetupContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Provides access to infrastructure dependencies required for setup operations.
    /// </summary>
    /// <remarks>
    /// This composite groups infrastructure services used by the setup wizard:
    /// configuration, caching, identity management, and database persistence.
    /// </remarks>
    public interface ISetupContext
    {
        /// <summary>
        /// Gets the configuration for reading app settings.
        /// </summary>
        IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the memory cache for storing temporary setup state.
        /// </summary>
        IMemoryCache MemoryCache { get; }

        /// <summary>
        /// Gets the user manager for creating and managing users during setup.
        /// </summary>
        UserManager<IdentityUser> UserManager { get; }

        /// <summary>
        /// Gets the role manager for creating and managing roles during setup.
        /// </summary>
        RoleManager<IdentityRole> RoleManager { get; }

        /// <summary>
        /// Gets the database context for persisting setup configuration.
        /// </summary>
        ApplicationDbContext Database { get; }
    }
}
