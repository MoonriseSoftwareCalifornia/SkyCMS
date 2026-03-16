// <copyright file="SetupCheck.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Threading.Tasks;

    /// <inheritdoc/>
    public class SetupCheckService : ISetupCheckService
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IConfiguration configuration;
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupCheckService"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="configuration">Configuration instance.</param>
        /// <param name="memoryCache">Memory cache instance.</param>
        public SetupCheckService(ApplicationDbContext dbContext, IConfiguration configuration, IMemoryCache memoryCache)
        {
            this.dbContext = dbContext;
            this.configuration = configuration;
            this.memoryCache = memoryCache;
        }

        /// <inheritdoc/>
        public string Message { get; internal set; }

        /// <inheritdoc/>
        public async Task<bool> IsSetup()
        {
            if (memoryCache.TryGetValue("SetupCompleted", out bool setupCompleted) && setupCompleted)
            {
                Message = "Setup is completed";
                return true;
            }

            var allowSetup = configuration.GetValue<bool?>("AllowSetup") ?? true;
            if (!allowSetup)
            {
                Message = "Setup is not allowed";
                return true;
            }

            try
            {
                // Check if the Settings table exists and has the AllowSetup = false entry
                var setting = await dbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == "SYSTEM" && s.Name == "AllowSetup");

                var completed = setting != null && setting.Value.Equals("false", StringComparison.OrdinalIgnoreCase);

                if (!completed)
                {
                    Message = "Setup is not completed";
                    return false;
                }

                memoryCache.Set("SetupCompleted", true);
                Message = "Setup is completed";
                return true;
            }
            catch (Exception)
            {
                Message = "Can't connect to database";
                return false;
            }
        }

        /// <inheritdoc/>
        public void Reset()
        {
            memoryCache.Remove("SetupCompleted");
        }
    }
}
