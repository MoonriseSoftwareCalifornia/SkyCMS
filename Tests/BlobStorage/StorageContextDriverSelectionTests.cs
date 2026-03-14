// <copyright file="StorageContextDriverSelectionTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.Reflection;
using Cosmos.BlobService;
using Cosmos.BlobService.Drivers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sky.Tests.BlobStorage
{
    /// <summary>
    /// Priority 4 tests for StorageContext driver selection and caching.
    /// Tests GetDriverFromConnectionString and GetOrCreateCachedDriver methods.
    /// </summary>
    [TestClass]
    public class StorageContextDriverSelectionTests
    {
        private IMemoryCache memoryCache;

        [TestInitialize]
        public void Setup()
        {
            memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        [TestCleanup]
        public void Cleanup()
        {
            memoryCache?.Dispose();
        }

        #region GetDriverFromConnectionString Tests

        [TestMethod]
        public void GetDriverFromConnectionString_WithKnownProviderFormats_ReturnsExpectedDriverType()
        {
            var scenarios = new[]
            {
                new
                {
                    ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
                    ExpectedDriverType = typeof(AzureStorage),
                },
                new
                {
                    ConnectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;",
                    ExpectedDriverType = typeof(AzureStorage),
                },
                new
                {
                    ConnectionString = "Bucket=test-bucket;Region=us-east-1;KeyId=AKIAIOSFODNN7EXAMPLE;Key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                    ExpectedDriverType = typeof(AmazonStorage),
                },
                new
                {
                    ConnectionString = "AccountId=123456789012;Bucket=test-bucket;KeyId=AKIAIOSFODNN7EXAMPLE;Key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                    ExpectedDriverType = typeof(AmazonStorage),
                },
            };

            foreach (var scenario in scenarios)
            {
                var context = new StorageContext(scenario.ConnectionString, memoryCache);
                var driver = GetPrivateDriver(context);

                Assert.IsNotNull(driver, "Driver should not be null");
                Assert.IsInstanceOfType(driver, scenario.ExpectedDriverType, "Driver type should match connection string provider");
            }
        }

        [TestMethod]
        public void GetDriverFromConnectionString_WithInvalidFormats_ThrowsException()
        {
            var invalidConnectionStrings = new[]
            {
                "Bucket=test-bucket;KeyId=AKIAIOSFODNN7EXAMPLE",
                "AccountId=123456789012;Bucket=test-bucket",
                "InvalidFormat=true;NoProvider=yes"
            };

            foreach (var connectionString in invalidConnectionStrings)
            {
                var exceptionThrown = false;
                try
                {
                    _ = new StorageContext(connectionString, memoryCache);
                }
                catch (InvalidOperationException)
                {
                    exceptionThrown = true;
                }

                Assert.IsTrue(exceptionThrown, "Should throw InvalidOperationException for invalid connection string format");
            }
        }

        [TestMethod]
        public void GetDriverFromConnectionString_WithNullOrEmpty_ReturnsNull()
        {
            // Arrange
            var context = new StorageContext(string.Empty, memoryCache);

            // Act
            var driver = GetPrivateDriver(context);

            // Assert
            Assert.IsNull(driver, "Driver should be null for empty connection string");
        }

        #endregion

        #region GetOrCreateCachedDriver Tests

        [TestMethod]
        public void GetOrCreateCachedDriver_FirstCall_CreatesNewDriver()
        {
            // Arrange
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net";
            var context = new StorageContext(connectionString, memoryCache);

            // Act
            var driver1 = GetPrivateDriver(context);
            var driver2 = GetPrivateDriver(context);

            // Assert
            Assert.IsNotNull(driver1, "First driver should not be null");
            Assert.IsNotNull(driver2, "Second driver should not be null");
            Assert.AreSame(driver1, driver2, "Both calls should return the same cached instance");
        }

        [TestMethod]
        public void GetOrCreateCachedDriver_DifferentConnectionStrings_CreatesDifferentDrivers()
        {
            // Arrange
            var connectionString1 = "DefaultEndpointsProtocol=https;AccountName=account1;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net";
            var connectionString2 = "DefaultEndpointsProtocol=https;AccountName=account2;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net";
            
            var context1 = new StorageContext(connectionString1, memoryCache);
            var context2 = new StorageContext(connectionString2, memoryCache);

            // Act
            var driver1 = GetPrivateDriver(context1);
            var driver2 = GetPrivateDriver(context2);

            // Assert
            Assert.IsNotNull(driver1, "First driver should not be null");
            Assert.IsNotNull(driver2, "Second driver should not be null");
            Assert.AreNotSame(driver1, driver2, "Different connection strings should create different driver instances");
        }

        [TestMethod]
        public void GetOrCreateCachedDriver_CachesAcrossProviderTypes()
        {
            // Arrange
            var azureConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net";
            var s3ConnectionString = "Bucket=test-bucket;Region=us-east-1;KeyId=AKIAIOSFODNN7EXAMPLE;Key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";
            
            var azureContext = new StorageContext(azureConnectionString, memoryCache);
            var s3Context = new StorageContext(s3ConnectionString, memoryCache);

            // Act
            var azureDriver = GetPrivateDriver(azureContext);
            var s3Driver = GetPrivateDriver(s3Context);

            // Assert
            Assert.IsInstanceOfType(azureDriver, typeof(AzureStorage), "First driver should be Azure");
            Assert.IsInstanceOfType(s3Driver, typeof(AmazonStorage), "Second driver should be Amazon");
            Assert.AreNotSame(azureDriver, s3Driver, "Different provider types should have different instances");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Uses reflection to access the private primaryDriver field from StorageContext.
        /// </summary>
        private ICosmosStorage GetPrivateDriver(StorageContext context)
        {
            var fieldInfo = typeof(StorageContext).GetField("primaryDriver", BindingFlags.NonPublic | BindingFlags.Instance);
            return fieldInfo?.GetValue(context) as ICosmosStorage;
        }

        #endregion
    }
}
