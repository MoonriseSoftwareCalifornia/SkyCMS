// <copyright file="ExtensionsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="Cosmos.Common.Extensions"/>.
    /// </summary>
    [TestClass]
    public class ExtensionsTests
    {
        [TestMethod]
        public void AddFlexDbDataProtection_WithMissingConnectionString_ShouldThrowArgumentNullException()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

            try
            {
                services.AddFlexDbDataProtection(config);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("ApplicationDbContextConnection", ex.ParamName);
            }
        }

        [TestMethod]
        public void AddFlexDbDataProtection_WithMultiTenantMissingConfigConnectionString_ShouldThrowArgumentNullException()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("MultiTenantEditor", "true")
                })
                .Build();

            try
            {
                services.AddFlexDbDataProtection(config);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("ApplicationDbContextConnection", ex.ParamName);
            }
        }

        [TestMethod]
        public void AddFlexDbDataProtection_WithValidConnection_ShouldRegisterDataProtectionServices()
        {
            var services = new ServiceCollection();
            var dbPath = Path.Combine(Path.GetTempPath(), $"dp-{Guid.NewGuid():N}.db");
            var conn = $"Data Source={dbPath}";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("ConnectionStrings:ApplicationDbContextConnection", conn)
                })
                .Build();

            services.AddFlexDbDataProtection(config);

            var hasDbContextSingleton = services.Any(d => d.ServiceType == typeof(DataProtectionDbContext));
            Assert.IsTrue(hasDbContextSingleton);

            using var provider = services.BuildServiceProvider();
            var dbContext = provider.GetService<DataProtectionDbContext>();
            var dataProtectionProvider = provider.GetService<IDataProtectionProvider>();

            Assert.IsNotNull(dbContext);
            Assert.IsNotNull(dataProtectionProvider);
        }
    }
}
