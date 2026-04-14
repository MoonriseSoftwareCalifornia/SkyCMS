// <copyright file="SetupCheckService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Services.Caching;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;

    /// <inheritdoc/>
    public class SetupCheckService : ISetupCheckService
    {
        private const string SetupCompletedCacheKey = "SetupCompleted";

        private readonly ApplicationDbContext dbContext;
        private readonly IConfiguration configuration;
        private readonly ICacheService<bool> cacheService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupCheckService"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="configuration">Configuration instance.</param>
        /// <param name="cacheService">Cache service instance.</param>
        public SetupCheckService(ApplicationDbContext dbContext, IConfiguration configuration, ICacheService<bool> cacheService)
        {
            this.dbContext = dbContext;
            this.configuration = configuration;
            this.cacheService = cacheService;
        }

        /// <inheritdoc/>
        public string Message { get; internal set; }

        /// <inheritdoc/>
        public async Task<bool> IsSetup()
        {
            if (cacheService.TryGet(SetupCompletedCacheKey, out var setupCompleted) && setupCompleted)
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

                cacheService.Set(SetupCompletedCacheKey, true, TimeSpan.FromMinutes(5));
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
            cacheService.Remove(SetupCompletedCacheKey);
        }
    }
}
