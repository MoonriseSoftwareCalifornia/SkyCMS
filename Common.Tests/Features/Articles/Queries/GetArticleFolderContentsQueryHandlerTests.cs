// <copyright file="GetArticleFolderContentsQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Queries
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="GetArticleFolderContentsQueryHandler"/>.
    /// Validates folder contents retrieval from storage.
    /// </summary>
    [TestClass]
    public class GetArticleFolderContentsQueryHandlerTests : CommonTestsBase
    {
        /// <summary>
        /// Initializes the shared test infrastructure for this test class.
        /// </summary>
        /// <param name="context">Test context provided by MSTest.</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            ContextPool = new TestDbContextPool();
        }

        /// <summary>
        /// Cleans up the shared test infrastructure after all tests complete.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            ContextPool?.Dispose();
        }

        [TestMethod]
        public void Constructor_WithValidDependencies_ShouldSucceed()
        {
            var mockStorageContext = new Mock<IStorageContext>();

            var handler = new GetArticleFolderContentsQueryHandler(mockStorageContext.Object);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public void Constructor_WithNullStorageContext_ShouldThrowArgumentNullException()
        {
            try
            {
                var handler = new GetArticleFolderContentsQueryHandler(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected exception - test passes
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithNullQuery_ShouldThrowArgumentNullException()
        {
            var mockStorageContext = new Mock<IStorageContext>();
            var handler = new GetArticleFolderContentsQueryHandler(mockStorageContext.Object);

            try
            {
                await handler.HandleAsync(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected exception - test passes
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithArticleNumber_ShouldConstructCorrectPath()
        {
            var articleNumber = 12345;
            var path = string.Empty;
            var expectedPath = "/pub/articles/12345/";
            var mockStorageContext = new Mock<IStorageContext>();
            mockStorageContext.Setup(s => s.GetFilesAndDirectories(It.IsAny<string>()))
                .ReturnsAsync(new List<FileManagerEntry>());

            var handler = new GetArticleFolderContentsQueryHandler(mockStorageContext.Object);
            var query = new GetArticleFolderContentsQuery(articleNumber, path);

            await handler.HandleAsync(query);

            mockStorageContext.Verify(s => s.GetFilesAndDirectories(expectedPath), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithPathAndLeadingSlash_ShouldNormalizePath()
        {
            var articleNumber = 12345;
            var path = "/subfolder";
            var expectedPath = "/pub/articles/12345/subfolder";
            var mockStorageContext = new Mock<IStorageContext>();
            mockStorageContext.Setup(s => s.GetFilesAndDirectories(It.IsAny<string>()))
                .ReturnsAsync(new List<FileManagerEntry>());

            var handler = new GetArticleFolderContentsQueryHandler(mockStorageContext.Object);
            var query = new GetArticleFolderContentsQuery(articleNumber, path);

            await handler.HandleAsync(query);

            mockStorageContext.Verify(s => s.GetFilesAndDirectories(expectedPath), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithPathWithoutLeadingSlash_ShouldNormalizePath()
        {
            var articleNumber = 12345;
            var path = "subfolder";
            var expectedPath = "/pub/articles/12345/subfolder";
            var mockStorageContext = new Mock<IStorageContext>();
            mockStorageContext.Setup(s => s.GetFilesAndDirectories(It.IsAny<string>()))
                .ReturnsAsync(new List<FileManagerEntry>());

            var handler = new GetArticleFolderContentsQueryHandler(mockStorageContext.Object);
            var query = new GetArticleFolderContentsQuery(articleNumber, path);

            await handler.HandleAsync(query);

            mockStorageContext.Verify(s => s.GetFilesAndDirectories(expectedPath), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_ReturnsStorageContextResults()
        {
            var expectedContents = new List<FileManagerEntry>
            {
                new FileManagerEntry { Name = "file1.txt", IsDirectory = false },
                new FileManagerEntry { Name = "folder1", IsDirectory = true }
            };

            var mockStorageContext = new Mock<IStorageContext>();
            mockStorageContext.Setup(s => s.GetFilesAndDirectories(It.IsAny<string>()))
                .ReturnsAsync(expectedContents);

            var handler = new GetArticleFolderContentsQueryHandler(mockStorageContext.Object);
            var query = new GetArticleFolderContentsQuery(12345, string.Empty);

            var result = await handler.HandleAsync(query);

            Assert.AreEqual(expectedContents, result);
        }

        [TestMethod]
        public async Task HandleAsync_WithEmptyFolder_ShouldReturnEmptyList()
        {
            var mockStorageContext = new Mock<IStorageContext>();
            mockStorageContext.Setup(s => s.GetFilesAndDirectories(It.IsAny<string>()))
                .ReturnsAsync(new List<FileManagerEntry>());

            var handler = new GetArticleFolderContentsQueryHandler(mockStorageContext.Object);
            var query = new GetArticleFolderContentsQuery(12345, string.Empty);

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }
    }
}
