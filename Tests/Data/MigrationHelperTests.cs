// <copyright file="MigrationHelperTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Data
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Data;

    /// <summary>
    /// Unit tests for <see cref="MigrationHelper"/>.
    /// </summary>
    [TestClass]
    public class MigrationHelperTests
    {
        private Mock<ILogger> loggerMock = null!;

        [TestInitialize]
        public void Setup()
        {
            loggerMock = new Mock<ILogger>();
        }

        [TestMethod]
        public async Task ApplyMigrationsAsync_ThrowsArgumentException_WhenConnectionStringIsNullOrEmpty()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                MigrationHelper.ApplyMigrationsAsync(null, loggerMock.Object));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                MigrationHelper.ApplyMigrationsAsync("", loggerMock.Object));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                MigrationHelper.ApplyMigrationsAsync("   ", loggerMock.Object));
        }

        [TestMethod]
        public async Task MarkMigrationAsAppliedAsync_ThrowsArgumentException_WhenConnectionStringOrMigrationIdIsNullOrEmpty()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                MigrationHelper.MarkMigrationAsAppliedAsync(null, "migrationId", loggerMock.Object));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                MigrationHelper.MarkMigrationAsAppliedAsync("", "migrationId", loggerMock.Object));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                MigrationHelper.MarkMigrationAsAppliedAsync("valid", null, loggerMock.Object));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                MigrationHelper.MarkMigrationAsAppliedAsync("valid", "", loggerMock.Object));
        }

        [TestMethod]
        public async Task DatabaseExistsWithoutMigrationsAsync_ThrowsArgumentException_WhenConnectionStringIsNullOrEmpty()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                MigrationHelper.DatabaseExistsWithoutMigrationsAsync(null, loggerMock.Object));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                MigrationHelper.DatabaseExistsWithoutMigrationsAsync("", loggerMock.Object));
        }

        [DataTestMethod]
        [DataRow("AccountEndpoint=https://localhost:8081;", "CosmosDb")]
        [DataRow("Server=myServer;Database=myDb;", "SqlServer")]
        [DataRow("Port=3306;Server=myServer;Uid=user;", "MySql")]
        [DataRow("Data Source=mydb.sqlite;", "Sqlite")]
        public void DetermineProvider_ReturnsExpectedProvider(string connectionString, string expectedProvider)
        {
            var provider = typeof(MigrationHelper)
                .GetMethod("DetermineProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { connectionString });

            Assert.AreEqual(expectedProvider, provider.ToString());
        }
    }
}
