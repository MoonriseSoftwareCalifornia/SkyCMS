using AspNetCore.Identity.FlexDb;
using AspNetCore.Identity.FlexDb.Strategies;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Identity.CosmosDb.Tests.Net9
{
    [TestClass]
    public class StrategyInferenceTests
    {
        [TestMethod]
        public void DefaultStrategies_ExposeNormalizedProviderIdentifiers()
        {
            var strategies = CosmosDbOptionsBuilder.GetDefaultStrategies();

            Assert.AreEqual("Microsoft.EntityFrameworkCore.Cosmos", strategies[0].ProviderName);
            Assert.AreEqual("Microsoft.EntityFrameworkCore.SqlServer", strategies[1].ProviderName);
            Assert.AreEqual("Pomelo.EntityFrameworkCore.MySql", strategies[2].ProviderName);
            Assert.AreEqual("Microsoft.EntityFrameworkCore.Sqlite", strategies[3].ProviderName);
        }

        [TestMethod]
        public void InferDatabaseProviderShortName_WithSqliteConnection_ReturnsSqlite()
        {
            var provider = Utilities.InferDatabaseProviderShortName("Data Source=./test.db;");

            Assert.AreEqual("SQLite", provider);
        }

        [TestMethod]
        public void InferDatabaseProvider_WithCosmosConnection_ReturnsEfProviderName()
        {
            var provider = Utilities.InferDatabaseProvider("AccountEndpoint=https://local.documents.azure.com:443/;AccountKey=key123==;Database=testdb;");

            Assert.AreEqual("Microsoft.EntityFrameworkCore.Cosmos", provider);
        }

        [TestMethod]
        public void ConfigureDbOptions_WithOverlappingStrategies_UsesLowestPriority()
        {
            var optionsBuilder = new DbContextOptionsBuilder();
            var lowPriority = new TestStrategy("low", 20);
            var highPriority = new TestStrategy("high", 10);

            CosmosDbOptionsBuilder.ConfigureDbOptions(
                optionsBuilder,
                connectionString: "Server=example;",
                strategies: new[] { lowPriority, highPriority });

            Assert.IsFalse(lowPriority.ConfigureCalled);
            Assert.IsTrue(highPriority.ConfigureCalled);
        }

        [TestMethod]
        public void GetAccountProperties_WithValidConnectionString_ExtractsExpectedParts()
        {
            var result = CosmosDbConfigurationStrategy.GetAccountProperties(
                "Database=testdb;AccountKey=key123==;AccountEndpoint=https://local.documents.azure.com:443/;");

            Assert.AreEqual("testdb", result.DatabaseName);
            Assert.AreEqual("https://local.documents.azure.com:443/", result.AccountEndpoint);
            Assert.AreEqual("key123==", result.AccountKey);
        }

        private sealed class TestStrategy : IDatabaseConfigurationStrategy
        {
            public TestStrategy(string providerName, int priority)
            {
                ProviderName = providerName;
                Priority = priority;
            }

            public string ProviderName { get; }

            public int Priority { get; }

            public bool ConfigureCalled { get; private set; }

            public bool CanHandle(string connectionString)
            {
                return connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase);
            }

            public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
            {
                ConfigureCalled = true;
                optionsBuilder.EnableDetailedErrors();
            }
        }
    }
}
