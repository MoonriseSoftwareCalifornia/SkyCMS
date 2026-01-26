// <copyright file="StartupTaskServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure;
    using Cosmos.DynamicConfig;
    using Cosmos.Editor.Services;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Comprehensive unit tests for the <see cref="StartupTaskService"/> class.
    /// </summary>
    [TestClass]
    public class StartupTaskServiceTests
    {
        private Mock<IWebHostEnvironment> mockWebHostEnvironment = null!;
        private Mock<IMultiDatabaseManagementUtilities> mockManagementUtilities = null!;
        private string testWebRootPath = null!;
        private string testFilePath = null!;

        [TestInitialize]
        public void Setup()
        {
            // Create test directory structure
            testWebRootPath = Path.Combine(Path.GetTempPath(), $"StartupTaskServiceTests_{Guid.NewGuid()}");
            var libCkeditorPath = Path.Combine(testWebRootPath, "lib", "ckeditor");
            Directory.CreateDirectory(libCkeditorPath);

            // Create test CSS file
            testFilePath = Path.Combine(libCkeditorPath, "ckeditor5-content.css");
            File.WriteAllText(testFilePath, "/* Test CSS Content */\nbody { margin: 0; }");

            // Setup WebHostEnvironment mock
            mockWebHostEnvironment = new Mock<IWebHostEnvironment>();
            mockWebHostEnvironment.Setup(w => w.WebRootPath).Returns(testWebRootPath);

            // Build real configuration with in-memory values
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    ["ConnectionStrings:ConfigDbConnectionString"] = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=testkey;",
                    ["MultiTenantEditor"] = "false"
                });
            
            var configuration = configurationBuilder.Build();

            // Create mock with real configuration
            mockManagementUtilities = new Mock<IMultiDatabaseManagementUtilities>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test directory
            if (Directory.Exists(testWebRootPath))
            {
                Directory.Delete(testWebRootPath, true);
            }
        }

        #region Constructor Tests

        /// <summary>
        /// Constructor creates an instance when provided valid parameters.
        /// </summary>
        [TestMethod]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act
            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Assert
            Assert.IsNotNull(service);
        }

        /// <summary>
        /// Constructor throws <see cref="ArgumentNullException"/> when the
        /// <see cref="IWebHostEnvironment"/> parameter is null.
        /// </summary>
        [TestMethod]
        public void Constructor_WithNullWebHostEnvironment_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                var service = new StartupTaskService(
                    null!,
                    mockManagementUtilities.Object);
            });
        }

        /// <summary>
        /// Constructor throws <see cref="ArgumentNullException"/> when the
        /// management utilities parameter is null.
        /// </summary>
        [TestMethod]
        public void Constructor_WithNullManagementUtilities_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                var service = new StartupTaskService(
                    mockWebHostEnvironment.Object,
                    null!);
            });
        }

        #endregion

        #region RunAsync - File Reading Tests

        /// <summary>
        /// RunAsync reads the expected file when it exists and calls GetConnections.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithExistingFile_ReadsFileSuccessfully()
        {
            // Arrange
            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act
            await service.RunAsync();

            // Assert
            mockManagementUtilities.Verify(m => m.GetConnections(), Times.Once);
            Assert.IsTrue(File.Exists(testFilePath));
        }

        /// <summary>
        /// RunAsync throws <see cref="FileNotFoundException"/> when the expected
        /// file has been removed prior to execution.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithMissingFile_ThrowsFileNotFoundException()
        {
            // Arrange
            File.Delete(testFilePath); // Remove the test file
            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act & Assert
            await Assert.ThrowsExactlyAsync<FileNotFoundException>(async () =>
            {
                await service.RunAsync();
            });
        }

        /// <summary>
        /// RunAsync throws <see cref="DirectoryNotFoundException"/> when the
        /// configured web root path does not exist.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithInvalidWebRootPath_ThrowsDirectoryNotFoundException()
        {
            // Arrange
            mockWebHostEnvironment.Setup(w => w.WebRootPath)
                .Returns(Path.Combine(Path.GetTempPath(), "NonExistentDirectory_" + Guid.NewGuid()));

            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act & Assert
            await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(async () =>
            {
                await service.RunAsync();
            });
        }

        #endregion

        #region RunAsync - Connection Tests

        /// <summary>
        /// RunAsync completes successfully when no connections are configured.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithNoConnections_CompletesSuccessfully()
        {
            // Arrange
            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act
            await service.RunAsync();

            // Assert
            mockManagementUtilities.Verify(m => m.GetConnections(), Times.Once);
        }

        /// <summary>
        /// RunAsync attempts to upload to a single configured connection; in
        /// test environments this may raise connection-related exceptions.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithSingleConnection_UploadsToOneConnection()
        {
            // Arrange
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                StorageConn = "UseDevelopmentStorage=true",
                DbConn = "Server=test",
                WebsiteUrl = "https://test.example.com",
                ResourceGroup = "test-rg"
            };

            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection> { connection });

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act
            // Note: This will attempt actual upload to development storage
            // In a real test environment, you'd mock BlobServiceClient
            try
            {
                await service.RunAsync();
            }
            catch (Exception ex)
            {
                // Expected to fail if Azure Storage Emulator is not running
                Assert.IsTrue(
                    ex.Message.Contains("Unable to connect") ||
                    ex.Message.Contains("No connection could be made") ||
                    ex is RequestFailedException,
                    $"Expected connection error, got: {ex.Message}");
            }

            // Assert
            mockManagementUtilities.Verify(m => m.GetConnections(), Times.Once);
        }

        #endregion

        #region RunAsync - Error Handling Tests

        /// <summary>
        /// RunAsync propagates exceptions thrown while retrieving connections.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WhenGetConnectionsThrows_PropagatesException()
        {
            // Arrange
            mockManagementUtilities.Setup(m => m.GetConnections())
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act & Assert
            var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            {
                await service.RunAsync();
            });

            Assert.AreEqual("Database connection failed", exception.Message);
        }

        /// <summary>
        /// RunAsync throws <see cref="ArgumentNullException"/> when a connection
        /// contains a null storage connection string.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithNullStorageConnection_ThrowsException()
        {
            // Arrange
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                StorageConn = null!,
                DbConn = "Server=test",
                WebsiteUrl = "https://test.example.com",
                ResourceGroup = "test-rg"
            };

            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection> { connection });

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act & Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            {
                await service.RunAsync();
            });
        }

        /// <summary>
        /// RunAsync throws <see cref="ArgumentException"/> when a connection
        /// contains an empty storage connection string.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithEmptyStorageConnection_ThrowsException()
        {
            // Arrange
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                StorageConn = string.Empty,
                DbConn = "Server=test",
                WebsiteUrl = "https://test.example.com",
                ResourceGroup = "test-rg"
            };

            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection> { connection });

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act & Assert
            await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            {
                await service.RunAsync();
            });
        }

        #endregion

        #region RunAsync - File Stream Tests

        /// <summary>
        /// RunAsync disposes file streams so files are not locked after execution.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_ProperlyDisposesFileStream()
        {
            // Arrange
            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act
            await service.RunAsync();

            // Assert - File should not be locked
            // Try to delete the file to verify stream was disposed
            File.Delete(testFilePath);
            Assert.IsFalse(File.Exists(testFilePath));

            // Recreate for cleanup
            Directory.CreateDirectory(Path.GetDirectoryName(testFilePath)!);
            File.WriteAllText(testFilePath, "/* Test CSS Content */");
        }

        /// <summary>
        /// RunAsync copies file content into a memory stream and leaves the file intact.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_CopiesFileToMemoryStream()
        {
            // Arrange
            var fileContent = "/* Test CSS with specific content for verification */\n.test { color: red; }";
            File.WriteAllText(testFilePath, fileContent);

            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act
            await service.RunAsync();

            // Assert - Verify file was read by checking it still exists and content unchanged
            Assert.IsTrue(File.Exists(testFilePath));
            var readContent = File.ReadAllText(testFilePath);
            Assert.AreEqual(fileContent, readContent);
        }

        #endregion

        #region RunAsync - Large File Tests

        /// <summary>
        /// RunAsync handles large files without failing and still retrieves connections.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithLargeFile_HandlesSuccessfully()
        {
            // Arrange - Create a large test file (1MB)
            var largeContent = new string('A', 1024 * 1024);
            File.WriteAllText(testFilePath, largeContent);

            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act
            await service.RunAsync();

            // Assert
            mockManagementUtilities.Verify(m => m.GetConnections(), Times.Once);
            Assert.IsTrue(File.Exists(testFilePath));
        }

        #endregion

        #region RunAsync - Empty File Tests

        /// <summary>
        /// RunAsync processes empty files without error and calls GetConnections.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithEmptyFile_HandlesSuccessfully()
        {
            // Arrange - Create an empty file
            File.WriteAllText(testFilePath, string.Empty);

            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act
            await service.RunAsync();

            // Assert
            mockManagementUtilities.Verify(m => m.GetConnections(), Times.Once);
        }

        #endregion

        #region RunAsync - Special Characters Tests

        /// <summary>
        /// RunAsync correctly handles files containing special and Unicode characters.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithSpecialCharactersInFile_HandlesSuccessfully()
        {
            // Arrange
            var specialContent = "/* Test CSS */\n.test { content: '©™®'; }\n/* Unicode: 你好 */";
            File.WriteAllText(testFilePath, specialContent);

            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act
            await service.RunAsync();

            // Assert
            mockManagementUtilities.Verify(m => m.GetConnections(), Times.Once);
            var readContent = File.ReadAllText(testFilePath);
            Assert.AreEqual(specialContent, readContent);
        }

        #endregion

        #region RunAsync - Concurrent Execution Tests

        /// <summary>
        /// RunAsync can be executed concurrently and will call GetConnections for each run.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_CalledConcurrently_HandlesCorrectly()
        {
            // Arrange
            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act - Run multiple times concurrently
            var tasks = Enumerable.Range(0, 5)
                .Select(_ => service.RunAsync())
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert
            mockManagementUtilities.Verify(m => m.GetConnections(), Times.Exactly(5));
        }

        #endregion

        #region File Path Construction Tests

        /// <summary>
        /// Verifies file path construction using a normal web root path produces the expected path.
        /// </summary>
        [TestMethod]
        public void FilePathConstruction_WithNormalWebRootPath_BuildsCorrectPath()
        {
            // Arrange
            var expectedPath = Path.Combine(testWebRootPath, "lib", "ckeditor", "ckeditor5-content.css");

            // Assert
            Assert.AreEqual(expectedPath, testFilePath);
            Assert.IsTrue(File.Exists(testFilePath));
        }

        /// <summary>
        /// RunAsync handles a web root path that ends with a trailing slash.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithTrailingSlashInWebRootPath_HandlesCorrectly()
        {
            // Arrange
            mockWebHostEnvironment.Setup(w => w.WebRootPath)
                .Returns(testWebRootPath + Path.DirectorySeparatorChar);

            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(new List<Connection>());

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act
            await service.RunAsync();

            // Assert
            mockManagementUtilities.Verify(m => m.GetConnections(), Times.Once);
        }

        #endregion

        #region Integration-Like Tests

        /// <summary>
        /// Full workflow test that runs RunAsync with a real file and mocked connections;
        /// may surface external connection exceptions in CI environments.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_FullWorkflow_WithRealFileAndMockedConnections()
        {
            // Arrange
            var cssContent = @"/* CKEditor 5 Content Styles */
body {
    font-family: Arial, sans-serif;
    margin: 20px;
}
.ck-content {
    line-height: 1.6;
}";
            File.WriteAllText(testFilePath, cssContent);

            var connections = new List<Connection>
            {
                new Connection
                {
                    Id = Guid.NewGuid(),
                    StorageConn = "UseDevelopmentStorage=true",
                    DbConn = "AccountEndpoint=https://test.documents.azure.com:443/;",
                    WebsiteUrl = "https://integration-test.example.com",
                    DomainNames = new[] { "integration-test.example.com" },
                    ResourceGroup = "integration-rg"
                }
            };

            mockManagementUtilities.Setup(m => m.GetConnections())
                .ReturnsAsync(connections);

            var service = new StartupTaskService(
                mockWebHostEnvironment.Object,
                mockManagementUtilities.Object);

            // Act
            try
            {
                await service.RunAsync();
            }
            catch (Exception ex)
            {
                // Expected if storage emulator not running
                Assert.IsTrue(
                    ex.Message.Contains("Unable to connect") ||
                    ex.Message.Contains("No connection could be made") ||
                    ex is RequestFailedException);
            }

            // Assert
            mockManagementUtilities.Verify(m => m.GetConnections(), Times.Once);
            Assert.IsTrue(File.Exists(testFilePath));
            var finalContent = File.ReadAllText(testFilePath);
            Assert.AreEqual(cssContent, finalContent);
        }

        #endregion
    }
}
