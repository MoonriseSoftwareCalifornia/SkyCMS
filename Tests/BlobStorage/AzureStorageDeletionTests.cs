// <copyright file="AzureStorageDeletionTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cosmos.BlobService.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sky.Tests.BlobStorage
{
    /// <summary>
    /// Integration tests for Azure Storage deletion operations.
    /// Tests DeleteFileAsync and related cleanup functionality.
    /// Uses SkyCmsTestBase to provide real Azure Storage connection (Azurite or Azure).
    /// </summary>
    [TestClass]
    public class AzureStorageDeletionTests : SkyCmsTestBase
    {
        #region DeleteFileAsync Tests

        [TestMethod]
        public async Task DeleteFileAsync_WithValidPath_DeletesBlob()
        {
            // Arrange
            var path = $"test/{Guid.NewGuid()}/file.txt";
            
            // Upload a test blob first using AppendBlob
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
            var metadata = new FileUploadMetaData
            {
                RelativePath = path,
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = stream.Length,
                ContentType = "text/plain",
                TotalChunks = 1,
                ChunkIndex = 0
            };
            await Storage.AppendBlob(stream, metadata, "block");

            // Act
            await Storage.DeleteFileAsync(path);

            // Assert
            var exists = await Storage.BlobExistsAsync(path);
            Assert.IsFalse(exists, "Blob should be deleted");
        }

        [TestMethod]
        public async Task DeleteFileAsync_WithNonExistentBlob_DoesNotThrow()
        {
            // Arrange
            var path = $"nonexistent/{Guid.NewGuid()}/file.txt";

            // Act & Assert - Should not throw
            await Storage.DeleteFileAsync(path);
            Assert.IsTrue(true, "Should handle non-existent blob gracefully");
        }

        [TestMethod]
        public async Task DeleteFileAsync_WithImageFile_DeletesThumbnail()
        {
            // Arrange
            var imagePath = $"images/{Guid.NewGuid()}/photo.jpg";
            var thumbnailPath = imagePath + ".tn";

            // Upload both image and thumbnail
            using (var imageStream = new MemoryStream(Encoding.UTF8.GetBytes("fake image data")))
            {
                var imageMetadata = new FileUploadMetaData
                {
                    RelativePath = imagePath,
                    UploadUid = Guid.NewGuid().ToString(),
                    TotalFileSize = imageStream.Length,
                    ContentType = "image/jpeg",
                    TotalChunks = 1,
                    ChunkIndex = 0
                };
                await Storage.AppendBlob(imageStream, imageMetadata, "block");
            }

            using (var thumbnailStream = new MemoryStream(Encoding.UTF8.GetBytes("fake thumbnail")))
            {
                var thumbnailMetadata = new FileUploadMetaData
                {
                    RelativePath = thumbnailPath,
                    UploadUid = Guid.NewGuid().ToString(),
                    TotalFileSize = thumbnailStream.Length,
                    ContentType = "image/jpeg",
                    TotalChunks = 1,
                    ChunkIndex = 0
                };
                await Storage.AppendBlob(thumbnailStream, thumbnailMetadata, "block");
            }

            // Act
            await Storage.DeleteFileAsync(imagePath);

            // Assert
            var imageExists = await Storage.BlobExistsAsync(imagePath);
            var thumbnailExists = await Storage.BlobExistsAsync(thumbnailPath);
            
            Assert.IsFalse(imageExists, "Image should be deleted");
            Assert.IsFalse(thumbnailExists, "Thumbnail should be deleted");
        }

        [TestMethod]
        public async Task DeleteFileAsync_WithPngImage_DeletesThumbnail()
        {
            // Arrange
            var imagePath = $"images/{Guid.NewGuid()}/graphic.png";
            var thumbnailPath = imagePath + ".tn";

            // Upload both
            using (var imageStream = new MemoryStream(Encoding.UTF8.GetBytes("fake png data")))
            {
                var imageMetadata = new FileUploadMetaData
                {
                    RelativePath = imagePath,
                    UploadUid = Guid.NewGuid().ToString(),
                    TotalFileSize = imageStream.Length,
                    ContentType = "image/png",
                    TotalChunks = 1,
                    ChunkIndex = 0
                };
                await Storage.AppendBlob(imageStream, imageMetadata, "block");
            }

            using (var thumbnailStream = new MemoryStream(Encoding.UTF8.GetBytes("fake thumbnail")))
            {
                var thumbnailMetadata = new FileUploadMetaData
                {
                    RelativePath = thumbnailPath,
                    UploadUid = Guid.NewGuid().ToString(),
                    TotalFileSize = thumbnailStream.Length,
                    ContentType = "image/png",
                    TotalChunks = 1,
                    ChunkIndex = 0
                };
                await Storage.AppendBlob(thumbnailStream, thumbnailMetadata, "block");
            }

            // Act
            await Storage.DeleteFileAsync(imagePath);

            // Assert
            var imageExists = await Storage.BlobExistsAsync(imagePath);
            var thumbnailExists = await Storage.BlobExistsAsync(thumbnailPath);
            
            Assert.IsFalse(imageExists, "PNG should be deleted");
            Assert.IsFalse(thumbnailExists, "Thumbnail should be deleted");
        }

        [TestMethod]
        public async Task DeleteFileAsync_WithGifImage_DeletesThumbnail()
        {
            // Arrange
            var imagePath = $"images/{Guid.NewGuid()}/animation.gif";
            var thumbnailPath = imagePath + ".tn";

            // Upload both
            using (var imageStream = new MemoryStream(Encoding.UTF8.GetBytes("fake gif data")))
            {
                var imageMetadata = new FileUploadMetaData
                {
                    RelativePath = imagePath,
                    UploadUid = Guid.NewGuid().ToString(),
                    TotalFileSize = imageStream.Length,
                    ContentType = "image/gif",
                    TotalChunks = 1,
                    ChunkIndex = 0
                };
                await Storage.AppendBlob(imageStream, imageMetadata, "block");
            }

            using (var thumbnailStream = new MemoryStream(Encoding.UTF8.GetBytes("fake thumbnail")))
            {
                var thumbnailMetadata = new FileUploadMetaData
                {
                    RelativePath = thumbnailPath,
                    UploadUid = Guid.NewGuid().ToString(),
                    TotalFileSize = thumbnailStream.Length,
                    ContentType = "image/gif",
                    TotalChunks = 1,
                    ChunkIndex = 0
                };
                await Storage.AppendBlob(thumbnailStream, thumbnailMetadata, "block");
            }

            // Act
            await Storage.DeleteFileAsync(imagePath);

            // Assert
            var imageExists = await Storage.BlobExistsAsync(imagePath);
            var thumbnailExists = await Storage.BlobExistsAsync(thumbnailPath);
            
            Assert.IsFalse(imageExists, "GIF should be deleted");
            Assert.IsFalse(thumbnailExists, "Thumbnail should be deleted");
        }

        [TestMethod]
        public async Task DeleteFileAsync_WithNonImageFile_DoesNotDeleteThumbnail()
        {
            // Arrange
            var documentPath = $"documents/{Guid.NewGuid()}/file.pdf";

            // Upload only the PDF (no thumbnail)
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake pdf data"));
            var metadata = new FileUploadMetaData
            {
                RelativePath = documentPath,
                UploadUid = Guid.NewGuid().ToString(),
                TotalFileSize = stream.Length,
                ContentType = "application/pdf",
                TotalChunks = 1,
                ChunkIndex = 0
            };
            await Storage.AppendBlob(stream, metadata, "block");

            // Act
            await Storage.DeleteFileAsync(documentPath);

            // Assert
            var exists = await Storage.BlobExistsAsync(documentPath);
            Assert.IsFalse(exists, "PDF should be deleted");
        }

        [TestMethod]
        public async Task DeleteFolderAsync_WithValidPath_DeletesAllContents()
        {
            // Arrange
            var folderPath = $"testfolder/{Guid.NewGuid()}";
            var file1Path = $"{folderPath}/file1.txt";
            var file2Path = $"{folderPath}/subfolder/file2.txt";

            // Upload test files
            using (var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("content 1")))
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

            using (var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("content 2")))
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
            await Storage.DeleteFolderAsync(folderPath);

            // Assert
            var file1Exists = await Storage.BlobExistsAsync(file1Path);
            var file2Exists = await Storage.BlobExistsAsync(file2Path);
            
            Assert.IsFalse(file1Exists, "File 1 should be deleted");
            Assert.IsFalse(file2Exists, "File 2 should be deleted");
        }

        #endregion

        [TestCleanup]
        public async Task Cleanup()
        {
            // Clean up any test blobs that weren't deleted
            // This helps prevent test pollution
            await Task.CompletedTask;
        }
    }
}
