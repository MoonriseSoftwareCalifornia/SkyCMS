// <copyright file="CosmosRoleStoreTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AspNetCore.Identity.CosmosDb.Tests.Net9.Stores
{
    /// <summary>
    /// Priority 2 tests for CosmosRoleStore operations.
    /// Tests CreateAsync, UpdateAsync, DeleteAsync, FindByNameAsync, and claims management.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CosmosRoleStoreExtendedTests : CosmosIdentityTestsBase
    {
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
        public async Task CreateAsync_WithValidRole_CreatesRole(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var roleName = $"TestRole_{Guid.NewGuid():N}";
            var role = new IdentityRole(roleName)
            {
                Id = Guid.NewGuid().ToString(),
                NormalizedName = roleName.ToUpper()
            };

            // Act
            var result = await roleStore.CreateAsync(role, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var retrievedRole = await roleStore.FindByIdAsync(role.Id, CancellationToken.None);
            Assert.IsNotNull(retrievedRole, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(roleName, retrievedRole.Name, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task CreateAsync_WithNullRole_ThrowsArgumentNullException(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            // Act & Assert
            try
            {
                await roleStore.CreateAsync(null, CancellationToken.None);
                Assert.Fail($"Expected ArgumentNullException for provider: {provider.DisplayName}");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task CreateAsync_WithDuplicateRoleName_ReturnsFailed(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var roleName = $"DuplicateRole_{Guid.NewGuid():N}";
            var role1 = new IdentityRole(roleName)
            {
                Id = Guid.NewGuid().ToString(),
                NormalizedName = roleName.ToUpper()
            };
            await roleStore.CreateAsync(role1, CancellationToken.None);

            var role2 = new IdentityRole(roleName)
            {
                Id = Guid.NewGuid().ToString(),
                NormalizedName = roleName.ToUpper()
            };

            // Act
            var result = await roleStore.CreateAsync(role2, CancellationToken.None);

            // Assert
            Assert.IsFalse(result.Succeeded, $"Should fail for duplicate role name on provider: {provider.DisplayName}");
        }

        #endregion

        #region UpdateAsync Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task UpdateAsync_WithValidChanges_UpdatesRole(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);
            var newRoleName = $"UpdatedRole_{Guid.NewGuid():N}";

            // Act
            await roleStore.SetRoleNameAsync(role, newRoleName, CancellationToken.None);
            await roleStore.SetNormalizedRoleNameAsync(role, newRoleName.ToUpper(), CancellationToken.None);
            var result = await roleStore.UpdateAsync(role, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var updatedRole = await roleStore.FindByIdAsync(role.Id, CancellationToken.None);
            Assert.AreEqual(newRoleName, updatedRole.Name, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task UpdateAsync_WithNullRole_ThrowsArgumentNullException(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            // Act & Assert
            try
            {
                await roleStore.UpdateAsync(null, CancellationToken.None);
                Assert.Fail($"Expected ArgumentNullException for provider: {provider.DisplayName}");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }

        #endregion

        #region DeleteAsync Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task DeleteAsync_WithValidRole_DeletesRole(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);
            using var dbContext = _testUtilities.GetDbContext(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);
            var roleId = role.Id;

            // Act
            var result = await roleStore.DeleteAsync(role, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var deletedRole = await roleStore.FindByIdAsync(roleId, CancellationToken.None);
            Assert.IsNull(deletedRole, $"Role should be deleted for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task DeleteAsync_WithNullRole_ThrowsArgumentNullException(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            // Act & Assert
            try
            {
                await roleStore.DeleteAsync(null, CancellationToken.None);
                Assert.Fail($"Expected ArgumentNullException for provider: {provider.DisplayName}");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task DeleteAsync_RemovesAssociatedUserRoles(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);
            using var dbContext = _testUtilities.GetDbContext(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);
            var roleId = role.Id;
            var user = await GetMockRandomUserAsync(userStore);

            // Add user to role
            await userStore.AddToRoleAsync(user, role.NormalizedName, CancellationToken.None);

            // Verify association exists
            var userRoleCount = await dbContext.UserRoles.Where(ur => ur.RoleId == roleId).CountAsync();
            Assert.AreEqual(1, userRoleCount, $"Failed for provider: {provider.DisplayName}");

            // Act
            var result = await roleStore.DeleteAsync(role, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var userRoleCountAfterDelete = await dbContext.UserRoles.Where(ur => ur.RoleId == roleId).CountAsync();
            Assert.AreEqual(0, userRoleCountAfterDelete, $"User role associations should be deleted for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task DeleteAsync_RemovesAssociatedRoleClaims(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);
            using var dbContext = _testUtilities.GetDbContext(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);
            var roleId = role.Id;
            var claim = new Claim("TestType", "TestValue");

            await roleStore.AddClaimAsync(role, claim, CancellationToken.None);

            // Verify claim exists
            var claimCount = await dbContext.RoleClaims.Where(rc => rc.RoleId == roleId).CountAsync();
            Assert.AreEqual(1, claimCount, $"Failed for provider: {provider.DisplayName}");

            // Act
            var result = await roleStore.DeleteAsync(role, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var claimCountAfterDelete = await dbContext.RoleClaims.Where(rc => rc.RoleId == roleId).CountAsync();
            Assert.AreEqual(0, claimCountAfterDelete, $"Role claims should be deleted for provider: {provider.DisplayName}");
        }

        #endregion

        #region FindByNameAsync Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByNameAsync_WithValidName_ReturnsCorrectRole(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);

            // Act
            var foundRole = await roleStore.FindByNameAsync(role.NormalizedName, CancellationToken.None);

            // Assert
            Assert.IsNotNull(foundRole, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(role.Id, foundRole.Id, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(role.Name, foundRole.Name, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByNameAsync_IsCaseInsensitive(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);

            // Act
            var foundRoleUpper = await roleStore.FindByNameAsync(role.Name.ToUpperInvariant(), CancellationToken.None);
            var foundRoleLower = await roleStore.FindByNameAsync(role.Name.ToLowerInvariant(), CancellationToken.None);

            // Assert
            Assert.IsNotNull(foundRoleUpper, $"Failed for provider: {provider.DisplayName}");
            Assert.IsNotNull(foundRoleLower, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(role.Id, foundRoleUpper.Id, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(role.Id, foundRoleLower.Id, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByNameAsync_WithNonExistentName_ReturnsNull(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var nonExistentName = $"NonExistent_{Guid.NewGuid():N}";

            // Act
            var foundRole = await roleStore.FindByNameAsync(nonExistentName, CancellationToken.None);

            // Assert
            Assert.IsNull(foundRole, $"Should return null for non-existent role name on provider: {provider.DisplayName}");
        }

        #endregion

        #region Claims Management Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task AddClaimAsync_AddsClaimToRole(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);
            var claim = new Claim("Permission", "CanEdit");

            // Act
            await roleStore.AddClaimAsync(role, claim, CancellationToken.None);

            // Assert
            var claims = await roleStore.GetClaimsAsync(role, CancellationToken.None);
            Assert.AreEqual(1, claims.Count, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual("Permission", claims[0].Type, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual("CanEdit", claims[0].Value, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task RemoveClaimAsync_RemovesClaimFromRole(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);
            var claim = new Claim("Permission", "CanDelete");

            await roleStore.AddClaimAsync(role, claim, CancellationToken.None);
            var claimsBeforeRemove = await roleStore.GetClaimsAsync(role, CancellationToken.None);
            Assert.AreEqual(1, claimsBeforeRemove.Count, $"Failed for provider: {provider.DisplayName}");

            // Act
            await roleStore.RemoveClaimAsync(role, claim, CancellationToken.None);

            // Assert
            var claimsAfterRemove = await roleStore.GetClaimsAsync(role, CancellationToken.None);
            Assert.AreEqual(0, claimsAfterRemove.Count, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetClaimsAsync_ForRoleWithoutClaims_ReturnsEmptyList(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);

            // Act
            var claims = await roleStore.GetClaimsAsync(role, CancellationToken.None);

            // Assert
            Assert.IsNotNull(claims, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(0, claims.Count, $"New role should not have claims for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task AddMultipleClaims_AllClaimsArePersisted(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);
            var claim1 = new Claim("Permission", "CanRead");
            var claim2 = new Claim("Permission", "CanWrite");
            var claim3 = new Claim("Department", "IT");

            // Act
            await roleStore.AddClaimAsync(role, claim1, CancellationToken.None);
            await roleStore.AddClaimAsync(role, claim2, CancellationToken.None);
            await roleStore.AddClaimAsync(role, claim3, CancellationToken.None);

            // Assert
            var claims = await roleStore.GetClaimsAsync(role, CancellationToken.None);
            Assert.AreEqual(3, claims.Count, $"Failed for provider: {provider.DisplayName}");
        }

        #endregion

        #region Additional Role Property Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetRoleIdAsync_ReturnsCorrectId(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);

            // Act
            var roleId = await roleStore.GetRoleIdAsync(role, CancellationToken.None);

            // Assert
            Assert.AreEqual(role.Id, roleId, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetRoleNameAsync_ReturnsCorrectName(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);

            // Act
            var roleName = await roleStore.GetRoleNameAsync(role, CancellationToken.None);

            // Assert
            Assert.AreEqual(role.Name, roleName, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetNormalizedRoleNameAsync_ReturnsNormalizedName(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString);

            var role = await GetMockRandomRoleAsync(roleStore);

            // Act
            var normalizedName = await roleStore.GetNormalizedRoleNameAsync(role, CancellationToken.None);

            // Assert
            Assert.AreEqual(role.NormalizedName, normalizedName, $"Failed for provider: {provider.DisplayName}");
        }

        #endregion
    }
}
