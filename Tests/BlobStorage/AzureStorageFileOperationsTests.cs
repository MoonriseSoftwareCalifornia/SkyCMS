// <copyright file="AzureStorageFileOperationsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cosmos.BlobService;
using Cosmos.BlobService.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sky.Tests.BlobStorage
{
    /// <summary>
    /// Integration tests for Azure Storage file operations.
    /// Tests CopyAsync and GetFilesAndDirectories methods.
    /// Uses SkyCmsTestBase to provide real Azure Storage connection (Azurite or Azure).
    /// </summary>
    [TestClass]
    public class AzureStorageFileOperationsTests : SkyCmsTestBase
    {
        #region CopyAsync Tests

        [TestMethod]
        public async Task CopyAsync_WithValidPaths_CopiesBlob()
        {
            // Arrange
            var sourcePath = $"copytest/{Guid.NewGuid()}/source.txt";
            var destinationPath = $"copytest/{Guid.NewGuid()}/destination.txt";
            
            // Upload source file
            using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes("test content for copy"));
            var sourceMetadata = new FileUploadMetaData
            {
                RelativePath = sourcePath,
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = sourceStream.Length,
                ContentType = "text/plain",
                TotalChunks = 1,
                ChunkIndex = 0
            };
            await Storage.AppendBlob(sourceStream, sourceMetadata, "block");

            // Act
            await Storage.CopyAsync(sourcePath, destinationPath);

            // Assert
            var sourceExists = await Storage.BlobExistsAsync(sourcePath);
            var destinationExists = await Storage.BlobExistsAsync(destinationPath);
            
            Assert.IsTrue(sourceExists, "Source should still exist after copy");
            Assert.IsTrue(destinationExists, "Destination should exist after copy");
        }

        [TestMethod]
        public async Task CopyAsync_WithLeadingSlashes_TrimsSlashes()
        {
            // Arrange
            var sourcePath = $"copytest/{Guid.NewGuid()}/source.txt";
            var destinationPath = $"copytest/{Guid.NewGuid()}/destination.txt";
            
            // Upload source file
            using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
            var sourceMetadata = new FileUploadMetaData
            {
                RelativePath = sourcePath,
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = sourceStream.Length,
                ContentType = "text/plain",
                TotalChunks = 1,
                ChunkIndex = 0
            };
            await Storage.AppendBlob(sourceStream, sourceMetadata, "block");

            // Act - Use paths with leading slashes
            await Storage.CopyAsync("/" + sourcePath, "/" + destinationPath);

            // Assert
            var destinationExists = await Storage.BlobExistsAsync(destinationPath);
            Assert.IsTrue(destinationExists, "Should handle leading slashes and copy successfully");
        }

        [TestMethod]
        public async Task CopyAsync_ToSameDirectory_Works()
        {
            // Arrange
            var testFolder = $"copytest/{Guid.NewGuid()}";
            var sourcePath = $"{testFolder}/original.txt";
            var destinationPath = $"{testFolder}/copy.txt";
            
            // Upload source file
            using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes("original content"));
            var sourceMetadata = new FileUploadMetaData
            {
                RelativePath = sourcePath,
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = sourceStream.Length,
                ContentType = "text/plain",
                TotalChunks = 1,
                ChunkIndex = 0
            };
            await Storage.AppendBlob(sourceStream, sourceMetadata, "block");

            // Act
            await Storage.CopyAsync(sourcePath, destinationPath);

            // Assert
            var destinationExists = await Storage.BlobExistsAsync(destinationPath);
            Assert.IsTrue(destinationExists, "Should copy within same directory");
        }

        [TestMethod]
        public async Task CopyAsync_ToDifferentDirectory_Works()
        {
            // Arrange
            var sourceFolder = $"copytest/{Guid.NewGuid()}/folder1";
            var destFolder = $"copytest/{Guid.NewGuid()}/folder2";
            var sourcePath = $"{sourceFolder}/file.txt";
            var destinationPath = $"{destFolder}/file.txt";
            
            // Upload source file
            using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes("content to copy"));
            var sourceMetadata = new FileUploadMetaData
            {
                RelativePath = sourcePath,
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = sourceStream.Length,
                ContentType = "text/plain",
                TotalChunks = 1,
                ChunkIndex = 0
            };
            await Storage.AppendBlob(sourceStream, sourceMetadata, "block");

            // Act
            await Storage.CopyAsync(sourcePath, destinationPath);

            // Assert
            var destinationExists = await Storage.BlobExistsAsync(destinationPath);
            Assert.IsTrue(destinationExists, "Should copy to different directory");
        }

        [TestMethod]
        public async Task CopyAsync_CopiesFolderContents_Recursively()
        {
            // Arrange
            var sourceFolder = $"copytest/{Guid.NewGuid()}/sourcefolder";
            var destFolder = $"copytest/{Guid.NewGuid()}/destfolder";
            var file1Path = $"{sourceFolder}/file1.txt";
            var file2Path = $"{sourceFolder}/subfolder/file2.txt";

            // Upload multiple files in source folder
            using (var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("file 1 content")))
            {
                var metadata1 = new FileUploadMetaData
                {
                    RelativePath = file1Path,
                    UploadUid = Guid.NewGuid().ToString(),
                    TotalFileSize = stream1.Length,
                    ContentType = "text/plain",
                    TotalChunks = 1,
                    ChunkIndex = 0
                };
                await Storage.AppendBlob(stream1, metadata1, "block");
            }

            using (var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("file 2 content")))
            {
                var metadata2 = new FileUploadMetaData
                {
                    RelativePath = file2Path,
                    UploadUid = Guid.NewGuid().ToString(),
                    TotalFileSize = stream2.Length,
                    ContentType = "text/plain",
                    TotalChunks = 1,
                    ChunkIndex = 0
                };
                await Storage.AppendBlob(stream2, metadata2, "block");
            }

            // Act
            await Storage.CopyAsync(sourceFolder, destFolder);

            // Assert
            var destFile1 = file1Path.Replace(sourceFolder, destFolder);
            var destFile2 = file2Path.Replace(sourceFolder, destFolder);
            
            var dest1Exists = await Storage.BlobExistsAsync(destFile1);
            var dest2Exists = await Storage.BlobExistsAsync(destFile2);
            
            Assert.IsTrue(dest1Exists, "First file should be copied");
            Assert.IsTrue(dest2Exists, "Nested file should be copied");
        }

        #endregion

        #region GetFilesAndDirectories Tests

        [TestMethod]
        public async Task GetFilesAndDirectories_WithEmptyPath_ReturnsRootItems()
        {
            // Act
            var result = await Storage.GetFilesAndDirectories(string.Empty);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsInstanceOfType(result, typeof(List<FileManagerEntry>), "Should return list of FileManagerEntry");
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_WithRootSlash_ReturnsRootItems()
        {
            // Act
            var result = await Storage.GetFilesAndDirectories("/");

            // Assert
            Assert.IsNotNull(result, "Result should not be null for root path");
            Assert.IsInstanceOfType(result, typeof(List<FileManagerEntry>), "Should return list");
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_WithValidPath_ReturnsItems()
        {
            // Arrange
            var testPath = $"filetest/{Guid.NewGuid()}";
            var filePath = $"{testPath}/testfile.txt";
            
            // Upload a test file
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
            var metadata = new FileUploadMetaData
            {
                RelativePath = filePath,
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = stream.Length,
                ContentType = "text/plain",
                TotalChunks = 1,
                ChunkIndex = 0
            };
            await Storage.AppendBlob(stream, metadata, "block");

            // Act
            var result = await Storage.GetFilesAndDirectories(testPath);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Count > 0, "Should return at least one item");
            Assert.IsTrue(result.Any(r => r.Name == "testfile.txt"), "Should contain the uploaded file");
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_WithLeadingSlash_TrimsSlash()
        {
            // Arrange
            var testPath = $"filetest/{Guid.NewGuid()}";
            var filePath = $"{testPath}/file.txt";
            
            // Upload a test file
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
            var metadata = new FileUploadMetaData
            {
                RelativePath = filePath,
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = stream.Length,
                ContentType = "text/plain",
                TotalChunks = 1,
                ChunkIndex = 0
            };
            await Storage.AppendBlob(stream, metadata, "block");

            // Act - Use path with leading slash
            var result = await Storage.GetFilesAndDirectories("/" + testPath);

            // Assert
            Assert.IsNotNull(result, "Should handle leading slash");
            Assert.IsTrue(result.Count > 0, "Should return items");
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_FiltersFolderStubFiles()
        {
            // Arrange
            var testPath = $"filetest/{Guid.NewGuid()}";
            
            // Create a folder (which creates a folder.stubxx marker)
            await Storage.CreateFolder(testPath);

            // Act
            var result = await Storage.GetFilesAndDirectories(testPath);

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
            var testPath = $"filetest/{Guid.NewGuid()}";
            var filePath = $"{testPath}/document.txt";
            var subfolderPath = $"{testPath}/subfolder";
            
            // Upload a file
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("file content")))
            {
                var metadata = new FileUploadMetaData
                {
                    RelativePath = filePath,
                    UploadUid = Guid.NewGuid().ToString(),
                    TotalFileSize = stream.Length,
                    ContentType = "text/plain",
                    TotalChunks = 1,
                    ChunkIndex = 0
                };
                await Storage.AppendBlob(stream, metadata, "block");
            }
            
            // Create a folder
            await Storage.CreateFolder(subfolderPath);

            // Act
            var result = await Storage.GetFilesAndDirectories(testPath);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            
            var file = result.FirstOrDefault(r => r.Name == "document.txt");
            var folder = result.FirstOrDefault(r => r.Name == "subfolder");
            
            Assert.IsNotNull(file, "Should contain the file");
            Assert.IsNotNull(folder, "Should contain the folder");
            Assert.IsFalse(file.IsDirectory, "File should not be marked as directory");
            Assert.IsTrue(folder.IsDirectory, "Folder should be marked as directory");
            Assert.IsTrue(!string.IsNullOrEmpty(file.Extension), "File should have an extension");
            Assert.IsTrue(string.IsNullOrEmpty(folder.Extension), "Folder should not have an extension");
        }

        [TestMethod]
        public async Task GetFilesAndDirectories_SetsCorrectProperties()
        {
            // Arrange
            var testPath = $"filetest/{Guid.NewGuid()}";
            var filePath = $"{testPath}/properties.txt";
            
            // Upload a file
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content for properties"));
            var metadata = new FileUploadMetaData
            {
                RelativePath = filePath,
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = stream.Length,
                ContentType = "text/plain",
                TotalChunks = 1,
                ChunkIndex = 0
            };
            await Storage.AppendBlob(stream, metadata, "block");

            // Act
            var result = await Storage.GetFilesAndDirectories(testPath);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Count > 0, "Should have at least one entry");
            
            foreach (var entry in result)
            {
                Assert.IsNotNull(entry.Name, "Entry should have a name");
                Assert.IsNotNull(entry.Path, "Entry should have a path");
                Assert.IsTrue(entry.Created != default, "Entry should have a created date");
                Assert.IsTrue(entry.Modified != default, "Entry should have a modified date");
            }
        }

        #endregion

        [TestCleanup]
        public async Task Cleanup()
        {
            // Clean up any test blobs
            await Task.CompletedTask;
        }
    }
}
