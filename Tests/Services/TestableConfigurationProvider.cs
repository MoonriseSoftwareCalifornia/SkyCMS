// <copyright file="TestableConfigurationProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services
{
    using Cosmos.DynamicConfig;
    using Cosmos.DynamicConfig.Configurations;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Testable version of DynamicConfigurationProvider that allows injecting a test DbContext.
    /// </summary>
    public class TestableConfigurationProvider : DynamicConfigurationProvider
    {
        private readonly DbContextOptions<DynamicConfigDbContext> testDbContextOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestableConfigurationProvider"/> class.
        /// </summary>
        /// <param name="configuration">Configuration.</param>
        /// <param name="httpContextAccessor">HTTP context accessor.</param>
        /// <param name="memoryCache">Memory cache.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="testDbContextOptions">Test database context options for creating instances.</param>
        public TestableConfigurationProvider(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache memoryCache,
            ILogger<DynamicConfigurationProvider> logger,
            DbContextOptions<DynamicConfigDbContext> testDbContextOptions,
            IOptions<ProxySettings> proxyOptions)
            : base(configuration, httpContextAccessor, memoryCache, logger, proxyOptions)
        {
            this.testDbContextOptions = testDbContextOptions;
        }

        /// <summary>
        /// Override to return a new DbContext instance with the same options.
        /// This allows the 'await using' statement to dispose each instance without affecting the shared database.
        /// </summary>
        /// <returns>New database context instance sharing the test database.</returns>
        protected override DynamicConfigDbContext GetDbContext()
        {
            return new DynamicConfigDbContext(testDbContextOptions);
        }
    }
}