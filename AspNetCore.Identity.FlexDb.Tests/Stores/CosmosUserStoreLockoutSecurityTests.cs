// <copyright file="CosmosUserStoreLockoutSecurityTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace AspNetCore.Identity.CosmosDb.Tests.Net9.Stores
{
    /// <summary>
    /// Priority 2 tests for CosmosUserStore lockout and security features.
    /// Tests SetLockoutEndDateAsync, IncrementAccessFailedCountAsync, and account lockout scenarios.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CosmosUserStoreLockoutSecurityTests : CosmosIdentityTestsBase
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

        #region Lockout End Date Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetLockoutEndDateAsync_WithFutureDate_LocksAccount(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);
            var lockoutEnd = DateTimeOffset.UtcNow.AddHours(1);

            // Act
            await userStore.SetLockoutEndDateAsync(user, lockoutEnd, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var retrievedLockoutEnd = await userStore.GetLockoutEndDateAsync(updatedUser, CancellationToken.None);

            Assert.IsNotNull(retrievedLockoutEnd, $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(retrievedLockoutEnd.Value > DateTimeOffset.UtcNow, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetLockoutEndDateAsync_WithNull_UnlocksAccount(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);
            var lockoutEnd = DateTimeOffset.UtcNow.AddHours(1);
            await userStore.SetLockoutEndDateAsync(user, lockoutEnd, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Act - Clear lockout
            await userStore.SetLockoutEndDateAsync(user, null, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var retrievedLockoutEnd = await userStore.GetLockoutEndDateAsync(updatedUser, CancellationToken.None);

            Assert.IsNull(retrievedLockoutEnd, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetLockoutEndDateAsync_ForNewUser_ReturnsNull(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var lockoutEnd = await userStore.GetLockoutEndDateAsync(user, CancellationToken.None);

            // Assert
            Assert.IsNull(lockoutEnd, $"New user should not be locked out for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetLockoutEnabledAsync_ForNewUser_ReturnsTrue(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var lockoutEnabled = await userStore.GetLockoutEnabledAsync(user, CancellationToken.None);

            // Assert
            Assert.IsTrue(lockoutEnabled, $"Lockout should be enabled by default for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetLockoutEnabledAsync_ToFalse_DisablesLockout(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act
            await userStore.SetLockoutEnabledAsync(user, false, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var lockoutEnabled = await userStore.GetLockoutEnabledAsync(updatedUser, CancellationToken.None);

            Assert.IsFalse(lockoutEnabled, $"Failed for provider: {provider.DisplayName}");
        }

        #endregion

        #region Access Failed Count Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task IncrementAccessFailedCountAsync_IncrementsCounter(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);
            var initialCount = await userStore.GetAccessFailedCountAsync(user, CancellationToken.None);

            // Act
            var newCount = await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var finalCount = await userStore.GetAccessFailedCountAsync(updatedUser, CancellationToken.None);

            Assert.AreEqual(initialCount + 1, newCount, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(newCount, finalCount, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task IncrementAccessFailedCount_MultipleIncrements_TracksCorrectly(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act - Increment 3 times
            await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);
            await userStore.UpdateAsync(user);

            await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);
            await userStore.UpdateAsync(user);

            await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var finalCount = await userStore.GetAccessFailedCountAsync(updatedUser, CancellationToken.None);

            Assert.AreEqual(3, finalCount, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task ResetAccessFailedCountAsync_ResetsToZero(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Increment multiple times
            await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);
            await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Act
            await userStore.ResetAccessFailedCountAsync(user, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var finalCount = await userStore.GetAccessFailedCountAsync(updatedUser, CancellationToken.None);

            Assert.AreEqual(0, finalCount, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetAccessFailedCountAsync_ForNewUser_ReturnsZero(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var count = await userStore.GetAccessFailedCountAsync(user, CancellationToken.None);

            // Assert
            Assert.AreEqual(0, count, $"New user should have zero failed access attempts for provider: {provider.DisplayName}");
        }

        #endregion

        #region Integration Scenario Tests

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task AccountLockout_AfterMaxFailedAttempts_LocksAccount(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);
            const int maxFailedAttempts = 5;

            // Act - Simulate failed login attempts
            for (int i = 0; i < maxFailedAttempts; i++)
            {
                await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);
                await userStore.UpdateAsync(user);
            }

            var failedCount = await userStore.GetAccessFailedCountAsync(user, CancellationToken.None);

            // Simulate lockout after threshold
            if (failedCount >= maxFailedAttempts)
            {
                var lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
                await userStore.SetLockoutEndDateAsync(user, lockoutEnd, CancellationToken.None);
                await userStore.UpdateAsync(user);
            }

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var lockoutEndDate = await userStore.GetLockoutEndDateAsync(updatedUser, CancellationToken.None);

            Assert.AreEqual(maxFailedAttempts, failedCount, $"Failed for provider: {provider.DisplayName}");
            Assert.IsNotNull(lockoutEndDate, $"Account should be locked for provider: {provider.DisplayName}");
            Assert.IsTrue(lockoutEndDate.Value > DateTimeOffset.UtcNow, $"Failed for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SuccessfulLogin_ResetsAccessFailedCount(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Simulate failed attempts
            await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);
            await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);
            await userStore.UpdateAsync(user);

            var failedCount = await userStore.GetAccessFailedCountAsync(user, CancellationToken.None);
            Assert.AreEqual(2, failedCount, $"Failed for provider: {provider.DisplayName}");

            // Act - Simulate successful login
            await userStore.ResetAccessFailedCountAsync(user, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var finalCount = await userStore.GetAccessFailedCountAsync(updatedUser, CancellationToken.None);

            Assert.AreEqual(0, finalCount, $"Failed count should reset after successful login for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task LockoutDisabled_AllowsLoginDespiteFailedAttempts(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Disable lockout
            await userStore.SetLockoutEnabledAsync(user, false, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Act - Increment failed attempts
            for (int i = 0; i < 10; i++)
            {
                await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);
            }
            await userStore.UpdateAsync(user);

            // Assert
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var lockoutEnabled = await userStore.GetLockoutEnabledAsync(updatedUser, CancellationToken.None);
            var lockoutEnd = await userStore.GetLockoutEndDateAsync(updatedUser, CancellationToken.None);

            Assert.IsFalse(lockoutEnabled, $"Lockout should be disabled for provider: {provider.DisplayName}");
            Assert.IsNull(lockoutEnd, $"Account should not be locked when lockout is disabled for provider: {provider.DisplayName}");
        }

        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task ExpiredLockout_AllowsLogin(TestDatabaseProvider provider)
        {
            // Arrange
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            var user = await GetMockRandomUserAsync(userStore);

            // Set lockout to past date (expired)
            var expiredLockout = DateTimeOffset.UtcNow.AddMinutes(-1);
            await userStore.SetLockoutEndDateAsync(user, expiredLockout, CancellationToken.None);
            await userStore.UpdateAsync(user);

            // Act
            var updatedUser = await userStore.FindByIdAsync(user.Id);
            var lockoutEnd = await userStore.GetLockoutEndDateAsync(updatedUser, CancellationToken.None);

            // Assert
            Assert.IsNotNull(lockoutEnd, $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(lockoutEnd.Value < DateTimeOffset.UtcNow, $"Lockout should be expired for provider: {provider.DisplayName}");
        }

        #endregion
    }
}
