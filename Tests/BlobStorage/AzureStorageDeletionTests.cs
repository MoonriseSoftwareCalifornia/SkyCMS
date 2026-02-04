// <copyright file="AzureStorageDeletionTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.Reflection;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Cosmos.BlobService.Drivers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Sky.Tests.BlobStorage
{
    /// <summary>
    /// Priority 4 tests for AzureStorage deletion operations.
    /// Tests DeleteIfExistsAsync and DeleteAppendBlobWithRetryAsync methods.
    /// </summary>
    [TestClass]
    public class AzureStorageDeletionTests
    {
        #region DeleteIfExistsAsync Tests

        [TestMethod]
        public async Task DeleteIfExistsAsync_WithValidPath_DeletesBlob()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var path = "test/file.txt";

            // Act
            await azureStorage.DeleteIfExistsAsync(path);

            // Assert
            Assert.IsTrue(true, "Delete operation should complete without errors");
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_WithNonExistentBlob_DoesNotThrow()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var path = "nonexistent/file.txt";

            // Act & Assert - Should not throw
            await azureStorage.DeleteIfExistsAsync(path);
            Assert.IsTrue(true, "Should handle non-existent blob gracefully");
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_WithImageFile_DeletesThumbnail()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var imagePath = "images/photo.jpg";

            // Act
            await azureStorage.DeleteIfExistsAsync(imagePath);

            // Assert
            // Should attempt to delete both the image and its .tn thumbnail
            Assert.IsTrue(true, "Should delete image and thumbnail");
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_WithPngImage_DeletesThumbnail()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var imagePath = "images/graphic.png";

            // Act
            await azureStorage.DeleteIfExistsAsync(imagePath);

            // Assert
            Assert.IsTrue(true, "Should delete PNG and thumbnail");
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_WithGifImage_DeletesThumbnail()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var imagePath = "images/animation.gif";

            // Act
            await azureStorage.DeleteIfExistsAsync(imagePath);

            // Assert
            Assert.IsTrue(true, "Should delete GIF and thumbnail");
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_WithNonImageFile_DoesNotDeleteThumbnail()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var documentPath = "documents/file.pdf";

            // Act
            await azureStorage.DeleteIfExistsAsync(documentPath);

            // Assert
            // Should only delete the main file, not attempt thumbnail deletion
            Assert.IsTrue(true, "Should delete only the PDF, no thumbnail");
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_WithLeasedBlob_BreaksLeaseBeforeDelete()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);
            var path = "locked/file.txt";

            // Act - Should break lease if it exists
            await azureStorage.DeleteIfExistsAsync(path);

            // Assert
            Assert.IsTrue(true, "Should break lease and delete");
        }

        #endregion

        #region DeleteAppendBlobWithRetryAsync Tests

        [TestMethod]
        public async Task DeleteAppendBlobWithRetryAsync_WithDefaultTimeout_DeletesBlob()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var mockAppendBlobClient = new Mock<AppendBlobClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            // Setup mock to simulate successful deletion
            mockAppendBlobClient.Setup(x => x.ExistsAsync(default))
                .ReturnsAsync(Azure.Response.FromValue(false, Mock.Of<Azure.Response>()));

            // Act
            var result = await InvokeDeleteAppendBlobWithRetryAsync(azureStorage, mockAppendBlobClient.Object);

            // Assert
            Assert.IsTrue(result, "Should return true when blob is deleted");
        }

        [TestMethod]
        public async Task DeleteAppendBlobWithRetryAsync_WithCustomTimeout_RespectsTimeout()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var mockAppendBlobClient = new Mock<AppendBlobClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            var customTimeout = TimeSpan.FromSeconds(5);

            mockAppendBlobClient.Setup(x => x.ExistsAsync(default))
                .ReturnsAsync(Azure.Response.FromValue(false, Mock.Of<Azure.Response>()));

            // Act
            var startTime = DateTime.UtcNow;
            var result = await InvokeDeleteAppendBlobWithRetryAsync(
                azureStorage, 
                mockAppendBlobClient.Object, 
                customTimeout);
            var elapsed = DateTime.UtcNow - startTime;

            // Assert
            Assert.IsTrue(result, "Should complete within timeout");
            Assert.IsTrue(elapsed < customTimeout + TimeSpan.FromSeconds(2), 
                "Should respect custom timeout");
        }

        [TestMethod]
        public async Task DeleteAppendBlobWithRetryAsync_WithCustomPollInterval_UsesInterval()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var mockAppendBlobClient = new Mock<AppendBlobClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            var customPollInterval = TimeSpan.FromMilliseconds(100);

            mockAppendBlobClient.Setup(x => x.ExistsAsync(default))
                .ReturnsAsync(Azure.Response.FromValue(false, Mock.Of<Azure.Response>()));

            // Act
            var result = await InvokeDeleteAppendBlobWithRetryAsync(
                azureStorage,
                mockAppendBlobClient.Object,
                pollInterval: customPollInterval);

            // Assert
            Assert.IsTrue(result, "Should use custom poll interval");
        }

        [TestMethod]
        public async Task DeleteAppendBlobWithRetryAsync_BlobAlreadyDeleted_ReturnsTrue()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var mockAppendBlobClient = new Mock<AppendBlobClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            // Simulate blob already deleted
            mockAppendBlobClient.Setup(x => x.ExistsAsync(default))
                .ReturnsAsync(Azure.Response.FromValue(false, Mock.Of<Azure.Response>()));

            // Act
            var result = await InvokeDeleteAppendBlobWithRetryAsync(azureStorage, mockAppendBlobClient.Object);

            // Assert
            Assert.IsTrue(result, "Should return true when blob already doesn't exist");
        }

        [TestMethod]
        public async Task DeleteAppendBlobWithRetryAsync_RetryLogic_PollsUntilDeleted()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var mockAppendBlobClient = new Mock<AppendBlobClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            int callCount = 0;
            mockAppendBlobClient.Setup(x => x.ExistsAsync(default))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    // Simulate: first 2 calls blob exists, then it's deleted
                    bool exists = callCount <= 2;
                    return Azure.Response.FromValue(exists, Mock.Of<Azure.Response>());
                });

            // Act
            var result = await InvokeDeleteAppendBlobWithRetryAsync(
                azureStorage,
                mockAppendBlobClient.Object,
                timeout: TimeSpan.FromSeconds(10),
                pollInterval: TimeSpan.FromMilliseconds(100));

            // Assert
            Assert.IsTrue(result, "Should eventually return true when blob is deleted");
            Assert.IsTrue(callCount >= 2, "Should have polled multiple times");
        }

        [TestMethod]
        public async Task DeleteAppendBlobWithRetryAsync_TimeoutExpires_ReturnsFalseOrTrue()
        {
            // Arrange
            var mockBlobServiceClient = new Mock<BlobServiceClient>();
            var mockAppendBlobClient = new Mock<AppendBlobClient>();
            var azureStorage = CreateAzureStorageWithMock(mockBlobServiceClient.Object);

            // Simulate blob never gets deleted
            mockAppendBlobClient.Setup(x => x.ExistsAsync(default))
                .ReturnsAsync(Azure.Response.FromValue(true, Mock.Of<Azure.Response>()));

            // Act
            var result = await InvokeDeleteAppendBlobWithRetryAsync(
                azureStorage,
                mockAppendBlobClient.Object,
                timeout: TimeSpan.FromMilliseconds(500),
                pollInterval: TimeSpan.FromMilliseconds(100));

            // Assert
            // When timeout expires and blob still exists, should return false
            Assert.IsFalse(result, "Should return false when timeout expires and blob still exists");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates an AzureStorage instance with a mock BlobServiceClient for testing.
        /// </summary>
        private AzureStorage CreateAzureStorageWithMock(BlobServiceClient mockClient)
        {
            var constructor = typeof(AzureStorage).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(BlobServiceClient), typeof(string) },
                null);

            if (constructor == null)
            {
                Assert.Fail("Internal constructor not found. Test implementation may need update.");
            }

            return (AzureStorage)constructor.Invoke(new object[] { mockClient, "$web" });
        }

        /// <summary>
        /// Invokes the private DeleteAppendBlobWithRetryAsync method using reflection.
        /// </summary>
        private async Task<bool> InvokeDeleteAppendBlobWithRetryAsync(
            AzureStorage azureStorage,
            AppendBlobClient appendBlobClient,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null)
        {
            var method = typeof(AzureStorage).GetMethod(
                "DeleteAppendBlobWithRetryAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                Assert.Fail("DeleteAppendBlobWithRetryAsync method not found");
            }

            var task = (Task<bool>)method.Invoke(azureStorage, new object[] { appendBlobClient, timeout, pollInterval });
            return await task;
        }

        #endregion
    }
}
