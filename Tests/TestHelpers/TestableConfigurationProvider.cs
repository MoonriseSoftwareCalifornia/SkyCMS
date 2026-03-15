using Cosmos.DynamicConfig;
using Cosmos.DynamicConfig.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sky.Tests.TestHelpers
{
    /// <summary>
    /// Testable version of DynamicConfigurationProvider that uses in-memory database.
    /// </summary>
    internal class TestableConfigurationProvider : DynamicConfigurationProvider
    {
        private readonly DbContextOptions<DynamicConfigDbContext> _testOptions;

        public TestableConfigurationProvider(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache memoryCache,
            ILogger<DynamicConfigurationProvider> logger,
            DbContextOptions<DynamicConfigDbContext> testOptions,
            IOptions<ProxySettings> proxySettings)
            : base(configuration, httpContextAccessor, memoryCache, logger, proxySettings)
        {
            _testOptions = testOptions;
        }

        protected override DynamicConfigDbContext GetDbContext()
        {
            return new DynamicConfigDbContext(_testOptions);
        }
    }
}
