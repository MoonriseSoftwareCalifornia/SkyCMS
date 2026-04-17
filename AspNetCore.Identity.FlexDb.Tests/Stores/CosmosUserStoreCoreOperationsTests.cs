// <copyright file="CosmosUserStoreCoreOperationsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Identity.CosmosDb.Tests.Net9.Stores
{
    /// <summary>
    /// Priority 2 tests for CosmosUserStore core CRUD operations.
    /// Tests CreateAsync, UpdateAsync, DeleteAsync, and related operations with comprehensive coverage.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CosmosUserStoreCoreOperationsTests : CosmosIdentityTestsBase
    {
        /// <summary>
        /// Provides test data for all available database providers
        /// </summary>
        public static IEnumerable<object[]> GetTestProviders()
        {
            var providers = TestUtilities.GetAvailableProviders();

            foreach (var provider in providers)
            {
                yield return new object[] { provider };
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            if (_testUtilities == null)
            {
                _testUtilities = new TestUtilities();
            }
            if (_random == null)
            {
                _random = new Random();
            }
        }

        #region CreateAsync Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task CreateAsync_WithValidUser_CreatesUser(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);

            var uniqueEmail = $"createtest_{Guid.NewGuid():N}@testdomain.com";
            var user = new IdentityUser(uniqueEmail)
            {
                Email = uniqueEmail,
                NormalizedUserName = uniqueEmail.ToUpper(),
                NormalizedEmail = uniqueEmail.ToUpper(),
                Id = Guid.NewGuid().ToString(),
                EmailConfirmed = false,
                PhoneNumberConfirmed = false,
                TwoFactorEnabled = false,
                LockoutEnabled = true
            };

            // Act
            var result = await userStore.CreateAsync(user);

            // Assert
            Assert.IsNotNull(result, $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}. Errors: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            var retrievedUser = await userStore.FindByIdAsync(user.Id);
            Assert.IsNotNull(retrievedUser, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(uniqueEmail, retrievedUser.UserName, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(uniqueEmail, retrievedUser.Email, $"Failed for provider: {provider.DisplayName}");
            Assert.IsFalse(retrievedUser.EmailConfirmed, $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(retrievedUser.LockoutEnabled, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task CreateAsync_WithNullUser_ThrowsArgumentNullException(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);

            // Act & Assert
            try
            {
                await userStore.CreateAsync(null);
                Assert.Fail($"Expected ArgumentNullException for provider: {provider.DisplayName}");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task CreateAsync_WithNullEmail_ThrowsArgumentNullException(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);

            var user = new IdentityUser($"user_{Guid.NewGuid():N}")
            {
                Email = null, // Invalid - null email
                Id = Guid.NewGuid().ToString()
            };

            // Act & Assert
            try
            {
                await userStore.CreateAsync(user);
                Assert.Fail($"Expected ArgumentNullException for provider: {provider.DisplayName}");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task CreateAsync_WithNullUserName_ThrowsArgumentNullException(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);

            var user = new IdentityUser
            {
                UserName = null, // Invalid - null username
                Email = $"test_{Guid.NewGuid():N}@testdomain.com",
                Id = Guid.NewGuid().ToString()
            };

            // Act & Assert
            try
            {
                await userStore.CreateAsync(user);
                Assert.Fail($"Expected ArgumentNullException for provider: {provider.DisplayName}");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task CreateAsync_WithDuplicateEmail_ReturnsFailed(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);

            var duplicateEmail = $"duplicate_{Guid.NewGuid():N}@testdomain.com";
            var user1 = new IdentityUser(duplicateEmail)
            {
                Email = duplicateEmail,
                NormalizedUserName = duplicateEmail.ToUpper(),
                NormalizedEmail = duplicateEmail.ToUpper(),
                Id = Guid.NewGuid().ToString()
            };

            await userStore.CreateAsync(user1);

            var user2 = new IdentityUser(duplicateEmail)
            {
                Email = duplicateEmail,
                NormalizedUserName = duplicateEmail.ToUpper(),
                NormalizedEmail = duplicateEmail.ToUpper(),
                Id = Guid.NewGuid().ToString()
            };

            // Act
            var result = await userStore.CreateAsync(user2);

            // Assert
            Assert.IsNotNull(result, $"Failed for provider: {provider.DisplayName}");
            Assert.IsFalse(result.Succeeded, $"Should have failed for duplicate email on provider: {provider.DisplayName}");
            Assert.IsTrue(result.Errors.Any(), $"Failed for provider: {provider.DisplayName}");
        }

        #endregion

        #region UpdateAsync Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task UpdateAsync_WithValidChanges_UpdatesUser(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);
            using var dbContext = _testUtilities.GetDbContext(provider.ConnectionString);

            var user = await GetMockRandomUserAsync(userStore);
            var originalEmail = user.Email;
            var newPhoneNumber = "555-1234";

            // Act - Update phone number
            await userStore.SetPhoneNumberAsync(user, newPhoneNumber);
            var updateResult = await userStore.UpdateAsync(user);

            // Assert
            Assert.IsTrue(updateResult.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var updatedUser = await userStore.FindByIdAsync(user.Id);
            Assert.IsNotNull(updatedUser, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(newPhoneNumber, updatedUser.PhoneNumber, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(originalEmail, updatedUser.Email, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task UpdateAsync_WithNullUser_ThrowsArgumentNullException(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);

            // Act & Assert
            try
            {
                await userStore.UpdateAsync(null);
                Assert.Fail($"Expected ArgumentNullException for provider: {provider.DisplayName}");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task UpdateAsync_EmailConfirmation_UpdatesSuccessfully(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);

            var user = await GetMockRandomUserAsync(userStore);
            Assert.IsFalse(user.EmailConfirmed, $"Failed for provider: {provider.DisplayName}");

            // Act
            await userStore.SetEmailConfirmedAsync(user, true);
            var updateResult = await userStore.UpdateAsync(user);

            // Assert
            Assert.IsTrue(updateResult.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var updatedUser = await userStore.FindByIdAsync(user.Id);
            Assert.IsTrue(updatedUser.EmailConfirmed, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task UpdateAsync_SecurityStamp_UpdatesSuccessfully(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);

            var user = await GetMockRandomUserAsync(userStore);
            var newSecurityStamp = Guid.NewGuid().ToString();

            // Act
            await userStore.SetSecurityStampAsync(user, newSecurityStamp, CancellationToken.None);
            var updateResult = await userStore.UpdateAsync(user);

            // Assert
            Assert.IsTrue(updateResult.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var updatedUser = await userStore.FindByIdAsync(user.Id);
            Assert.AreEqual(newSecurityStamp, updatedUser.SecurityStamp, $"Failed for provider: {provider.DisplayName}");
        }

        #endregion

        #region DeleteAsync Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task DeleteAsync_WithValidUser_DeletesUser(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);
            using var dbContext = _testUtilities.GetDbContext(provider.ConnectionString);

            var user = await GetMockRandomUserAsync(userStore);
            var userId = user.Id;

            // Act
            var result = await userStore.DeleteAsync(user);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var deletedUser = await userStore.FindByIdAsync(userId);
            Assert.IsNull(deletedUser, $"User should be deleted for provider: {provider.DisplayName}");

            var userCount = await dbContext.Users.Where(u => u.Id == userId).CountAsync();
            Assert.AreEqual(0, userCount, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task DeleteAsync_WithNullUser_ThrowsArgumentNullException(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);

            // Act & Assert
            try
            {
                await userStore.DeleteAsync(null);
                Assert.Fail($"Expected ArgumentNullException for provider: {provider.DisplayName}");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task DeleteAsync_RemovesAssociatedClaims(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);
            using var dbContext = _testUtilities.GetDbContext(provider.ConnectionString);

            var user = await GetMockRandomUserAsync(userStore);
            var userId = user.Id;
            var claim = GetMockClaim();
            await userStore.AddClaimsAsync(user, new[] { claim });

            // Verify claim exists
            var claimCount = await dbContext.UserClaims.Where(c => c.UserId == userId).CountAsync();
            Assert.AreEqual(1, claimCount, $"Failed for provider: {provider.DisplayName}");

            // Act
            var result = await userStore.DeleteAsync(user);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var claimCountAfterDelete = await dbContext.UserClaims.Where(c => c.UserId == userId).CountAsync();
            Assert.AreEqual(0, claimCountAfterDelete, $"Claims should be deleted for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task DeleteAsync_RemovesAssociatedRoles(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);
            using var dbContext = _testUtilities.GetDbContext(provider.ConnectionString);

            var user = await GetMockRandomUserAsync(userStore);
            var userId = user.Id;
            var role = await GetMockRandomRoleAsync(roleStore);
            await userStore.AddToRoleAsync(user, role.NormalizedName);

            // Verify role association exists
            var roleCount = await dbContext.UserRoles.Where(ur => ur.UserId == userId).CountAsync();
            Assert.AreEqual(1, roleCount, $"Failed for provider: {provider.DisplayName}");

            // Act
            var result = await userStore.DeleteAsync(user);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var roleCountAfterDelete = await dbContext.UserRoles.Where(ur => ur.UserId == userId).CountAsync();
            Assert.AreEqual(0, roleCountAfterDelete, $"User roles should be deleted for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task DeleteAsync_RemovesAssociatedLogins(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);
            using var dbContext = _testUtilities.GetDbContext(provider.ConnectionString);

            var user = await GetMockRandomUserAsync(userStore);
            var userId = user.Id;
            var login = GetMockLoginInfoAsync();
            await userStore.AddLoginAsync(user, login);

            // Verify login exists
            var loginCount = await dbContext.UserLogins.Where(l => l.UserId == userId).CountAsync();
            Assert.AreEqual(1, loginCount, $"Failed for provider: {provider.DisplayName}");

            // Act
            var result = await userStore.DeleteAsync(user);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var loginCountAfterDelete = await dbContext.UserLogins.Where(l => l.UserId == userId).CountAsync();
            Assert.AreEqual(0, loginCountAfterDelete, $"User logins should be deleted for provider: {provider.DisplayName}");
        }

        #endregion
    }
}
