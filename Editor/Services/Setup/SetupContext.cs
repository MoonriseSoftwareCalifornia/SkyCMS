// <copyright file="SetupContext.cs" company="Moonrise Software, LLC">
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
    /// Implementation of setup context providing infrastructure dependencies.
    /// </summary>
    public class SetupContext : ISetupContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SetupContext"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="memoryCache">The memory cache.</param>
        /// <param name="userManager">The user manager.</param>
        /// <param name="roleManager">The role manager.</param>
        /// <param name="database">The database context.</param>
        public SetupContext(
            IConfiguration configuration,
            IMemoryCache memoryCache,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext database)
        {
            Configuration = configuration;
            MemoryCache = memoryCache;
            UserManager = userManager;
            RoleManager = roleManager;
            Database = database;
        }

        /// <inheritdoc/>
        public IConfiguration Configuration { get; }

        /// <inheritdoc/>
        public IMemoryCache MemoryCache { get; }

        /// <inheritdoc/>
        public UserManager<IdentityUser> UserManager { get; }

        /// <inheritdoc/>
        public RoleManager<IdentityRole> RoleManager { get; }

        /// <inheritdoc/>
        public ApplicationDbContext Database { get; }
    }
}
