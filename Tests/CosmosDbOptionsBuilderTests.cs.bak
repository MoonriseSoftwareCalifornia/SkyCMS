// <copyright file="CosmosDbOptionsBuilderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.FlexDb
{
    using AspNetCore.Identity.FlexDb;
    using AspNetCore.Identity.FlexDb.Strategies;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Tests for CosmosDbOptionsBuilder.
    /// </summary>
    [TestClass]
    public class CosmosDbOptionsBuilderTests
    {
        [TestMethod]
        public void ConfigureDbOptions_CosmosConnectionString_UsesCosmosStrategy()
        {
            // Arrange
            var connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=testkey123==;Database=TestDb;";
            var optionsBuilder = new DbContextOptionsBuilder();

            // Act
            CosmosDbOptionsBuilder.ConfigureDbOptions(optionsBuilder, connectionString);

            // Assert
            Assert.IsNotNull(optionsBuilder.Options);
        }

        [TestMethod]
        public void ConfigureDbOptions_SqlServerConnectionString_UsesSqlServerStrategy()
        {
            // Arrange
            var connectionString = "Server=localhost;Database=TestDb;User ID=sa;Password=pass123;";
            var optionsBuilder = new DbContextOptionsBuilder();

            // Act
            CosmosDbOptionsBuilder.ConfigureDbOptions(optionsBuilder, connectionString);

            // Assert
            Assert.IsNotNull(optionsBuilder.Options);
        }

        [TestMethod]
        public void ConfigureDbOptions_UnsupportedConnectionString_ThrowsArgumentException()
        {
            // Arrange
            var connectionString = "InvalidConnectionString";
            var optionsBuilder = new DbContextOptionsBuilder();

            // Act
            Assert.ThrowsExactly<ArgumentException>(() => CosmosDbOptionsBuilder.ConfigureDbOptions(optionsBuilder, connectionString));

            // Assert is handled by ExpectedException
        }

        [TestMethod]
        public void ConfigureDbOptions_NullOptionsBuilder_ThrowsArgumentNullException()
        {
            // Arrange
            var connectionString = "Server=localhost;Database=TestDb;User ID=sa;Password=pass123;";

            // Act
            Assert.ThrowsExactly<ArgumentNullException>(() => CosmosDbOptionsBuilder.ConfigureDbOptions(null, connectionString));

            // Assert is handled by ExpectedException
        }

        [TestMethod]
        public void GetDefaultStrategies_ReturnsAllStrategies()
        {
            // Act
            var strategies = CosmosDbOptionsBuilder.GetDefaultStrategies();

            // Assert
            Assert.IsNotNull(strategies);
            Assert.HasCount(4, strategies); // Cosmos, SQL Server, MySQL
        }

        [TestMethod]
        public void ConfigureDbOptions_CustomStrategies_UsesProvidedStrategies()
        {
            // Arrange
            var connectionString = "Server=localhost;Database=TestDb;User ID=sa;Password=pass123;";
            var optionsBuilder = new DbContextOptionsBuilder();
            var customStrategies = new List<IDatabaseConfigurationStrategy>
            {
                new SqlServerConfigurationStrategy()
            };

            // Act
            CosmosDbOptionsBuilder.ConfigureDbOptions(optionsBuilder, connectionString, customStrategies);

            // Assert
            Assert.IsNotNull(optionsBuilder.Options);
        }
    }
}
