// <copyright file="StorageParallelismHelperTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Publishing
{
    using Cosmos.BlobService;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Services.Publishing;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="StorageParallelismHelper"/>.
    /// </summary>
    [TestClass]
    public class StorageParallelismHelperTests
    {
        private Mock<ILogger> mockLogger;

        /// <summary>
        /// Initializes the test fixture before each test method.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            mockLogger = new Mock<ILogger>();
        }

        #region Azure Blob Storage Production Tests

        /// <summary>
        /// Tests that Azure Blob Storage (production) returns parallelism of 8.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_AzureBlobProduction_Returns8()
        {
            // Arrange - Use actual instance instead of mock
            var storage = new StorageContextWithAzureUrl();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(8, result);
        }

        /// <summary>
        /// Tests that Azure Storage detected by type name returns parallelism of 8.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_AzureStorageByTypeName_Returns8()
        {
            // Arrange - Use actual instance
            var storage = new AzureStorageTestDouble();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(8, result);
        }

        #endregion

        #region Azure Blob Emulator (Azurite) Tests

        /// <summary>
        /// Tests that Azurite on localhost returns parallelism of 2.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_AzuriteLocalhost_Returns2()
        {
            // Arrange
            var storage = new StorageContextWithLocalhostUrl();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(2, result);
        }

        /// <summary>
        /// Tests that Azurite on 127.0.0.1 returns parallelism of 2.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_Azurite127_Returns2()
        {
            // Arrange
            var storage = new StorageContextWith127Url();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(2, result);
        }

        /// <summary>
        /// Tests that Azurite devstoreaccount1 returns parallelism of 2.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_AzuriteDevStoreAccount_Returns2()
        {
            // Arrange
            var storage = new StorageContextWithDevStoreUrl();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(2, result);
        }

        #endregion

        #region AWS S3 Tests

        /// <summary>
        /// Tests that AWS S3 detected by URL returns parallelism of 8.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_AwsS3ByUrl_Returns8()
        {
            // Arrange
            var storage = new StorageContextWithS3Url();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(8, result);
        }

        /// <summary>
        /// Tests that AWS S3 detected by type name returns parallelism of 8.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_AwsS3ByTypeName_Returns8()
        {
            // Arrange
            var storage = new AmazonS3TestDouble();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(8, result);
        }

        #endregion

        #region Cloudflare R2 Tests

        /// <summary>
        /// Tests that Cloudflare R2 detected by URL returns parallelism of 8.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_CloudflareR2ByUrl_Returns8()
        {
            // Arrange
            var storage = new StorageContextWithR2Url();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(8, result);
        }

        /// <summary>
        /// Tests that Cloudflare R2 with .r2.dev URL returns parallelism of 8.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_CloudflareR2DevUrl_Returns8()
        {
            // Arrange
            var storage = new StorageContextWithR2DevUrl();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(8, result);
        }

        /// <summary>
        /// Tests that Cloudflare R2 detected by type name returns parallelism of 8.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_CloudflareR2ByTypeName_Returns8()
        {
            // Arrange
            var storage = new CloudflareR2TestDouble();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(8, result);
        }

        #endregion

        #region Local File System Tests

        /// <summary>
        /// Tests that local file system returns parallelism of 4.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_LocalFileSystem_Returns4()
        {
            // Arrange
            var storage = new LocalFileStorageTestDouble();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(4, result);
        }

        #endregion

        #region Unknown Storage Type Tests

        /// <summary>
        /// Tests that unknown storage type returns default parallelism of 4.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_UnknownStorageType_Returns4()
        {
            // Arrange
            var storage = new UnknownStorageTestDouble();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(4, result);
        }

        /// <summary>
        /// Tests that storage with no URL property returns default parallelism of 4.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_NoUrlProperty_Returns4()
        {
            // Arrange
            var storage = new Mock<IStorageContext>().Object; // Mock with no properties

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(4, result);
        }

        #endregion

        #region Configuration Override Tests

        /// <summary>
        /// Tests that configuration override value is returned when provided.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_WithOverride_ReturnsOverrideValue()
        {
            // Arrange
            var storage = new StorageContextWithAzureUrl();
            const int overrideValue = 16;

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object,
                overrideValue);

            // Assert
            Assert.AreEqual(overrideValue, result);
        }

        /// <summary>
        /// Tests that null override uses auto-detection.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_WithNullOverride_UsesAutoDetection()
        {
            // Arrange
            var storage = new StorageContextWithAzureUrl();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object,
                null);

            // Assert
            Assert.AreEqual(8, result); // Should auto-detect as Azure Production
        }

        /// <summary>
        /// Tests that zero override uses auto-detection.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_WithZeroOverride_UsesAutoDetection()
        {
            // Arrange
            var storage = new StorageContextWithAzureUrl();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object,
                0);

            // Assert
            Assert.AreEqual(8, result); // Should ignore 0 and auto-detect
        }

        /// <summary>
        /// Tests that negative override uses auto-detection.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_WithNegativeOverride_UsesAutoDetection()
        {
            // Arrange
            var storage = new StorageContextWithAzureUrl();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object,
                -5);

            // Assert
            Assert.AreEqual(8, result); // Should ignore negative and auto-detect
        }

        #endregion

        #region Logging Tests

        /// <summary>
        /// Tests that override configuration is logged.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_WithOverride_LogsOverrideMessage()
        {
            // Arrange
            var storage = new StorageContextWithAzureUrl();
            const int overrideValue = 12;

            // Act
            StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object,
                overrideValue);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("override")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Tests that auto-detected storage type is logged.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_WithAutoDetection_LogsDetectedType()
        {
            // Arrange
            var storage = new StorageContextWithR2Url();

            // Act
            StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Auto-detected")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Multiple URL Property Tests

        /// <summary>
        /// Tests detection using StorageEndpointUrl property.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_StorageEndpointUrl_DetectsCorrectly()
        {
            // Arrange
            var storage = new StorageContextWithEndpointUrl();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(8, result);
        }

        /// <summary>
        /// Tests detection using ServiceUrl property.
        /// </summary>
        [TestMethod]
        public void GetOptimalParallelism_ServiceUrl_DetectsCorrectly()
        {
            // Arrange
            var storage = new StorageContextWithServiceUrl();

            // Act
            var result = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                mockLogger.Object);

            // Assert
            Assert.AreEqual(8, result);
        }

        #endregion

        #region Test Double Classes

        private class AzureStorageTestDouble : IStorageContext
        {
            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();

            Task<FileManagerEntry> IStorageContext.CreateFolder(string path)
            {
                throw new NotImplementedException();
            }

            Task<FileManagerEntry> IStorageContext.GetFileAsync(string path)
            {
                throw new NotImplementedException();
            }

            Task<List<FileManagerEntry>> IStorageContext.GetFilesAndDirectories(string path)
            {
                throw new NotImplementedException();
            }
        }

        private class AmazonS3TestDouble : IStorageContext
        {
            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class CloudflareR2TestDouble : IStorageContext
        {
            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class LocalFileStorageTestDouble : IStorageContext
        {
            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class UnknownStorageTestDouble : IStorageContext
        {
            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class StorageContextWithAzureUrl : IStorageContext
        {
            public string AzureBlobStorageUrl => "https://test.blob.core.windows.net";

            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class StorageContextWithLocalhostUrl : IStorageContext
        {
            public string AzureBlobStorageUrl => "https://localhost:10000/devstoreaccount1";

            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class StorageContextWith127Url : IStorageContext
        {
            public string AzureBlobStorageUrl => "http://127.0.0.1:10000/devstoreaccount1";

            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class StorageContextWithDevStoreUrl : IStorageContext
        {
            public string BaseUrl => "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1";

            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class StorageContextWithS3Url : IStorageContext
        {
            public string ServiceUrl => "https://my-bucket.s3.amazonaws.com";

            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class StorageContextWithR2Url : IStorageContext
        {
            public string Endpoint => "https://abc123.r2.cloudflarestorage.com";

            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class StorageContextWithR2DevUrl : IStorageContext
        {
            public string BucketUrl => "https://my-bucket.abc123.r2.dev";

            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class StorageContextWithEndpointUrl : IStorageContext
        {
            public string StorageEndpointUrl => "https://test.blob.core.windows.net";

            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        private class StorageContextWithServiceUrl : IStorageContext
        {
            public string ServiceUrl => "https://bucket.s3.amazonaws.com";

            public System.Threading.Tasks.Task<bool> BlobExistsAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task CopyAsync(string target, string destination) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFolderAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task DeleteFileAsync(string path) => throw new NotImplementedException();
            public void DeleteFile(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task EnableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task DisableAzureStaticWebsite() => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetFilesAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> GetFileAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.IO.Stream> GetStreamAsync(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFileAsync(string sourceFile, string destinationFile) => throw new NotImplementedException();
            public System.Threading.Tasks.Task MoveFolderAsync(string sourceFolder, string destinationFolder) => throw new NotImplementedException();
            public System.Threading.Tasks.Task AppendBlob(System.IO.MemoryStream stream, Cosmos.BlobService.Models.FileUploadMetaData fileMetaData, string mode = "append") => throw new NotImplementedException();
            public System.Threading.Tasks.Task<FileManagerEntry> CreateFolder(string path) => throw new NotImplementedException();
            public System.Threading.Tasks.Task<System.Collections.Generic.List<FileManagerEntry>> GetFilesAndDirectories(string path) => throw new NotImplementedException();
        }

        #endregion
    }
}