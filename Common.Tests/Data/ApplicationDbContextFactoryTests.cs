// <copyright file="ApplicationDbContextFactoryTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Data
{
    using Cosmos.Common.Data;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="ApplicationDbContextFactory"/>.
    /// </summary>
    [TestClass]
    public class ApplicationDbContextFactoryTests
    {
        [TestMethod]
        public void CreateDbContext_ShouldReturnContextInstance()
        {
            var factory = new ApplicationDbContextFactory();

            var context = factory.CreateDbContext([]);

            Assert.IsNotNull(context);
            context.Dispose();
        }

        [TestMethod]
        public void CreateDbContext_ShouldUseSqliteProvider()
        {
            var factory = new ApplicationDbContextFactory();

            var context = factory.CreateDbContext([]);

            Assert.IsNotNull(context.Database.ProviderName);
            StringAssert.Contains(context.Database.ProviderName, "Sqlite");
            context.Dispose();
        }
    }
}
