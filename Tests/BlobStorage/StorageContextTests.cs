// <copyright file="StorageContextTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.BlobStorage
{
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using static StorageContextConfigUtilities;

    /// <summary>
    /// Integration tests for the <see cref="StorageContext"/> class.
    /// Tests file and folder operations across multiple cloud storage providers (Azure, Amazon S3, Cloudflare R2).
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class StorageContextTests
    {
        private StorageContext storageContext;
        private string testImagePath;
        private string testFolderPrefix;
        private StorageProvider currentProvider;

        /// <summary>
        /// Sets up test dependencies for a specific provider.
        /// Creates unique test folder prefix and test image file.
        /// </summary>
        /// <param name="provider">The storage provider to test.</param>
        private void SetupForProvider(StorageProvider provider)
        {
            currentProvider = provider;
            storageContext = StorageContextConfigUtilities.GetStorageContext(provider);

            // Use unique prefix for each test with provider name to avoid cross-provider conflicts
            testFolderPrefix = $"/test-{provider.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}";

            // Create a temporary test image file
            testImagePath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.jpg");
            CreateTestImageFile(testImagePath);
        }

        /// <summary>
        /// Cleans up test resources after each test.
        /// Deletes test folders and temporary files.
        /// </summary>
        private async Task CleanupForProvider()
        {
            // Clean up test folders created during the test
            try
            {
                if (storageContext != null)
                {
                    await storageContext.DeleteFolderAsync(testFolderPrefix);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            // Delete temporary test file
            if (File.Exists(testImagePath))
            {
                File.Delete(testImagePath);
            }
        }

        #region Folder Operations Tests

        /// <summary>
        /// Tests that creating a folder successfully adds it to storage across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task CreateFolder_WithValidPath_CreatesFolder(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folderPath = $"{testFolderPrefix}/new-folder";

            try
            {
                // Act
                var result = await storageContext.CreateFolder(folderPath);

                // Assert
                Assert.IsNotNull(result, $"[{provider}] Folder creation should return metadata");
                Assert.AreEqual("new-folder", result.Name, $"[{provider}] Folder name should match");
                Assert.IsTrue(result.IsDirectory, $"[{provider}] Should be marked as directory");

                var folders = await storageContext.GetFilesAndDirectories(testFolderPrefix);
                Assert.AreEqual(1, folders.Count, $"[{provider}] Should have exactly one folder");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        /// <summary>
        /// Tests nested folder creation and listing behavior across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task NestedFolders_CreateAndList_ReturnsExpectedStructure(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder1 = $"{testFolderPrefix}/folder1";
            var subfolder1 = $"{folder1}/subfolder1";
            var subfolder2 = $"{folder1}/subfolder2";
            var subfolder3 = $"{subfolder2}/subfolder3";

            try
            {
                await storageContext.CreateFolder(folder1);
                await storageContext.CreateFolder(subfolder1);
                await storageContext.CreateFolder(subfolder2);
                await storageContext.CreateFolder(subfolder3);

                // Act
                var folder1Contents = await storageContext.GetFilesAndDirectories(folder1);
                var subfolder1Contents = await storageContext.GetFilesAndDirectories(subfolder1);
                var subfolder2Contents = await storageContext.GetFilesAndDirectories(subfolder2);
                var subfolder3Contents = await storageContext.GetFilesAndDirectories(subfolder3);

                // Assert
                const int ExpectedSubfoldersInFolder1 = 2;
                const int ExpectedItemsInLeafFolders = 0;
                Assert.AreEqual(ExpectedSubfoldersInFolder1, folder1Contents.Count, $"[{provider}] folder1 should have 2 subfolders");
                Assert.AreEqual(ExpectedItemsInLeafFolders, subfolder1Contents.Count, $"[{provider}] subfolder1 should be empty");
                Assert.AreEqual(1, subfolder2Contents.Count, $"[{provider}] subfolder2 should have 1 subfolder");
                Assert.AreEqual(ExpectedItemsInLeafFolders, subfolder3Contents.Count, $"[{provider}] subfolder3 should be empty");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        /// <summary>
        /// Tests that moving a folder relocates all contents and removes the source across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task MoveFolder_WithContents_RelocatesAllItems(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var sourceFolder = $"{testFolderPrefix}/source-folder";
            var targetFolder = $"{testFolderPrefix}/target-folder";

            try
            {
                await storageContext.CreateFolder(sourceFolder);
                await UploadTestFile(sourceFolder, "test-file.jpg");

                // Act
                await storageContext.MoveFolderAsync(sourceFolder, targetFolder);

                // Assert
                var targetContents = await storageContext.GetFilesAndDirectories(targetFolder);
                var sourceContents = await storageContext.GetFilesAndDirectories(sourceFolder);

                Assert.AreEqual(1, targetContents.Count, $"[{provider}] Target folder should contain the moved file");
                Assert.AreEqual(0, sourceContents.Count, $"[{provider}] Source folder should be empty after move");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        /// <summary>
        /// Tests that DeleteFolderAsync removes all user files (excluding folder markers) across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task DeleteFolder_WithContents_RemovesAllUserFiles(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder = $"{testFolderPrefix}/delete-test";

            try
            {
                await storageContext.CreateFolder(folder);
                await UploadTestFile(folder, "file1.jpg");
                await UploadTestFile(folder, "file2.jpg");

                // Get initial file list using GetFilesAndDirectories (not GetFilesAsync)
                var filesBeforeDeletion = await storageContext.GetFilesAndDirectories(folder);
                var userFilesBeforeDeletion = filesBeforeDeletion
                    .Where(f => !f.IsDirectory && !f.Name.EndsWith("folder.stubxx", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Assert.AreEqual(2, userFilesBeforeDeletion.Count, $"[{provider}] Should have 2 user files before deletion");

                // Act
                await storageContext.DeleteFolderAsync(folder);

                // Assert - Verify files are deleted using GetFilesAndDirectories
                var filesAfterDeletion = await storageContext.GetFilesAndDirectories(folder);
                var userFilesAfterDeletion = filesAfterDeletion
                    .Where(f => !f.IsDirectory && !f.Name.EndsWith("folder.stubxx", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Assert.AreEqual(0, userFilesAfterDeletion.Count, $"[{provider}] All user files should be deleted after folder deletion");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        #endregion

        #region File Upload Tests

        /// <summary>
        /// Tests that AppendBlob successfully uploads a file across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task AppendBlob_WithValidMetadata_UploadsFileSuccessfully(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder = $"{testFolderPrefix}/upload-test";

            try
            {
                await storageContext.CreateFolder(folder);

                await using var memStream = new MemoryStream();
                await using var fileStream = File.OpenRead(testImagePath);
                await fileStream.CopyToAsync(memStream);
                memStream.Position = 0;

                var fileName = "uploaded-file.jpg";
                var filePath = $"{folder}/{fileName}";

                var fileUploadMetadata = new FileUploadMetaData
                {
                    UploadUid = Guid.NewGuid().ToString(),
                    FileName = fileName,
                    RelativePath = filePath.TrimStart('/'),
                    ContentType = "image/jpeg",
                    ChunkIndex = 0,
                    TotalChunks = 1,
                    TotalFileSize = memStream.Length
                };

                // Act
                await storageContext.AppendBlob(memStream, fileUploadMetadata);

                // Assert
                var uploadedFile = await storageContext.GetFileAsync(fileUploadMetadata.RelativePath);
                Assert.IsNotNull(uploadedFile, $"[{provider}] Uploaded file should exist");

                using var downloadedStream = await storageContext.GetStreamAsync(fileUploadMetadata.RelativePath);
                Assert.AreEqual(memStream.Length, downloadedStream.Length, $"[{provider}] Downloaded file should match uploaded size");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        /// <summary>
        /// Tests uploading multiple files to nested folders across all providers.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task AppendBlob_MultipleFilesInNestedFolders_UploadsAllSuccessfully(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder1 = $"{testFolderPrefix}/multi-folder1";
            var folder2 = $"{testFolderPrefix}/multi-folder1/subfolder";

            try
            {
                await storageContext.CreateFolder(folder1);
                await storageContext.CreateFolder(folder2);

                await using var memStream = new MemoryStream();
                await using var fileStream = File.OpenRead(testImagePath);
                await fileStream.CopyToAsync(memStream);
                memStream.Position = 0;

                const int FilesInFolder1 = 5;
                const int FilesInFolder2 = 9;

                // Act - Upload files to folder1
                for (var i = 0; i < FilesInFolder1; i++)
                {
                    memStream.Position = 0;
                    await UploadTestFileWithStream(folder1, $"file{i}.jpg", memStream);
                }

                // Act - Upload files to folder2
                for (var i = 0; i < FilesInFolder2; i++)
                {
                    memStream.Position = 0;
                    await UploadTestFileWithStream(folder2, $"file{i}.jpg", memStream);
                }

                // Assert
                var folder1Contents = await storageContext.GetFilesAndDirectories(folder1);
                var folder2Contents = await storageContext.GetFilesAndDirectories(folder2);

                const int ExpectedFolder1Items = FilesInFolder1 + 1; // 5 files + 1 subfolder
                Assert.AreEqual(ExpectedFolder1Items, folder1Contents.Count,
                    $"[{provider}] folder1 should contain {FilesInFolder1} files and 1 subfolder");
                Assert.AreEqual(FilesInFolder2, folder2Contents.Count,
                    $"[{provider}] folder2 should contain {FilesInFolder2} files");

                // Verify all entries have proper metadata
                foreach (var entry in folder1Contents.Concat(folder2Contents))
                {
                    Assert.IsNotNull(entry.Name, $"[{provider}] Entry name should not be null");
                    Assert.IsNotNull(entry.Path, $"[{provider}] Entry path should not be null");
                }
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        #endregion

        #region File Copy and Move Tests

        /// <summary>
        /// Tests that CopyAsync creates a duplicate file at the destination across all providers.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task CopyAsync_WithValidPaths_CreatesFileCopy(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder = $"{testFolderPrefix}/copy-test";

            try
            {
                await storageContext.CreateFolder(folder);
                await UploadTestFile(folder, "source.jpg");
                var sourcePath = $"{folder}/source.jpg";
                var destPath = $"{folder}/destination.jpg";

                // Act
                var sourceMetadata = await storageContext.GetFileAsync(sourcePath);
                await storageContext.CopyAsync(sourcePath, destPath);
                var destMetadata = await storageContext.GetFileAsync(destPath);

                // Assert
                Assert.IsNotNull(sourceMetadata, $"[{provider}] Source file should exist");
                Assert.IsNotNull(destMetadata, $"[{provider}] Destination file should exist");
                Assert.AreNotEqual(sourceMetadata.Path, destMetadata.Path, $"[{provider}] Source and destination paths should differ");
                Assert.AreEqual(sourceMetadata.Size, destMetadata.Size, $"[{provider}] File sizes should match");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        /// <summary>
        /// Tests that MoveFileAsync relocates a file and removes the source across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task MoveFileAsync_WithValidPaths_RelocatesFile(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder = $"{testFolderPrefix}/move-test";

            try
            {
                await storageContext.CreateFolder(folder);

                await using var memStream = new MemoryStream();
                await using var fileStream = File.OpenRead(testImagePath);
                await fileStream.CopyToAsync(memStream);
                memStream.Position = 0;

                var sourcePath = $"{folder}/source.jpg";
                var destPath = $"{folder}/moved.jpg";

                var fileUploadMetadata = new FileUploadMetaData
                {
                    UploadUid = Guid.NewGuid().ToString(),
                    FileName = "source.jpg",
                    RelativePath = sourcePath.TrimStart('/'),
                    ContentType = "image/jpeg",
                    ChunkIndex = 0,
                    TotalChunks = 1,
                    TotalFileSize = memStream.Length
                };

                await storageContext.AppendBlob(memStream, fileUploadMetadata);

                // Act
                Assert.IsTrue(await storageContext.BlobExistsAsync(sourcePath), $"[{provider}] Source file should exist before move");

                await storageContext.MoveFileAsync(sourcePath, destPath);

                // Assert
                Assert.IsFalse(await storageContext.BlobExistsAsync(sourcePath), $"[{provider}] Source file should not exist after move");
                Assert.IsTrue(await storageContext.BlobExistsAsync(destPath), $"[{provider}] Destination file should exist after move");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        #endregion

        #region File Retrieval Tests

        /// <summary>
        /// Tests that GetFilesAndDirectories returns files and folders from root across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task GetFilesAndDirectories_FromRoot_ReturnsContent(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);

            try
            {
                await storageContext.CreateFolder(testFolderPrefix);

                // Act
                var rootContents = await storageContext.GetFilesAndDirectories("/");

                // Assert
                Assert.IsNotNull(rootContents, $"[{provider}] Root listing should not be null");
                Assert.IsTrue(rootContents.Count > 0, $"[{provider}] Root should contain at least the test folder");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        /// <summary>
        /// Tests that existence and metadata retrieval APIs correctly handle existing and non-existing files across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task FileExistenceApis_ChecksExistenceAndMissingMetadata_ReturnsCorrectResult(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder = $"{testFolderPrefix}/exists-test";

            try
            {
                await storageContext.CreateFolder(folder);
                await UploadTestFile(folder, "exists.jpg");
                var existingPath = $"{folder}/exists.jpg";
                var nonExistentPath = $"{folder}/does-not-exist.jpg";

                // Act
                var existsResult = await storageContext.BlobExistsAsync(existingPath);
                var notExistsResult = await storageContext.BlobExistsAsync(nonExistentPath);
                var missingFileMetadata = await storageContext.GetFileAsync(nonExistentPath);

                // Assert
                Assert.IsTrue(existsResult, $"[{provider}] Existing file should return true");
                Assert.IsFalse(notExistsResult, $"[{provider}] Non-existent file should return false");
                Assert.IsNull(missingFileMetadata, $"[{provider}] Non-existent file metadata should be null");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        /// <summary>
        /// Tests that GetStreamAsync returns readable stream for existing file across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task GetStreamAsync_ExistingFile_ReturnsReadableStream(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder = $"{testFolderPrefix}/stream-test";

            try
            {
                await storageContext.CreateFolder(folder);

                await using var originalStream = new MemoryStream();
                await using (var fileStream = File.OpenRead(testImagePath))
                {
                    await fileStream.CopyToAsync(originalStream);
                }
                originalStream.Position = 0;

                var filePath = $"{folder}/stream-test.jpg";
                await UploadTestFileWithStream(folder, "stream-test.jpg", originalStream);

                // Act
                using var downloadedStream = await storageContext.GetStreamAsync(filePath);

                // Assert
                Assert.IsNotNull(downloadedStream, $"[{provider}] Stream should not be null");
                Assert.IsTrue(downloadedStream.CanRead, $"[{provider}] Stream should be readable");
                Assert.AreEqual(originalStream.Length, downloadedStream.Length, $"[{provider}] Stream length should match original");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        /// <summary>
        /// Tests that GetFilesAsync returns all file paths including subfolders across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task GetFilesAsync_WithSubfolders_ReturnsAllFilePaths(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder = $"{testFolderPrefix}/files-test";
            var subfolder = $"{folder}/subfolder";

            try
            {
                await storageContext.CreateFolder(folder);
                await storageContext.CreateFolder(subfolder);

                await UploadTestFile(folder, "file1.jpg");
                await UploadTestFile(subfolder, "file2.jpg");

                // Act
                var allFiles = await storageContext.GetFilesAsync(folder);

                // Assert
                const int ExpectedFileCount = 2; // file1.jpg and file2.jpg
                Assert.AreEqual(ExpectedFileCount, allFiles.Count, $"[{provider}] Should return all files including those in subfolders");
                Assert.IsTrue(allFiles.Any(f => f.Contains("file1.jpg")), $"[{provider}] Should include file1.jpg");
                Assert.IsTrue(allFiles.Any(f => f.Contains("file2.jpg")), $"[{provider}] Should include file2.jpg");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        #endregion

        #region Edge Cases and Error Handling Tests

        /// <summary>
        /// Tests that GetFileAsync handles paths with leading slashes correctly across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task GetFileAsync_WithLeadingSlash_HandlesPathCorrectly(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder = $"{testFolderPrefix}/slash-test";

            try
            {
                await storageContext.CreateFolder(folder);
                await UploadTestFile(folder, "test.jpg");

                var pathWithSlash = $"/{folder.TrimStart('/')}/test.jpg";
                var pathWithoutSlash = $"{folder.TrimStart('/')}/test.jpg";

                // Act
                var resultWithSlash = await storageContext.GetFileAsync(pathWithSlash);
                var resultWithoutSlash = await storageContext.GetFileAsync(pathWithoutSlash);

                // Assert
                Assert.IsNotNull(resultWithSlash, $"[{provider}] Should handle path with leading slash");
                Assert.IsNotNull(resultWithoutSlash, $"[{provider}] Should handle path without leading slash");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        /// <summary>
        /// Tests that deleting an empty folder works correctly across all providers.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetTestProviders), typeof(StorageContextConfigUtilities), DynamicDataSourceType.Method)]
        public async Task DeleteFolder_EmptyFolder_DeletesSuccessfully(StorageProvider provider)
        {
            // Arrange
            SetupForProvider(provider);
            var folder = $"{testFolderPrefix}/empty-folder";

            try
            {
                await storageContext.CreateFolder(folder);

                // Act
                await storageContext.DeleteFolderAsync(folder);

                // Assert
                var contents = await storageContext.GetFilesAndDirectories(folder);
                Assert.AreEqual(0, contents.Count, $"[{provider}] Deleted folder should be empty");
            }
            finally
            {
                await CleanupForProvider();
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a test image file with minimal valid JPEG data.
        /// </summary>
        /// <param name="path">Path where to create the file.</param>
        private void CreateTestImageFile(string path)
        {
            // Create a minimal valid JPEG file (1x1 pixel)
            byte[] jpegData =
            [
                0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46,
                0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48,
                0x00, 0x48, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
                0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01,
                0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4,
                0x00, 0x14, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x03, 0xFF, 0xC4, 0x00, 0x14,
                0x10, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01,
                0x00, 0x00, 0x3F, 0x00, 0x37, 0xFF, 0xD9
            ];

            File.WriteAllBytes(path, jpegData);
        }

        /// <summary>
        /// Uploads a test file to the specified folder.
        /// </summary>
        /// <param name="folder">Folder path.</param>
        /// <param name="fileName">File name.</param>
        /// <returns>Uploaded file path.</returns>
        private async Task<string> UploadTestFile(string folder, string fileName)
        {
            await using var memStream = new MemoryStream();
            await using (var fileStream = File.OpenRead(testImagePath))
            {
                await fileStream.CopyToAsync(memStream);
            }
            memStream.Position = 0;

            return await UploadTestFileWithStream(folder, fileName, memStream);
        }

        /// <summary>
        /// Uploads a test file using the provided stream.
        /// </summary>
        /// <param name="folder">Folder path.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="stream">Stream containing file data.</param>
        /// <returns>Uploaded file path.</returns>
        private async Task<string> UploadTestFileWithStream(string folder, string fileName, MemoryStream stream)
        {
            var filePath = $"{folder}/{fileName}";

            var fileUploadMetadata = new FileUploadMetaData
            {
                UploadUid = Guid.NewGuid().ToString(),
                FileName = fileName,
                RelativePath = filePath.TrimStart('/'),
                ContentType = "image/jpeg",
                ChunkIndex = 0,
                TotalChunks = 1,
                TotalFileSize = stream.Length
            };

            await storageContext.AppendBlob(stream, fileUploadMetadata);
            return filePath;
        }

        #endregion
    }
}
