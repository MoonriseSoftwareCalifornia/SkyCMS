// <copyright file="CosmosUserStoreEmailPasswordTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace AspNetCore.Identity.CosmosDb.Tests.Net9.Stores
{
    /// <summary>
    /// Priority 2 tests for CosmosUserStore email and password management.
    /// Tests FindByEmailAsync, FindByNameAsync, SetPasswordHashAsync, email confirmation, etc.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CosmosUserStoreEmailPasswordTests : CosmosIdentityTestsBase
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

        #region FindByEmailAsync Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByEmailAsync_WithValidEmail_ReturnsCorrectUser(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var foundUser = await userStore.FindByEmailAsync(user.NormalizedEmail);

            // Assert
            Assert.IsNotNull(foundUser, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user.Id, foundUser.Id, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user.Email, foundUser.Email, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByEmailAsync_WithLowercaseEmail_FindsUser(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act - Search with lowercase email (should still find via normalized email)
            var foundUser = await userStore.FindByEmailAsync(user.Email.ToLowerInvariant());

            // Assert
            Assert.IsNotNull(foundUser, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user.Id, foundUser.Id, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByEmailAsync_WithNonExistentEmail_ReturnsNull(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var nonExistentEmail = $"nonexistent_{Guid.NewGuid():N}@testdomain.com";

            // Act
            var foundUser = await userStore.FindByEmailAsync(nonExistentEmail.ToUpper());

            // Assert
            Assert.IsNull(foundUser, $"Should return null for non-existent email on provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByEmailAsync_WithNullEmail_ReturnsNull(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            // Act
            var foundUser = await userStore.FindByEmailAsync(null);

            // Assert
            Assert.IsNull(foundUser, $"Should return null for null email on provider: {provider.DisplayName}");
        }

        #endregion

        #region FindByNameAsync Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByNameAsync_WithValidUsername_ReturnsCorrectUser(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var foundUser = await userStore.FindByNameAsync(user.NormalizedUserName);

            // Assert
            Assert.IsNotNull(foundUser, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user.Id, foundUser.Id, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user.UserName, foundUser.UserName, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByNameAsync_IsCaseInsensitive(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act - Search with different casing
            var foundUserUpper = await userStore.FindByNameAsync(user.UserName.ToUpperInvariant());
            var foundUserLower = await userStore.FindByNameAsync(user.UserName.ToLowerInvariant());

            // Assert
            Assert.IsNotNull(foundUserUpper, $"Failed for provider: {provider.DisplayName}");
            Assert.IsNotNull(foundUserLower, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user.Id, foundUserUpper.Id, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user.Id, foundUserLower.Id, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByNameAsync_WithNonExistentUsername_ReturnsNull(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var nonExistentUsername = $"nonexistent_{Guid.NewGuid():N}";

            // Act
            var foundUser = await userStore.FindByNameAsync(nonExistentUsername.ToUpper());

            // Assert
            Assert.IsNull(foundUser, $"Should return null for non-existent username on provider: {provider.DisplayName}");
        }

        #endregion

        #region Password Hash Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetPasswordHashAsync_WithValidHash_SetsPassword(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);
            var passwordHash = "AQAAAAIAAYagAAAAEDummyHashForTestingPurposes12345";

            // Act
            await userStore.SetPasswordHashAsync(user, passwordHash, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var retrievedHash = await userStore.GetPasswordHashAsync(user, CancellationToken.None);
            Assert.AreEqual(passwordHash, retrievedHash, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetPasswordHashAsync_ForUserWithoutPassword_ReturnsNull(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var hash = await userStore.GetPasswordHashAsync(user, CancellationToken.None);

            // Assert - New users should not have a password hash
            Assert.IsNull(hash, $"New user should not have password hash for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task HasPasswordAsync_WithPassword_ReturnsTrue(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);
            var passwordHash = "AQAAAAIAAYagAAAAETestHash123456";
            await userStore.SetPasswordHashAsync(user, passwordHash, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Act
            var hasPassword = await userStore.HasPasswordAsync(user, CancellationToken.None);

            // Assert
            Assert.IsTrue(hasPassword, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task HasPasswordAsync_WithoutPassword_ReturnsFalse(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var hasPassword = await userStore.HasPasswordAsync(user, CancellationToken.None);

            // Assert
            Assert.IsFalse(hasPassword, $"Failed for provider: {provider.DisplayName}");
        }

        #endregion

        #region Email Confirmation Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetEmailConfirmedAsync_ForNewUser_ReturnsFalse(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var emailConfirmed = await userStore.GetEmailConfirmedAsync(user, CancellationToken.None);

            // Assert
            Assert.IsFalse(emailConfirmed, $"New user email should not be confirmed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetEmailConfirmedAsync_ToTrue_UpdatesEmailConfirmation(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);
            Assert.IsFalse(user.EmailConfirmed, $"Failed for provider: {provider.DisplayName}");

            // Act
            await userStore.SetEmailConfirmedAsync(user, true, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var emailConfirmed = await userStore.GetEmailConfirmedAsync(updatedUser, CancellationToken.None);
            Assert.IsTrue(emailConfirmed, $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(updatedUser.EmailConfirmed, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetEmailConfirmedAsync_ToFalse_UpdatesEmailConfirmation(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);
            await userStore.SetEmailConfirmedAsync(user, true, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Act
            await userStore.SetEmailConfirmedAsync(user, false, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var emailConfirmed = await userStore.GetEmailConfirmedAsync(updatedUser, CancellationToken.None);
            Assert.IsFalse(emailConfirmed, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetEmailAsync_UpdatesEmailAddress(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);
            var newEmail = $"newemail_{Guid.NewGuid():N}@testdomain.com";

            // Act
            await userStore.SetEmailAsync(user, newEmail, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var retrievedEmail = await userStore.GetEmailAsync(updatedUser, CancellationToken.None);
            Assert.AreEqual(newEmail, retrievedEmail, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetNormalizedEmailAsync_ReturnsNormalizedEmail(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var normalizedEmail = await userStore.GetNormalizedEmailAsync(user, CancellationToken.None);

            // Assert
            Assert.IsNotNull(normalizedEmail, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user.Email.ToUpperInvariant(), normalizedEmail, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetNormalizedEmailAsync_UpdatesNormalizedEmail(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);
            var newNormalizedEmail = $"NEWEMAIL_{Guid.NewGuid():N}@TESTDOMAIN.COM";

            // Act
            await userStore.SetNormalizedEmailAsync(user, newNormalizedEmail, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var retrievedNormalizedEmail = await userStore.GetNormalizedEmailAsync(updatedUser, CancellationToken.None);
            Assert.AreEqual(newNormalizedEmail, retrievedNormalizedEmail, $"Failed for provider: {provider.DisplayName}");
        }

        #endregion
    }
}
