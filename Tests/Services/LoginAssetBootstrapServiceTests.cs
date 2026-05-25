// <copyright file="LoginAssetBootstrapServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and contributors participating to project.
// </copyright>

namespace Sky.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Cosmos.DynamicConfig;
    using Cosmos.Editor.Services;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Logging;
    using Moq;

    /// <summary>
    /// Tests for <see cref="LoginAssetBootstrapService"/>.
    /// </summary>
    [TestClass]
    public class LoginAssetBootstrapServiceTests
    {
        private Mock<IWebHostEnvironment> webHostEnvironment = null!;
        private Mock<IDynamicConfigurationProvider> dynamicConfigurationProvider = null!;
        private Mock<ILoginAssetBlobClient> blobClient = null!;
        private Mock<ILogger<LoginAssetBootstrapService>> logger = null!;
        private string webRootPath = null!;

        [TestInitialize]
        public void Setup()
        {
            webRootPath = Path.Combine(Path.GetTempPath(), $"LoginAssetBootstrapServiceTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(webRootPath, "lib", "picocss"));
            Directory.CreateDirectory(Path.Combine(webRootPath, "lib", "ckeditor"));

            File.WriteAllText(Path.Combine(webRootPath, "lib", "picocss", "pico.conditional.min.css"), "pico css");
            File.WriteAllText(Path.Combine(webRootPath, "lib", "ckeditor", "ckeditor5-content.css"), "ckeditor css");

            webHostEnvironment = new Mock<IWebHostEnvironment>();
            webHostEnvironment.SetupGet(w => w.WebRootPath).Returns(webRootPath);

            dynamicConfigurationProvider = new Mock<IDynamicConfigurationProvider>();
            dynamicConfigurationProvider.Setup(p => p.GetStorageConnectionStringAsync(It.IsAny<string>(), default))
                .ReturnsAsync("UseDevelopmentStorage=true");

            blobClient = new Mock<ILoginAssetBlobClient>();
            blobClient.Setup(b => b.EnsureContainerExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            logger = new Mock<ILogger<LoginAssetBootstrapService>>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(webRootPath))
            {
                Directory.Delete(webRootPath, true);
            }
        }

        [TestMethod]
        public async Task EnsureRequiredAssetsAsync_WhenAssetsMissing_UploadsBothFiles()
        {
            blobClient.SetupSequence(b => b.BlobExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false)
                .ReturnsAsync(false);

            var service = CreateService();

            await service.EnsureRequiredAssetsAsync("example.com");

            blobClient.Verify(b => b.EnsureContainerExistsAsync("UseDevelopmentStorage=true", "$web"), Times.Once);
            blobClient.Verify(b => b.UploadAsync(
                "UseDevelopmentStorage=true",
                "$web",
                "lib/picocss/pico.conditional.min.css",
                It.IsAny<Stream>(),
                "text/css"), Times.Once);
            blobClient.Verify(b => b.UploadAsync(
                "UseDevelopmentStorage=true",
                "$web",
                "lib/ckeditor/ckeditor5-content.css",
                It.IsAny<Stream>(),
                "text/css"), Times.Once);
        }

        [TestMethod]
        public async Task EnsureRequiredAssetsAsync_WhenAssetsAlreadyExist_DoesNotUploadAgain()
        {
            blobClient.Setup(b => b.BlobExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var service = CreateService();

            await service.EnsureRequiredAssetsAsync();

            blobClient.Verify(b => b.EnsureContainerExistsAsync("UseDevelopmentStorage=true", "$web"), Times.Once);
            blobClient.Verify(b => b.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task EnsureRequiredAssetsAsync_WhenStorageConnectionMissing_SkipsBootstrap()
        {
            dynamicConfigurationProvider.Setup(p => p.GetStorageConnectionStringAsync(It.IsAny<string>(), default))
                .ReturnsAsync((string?)null);

            var service = CreateService();

            await service.EnsureRequiredAssetsAsync();

            blobClient.Verify(b => b.EnsureContainerExistsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            blobClient.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
        }

        private LoginAssetBootstrapService CreateService()
        {
            return new LoginAssetBootstrapService(
                webHostEnvironment.Object,
                dynamicConfigurationProvider.Object,
                blobClient.Object,
                logger.Object);
        }
    }
}
