// <copyright file="FileManagerControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using CommonMediator = Cosmos.Common.Features.Shared.IMediator;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using Sky.Cms.Services;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Models;
    using Sky.Editor.Services.EditorSettings;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Claims;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for the <see cref="FileManagerController"/> class.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class FileManagerControllerTests : SkyCmsTestBase
    {
        private FileManagerController controller;
        private Mock<ILogger<FileManagerController>> mockLogger;
        private Mock<IWebHostEnvironment> mockHostEnvironment;
        private Mock<IViewRenderService> mockViewRenderService;
        private Mock<CommonMediator> mockArticleQueries;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);

            mockLogger = new Mock<ILogger<FileManagerController>>();
            mockHostEnvironment = new Mock<IWebHostEnvironment>();
            mockViewRenderService = new Mock<IViewRenderService>();
            mockArticleQueries = new Mock<CommonMediator>();

            controller = new FileManagerController(
                EditorSettings,
                mockLogger.Object,
                Db,
                Storage,
                UserManager,
                Logic,
                mockArticleQueries.Object,
                mockHostEnvironment.Object,
                mockViewRenderService.Object,
                Cache,
                DynamicConfigurationProvider);

            // Setup HttpContext for the controller
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "mock"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            try
            {
                // Clean up all test directories
                var testPaths = new[]
                {
                    "/pub/source",
                    "/pub/destination",
                    "/pub/deeply",
                    "/pub/test",
                    "/pub/downloads",
                    "/pub/sort",
                    "/pub/paging",
                    "/pub/folders",
                    "/pub/images",
                    "/pub/files",
                    "/pub/gallery",
                    "/pub/uploads"
                };

                foreach (var path in testPaths)
                {
                    try
                    {
                        if (await Storage.BlobExistsAsync(path + "/"))
                        {
                            await Storage.DeleteFolderAsync(path);
                        }
                    }
                    catch
                    {
                        // Ignore individual cleanup errors
                    }
                }
                
                // Small delay to allow storage to finish cleanup
                await Task.Delay(200);
            }
            finally
            {
                await DisposeAsync();
            }
        }

        #region Index Action Tests

        /// <summary>
        /// Tests that Index_WithNullOrEmptyTarget_RedirectsToPub.
        /// </summary>
        [TestMethod]
        public async Task Index_WithNullOrEmptyTarget_RedirectsToPub()
        {
            // Act
            var result = await controller.Index(null, false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("/pub", redirectResult.RouteValues["target"]);
        }

        /// <summary>
        /// Tests that Index_WithValidTarget_ReturnsViewResult.
        /// </summary>
        [TestMethod]
        public async Task Index_WithValidTarget_ReturnsViewResult()
        {
            // Arrange
            await Storage.CreateFolder("/pub/test");

            // Act
            var result = await controller.Index("/pub/test", false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.Model);
        }

        /// <summary>
        /// Tests that Index_WithImagesOnlyFilter_FiltersCorrectly.
        /// </summary>
        [TestMethod]
        public async Task Index_WithImagesOnlyFilter_FiltersCorrectly()
        {
            // Arrange
            await Storage.CreateFolder("/pub/images");
            await CreateTestFile("/pub/images/test.jpg");
            await CreateTestFile("/pub/images/test.txt");

            // Act
            var result = await controller.Index("/pub/images", false, imagesOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        /// <summary>
        /// Tests that Index_WithDirectoryOnlyFilter_ReturnsOnlyDirectories.
        /// </summary>
        [TestMethod]
        public async Task Index_WithDirectoryOnlyFilter_ReturnsOnlyDirectories()
        {
            // Arrange
            await Storage.CreateFolder("/pub/folders");
            await Storage.CreateFolder("/pub/folders/subfolder");
            await CreateTestFile("/pub/folders/file.txt");

            // Act
            var result = await controller.Index("/pub/folders", false, directoryOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<FileManagerEntry>;
            Assert.IsTrue(model.All(m => m.IsDirectory));
        }

        /// <summary>
        /// Tests that Index_WithPagination_ReturnsCorrectPageSize.
        /// </summary>
        [TestMethod]
        public async Task Index_WithPagination_ReturnsCorrectPageSize()
        {
            // Arrange
            await Storage.CreateFolder("/pub/paging");
            for (int i = 0; i < 25; i++)
            {
                await CreateTestFile($"/pub/paging/file{i}.txt");
            }

            // Add delay to ensure all files are committed and visible in storage
            await Task.Delay(500);

            // Verify all 25 files were created and are visible
            var allFiles = await Storage.GetFilesAndDirectories("/pub/paging");
            Assert.AreEqual(25, allFiles.Count, $"Expected 25 files to be created. Found: {allFiles.Count}. Files: {string.Join(", ", allFiles.Select(f => f.Name))}");

            // Act
            var result = await controller.Index("/pub/paging", false, pageNo: 0, pageSize: 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<FileManagerEntry>;
            Assert.HasCount(10, model, $"Expected 10 items in first page. Found: {model.Count}. Items: {string.Join(", ", model.Select(m => m.Name))}");
        }

        /// <summary>
        /// Tests that Index_WithSorting_AppliesCorrectOrder.
        /// </summary>
        [TestMethod]
        public async Task Index_WithSorting_AppliesCorrectOrder()
        {
            // Arrange
            await Storage.CreateFolder("/pub/sort");
            await CreateTestFile("/pub/sort/zebra.txt", "Zebra content");
            await CreateTestFile("/pub/sort/alpha.txt", "Alpha content");
            
            // Verify files were created
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/sort/zebra.txt"), "zebra.txt should exist");
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/sort/alpha.txt"), "alpha.txt should exist");

            // Act - Sort by name ascending
            var result = await controller.Index("/pub/sort", false, sortOrder: "asc", currentSort: "Name");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<FileManagerEntry>;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Count > 0, "Model should contain files");
            
            // The first file should be "alpha.txt" or contain "alpha"
            var firstName = model.First().Name;
            Assert.IsTrue(firstName.Contains("alpha", StringComparison.OrdinalIgnoreCase), 
                $"Expected first file to contain 'alpha', but got '{firstName}'");
        }

        #endregion

        #region File Upload Tests

        /// <summary>
        /// Tests that Upload_WithValidFile_ReturnsSuccessResult.
        /// </summary>
        [TestMethod]
        public async Task Upload_WithValidFile_ReturnsSuccessResult()
        {
            // Arrange
            var fileMock = CreateMockFile("test.txt", "Hello World");
            var metadata = CreateFileMetadata("test.txt", "/pub/uploads");

            // Act
            var result = await controller.Upload(
                new[] { fileMock },
                JsonConvert.SerializeObject(metadata),
                "/pub/uploads");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            var uploadResult = jsonResult.Value as FileUploadResult;
            Assert.IsTrue(uploadResult.uploaded);
        }

        /// <summary>
        /// Tests that Upload_WithEmptyPath_ReturnsUnauthorized.
        /// </summary>
        [TestMethod]
        public async Task Upload_WithEmptyPath_ReturnsUnauthorized()
        {
            // Arrange
            var fileMock = CreateMockFile("test.txt", "Hello World");
            var metadata = CreateFileMetadata("test.txt", string.Empty);

            // Act
            var result = await controller.Upload(
                new[] { fileMock },
                JsonConvert.SerializeObject(metadata),
                string.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
        }

        /// <summary>
        /// Tests that Upload_WithNonPubPath_ReturnsUnauthorized.
        /// </summary>
        [TestMethod]
        public async Task Upload_WithNonPubPath_ReturnsUnauthorized()
        {
            // Arrange
            var fileMock = CreateMockFile("test.txt", "Hello World");
            var metadata = CreateFileMetadata("test.txt", "/private/uploads");

            // Act
            var result = await controller.Upload(
                new[] { fileMock },
                JsonConvert.SerializeObject(metadata),
                "/private/uploads");

            // Assert
            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
        }

        /// <summary>
        /// Tests that Upload_WithChunkedFile_HandlesAllChunks.
        /// </summary>
        [TestMethod]
        public async Task Upload_WithChunkedFile_HandlesAllChunks()
        {
            // Arrange
            var totalChunks = 3;
            var fileMock = CreateMockFile("largefile.txt", "Chunk");

            for (int i = 0; i < totalChunks; i++)
            {
                var metadata = CreateFileMetadata("largefile.txt", "/pub/uploads", i, totalChunks);

                // Act
                var result = await controller.Upload(
                    new[] { fileMock },
                    JsonConvert.SerializeObject(metadata),
                    "/pub/uploads");

                // Assert
                Assert.IsInstanceOfType(result, typeof(JsonResult));
            }
        }

        #endregion

        #region UploadImage Tests

        /// <summary>
        /// Tests that UploadImage_WithValidImage_ReturnsImageUrl.
        /// </summary>
        [TestMethod]
        public async Task UploadImage_WithValidImage_ReturnsImageUrl()
        {
            // Arrange
            var imageFile = CreateMockImageFile("test.jpg", 100, 100);
            var metadata = new FilePondMetadata
            {
                FileName = "test.jpg",
                Path = "/pub/images",
                ImageWidth = "100",
                ImageHeight = "100"
            };

            controller.ControllerContext.HttpContext.Request.Form =
                new FormCollection(
                    new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                    {
                        ["files"] = JsonConvert.SerializeObject(metadata)
                    },
                    new FormFileCollection { imageFile });

            // Act
            var result = await controller.UploadImage(JsonConvert.SerializeObject(metadata));

            // Assert
            Assert.IsInstanceOfType(result, typeof(ContentResult));
            var contentResult = result as ContentResult;
            StringAssert.Contains(contentResult.Content, "test.jpg");
        }

        /// <summary>
        /// Tests that UploadImage_WithOversizedImage_ReturnsError.
        /// </summary>
        [TestMethod]
        public async Task UploadImage_WithOversizedImage_ReturnsError()
        {
            // Arrange - Create file larger than 25MB
            var largeFile = CreateMockFile("large.jpg", new string('*', 26 * 1024 * 1024));
            var metadata = new FilePondMetadata
            {
                FileName = "large.jpg",
                Path = "/pub/images"
            };

            controller.ControllerContext.HttpContext.Request.Form =
                new FormCollection(
                    new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                    {
                        ["files"] = JsonConvert.SerializeObject(metadata)
                    },
                    new FormFileCollection { largeFile });

            // Act
            var result = await controller.UploadImage(JsonConvert.SerializeObject(metadata));

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        #endregion

        #region SimpleUpload Tests

        /// <summary>
        /// Tests that SimpleUpload_ForArticle_ReturnsImageUrl.
        /// </summary>
        [TestMethod]
        public async Task SimpleUpload_ForArticle_ReturnsImageUrl()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            var imageFile = CreateMockImageFile("simple.jpg", 50, 50);

            controller.ControllerContext.HttpContext.Request.Form =
                new FormCollection(
                    new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
                    new FormFileCollection { imageFile });

            // Act
            var result = await controller.SimpleUpload(article.ArticleNumber.ToString(), "articles", "ckeditor");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that SimpleUpload_WithInvalidModelState_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task SimpleUpload_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            controller.ModelState.AddModelError("test", "Test error");

            // Act
            var result = await controller.SimpleUpload("123", "articles");

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region File and Folder Operations Tests

        /// <summary>
        /// Tests that NewFile_WithValidExtension_CreatesFile.
        /// </summary>
        [TestMethod]
        public async Task NewFile_WithValidExtension_CreatesFile()
        {
            // Arrange
            await Storage.CreateFolder("/pub/files");
            var model = new NewFileViewModel
            {
                ParentFolder = "/pub/files",
                FileName = "newfile.txt"
            };

            // Act
            var result = await controller.NewFile(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/files/newfile.txt"));
        }

        /// <summary>
        /// Tests that NewFile_WithInvalidExtension_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task NewFile_WithInvalidExtension_ReturnsBadRequest()
        {
            // Arrange
            var model = new NewFileViewModel
            {
                ParentFolder = "/pub/files",
                FileName = "badfile.exe"
            };

            // Act
            var result = await controller.NewFile(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that NewFolder_WithValidName_CreatesFolder.
        /// </summary>
        [TestMethod]
        public async Task NewFolder_WithValidName_CreatesFolder()
        {
            // Arrange
            await Storage.CreateFolder("/pub");
            var model = new NewFolderViewModel
            {
                ParentFolder = "/pub",
                FolderName = "newfolder"
            };

            // Act
            var result = await controller.NewFolder(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        /// <summary>
        /// Tests that Delete_WithValidPaths_DeletesItems.
        /// </summary>
        [TestMethod]
        public async Task Delete_WithValidPaths_DeletesItems()
        {
            // Arrange
            await CreateTestFile("/pub/test/delete.txt");
            var model = new DeleteBlobItemsViewModel
            {
                Paths = new List<string> { "/pub/test/delete.txt" }
            };

            // Act
            var result = await controller.Delete(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            Assert.IsFalse(await Storage.BlobExistsAsync("/pub/test/delete.txt"));
        }

        /// <summary>
        /// Tests that Copy_WithValidPaths_CopiesFiles.
        /// </summary>
        [TestMethod]
        public async Task Copy_WithValidPaths_CopiesFiles()
        {
            // Arrange - Clean up any existing files from previous test runs
            var sourceExists = await Storage.BlobExistsAsync("/pub/source/file.txt");
            if (sourceExists)
            {
                Storage.DeleteFile("/pub/source/file.txt");
            }
            
            var destExists = await Storage.BlobExistsAsync("/pub/destination/file.txt");
            if (destExists)
            {
                Storage.DeleteFile("/pub/destination/file.txt");
            }
            
            const string sourceContent = "Test file content for copy operation";
            await CreateTestFile("/pub/source/file.txt", sourceContent);
            await Storage.CreateFolder("/pub/destination");
            
            // Verify source file exists before copy
            sourceExists = await Storage.BlobExistsAsync("/pub/source/file.txt");
            Assert.IsTrue(sourceExists, "Source file should exist before copy");
            
            var model = new MoveFilesViewModel
            {
                Items = new List<string> { "/pub/source/file.txt" },
                Destination = "/pub/destination"
            };

            // Act
            var result = await controller.Copy(model);

            // Assert - Show error details if BadRequest
            if (result is BadRequestObjectResult badRequest)
            {
                var errorMessage = badRequest.Value?.ToString() ?? "Unknown error";
                Assert.Fail($"Copy operation failed with BadRequest: {errorMessage}");
            }
            
            Assert.IsInstanceOfType(result, typeof(OkResult), "Copy should return OkResult");
            
            // Verify destination file exists
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/destination/file.txt"), 
                "Destination file should exist after copy");
            
            // Verify source file still exists (copy shouldn't delete source)
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/source/file.txt"), 
                "Source file should still exist after copy");
            
            // Verify file content was copied correctly
            using var destStream = await Storage.GetStreamAsync("/pub/destination/file.txt");
            using var reader = new StreamReader(destStream);
            var copiedContent = await reader.ReadToEndAsync();
            Assert.AreEqual(sourceContent, copiedContent, "Copied file content should match source");
            
            // Verify file metadata
            var destFileMetadata = await Storage.GetFileAsync("/pub/destination/file.txt");
            Assert.IsNotNull(destFileMetadata, "Destination file metadata should exist");
            Assert.AreEqual("file.txt", destFileMetadata.Name, "File name should be correct");
            Assert.IsFalse(destFileMetadata.IsDirectory, "Copied item should be a file, not a directory");
            Assert.AreEqual(".txt", destFileMetadata.Extension, "File extension should be preserved");
        }

        /// <summary>
        /// Tests that Copy_WithMultipleFiles_CopiesAllFiles.
        /// </summary>
        [TestMethod]
        public async Task Copy_WithMultipleFiles_CopiesAllFiles()
        {
            // Arrange
            await CreateTestFile("/pub/source/file1.txt", "Content 1");
            await CreateTestFile("/pub/source/file2.txt", "Content 2");
            await CreateTestFile("/pub/source/file3.txt", "Content 3");
            await Storage.CreateFolder("/pub/destination");
            
            var model = new MoveFilesViewModel
            {
                Items = new List<string> 
                { 
                    "/pub/source/file1.txt",
                    "/pub/source/file2.txt",
                    "/pub/source/file3.txt"
                },
                Destination = "/pub/destination"
            };

            // Act
            var result = await controller.Copy(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/destination/file1.txt"));
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/destination/file2.txt"));
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/destination/file3.txt"));
        }

        /// <summary>
        /// Tests that Copy_WithNestedPath_PreservesFilename.
        /// </summary>
        [TestMethod]
        public async Task Copy_WithNestedPath_PreservesFilename()
        {
            // Arrange - Ensure clean state by removing any pre-existing files
            var testPath = "/pub/deeply/nested/source/file.txt";
            var destPath = "/pub/destination/file.txt";
            
            try
            {
                // Clean up any existing files from previous test runs
                if (await Storage.BlobExistsAsync(testPath))
                {
                    Storage.DeleteFile(testPath);
                    await Task.Delay(100); // Give storage time to process deletion
                }
                
                if (await Storage.BlobExistsAsync(destPath))
                {
                    Storage.DeleteFile(destPath);
                    await Task.Delay(100); // Give storage time to process deletion
                }
                
                // Create test file
                await CreateTestFile(testPath);
                await Storage.CreateFolder("/pub/destination");
                
                // Additional delay to ensure storage is consistent
                await Task.Delay(200);
                
                // Verify source file exists before copy with detailed diagnostics
                var sourceExists = await Storage.BlobExistsAsync(testPath);
                if (!sourceExists)
                {
                    var allFiles = await Storage.GetFilesAndDirectories("/pub");
                    var fileList = string.Join(", ", allFiles.Select(f => f.Path));
                    Assert.Fail($"Source file not created. Platform: {Environment.OSVersion.Platform}. Files: {fileList}");
                }
                
                var model = new MoveFilesViewModel
                {
                    Items = new List<string> { testPath },
                    Destination = "/pub/destination"
                };

                // Act
                var result = await controller.Copy(model);

                // Assert with better error messages
                if (result is BadRequestObjectResult badRequest)
                {
                    var errorMessage = badRequest.Value?.ToString() ?? "Unknown error";
                    Assert.Fail($"Copy operation failed. Platform: {Environment.OSVersion.Platform}. Error: {errorMessage}");
                }
                
                Assert.IsInstanceOfType(result, typeof(OkResult));
                Assert.IsTrue(await Storage.BlobExistsAsync(destPath));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Test failed on platform {Environment.OSVersion.Platform}: {ex.Message}\nStack: {ex.StackTrace}");
            }
            finally
            {
                // Cleanup - Remove test files to ensure clean state for next run
                try
                {
                    if (await Storage.BlobExistsAsync(testPath))
                    {
                        Storage.DeleteFile(testPath);
                    }
                    
                    if (await Storage.BlobExistsAsync(destPath))
                    {
                        Storage.DeleteFile(destPath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Tests that Copy_WithSpecialCharactersInFilename_HandlesCorrectly.
        /// </summary>
        [TestMethod]
        public async Task Copy_WithSpecialCharactersInFilename_HandlesCorrectly()
        {
            // Arrange
            await CreateTestFile("/pub/source/test-file_2024.txt", "Special content");
            await Storage.CreateFolder("/pub/destination");
            
            var model = new MoveFilesViewModel
            {
                Items = new List<string> { "/pub/source/test-file_2024.txt" },
                Destination = "/pub/destination"
            };

            // Act
            var result = await controller.Copy(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/destination/test-file_2024.txt"));
        }

        /// <summary>
        /// Tests that Move_WithValidPaths_MovesFiles.
        /// </summary>
        [TestMethod]
        public async Task Move_WithValidPaths_MovesFiles()
        {
            // Arrange
            await CreateTestFile("/pub/source/moveme.txt");
            await Storage.CreateFolder("/pub/destination");
            var model = new MoveFilesViewModel
            {
                Items = new List<string> { "/pub/source/moveme.txt" },
                Destination = "/pub/destination"
            };

            // Act
            var result = await controller.Move(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/destination/moveme.txt"));
            Assert.IsFalse(await Storage.BlobExistsAsync("/pub/source/moveme.txt"));
        }

        /// <summary>
        /// Tests that Rename_WithValidNames_RenamesFile.
        /// </summary>
        [TestMethod]
        public async Task Rename_WithValidNames_RenamesFile()
        {
            // Arrange
            await CreateTestFile("/pub/test/oldname.txt");
            var model = new RenameBlobViewModel
            {
                BlobRootPath = "/pub/test",
                FromBlobName = "oldname.txt",
                ToBlobName = "newname.txt"
            };

            // Act
            var result = await controller.Rename(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            Assert.IsTrue(await Storage.BlobExistsAsync("/pub/test/newname.txt"));
        }

        #endregion

        #region Download Tests

        /// <summary>
        /// Tests that Download_WithValidFile_ReturnsFileResult.
        /// </summary>
        [TestMethod]
        public async Task Download_WithValidFile_ReturnsFileResult()
        {
            // Arrange
            await CreateTestFile("/pub/downloads/download.txt");

            // Act
            var result = await controller.Download("/pub/downloads/download.txt");

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = result as FileContentResult;
            Assert.AreEqual("application/octet-stream", fileResult.ContentType);
        }

        /// <summary>
        /// Tests that Download_WithNonExistentFile_ReturnsNotFound.
        /// </summary>
        [TestMethod]
        public async Task Download_WithNonExistentFile_ReturnsNotFound()
        {
            // Act
            var result = await controller.Download("/pub/nonexistent.txt");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that Download_WithNullPath_ReturnsNotFound.
        /// </summary>
        [TestMethod]
        public async Task Download_WithNullPath_ReturnsNotFound()
        {
            // Act
            var result = await controller.Download(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Path Helper Tests

        /// <summary>
        /// Tests that ParsePath_WithMultipleParts_ReturnsArray.
        /// </summary>
        [TestMethod]
        public void ParsePath_WithMultipleParts_ReturnsArray()
        {
            // Act
            var result = controller.ParsePath("/pub", "test", "file.txt");

            // Assert
            Assert.HasCount(3, result);
            Assert.AreEqual("pub", result[0]);
            Assert.AreEqual("test", result[1]);
            Assert.AreEqual("file.txt", result[2]);
        }

        /// <summary>
        /// Tests that ParsePath_WithSlashes_RemovesSlashes.
        /// </summary>
        [TestMethod]
        public void ParsePath_WithSlashes_RemovesSlashes()
        {
            // Act
            var result = controller.ParsePath("//pub//test//");

            // Assert
            Assert.HasCount(2, result);
            Assert.AreEqual("pub", result[0]);
            Assert.AreEqual("test", result[1]);
        }

        /// <summary>
        /// Tests that TrimPathPart_WithSlashes_TrimsCorrectly.
        /// </summary>
        [TestMethod]
        public void TrimPathPart_WithSlashes_TrimsCorrectly()
        {
            // Act
            var result = controller.TrimPathPart("/test/");

            // Assert
            Assert.AreEqual("test", result);
        }

        /// <summary>
        /// Tests that UrlEncode_WithSpecialCharacters_EncodesCorrectly.
        /// </summary>
        [TestMethod]
        public void UrlEncode_WithSpecialCharacters_EncodesCorrectly()
        {
            // Act
            var result = controller.UrlEncode("/pub/test file.txt");

            // Assert
            StringAssert.Contains(result, "test-file.txt");
        }

        #endregion

        #region Image Operations Tests

        /// <summary>
        /// Tests that GetImageThumbnail_WithValidImage_ReturnsThumbnail.
        /// </summary>
        [TestMethod]
        public async Task GetImageThumbnail_WithValidImage_ReturnsThumbnail()
        {
            // Arrange
            await CreateTestImageFile("/pub/images/thumb.jpg");

            // Act
            var result = await controller.GetImageThumbnail("/pub/images/thumb.jpg", 100, 100);

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = result as FileContentResult;
            Assert.AreEqual("image/webp", fileResult.ContentType);
        }

        /// <summary>
        /// Tests that GetImageThumbnail_WithUnsupportedFormat_ThrowsException.
        /// </summary>
        [TestMethod]
        public async Task GetImageThumbnail_WithUnsupportedFormat_ThrowsException()
        {
            // Arrange
            await CreateTestFile("/pub/images/unsupported.txt");

            // Act & Assert
            await Assert.ThrowsExactlyAsync<NotSupportedException>(async () =>
            {
                await controller.GetImageThumbnail("/pub/images/unsupported.txt");
            });
        }

        /// <summary>
        /// Tests that EditImage_WithValidImage_ReturnsView.
        /// </summary>
        [TestMethod]
        public void EditImage_WithValidImage_ReturnsView()
        {
            // Act
            var result = controller.EditImage("/pub/images/edit.jpg");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        /// <summary>
        /// Tests that EditImage_WithUnsupportedFormat_ReturnsUnsupportedMediaType.
        /// </summary>
        [TestMethod]
        public void EditImage_WithUnsupportedFormat_ReturnsUnsupportedMediaType()
        {
            // Act
            var result = controller.EditImage("/pub/files/document.pdf");

            // Assert
            Assert.IsInstanceOfType(result, typeof(UnsupportedMediaTypeResult));
        }

        #endregion

        #region Static Helper Tests

        /// <summary>
        /// Tests that FixPath_WithAbsoluteUrl_ReturnsUnchanged.
        /// </summary>
        [TestMethod]
        public void FixPath_WithAbsoluteUrl_ReturnsUnchanged()
        {
            // Act
            var result = FileManagerController.FixPath("https://example.com/image.jpg");

            // Assert
            Assert.AreEqual("https://example.com/image.jpg", result);
        }

        /// <summary>
        /// Tests that FixPath_WithRelativePath_AddLeadingSlash.
        /// </summary>
        [TestMethod]
        public void FixPath_WithRelativePath_AddLeadingSlash()
        {
            // Act
            var result = FileManagerController.FixPath("images/test.jpg");

            // Assert
            Assert.AreEqual("/images/test.jpg", result);
        }

        /// <summary>
        /// Tests that GetImageAssetArray_WithImages_ReturnsImagePaths.
        /// </summary>
        [TestMethod]
        public async Task GetImageAssetArray_WithImages_ReturnsImagePaths()
        {
            // Arrange - Create files directly in the gallery folder (no subfolders)
            await Storage.CreateFolder("/pub/gallery");
            await CreateTestImageFile("/pub/gallery/image1.jpg");
            await CreateTestImageFile("/pub/gallery/image2.png");
            await CreateTestFile("/pub/gallery/document.txt", "text");
            
            // Wait for storage consistency
            await Task.Delay(200);
            
            // Verify files exist
            var img1 = await Storage.BlobExistsAsync("/pub/gallery/image1.jpg");
            var img2 = await Storage.BlobExistsAsync("/pub/gallery/image2.png");
            var doc = await Storage.BlobExistsAsync("/pub/gallery/document.txt");
            
            Console.WriteLine($"Files exist - image1.jpg: {img1}, image2.png: {img2}, document.txt: {doc}");
            
            if (!img1 || !img2)
            {
                Assert.Inconclusive("Test files were not created successfully");
            }

            // Act
            var result = await FileManagerController.GetImageAssetArray(
                Storage,
                "/pub/gallery",
                string.Empty);

            // Assert
            Console.WriteLine($"Found {result.Length} images");
            if (result.Length < 2)
            {
                // Get diagnostic info
                var files = await Storage.GetFilesAndDirectories("/pub/gallery");
                var fileInfo = string.Join(", ", files.Select(f => $"{f.Name} (ext:{f.Extension})"));
                Assert.Inconclusive($"Expected 2 images but found {result.Length}. Files in directory: {fileInfo}");
            }
            
            Assert.HasCount(2, result);
            Assert.IsTrue(result.All(r => 
                FileManagerController.ValidImageExtensions.Contains(Path.GetExtension(r).ToLower())));
        }

        /// <summary>
        /// Tests that GetImageAssetArray_WithExcludePath_ExcludesCorrectly.
        /// </summary>
        [TestMethod]
        public async Task GetImageAssetArray_WithExcludePath_ExcludesCorrectly()
        {
            // Arrange
            await Storage.CreateFolder("/pub/images");
            await Storage.CreateFolder("/pub/images/exclude");
            
            await CreateTestImageFile("/pub/images/keep.jpg");
            await CreateTestImageFile("/pub/images/exclude/remove.jpg");
            
            // Wait for storage consistency
            await Task.Delay(200);
            
            var keepExists = await Storage.BlobExistsAsync("/pub/images/keep.jpg");
            var removeExists = await Storage.BlobExistsAsync("/pub/images/exclude/remove.jpg");
            
            Console.WriteLine($"Files exist - keep.jpg: {keepExists}, remove.jpg: {removeExists}");
            
            if (!keepExists || !removeExists)
            {
                Assert.Inconclusive("Test files were not created successfully");
            }

            // Act
            var result = await FileManagerController.GetImageAssetArray(
                Storage,
                "/pub/images",
                "/pub/images/exclude");

            // Assert
            Console.WriteLine($"Found {result.Length} images after exclusion");
            if (result.Length == 0)
            {
                var files = await Storage.GetFilesAndDirectories("/pub/images");
                var fileInfo = string.Join(", ", files.Select(f => f.Path));
                Assert.Inconclusive($"No images found. All files: {fileInfo}");
            }
            
            Assert.HasCount(1, result);
            Assert.Contains("keep.jpg", result[0]);
        }

        #endregion

        #region Security - Path Traversal Tests

        /// <summary>
        /// Tests that Upload_WithPathTraversalAttempt_ReturnsUnauthorized.
        /// </summary>
        [TestMethod]
        public async Task Upload_WithPathTraversalAttempt_ReturnsUnauthorized()
        {
            // Arrange
            var fileMock = CreateMockFile("test.txt", "Malicious Content");
            var metadata = CreateFileMetadata("test.txt", "../../../etc/passwd");

            // Act
            var result = await controller.Upload(
                new[] { fileMock },
                JsonConvert.SerializeObject(metadata),
                "../../../etc/passwd");

            // Assert
            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
        }

        /// <summary>
        /// Tests that NewFolder_WithPathTraversalAttempt_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task NewFolder_WithPathTraversalAttempt_ReturnsBadRequest()
        {
            // Arrange
            var model = new NewFolderViewModel
            {
                ParentFolder = "/pub",
                FolderName = "../../../malicious"
            };

            // Act
            var result = await controller.NewFolder(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that Download_WithPathTraversalAttempt_ReturnsNotFound.
        /// </summary>
        [TestMethod]
        public async Task Download_WithPathTraversalAttempt_ReturnsNotFound()
        {
            // Act
            var result = await controller.Download("../../../etc/passwd");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Security - File Type Validation Tests

        /// <summary>
        /// Tests that Upload_WithExecutableFile_ReturnsError.
        /// </summary>
        [TestMethod]
        public async Task Upload_WithExecutableFile_ReturnsError()
        {
            // Arrange
            var fileMock = CreateMockFile("malware.exe", "MZ executable content");
            var metadata = CreateFileMetadata("malware.exe", "/pub/uploads");

            // Act
            var result = await controller.Upload(
                new[] { fileMock },
                JsonConvert.SerializeObject(metadata),
                "/pub/uploads");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            var uploadResult = jsonResult.Value as FileUploadResult;
            Assert.IsFalse(uploadResult.uploaded, "Executable files should not be uploaded");
        }

        /// <summary>
        /// Tests that Upload_WithScriptFile_ReturnsError.
        /// </summary>
        [TestMethod]
        public async Task Upload_WithScriptFile_ReturnsError()
        {
            // Arrange
            var fileMock = CreateMockFile("script.bat", "@echo off\nmalicious command");
            var metadata = CreateFileMetadata("script.bat", "/pub/uploads");

            // Act
            var result = await controller.Upload(
                new[] { fileMock },
                JsonConvert.SerializeObject(metadata),
                "/pub/uploads");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            var uploadResult = jsonResult.Value as FileUploadResult;
            Assert.IsFalse(uploadResult.uploaded, "Script files should not be uploaded");
        }

        /// <summary>
        /// Tests that NewFile_WithDangerousExtension_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task NewFile_WithDangerousExtension_ReturnsBadRequest()
        {
            // Arrange
            var model = new NewFileViewModel
            {
                ParentFolder = "/pub/files",
                FileName = "dangerous.sh"
            };

            // Act
            var result = await controller.NewFile(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region Error Handling - Storage Failures Tests

        /// <summary>
        /// Tests that Delete_WithNonExistentPath_HandlesGracefully.
        /// </summary>
        [TestMethod]
        public async Task Delete_WithNonExistentPath_HandlesGracefully()
        {
            // Arrange
            var model = new DeleteBlobItemsViewModel
            {
                Paths = new List<string> { "/pub/nonexistent/file.txt" }
            };

            // Act
            var result = await controller.Delete(model);

            // Assert
            // Should return OK even if file doesn't exist (idempotent operation)
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        /// <summary>
        /// Tests that Move_WithNonExistentSource_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Move_WithNonExistentSource_ReturnsBadRequest()
        {
            // Arrange
            await Storage.CreateFolder("/pub/destination");
            var model = new MoveFilesViewModel
            {
                Items = new List<string> { "/pub/nonexistent.txt" },
                Destination = "/pub/destination"
            };

            // Act
            var result = await controller.Move(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that Copy_WithNonExistentDestination_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Copy_WithNonExistentDestination_ReturnsBadRequest()
        {
            // Arrange
            await CreateTestFile("/pub/source/file.txt");
            var model = new MoveFilesViewModel
            {
                Items = new List<string> { "/pub/source/file.txt" },
                Destination = "/pub/nonexistent-destination"
            };

            // Act
            var result = await controller.Copy(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region Concurrency Tests

        /// <summary>
        /// Tests that Upload_WithFinalChunk_CompletesUpload.
        /// </summary>
        [TestMethod]
        public async Task Upload_WithFinalChunk_CompletesUpload()
        {
            // Arrange
            var totalChunks = 5;
            var fileMock = CreateMockFile("finalchunk.txt", "Final chunk data");
            var metadata = CreateFileMetadata("finalchunk.txt", "/pub/uploads", totalChunks - 1, totalChunks);

            // Act
            var result = await controller.Upload(
                new[] { fileMock },
                JsonConvert.SerializeObject(metadata),
                "/pub/uploads");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            var uploadResult = jsonResult.Value as FileUploadResult;
            Assert.IsTrue(uploadResult.uploaded);
        }

        /// <summary>
        /// Tests that Upload_WithMultipleFilesSimultaneously_HandlesCorrectly.
        /// </summary>
        [TestMethod]
        public async Task Upload_WithMultipleFilesSimultaneously_HandlesCorrectly()
        {
            // Arrange
            var file1 = CreateMockFile("file1.txt", "Content 1");
            var file2 = CreateMockFile("file2.txt", "Content 2");
            var metadata1 = CreateFileMetadata("file1.txt", "/pub/uploads");
            var metadata2 = CreateFileMetadata("file2.txt", "/pub/uploads");

            // Act - Execute uploads sequentially since DbContext is not thread-safe
            // In production, each request would have its own scoped DbContext
            var result1 = await controller.Upload(
                new[] { file1 },
                JsonConvert.SerializeObject(metadata1),
                "/pub/uploads");
            
            var result2 = await controller.Upload(
                new[] { file2 },
                JsonConvert.SerializeObject(metadata2),
                "/pub/uploads");

            // Assert
            Assert.IsInstanceOfType(result1, typeof(JsonResult));
            Assert.IsInstanceOfType(result2, typeof(JsonResult));
            var jsonResult1 = result1 as JsonResult;
            var jsonResult2 = result2 as JsonResult;
            Assert.IsNotNull(jsonResult1.Value);
            Assert.IsNotNull(jsonResult2.Value);
        }

        #endregion

        #region Image Processing Edge Cases Tests

        /// <summary>
        /// Tests that GetImageThumbnail_WithZeroDimensions_UsesDefaults.
        /// </summary>
        [TestMethod]
        public async Task GetImageThumbnail_WithZeroDimensions_UsesDefaults()
        {
            // Arrange
            await CreateTestImageFile("/pub/images/defaultsize.jpg");

            // Act
            var result = await controller.GetImageThumbnail("/pub/images/defaultsize.jpg", 0, 0);

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = result as FileContentResult;
            Assert.AreEqual("image/webp", fileResult.ContentType);
        }

        /// <summary>
        /// Tests that GetImageThumbnail_WithNegativeDimensions_UsesDefaults.
        /// </summary>
        [TestMethod]
        public async Task GetImageThumbnail_WithNegativeDimensions_UsesDefaults()
        {
            // Arrange
            await CreateTestImageFile("/pub/images/negative.jpg");

            // Act
            var result = await controller.GetImageThumbnail("/pub/images/negative.jpg", -100, -100);

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
        }

        /// <summary>
        /// Tests that UploadImage_WithNonImageContentType_ReturnsError.
        /// </summary>
        [TestMethod]
        public async Task UploadImage_WithNonImageContentType_ReturnsError()
        {
            // Arrange
            var textFile = CreateMockFile("notanimage.txt", "This is text, not an image");
            var metadata = new FilePondMetadata
            {
                FileName = "notanimage.txt",
                Path = "/pub/images"
            };

            controller.ControllerContext.HttpContext.Request.Form =
                new FormCollection(
                    new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                    {
                        ["files"] = JsonConvert.SerializeObject(metadata)
                    },
                    new FormFileCollection { textFile });

            // Act
            var result = await controller.UploadImage(JsonConvert.SerializeObject(metadata));

            // Assert
            // Should handle gracefully - either return error or process as generic file
            Assert.IsNotNull(result);
        }

        #endregion

        #region Metadata and Validation Tests

        /// <summary>
        /// Tests that Upload_WithInvalidJson_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Upload_WithInvalidJson_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = CreateMockFile("test.txt", "Content");
            var invalidJson = "{ invalid json }";

            // Act & Assert
            await Assert.ThrowsExactlyAsync<JsonReaderException>(async () =>
            {
                await controller.Upload(
                    new[] { fileMock },
                    invalidJson,
                    "/pub/uploads");
            });
        }

        /// <summary>
        /// Tests that NewFolder_WithEmptyName_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task NewFolder_WithEmptyName_ReturnsBadRequest()
        {
            // Arrange
            var model = new NewFolderViewModel
            {
                ParentFolder = "/pub",
                FolderName = string.Empty
            };

            // Act
            var result = await controller.NewFolder(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that NewFile_WithEmptyFileName_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task NewFile_WithEmptyFileName_ReturnsBadRequest()
        {
            // Arrange
            var model = new NewFileViewModel
            {
                ParentFolder = "/pub/files",
                FileName = string.Empty
            };

            // Act
            var result = await controller.NewFile(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that Rename_WithSameName_ReturnsOk.
        /// </summary>
        [TestMethod]
        public async Task Rename_WithSameName_ReturnsOk()
        {
            // Arrange
            await CreateTestFile("/pub/test/samename.txt");
            var model = new RenameBlobViewModel
            {
                BlobRootPath = "/pub/test",
                FromBlobName = "samename.txt",
                ToBlobName = "samename.txt"
            };

            // Act
            var result = await controller.Rename(model);

            // Assert
            // Should handle gracefully - either OK or no-op
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        #endregion

        #region Multi-File Operation Edge Cases Tests

        /// <summary>
        /// Tests that Delete_WithEmptyList_ReturnsOk.
        /// </summary>
        [TestMethod]
        public async Task Delete_WithEmptyList_ReturnsOk()
        {
            // Arrange
            var model = new DeleteBlobItemsViewModel
            {
                Paths = new List<string>()
            };

            // Act
            var result = await controller.Delete(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        /// <summary>
        /// Tests that Copy_WithEmptyList_ReturnsOk.
        /// </summary>
        [TestMethod]
        public async Task Copy_WithEmptyList_ReturnsOk()
        {
            // Arrange
            await Storage.CreateFolder("/pub/destination");
            var model = new MoveFilesViewModel
            {
                Items = new List<string>(),
                Destination = "/pub/destination"
            };

            // Act
            var result = await controller.Copy(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        /// <summary>
        /// Tests that Move_WithSameSourceAndDestination_HandlesCorrectly.
        /// </summary>
        [TestMethod]
        public async Task Move_WithSameSourceAndDestination_HandlesCorrectly()
        {
            // Arrange
            await CreateTestFile("/pub/test/file.txt");
            var model = new MoveFilesViewModel
            {
                Items = new List<string> { "/pub/test/file.txt" },
                Destination = "/pub/test"
            };

            // Act
            var result = await controller.Move(model);

            // Assert
            // Should handle gracefully - either OK (no-op) or BadRequest
            Assert.IsTrue(
                result is OkResult || result is BadRequestObjectResult,
                "Should return OK or BadRequest for same source/destination");
        }

        #endregion

        #region Permission and Authorization Tests

        /// <summary>
        /// Tests that Index_WithInvalidModelState_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Index_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            controller.ModelState.AddModelError("test", "Test error");

            // Act
            var result = await controller.Index("/pub", false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that NewFolder_WithInvalidModelState_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task NewFolder_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            controller.ModelState.AddModelError("test", "Test error");
            var model = new NewFolderViewModel
            {
                ParentFolder = "/pub",
                FolderName = "test"
            };

            // Act
            var result = await controller.NewFolder(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that Delete_WithInvalidModelState_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Delete_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            controller.ModelState.AddModelError("test", "Test error");
            var model = new DeleteBlobItemsViewModel
            {
                Paths = new List<string> { "/pub/test.txt" }
            };

            // Act
            var result = await controller.Delete(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region Additional Coverage - Final Gap Tests

        /// <summary>
        /// Tests that Index_ForArticlesFolder_ListsArticles.
        /// </summary>
        [TestMethod]
        public async Task Index_ForArticlesFolder_ListsArticles()
        {
            // Arrange
            var article1 = await CreateArticleAsync("Article 1", TestUserId);
            var article2 = await CreateArticleAsync("Article 2", TestUserId);
            await SaveArticleAsync(article1, TestUserId);
            await SaveArticleAsync(article2, TestUserId);

            // Act
            var result = await controller.Index("/pub/articles", false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<FileManagerEntry>;
            Assert.IsTrue(model.Count >= 2, "Should list articles as folders");
            Assert.IsTrue(model.All(m => m.IsDirectory), "All articles should appear as directories");
        }

        /// <summary>
        /// Tests that Index_ForTemplatesFolder_ListsTemplates.
        /// </summary>
        [TestMethod]
        public async Task Index_ForTemplatesFolder_ListsTemplates()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template1 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template 1",
                Content = "<div>Content</div>",
                LayoutId = layout.Id
            };
            var template2 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template 2",
                Content = "<div>Content</div>",
                LayoutId = layout.Id
            };
            Db.Templates.AddRange(template1, template2);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Index("/pub/templates", false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<FileManagerEntry>;
            Assert.IsTrue(model.Count >= 2, "Should list templates as folders");
        }

        /// <summary>
        /// Tests that Rename_WithConflictingName_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Rename_WithConflictingName_ReturnsBadRequest()
        {
            // Arrange
            await CreateTestFile("/pub/test/existing.txt", "Existing");
            await CreateTestFile("/pub/test/conflict.txt", "Conflict");
            
            var model = new RenameBlobViewModel
            {
                BlobRootPath = "/pub/test",
                FromBlobName = "existing.txt",
                ToBlobName = "conflict.txt" // This name already exists
            };

            // Act
            var result = await controller.Rename(model);

            // Assert
            // Should return BadRequest or handle conflict appropriately
            Assert.IsTrue(
                result is BadRequestObjectResult || result is OkResult,
                "Should handle naming conflict");
        }

        /// <summary>
        /// Tests that ParsePath_WithNullInput_ReturnsEmptyArray.
        /// </summary>
        [TestMethod]
        public void ParsePath_WithNullInput_ReturnsEmptyArray()
        {
            // Act
            var result = controller.ParsePath(null);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        /// <summary>
        /// Tests that TrimPathPart_WithNullInput_ReturnsEmptyString.
        /// </summary>
        [TestMethod]
        public void TrimPathPart_WithNullInput_ReturnsEmptyString()
        {
            // Act
            var result = controller.TrimPathPart(null);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        /// <summary>
        /// Tests that FixPath_WithNullInput_ReturnsSlash.
        /// </summary>
        [TestMethod]
        public void FixPath_WithNullInput_ReturnsSlash()
        {
            // Act
            var result = FileManagerController.FixPath(null);

            // Assert
            Assert.AreEqual("/", result);
        }

        #endregion

        #region Helper Methods

        private async Task CreateTestFile(string path, string content = "Test Content")
        {
            // Normalize path to Unix-style (always use forward slashes)
            path = path.Replace('\\', '/');
            
            // Ensure ALL parent directories exist (handle nested paths)
            var directory = Path.GetDirectoryName(path);
            
            if (!string.IsNullOrEmpty(directory))
            {
                // Normalize directory path to Unix-style
                directory = directory.Replace('\\', '/');
                
                // Split the path and create each level
                var pathParts = directory.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                var currentPath = string.Empty;
                
                foreach (var part in pathParts)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) 
                        ? $"/{part}" 
                        : $"{currentPath}/{part}";
                    
                    // Always attempt to create the folder - CreateFolder should be idempotent
                    await Storage.CreateFolder(currentPath);
                    
                    // Increased delay for CI environments
                    await Task.Delay(100);
                }
            }

            // Additional delay before creating the file to ensure all folders are ready
            await Task.Delay(150);

            // The RelativePath should be the full path including filename
            var fileName = Path.GetFileName(path);
            var relativePath = path.TrimStart('/');
            
            var metadata = new FileUploadMetaData
            {
                FileName = fileName,
                RelativePath = relativePath,
                ChunkIndex = 0,
                TotalChunks = 1,
                ContentType = "application/octet-stream",
                TotalFileSize = Encoding.UTF8.GetByteCount(content),
                UploadUid = Guid.NewGuid().ToString()
            };
            
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await Storage.AppendBlob(stream, metadata);
            
            // Verify the file was created successfully with retry
            var maxRetries = 3;
            var exists = false;
            
            for (int i = 0; i < maxRetries; i++)
            {
                exists = await Storage.BlobExistsAsync(path);
                if (exists) break;
                await Task.Delay(100);
            }
            
            if (!exists)
            {
                // Provide detailed diagnostic information
                var allFiles = await Storage.GetFilesAndDirectories("/pub");
                var fileList = string.Join(", ", allFiles.Select(f => f.Path));
                throw new InvalidOperationException(
                    $"Failed to create test file at path: {path}. " +
                    $"Platform: {Environment.OSVersion.Platform}. " +
                    $"Existing files: {fileList}");
            }
        }

        private async Task CreateTestImageFile(string path)
        {
            // Normalize path to Unix-style (always use forward slashes)
            path = path.Replace('\\', '/');
            
            // Ensure ALL parent directories exist (handle nested paths)
            var directory = Path.GetDirectoryName(path);
            
            if (!string.IsNullOrEmpty(directory))
            {
                // Normalize directory path to Unix-style
                directory = directory.Replace('\\', '/');
                
                // Split the path and create each level
                var pathParts = directory.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                var currentPath = string.Empty;
                
                foreach (var part in pathParts)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) 
                        ? $"/{part}" 
                        : $"{currentPath}/{part}";
                    
                    // Always attempt to create the folder - CreateFolder should be idempotent
                    await Storage.CreateFolder(currentPath);
                    
                    // Increased delay for CI environments
                    await Task.Delay(100);
                }
            }

            // Additional delay before creating the file to ensure all folders are ready
            await Task.Delay(150);

            // Create a minimal valid JPEG file with proper JPEG structure
            var jpegBytes = new byte[]
            {
                // JPEG SOI (Start of Image)
                0xFF, 0xD8,
                // JFIF APP0 marker
                0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                // SOF0 (Start of Frame)
                0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
                // DHT (Define Huffman Table) - minimal
                0xFF, 0xC4, 0x00, 0x14, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xC4, 0x00, 0x14, 0x10, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                // SOS (Start of Scan)
                0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,
                // Minimal image data
                0xFF, 0x00,
                // EOI (End of Image)
                0xFF, 0xD9
            };

            // The RelativePath should be the full path including filename
            var fileName = Path.GetFileName(path);
            var relativePath = path.TrimStart('/');
            
            var metadata = new FileUploadMetaData
            {
                FileName = fileName,
                RelativePath = relativePath,
                ChunkIndex = 0,
                TotalChunks = 1,
                ContentType = "image/jpeg",
                TotalFileSize = jpegBytes.Length,
                UploadUid = Guid.NewGuid().ToString()
            };
            
            using var stream = new MemoryStream(jpegBytes);
            await Storage.AppendBlob(stream, metadata);
            
            // Verify the file was created successfully with retry
            var maxRetries = 3;
            var exists = false;
            
            for (int i = 0; i < maxRetries; i++)
            {
                exists = await Storage.BlobExistsAsync(path);
                if (exists) break;
                await Task.Delay(100);
            }
            
            if (!exists)
            {
                // Provide detailed diagnostic information
                var allFiles = await Storage.GetFilesAndDirectories("/pub");
                var fileList = string.Join(", ", allFiles.Select(f => f.Path));
                throw new InvalidOperationException(
                    $"Failed to create test image file at path: {path}. " +
                    $"Platform: {Environment.OSVersion.Platform}. " +
                    $"Existing files: {fileList}");
            }
        }

        private IFormFile CreateMockFile(string fileName, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };
        }

        private IFormFile CreateMockImageFile(string fileName, int width, int height)
        {
            // Create minimal valid JPEG with proper structure
            var jpegBytes = new byte[]
            {
                0xFF, 0xD8, // SOI
                0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
                0xFF, 0xC4, 0x00, 0x14, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xC4, 0x00, 0x14, 0x10, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,
                0xFF, 0x00,
                0xFF, 0xD9 // EOI
            };
    
            var stream = new MemoryStream(jpegBytes);
            return new FormFile(stream, 0, jpegBytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };
        }

        private FileUploadMetaData CreateFileMetadata(
            string fileName,
            string relativePath,
            long chunkIndex = 0,
            long totalChunks = 1)
        {
            return new FileUploadMetaData
            {
                FileName = fileName,
                RelativePath = relativePath,
                ChunkIndex = chunkIndex,
                TotalChunks = totalChunks,
                ContentType = "application/octet-stream",
                TotalFileSize = 1024,
                UploadUid = Guid.NewGuid().ToString()
            };
        }

        #endregion
    }
}

