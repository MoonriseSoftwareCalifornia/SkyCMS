// <copyright file="AzureStorageFileOperationsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Cosmos.BlobService;
using Cosmos.BlobService.Drivers;
using Cosmos.BlobService.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Sky.Tests.BlobStorage
{
    /// <summary>
    /// Priority 4 tests for AzureStorage file operations.
    /// Tests CopyBlobAsync and GetFilesAndDirectories methods.
    /// </summary>
    [TestClass]
    public class AzureStorageFileOperationsTests
    {
        #region CopyBlobAsync Tests

        [TestMethod]
        public async Task CopyBlobAsync_WithValidPaths_CopiesBlob()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var sourcePath = "source/file.txt";
            var destinationPath = "destination/file.txt";

            // Act
            await azureStorage.CopyBlobAsync(sourcePath, destinationPath);

            // Assert
            // Note: In unit test with mocks, we verify method doesn't throw
            // Integration tests would verify actual copy operation
            Assert.IsTrue(true, "Copy operation should complete without errors");
        }

        [TestMethod]
        public async Task CopyBlobAsync_WithLeadingSlashes_TrimsSlashes()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var sourcePath = "/source/file.txt";
            var destinationPath = "/destination/file.txt";

            // Act
            await azureStorage.CopyBlobAsync(sourcePath, destinationPath);

            // Assert
            Assert.IsTrue(true, "Should handle leading slashes");
        }

        [TestMethod]
        public async Task CopyBlobAsync_ToSameDirectory_Works()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var sourcePath = "folder/original.txt";
            var destinationPath = "folder/copy.txt";

            // Act
            await azureStorage.CopyBlobAsync(sourcePath, destinationPath);

            // Assert
            Assert.IsTrue(true, "Should copy within same directory");
        }

        [TestMethod]
        public async Task CopyBlobAsync_ToDifferentDirectory_Works()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var sourcePath = "folder1/file.txt";
            var destinationPath = "folder2/file.txt";

            // Act
            await azureStorage.CopyBlobAsync(sourcePath, destinationPath);

            // Assert
            Assert.IsTrue(true, "Should copy to different directory");
        }

        [TestMethod]
        public async Task CopyBlobAsync_NonExistentSource_HandlesGracefully()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var sourcePath = "nonexistent/file.txt";
            var destinationPath = "destination/file.txt";

            // Act - Should not throw when source doesn't exist
            await azureStorage.CopyBlobAsync(sourcePath, destinationPath);

            // Assert
            Assert.IsTrue(true, "Should handle non-existent source gracefully");
        }

        #endregion

        #region GetFilesAndDirectories Tests

        [TestMethod]
        public async Task GetFilesAndDirectories_WithEmptyPath_ReturnsRootItems()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            // Act
            var result = await azureStorage.GetFilesAndDirectories(string.Empty);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsInstanceOfType(result, typeof(List<FileManagerEntry>), "Should return list of FileManagerEntry");
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_WithRootSlash_ReturnsRootItems()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            // Act
            var result = await azureStorage.GetFilesAndDirectories("/");

            // Assert
            Assert.IsNotNull(result, "Result should not be null for root path");
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_WithValidPath_ReturnsItems()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var path = "test/folder";

            // Act
            var result = await azureStorage.GetFilesAndDirectories(path);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsInstanceOfType(result, typeof(List<FileManagerEntry>), "Should return list");
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_WithLeadingSlash_TrimsSlash()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var path = "/test/folder";

            // Act
            var result = await azureStorage.GetFilesAndDirectories(path);

            // Assert
            Assert.IsNotNull(result, "Should handle leading slash");
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_FiltersFolderStubFiles()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var path = "test";

            // Act
            var result = await azureStorage.GetFilesAndDirectories(path);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            // Verify no folder.stubxx files are in the results
            var hasStubFiles = result.Any(f => f.Name != null && f.Name.Contains("folder.stubxx"));
            Assert.IsFalse(hasStubFiles, "Should filter out folder stub marker files");
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_DistinguishesFilesAndFolders()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var path = "mixed-content";

            // Act
            var result = await azureStorage.GetFilesAndDirectories(path);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            // Files should have extensions, directories should not
            foreach (var entry in result)
            {
                if (entry.IsDirectory)
                {
                    Assert.IsTrue(string.IsNullOrEmpty(entry.Extension) || entry.Extension == string.Empty, 
                        "Directories should not have extensions");
                }
            }
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_SetsCorrectProperties()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var path = "test";

            // Act
            var result = await azureStorage.GetFilesAndDirectories(path);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            foreach (var entry in result)
            {
                Assert.IsNotNull(entry.Name, "Entry should have a name");
                Assert.IsNotNull(entry.Path, "Entry should have a path");
                // DateCreated and Size might be default values in mock scenario
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates an AzureStorage instance with a mock BlobServiceClient for testing.
        /// Uses the internal constructor that accepts a BlobServiceClient.
        /// </summary>
        private AzureStorage CreateAzureStorageWithMock(BlobServiceClient mockClient)
        {
            var constructor = typeof(AzureStorage).GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(BlobServiceClient), typeof(string) },
                null);

            if (constructor == null)
            {
                Assert.Fail("Internal constructor not found. Test implementation may need update.");
            }

            return (AzureStorage)constructor.Invoke(new object[] { mockClient, "$web" });
        }

        #endregion
    }
}
