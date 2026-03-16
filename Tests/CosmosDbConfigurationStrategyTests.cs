namespace Sky.Tests.FlexDb
{
    using global::AspNetCore.Identity.FlexDb.Strategies;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;

    /// <summary>
    /// Tests for Cosmos DB configuration strategy.
    /// </summary>
    [TestClass]
    public class CosmosDbConfigurationStrategyTests
    {
        /// <summary>
        /// Tests that CanHandle_ValidCosmosConnectionString_ReturnsTrue.
        /// </summary>
        [TestMethod]
        public void CanHandle_ValidCosmosConnectionString_ReturnsTrue()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();
            var connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=testkey123==;Database=TestDb;";

            // Act
            var result = strategy.CanHandle(connectionString);

            // Assert
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Tests that CanHandle_SqlServerConnectionString_ReturnsFalse.
        /// </summary>
        [TestMethod]
        public void CanHandle_SqlServerConnectionString_ReturnsFalse()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();
            var connectionString = "Server=localhost;Database=TestDb;User ID=sa;Password=pass123;";

            // Act
            var result = strategy.CanHandle(connectionString);

            // Assert
            Assert.IsFalse(result);
        }

        /// <summary>
        /// Tests that CanHandle_EmptyString_ReturnsFalse.
        /// </summary>
        [TestMethod]
        public void CanHandle_EmptyString_ReturnsFalse()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();

            // Act
            var result = strategy.CanHandle(string.Empty);

            // Assert
            Assert.IsFalse(result);
        }

        /// <summary>
        /// Tests that CanHandle_NullString_ReturnsFalse.
        /// </summary>
        [TestMethod]
        public void CanHandle_NullString_ReturnsFalse()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();

            // Act
            var result = strategy.CanHandle(null);

            // Assert
            Assert.IsFalse(result);
        }

        /// <summary>
        /// Tests that Configure_MissingAccountEndpoint_ThrowsArgumentException.
        /// </summary>
        [TestMethod]
        public void Configure_MissingAccountEndpoint_ThrowsArgumentException()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();
            var connectionString = "AccountKey=testkey123==;Database=TestDb;";
            var optionsBuilder = new DbContextOptionsBuilder();

            // Act / Assert
            Assert.ThrowsExactly<ArgumentException>(() => strategy.Configure(optionsBuilder, connectionString));

            // Assert is handled by ExpectedException
        }

        /// <summary>
        /// Tests that Configure_MissingAccountKey_ThrowsArgumentException.
        /// </summary>
        [TestMethod]
        public void Configure_MissingAccountKey_ThrowsArgumentException()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();
            var connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;Database=TestDb;";
            var optionsBuilder = new DbContextOptionsBuilder();

            // Act
            Assert.ThrowsExactly<ArgumentException>(() => strategy.Configure(optionsBuilder, connectionString));

            // Assert is handled by ExpectedException
        }

        /// <summary>
        /// Tests that Configure_MissingDatabase_ThrowsArgumentException.
        /// </summary>
        [TestMethod]
        public void Configure_MissingDatabase_ThrowsArgumentException()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();
            var connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=testkey123==;";
            var optionsBuilder = new DbContextOptionsBuilder();

            // Act
            Assert.ThrowsExactly<ArgumentException>(() => strategy.Configure(optionsBuilder, connectionString));

            // Assert is handled by ExpectedException ArgumentException
        }

        /// <summary>
        /// Tests that Configure_NullOptionsBuilder_ThrowsArgumentNullException.
        /// </summary>
        [TestMethod]
        public void Configure_NullOptionsBuilder_ThrowsArgumentNullException()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();
            var connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=testkey123==;Database=TestDb;";

            // Act
            Assert.ThrowsExactly<ArgumentNullException>(() => strategy.Configure(null, connectionString));

            // Assert is handled by ExpectedException
        }

        /// <summary>
        /// Tests that Configure_NullConnectionString_ThrowsArgumentNullException.
        /// </summary>
        [TestMethod]
        public void Configure_NullConnectionString_ThrowsArgumentNullException()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();
            var optionsBuilder = new DbContextOptionsBuilder();

            // Act
            Assert.ThrowsExactly<ArgumentNullException>(() => strategy.Configure(optionsBuilder, null));

            // Assert is handled by ExpectedException
        }

        /// <summary>
        /// Tests that ProviderName_ReturnsCorrectValue.
        /// </summary>
        [TestMethod]
        public void ProviderName_ReturnsCorrectValue()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();

            // Act
            var providerName = strategy.ProviderName;

            // Assert
            Assert.AreEqual("Microsoft.EntityFrameworkCore.Cosmos", providerName);
        }

        /// <summary>
        /// Tests that Priority_ReturnsCorrectValue.
        /// </summary>
        [TestMethod]
        public void Priority_ReturnsCorrectValue()
        {
            // Arrange
            var strategy = new CosmosDbConfigurationStrategy();

            // Act
            var priority = strategy.Priority;

            // Assert
            Assert.AreEqual(10, priority);
        }
    }
}

