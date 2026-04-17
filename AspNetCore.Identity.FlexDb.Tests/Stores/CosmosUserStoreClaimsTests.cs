using System.Security.Claims;

namespace AspNetCore.Identity.CosmosDb.Tests.Net9.Stores
{
    /// <summary>
    /// Tests user store claim-related operations (add/replace/remove/get users for claim)
    /// across supported database providers to ensure claim handling is consistent.
    /// </summary>
    [TestClass()]
    [DoNotParallelize]
    public class CosmosUserStoreClaimsTests : CosmosIdentityTestsBase
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
            // Initialize utilities if not already done
            if (_testUtilities == null)
            {
                _testUtilities = new TestUtilities();
            }
            if (_random == null)
            {
                _random = new Random();
            }
        }

        #region methods implementing IUserClaimStore<TUserEntity>

        /// <summary>
        /// Consolidated CRUD tests for claim operations on the user store: add, replace,
        /// and remove claims, verifying expected claim sets after each operation.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task Consolidated_ClaimsAsync_CRUD_Tests(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString);
            var user1 = await GetMockRandomUserAsync(userStore);

            // Clean up claims before starting
            var claims = await userStore.GetClaimsAsync(user1, default);
            if (claims.Any())
            {
                await userStore.RemoveClaimsAsync(user1, claims, default);
            }

            var claim = new Claim[] { GetMockClaim("a"), GetMockClaim("b"), GetMockClaim("c") };
            var newClaim = GetMockClaim("d");

            await userStore.AddClaimsAsync(user1, claim, default);

            // Act - Create
            var result2 = await userStore.GetClaimsAsync(user1, default);

            // Assert - Create
            Assert.AreEqual(3, result2.Count, $"Failed for provider: {provider.DisplayName}");

            // Act - Replace
            await userStore.ReplaceClaimAsync(user1, claim.FirstOrDefault(), newClaim, default);

            // test - Replace
            var result3 = await userStore.GetClaimsAsync(user1, default);
            Assert.IsFalse(result3.Any(a => a.Type == claim.FirstOrDefault().Type), $"Failed for provider: {provider.DisplayName}");

            var testAny = result3.Any(a => a.Type == newClaim.Type);
            if (!testAny)
            {
                throw new Exception($"Replace failed for {provider.DisplayName} with {result3.Count} with types {string.Join(",", result3.Select(s => s.Type).ToArray())}.");
            }

            Assert.IsTrue(testAny, $"Failed for provider: {provider.DisplayName}");

            // Act - Delete
            await userStore.RemoveClaimsAsync(user1, result3, default);
            var result4 = await userStore.GetClaimsAsync(user1, default);
            Assert.IsFalse(result4.Any(), $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Adds the same claim to multiple users and verifies GetUsersForClaimAsync
        /// returns all users that have the claim.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetUsersForClaimAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            var val = Guid.NewGuid().ToString();
            var claims = new Claim[] { new Claim(val, val) };

            using (var userStore = _testUtilities.GetUserStore(provider.ConnectionString))
            {
                ;
                var user1 = await GetMockRandomUserAsync(userStore);
                await userStore.AddClaimsAsync(user1, claims, default);
            }

            using (var userStore = _testUtilities.GetUserStore(provider.ConnectionString))
            {
                var user2 = await GetMockRandomUserAsync(userStore);
                await userStore.AddClaimsAsync(user2, claims, default);
            }

            using (var userStore = _testUtilities.GetUserStore(provider.ConnectionString))
            {
                // Act
                var result1 = await userStore.GetUsersForClaimAsync(claims.FirstOrDefault(), default);

                // Assert
                Assert.AreEqual(2, result1.Count, $"Failed for provider: {provider.DisplayName}");
            }
        }

        #endregion
    }
}
