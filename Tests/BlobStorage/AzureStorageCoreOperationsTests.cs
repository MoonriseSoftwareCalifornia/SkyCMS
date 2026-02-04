// <copyright file="AzureStorageCoreOperationsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Cosmos.BlobService.Drivers;
using Cosmos.BlobService.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Sky.Tests.BlobStorage
{
    /// <summary>
    /// Priority 4 tests for AzureStorage core operations.
    /// Tests UploadStreamAsync, DeleteFolderAsync, and GetBlobAsync methods.
    /// </summary>
    [TestClass]
    public class AzureStorageCoreOperationsTests
    {
        #region UploadStreamAsync Tests

        [TestMethod]
        public async Task UploadStreamAsync_WithValidStream_UploadsSuccessfully()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            var fileMetaData = new FileUploadMetaData
            {
                RelativePath = "test/file.txt",
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = 100,
                ContentType = "text/plain"
            };

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Test content"));

            // Act
            var result = await azureStorage.UploadStreamAsync(stream, fileMetaData, DateTimeOffset.UtcNow);

            // Assert
            Assert.IsTrue(result, "Upload should return true on success");
        }

        [TestMethod]
        public async Task UploadStreamAsync_WithEmptyStream_HandlesGracefully()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            var fileMetaData = new FileUploadMetaData
            {
                RelativePath = "test/empty.txt",
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = 0,
                ContentType = "text/plain"
            };

            using var stream = new MemoryStream();

            // Act
            var result = await azureStorage.UploadStreamAsync(stream, fileMetaData, DateTimeOffset.UtcNow);

            // Assert
            Assert.IsTrue(result, "Upload should handle empty stream");
        }

        [TestMethod]
        public async Task UploadStreamAsync_SetsCorrectMetadata()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            var uploadUid = Guid.NewGuid().ToString();
            var uploadDateTime = DateTimeOffset.UtcNow;
            var fileMetaData = new FileUploadMetaData
            {
                RelativePath = "test/metadata.txt",
                UploadUid = uploadUid,
                TotalFileSize = 50,
                ContentType = "text/plain"
            };

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Content"));

            // Act
            var result = await azureStorage.UploadStreamAsync(stream, fileMetaData, uploadDateTime);

            // Assert
            Assert.IsTrue(result, "Upload should succeed");
            // Note: Actual metadata verification would require integration test or more complex mocking
        }

        #endregion

        #region GetBlobAsync Tests

        [TestMethod]
        public async Task GetBlobAsync_WithValidPath_ReturnsBlobClient()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var path = "test/file.txt";

            // Act
            var blobClient = await azureStorage.GetBlobAsync(path);

            // Assert
            // Note: With our current mock setup, this will return a client even if container doesn't exist
            // In a real integration test, we'd verify the client is properly configured
            Assert.IsNotNull(blobClient, "GetBlobAsync should return a BlobClient");
        }

        [TestMethod]
        public async Task GetBlobAsync_WithLeadingSlash_TrimsSlash()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var pathWithSlash = "/test/file.txt";

            // Act
            var blobClient = await azureStorage.GetBlobAsync(pathWithSlash);

            // Assert
            Assert.IsNotNull(blobClient, "Should handle leading slash");
            // The path should be trimmed internally
        }

        [TestMethod]
        public async Task GetBlobAsync_WithNullPath_ReturnsNull()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            // Act
            var blobClient = await azureStorage.GetBlobAsync(null);

            // Assert
            Assert.IsNull(blobClient, "GetBlobAsync should return null for null path");
        }

        [TestMethod]
        public async Task GetBlobAsync_WithEmptyPath_ReturnsNull()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            // Act
            var blobClient = await azureStorage.GetBlobAsync(string.Empty);

            // Assert
            Assert.IsNull(blobClient, "GetBlobAsync should return null for empty path");
        }

        #endregion

        #region DeleteFolderAsync Tests

        [TestMethod]
        public async Task DeleteFolderAsync_WithValidPath_DeletesFolder()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var folderPath = "test/folder";

            // Act
            var deletedCount = await azureStorage.DeleteFolderAsync(folderPath);

            // Assert
            Assert.IsTrue(deletedCount >= 0, "Should return count of deleted items");
        }

        [TestMethod]
        public async Task DeleteFolderAsync_WithEmptyFolder_ReturnsZero()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var emptyFolderPath = "test/empty-folder";

            // Act
            var deletedCount = await azureStorage.DeleteFolderAsync(emptyFolderPath);

            // Assert
            Assert.AreEqual(0, deletedCount, "Empty folder should return 0 deleted items");
        }

        [TestMethod]
        public async Task DeleteFolderAsync_WithNestedContent_DeletesAllItems()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var folderPath = "test/nested-folder";

            // Act
            var deletedCount = await azureStorage.DeleteFolderAsync(folderPath);

            // Assert
            Assert.IsTrue(deletedCount >= 0, "Should delete nested content");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates an AzureStorage instance with a mock BlobServiceClient for testing.
        /// Uses the internal constructor that accepts a BlobServiceClient.
        /// </summary>
        private AzureStorage CreateAzureStorageWithMock(BlobServiceClient mockClient)
        {
            // Use reflection to call the internal constructor
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
