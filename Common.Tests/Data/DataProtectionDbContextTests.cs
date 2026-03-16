// <copyright file="DataProtectionDbContextTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Data
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="DataProtectionDbContext"/>.
    /// </summary>
    [TestClass]
    public class DataProtectionDbContextTests
    {
        [TestMethod]
        public void Constructor_WithOptions_ShouldSucceed()
        {
            var options = new DbContextOptionsBuilder<DataProtectionDbContext>()
                .UseInMemoryDatabase($"dp-{Guid.NewGuid():N}")
                .Options;

            using var context = new DataProtectionDbContext(options);

            Assert.IsNotNull(context);
            Assert.IsNotNull(context.DataProtectionKeys);
        }

        [TestMethod]
        public async Task DataProtectionKeys_CanPersistAndRetrieveKey()
        {
            var options = new DbContextOptionsBuilder<DataProtectionDbContext>()
                .UseInMemoryDatabase($"dp-{Guid.NewGuid():N}")
                .Options;

            var key = new DataProtectionKey
            {
                FriendlyName = "test-key",
                Xml = "<key id='1'/>"
            };

            await using (var context = new DataProtectionDbContext(options))
            {
                context.DataProtectionKeys.Add(key);
                await context.SaveChangesAsync();
            }

            await using (var context = new DataProtectionDbContext(options))
            {
                var count = await context.DataProtectionKeys.CountAsync();
                var loaded = await context.DataProtectionKeys.FirstOrDefaultAsync();

                Assert.AreEqual(1, count);
                Assert.IsNotNull(loaded);
                Assert.AreEqual("test-key", loaded.FriendlyName);
            }
        }
    }
}
