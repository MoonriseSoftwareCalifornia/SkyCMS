// <copyright file="StorageContextTenantMemoizationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Sky.Tests.BlobStorage
{
    using Cosmos.BlobService;
    using Cosmos.BlobService.Drivers;
    using Cosmos.DynamicConfig;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class StorageContextTenantMemoizationTests
    {
        [TestMethod]
        public async Task GetPrimaryDriverAsync_SameTenant_ReusesMemoizedDriverWithoutSecondResolution()
        {
            var providerMock = new Mock<IDynamicConfigurationProvider>();
            providerMock.Setup(p => p.GetTenantDomainNameFromRequest())
                .Returns("tenant-a.example");
            providerMock.Setup(p => p.GetStorageConnectionStringAsync("tenant-a.example", It.IsAny<CancellationToken>()))
                .ReturnsAsync("DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;");

            var context = CreateMultiTenantContext(providerMock.Object);

            var driver1 = await InvokeGetPrimaryDriverAsync(context);
            var driver2 = await InvokeGetPrimaryDriverAsync(context);

            Assert.IsNotNull(driver1);
            Assert.AreSame(driver1, driver2);
            providerMock.Verify(
                p => p.GetStorageConnectionStringAsync("tenant-a.example", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task GetPrimaryDriverAsync_TenantSwitch_RefreshesDriverResolution()
        {
            var providerMock = new Mock<IDynamicConfigurationProvider>();
            providerMock.SetupSequence(p => p.GetTenantDomainNameFromRequest())
                .Returns("tenant-a.example")
                .Returns("tenant-b.example");

            providerMock.Setup(p => p.GetStorageConnectionStringAsync("tenant-a.example", It.IsAny<CancellationToken>()))
                .ReturnsAsync("DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;");
            providerMock.Setup(p => p.GetStorageConnectionStringAsync("tenant-b.example", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Bucket=test-bucket;Region=us-east-1;KeyId=AKIAIOSFODNN7EXAMPLE;Key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY");

            var context = CreateMultiTenantContext(providerMock.Object);

            var driver1 = await InvokeGetPrimaryDriverAsync(context);
            var driver2 = await InvokeGetPrimaryDriverAsync(context);

            Assert.IsNotNull(driver1);
            Assert.IsNotNull(driver2);
            Assert.AreNotSame(driver1, driver2);

            providerMock.Verify(
                p => p.GetStorageConnectionStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        private static StorageContext CreateMultiTenantContext(IDynamicConfigurationProvider dynamicProvider)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "MultiTenantEditor", "true" }
                })
                .Build();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var services = new ServiceCollection();
            services.AddSingleton(dynamicProvider);
            var serviceProvider = services.BuildServiceProvider();

            return new StorageContext(configuration, memoryCache, serviceProvider);
        }

        private static async Task<ICosmosStorage> InvokeGetPrimaryDriverAsync(StorageContext context)
        {
            var method = typeof(StorageContext).GetMethod(
                "GetPrimaryDriverAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var task = (Task<ICosmosStorage>)method.Invoke(context, null);
            return await task;
        }
    }
}
