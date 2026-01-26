// <copyright file="ReservedPathsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.ReservedPaths
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Models;
    using Sky.Editor.Services.ReservedPaths;

    /// <summary>
    /// Unit tests for <see cref="ReservedPaths"/> service.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class ReservedPathsTests : SkyCmsTestBase
    {
        private IReservedPaths reservedPathsService;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext();
            reservedPathsService = new ReservedPaths(Db);
        }

        #region GetReservedPaths Tests

        [TestMethod]
        public async Task GetReservedPaths_FirstCall_CreatesDefaultPaths()
        {
            // Act
            var paths = await reservedPathsService.GetReservedPaths();

            // Assert
            Assert.IsNotNull(paths);
            Assert.IsTrue(paths.Count > 0, "Should create default reserved paths");
            Assert.IsTrue(paths.Any(p => p.Path == "root"), "Should contain root path");
            Assert.IsTrue(paths.Any(p => p.Path == "admin"), "Should contain admin path");
            Assert.IsTrue(paths.Any(p => p.Path == "blog/*"), "Should contain blog wildcard path");
        }

        [TestMethod]
        public async Task GetReservedPaths_SecondCall_ReturnsSamePaths()
        {
            // Arrange
            var firstCall = await reservedPathsService.GetReservedPaths();
            var firstCount = firstCall.Count;

            // Act
            var secondCall = await reservedPathsService.GetReservedPaths();

            // Assert
            Assert.AreEqual(firstCount, secondCall.Count, "Should return same number of paths");
        }

        [TestMethod]
        public async Task GetReservedPaths_ContainsSystemRequiredPaths()
        {
            // Act
            var paths = await reservedPathsService.GetReservedPaths();

            // Assert - Check for critical system paths
            var systemPaths = paths.Where(p => p.CosmosRequired).ToList();
            Assert.IsTrue(systemPaths.Count > 0, "Should have system required paths");
            Assert.IsTrue(systemPaths.Any(p => p.Path == "editor/*"), "Should have editor path");
            Assert.IsTrue(systemPaths.Any(p => p.Path == "filemanager/*"), "Should have filemanager path");
            Assert.IsTrue(systemPaths.Any(p => p.Path == "layouts/*"), "Should have layouts path");
        }

        [TestMethod]
        public async Task GetReservedPaths_ContainsIdentityPaths()
        {
            // Act
            var paths = await reservedPathsService.GetReservedPaths();

            // Assert
            Assert.IsTrue(paths.Any(p => p.Path == "account"), "Should have account path");
            Assert.IsTrue(paths.Any(p => p.Path == "login"), "Should have login path");
            Assert.IsTrue(paths.Any(p => p.Path == "logout"), "Should have logout path");
            Assert.IsTrue(paths.Any(p => p.Path == "register"), "Should have register path");
        }

        [TestMethod]
        public async Task GetReservedPaths_ContainsContentPaths()
        {
            // Act
            var paths = await reservedPathsService.GetReservedPaths();

            // Assert
            Assert.IsTrue(paths.Any(p => p.Path == "rss"), "Should have RSS path");
            Assert.IsTrue(paths.Any(p => p.Path == "sitemap.xml"), "Should have sitemap path");
            Assert.IsTrue(paths.Any(p => p.Path == "toc.json"), "Should have TOC path");
        }

        [TestMethod]
        public async Task GetReservedPaths_AllPathsHaveNotes()
        {
            // Act
            var paths = await reservedPathsService.GetReservedPaths();

            // Assert
            foreach (var path in paths)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(path.Notes), 
                    $"Path '{path.Path}' should have notes");
            }
        }

        #endregion

        #region IsReserved Tests

        [TestMethod]
        public async Task IsReserved_ExactMatch_ReturnsTrue()
        {
            // Act
            var isReserved = await reservedPathsService.IsReserved("admin");

            // Assert
            Assert.IsTrue(isReserved, "admin should be reserved");
        }

        [TestMethod]
        public async Task IsReserved_CaseInsensitive_ReturnsTrue()
        {
            // Act
            var isReservedLower = await reservedPathsService.IsReserved("admin");
            var isReservedUpper = await reservedPathsService.IsReserved("ADMIN");
            var isReservedMixed = await reservedPathsService.IsReserved("AdMiN");

            // Assert
            Assert.IsTrue(isReservedLower, "lowercase admin should be reserved");
            Assert.IsTrue(isReservedUpper, "uppercase ADMIN should be reserved");
            Assert.IsTrue(isReservedMixed, "mixed case AdMiN should be reserved");
        }

        [TestMethod]
        public async Task IsReserved_NonReservedPath_ReturnsFalse()
        {
            // Act
            var isReserved = await reservedPathsService.IsReserved("my-custom-page");

            // Assert
            Assert.IsFalse(isReserved, "custom page should not be reserved");
        }

        [TestMethod]
        public async Task IsReserved_RootPath_ReturnsTrue()
        {
            // Act
            var isReserved = await reservedPathsService.IsReserved("root");

            // Assert
            Assert.IsTrue(isReserved, "root should be reserved");
        }

        [TestMethod]
        public async Task IsReserved_WildcardPath_ReturnsTrue()
        {
            // Act
            var isReserved = await reservedPathsService.IsReserved("blog/*");

            // Assert
            Assert.IsTrue(isReserved, "blog/* should be reserved");
        }

        [TestMethod]
        public async Task IsReserved_EmptyString_ReturnsFalse()
        {
            // Act
            var isReserved = await reservedPathsService.IsReserved(string.Empty);

            // Assert
            Assert.IsFalse(isReserved, "empty string should not be reserved");
        }

        #endregion

        #region Upsert Tests

        [TestMethod]
        public async Task Upsert_NewPath_AddsSuccessfully()
        {
            // Arrange
            var newPath = new ReservedPath
            {
                Path = "custom-reserved",
                CosmosRequired = false,
                Notes = "Custom reserved path for testing"
            };

            // Act
            await reservedPathsService.Upsert(newPath);
            var paths = await reservedPathsService.GetReservedPaths();

            // Assert
            Assert.IsTrue(paths.Any(p => p.Path == "custom-reserved"), 
                "Custom path should be added");
        }

        [TestMethod]
        public async Task Upsert_ExistingNonSystemPath_UpdatesSuccessfully()
        {
            // Arrange
            var newPath = new ReservedPath
            {
                Path = "custom-path",
                CosmosRequired = false,
                Notes = "Original notes"
            };
            await reservedPathsService.Upsert(newPath);

            var updatedPath = new ReservedPath
            {
                Path = "custom-path",
                CosmosRequired = false,
                Notes = "Updated notes"
            };

            // Act
            await reservedPathsService.Upsert(updatedPath);
            var paths = await reservedPathsService.GetReservedPaths();

            // Assert
            var savedPath = paths.First(p => p.Path == "custom-path");
            Assert.AreEqual("Updated notes", savedPath.Notes, 
                "Notes should be updated");
        }

        [TestMethod]
        public async Task Upsert_SystemRequiredPath_ThrowsException()
        {
            // Arrange
            var systemPath = new ReservedPath
            {
                Path = "admin",
                CosmosRequired = true,
                Notes = "Trying to update system path"
            };

            // Act & Assert
            await Assert.ThrowsExactlyAsync<Exception>(async () =>
                await reservedPathsService.Upsert(systemPath),
                "Should not allow updating system required paths");
        }

        [TestMethod]
        public async Task Upsert_CaseInsensitiveDuplicate_UpdatesExisting()
        {
            // Arrange
            var newPath = new ReservedPath
            {
                Path = "MyPath",
                CosmosRequired = false,
                Notes = "Original"
            };
            await reservedPathsService.Upsert(newPath);

            var duplicatePath = new ReservedPath
            {
                Path = "mypath", // Different case
                CosmosRequired = false,
                Notes = "Updated"
            };

            // Act
            await reservedPathsService.Upsert(duplicatePath);
            var paths = await reservedPathsService.GetReservedPaths();

            // Assert
            var matchingPaths = paths.Where(p => 
                p.Path.Equals("MyPath", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.AreEqual(1, matchingPaths.Count, 
                "Should not create duplicate with different case");
        }

        #endregion

        #region Remove Tests

        [TestMethod]
        public async Task Remove_NonSystemPath_RemovesSuccessfully()
        {
            // Arrange
            var newPath = new ReservedPath
            {
                Path = "removable-path",
                CosmosRequired = false,
                Notes = "Path to be removed"
            };
            await reservedPathsService.Upsert(newPath);

            // Act
            await reservedPathsService.Remove("removable-path");
            var paths = await reservedPathsService.GetReservedPaths();

            // Assert
            Assert.IsFalse(paths.Any(p => p.Path == "removable-path"), 
                "Path should be removed");
        }

        [TestMethod]
        public async Task Remove_SystemRequiredPath_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsExactlyAsync<Exception>(async () =>
                await reservedPathsService.Remove("admin"),
                "Should not allow removing system required paths");
        }

        [TestMethod]
        public async Task Remove_NonExistentPath_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            await reservedPathsService.Remove("non-existent-path");

            // Verify service still works
            var paths = await reservedPathsService.GetReservedPaths();
            Assert.IsNotNull(paths);
        }

        [TestMethod]
        public async Task Remove_CaseInsensitive_RemovesCorrectly()
        {
            // Arrange
            var newPath = new ReservedPath
            {
                Path = "RemovePath",
                CosmosRequired = false,
                Notes = "Test"
            };
            await reservedPathsService.Upsert(newPath);

            // Act
            await reservedPathsService.Remove("removepath"); // Different case
            var paths = await reservedPathsService.GetReservedPaths();

            // Assert
            Assert.IsFalse(paths.Any(p => 
                p.Path.Equals("RemovePath", StringComparison.OrdinalIgnoreCase)), 
                "Should remove path regardless of case");
        }

        [TestMethod]
        public async Task Remove_RootPath_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsExactlyAsync<Exception>(async () =>
                await reservedPathsService.Remove("root"),
                "Should not allow removing root path");
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task FullWorkflow_AddUpdateRemove_WorksCorrectly()
        {
            // Arrange
            var path = new ReservedPath
            {
                Path = "workflow-test",
                CosmosRequired = false,
                Notes = "Original notes"
            };

            // Act 1: Add
            await reservedPathsService.Upsert(path);
            var pathsAfterAdd = await reservedPathsService.GetReservedPaths();
            var addedPath = pathsAfterAdd.First(p => p.Path == "workflow-test");

            // Assert 1
            Assert.AreEqual("Original notes", addedPath.Notes);

            // Act 2: Update
            path.Notes = "Updated notes";
            await reservedPathsService.Upsert(path);
            var pathsAfterUpdate = await reservedPathsService.GetReservedPaths();
            var updatedPath = pathsAfterUpdate.First(p => p.Path == "workflow-test");

            // Assert 2
            Assert.AreEqual("Updated notes", updatedPath.Notes);

            // Act 3: Remove
            await reservedPathsService.Remove("workflow-test");
            var pathsAfterRemove = await reservedPathsService.GetReservedPaths();

            // Assert 3
            Assert.IsFalse(pathsAfterRemove.Any(p => p.Path == "workflow-test"));
        }

        [TestMethod]
        public async Task MultipleCustomPaths_ManageIndependently()
        {
            // Arrange
            var path1 = new ReservedPath
            {
                Path = "custom-1",
                CosmosRequired = false,
                Notes = "First custom"
            };

            var path2 = new ReservedPath
            {
                Path = "custom-2",
                CosmosRequired = false,
                Notes = "Second custom"
            };

            // Act
            await reservedPathsService.Upsert(path1);
            await reservedPathsService.Upsert(path2);

            var isReserved1 = await reservedPathsService.IsReserved("custom-1");
            var isReserved2 = await reservedPathsService.IsReserved("custom-2");

            await reservedPathsService.Remove("custom-1");

            var isReserved1After = await reservedPathsService.IsReserved("custom-1");
            var isReserved2After = await reservedPathsService.IsReserved("custom-2");

            // Assert
            Assert.IsTrue(isReserved1, "custom-1 should initially be reserved");
            Assert.IsTrue(isReserved2, "custom-2 should initially be reserved");
            Assert.IsFalse(isReserved1After, "custom-1 should be removed");
            Assert.IsTrue(isReserved2After, "custom-2 should still be reserved");
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public async Task Upsert_PathWithSlashes_HandlesCorrectly()
        {
            // Arrange
            var path = new ReservedPath
            {
                Path = "custom/nested/path",
                CosmosRequired = false,
                Notes = "Nested path"
            };

            // Act
            await reservedPathsService.Upsert(path);
            var isReserved = await reservedPathsService.IsReserved("custom/nested/path");

            // Assert
            Assert.IsTrue(isReserved, "Nested path should be reserved");
        }

        [TestMethod]
        public async Task Upsert_PathWithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var path = new ReservedPath
            {
                Path = "custom-path_123",
                CosmosRequired = false,
                Notes = "Path with special chars"
            };

            // Act
            await reservedPathsService.Upsert(path);
            var isReserved = await reservedPathsService.IsReserved("custom-path_123");

            // Assert
            Assert.IsTrue(isReserved, "Path with special characters should be reserved");
        }

        [TestMethod]
        public async Task GetReservedPaths_AfterMultipleOperations_RemainsConsistent()
        {
            // Arrange
            var initialPaths = await reservedPathsService.GetReservedPaths();
            var initialCount = initialPaths.Count;

            // Act - Perform multiple operations
            await reservedPathsService.Upsert(new ReservedPath 
            { 
                Path = "temp1", 
                CosmosRequired = false, 
                Notes = "Temp" 
            });
            await reservedPathsService.Upsert(new ReservedPath 
            { 
                Path = "temp2", 
                CosmosRequired = false, 
                Notes = "Temp" 
            });
            await reservedPathsService.Remove("temp1");

            var finalPaths = await reservedPathsService.GetReservedPaths();

            // Assert
            Assert.AreEqual(initialCount + 1, finalPaths.Count, 
                "Should have one more path than initial");
            Assert.IsTrue(finalPaths.Any(p => p.Path == "temp2"), 
                "temp2 should exist");
            Assert.IsFalse(finalPaths.Any(p => p.Path == "temp1"), 
                "temp1 should not exist");
        }

        #endregion

        [TestCleanup]
        public async Task TestCleanup()
        {
            await DisposeAsync();
        }
    }
}
