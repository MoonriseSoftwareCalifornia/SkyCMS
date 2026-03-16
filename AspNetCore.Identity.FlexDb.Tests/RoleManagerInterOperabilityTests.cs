using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace AspNetCore.Identity.CosmosDb.Tests.Net9
{
    /// <summary>
    /// Tests RoleManager interoperability across supported database providers.
    /// Covers create/delete/find/update operations, claims management, role name/id retrieval,
    /// normalization, and existence checks to ensure consistent behavior across providers.
    /// </summary>
    [TestClass()]
    [DoNotParallelize]
    public class RoleManagerInterOperabilityTests : CosmosIdentityTestsBase
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

        // Creates a new test role using the mock RoleManager to do so
        private async Task<IdentityRole> GetTestRole(RoleManager<IdentityRole> roleManager)
        {
            var role = await GetMockRandomRoleAsync(null, false);

            var result = await roleManager.CreateAsync(role);

            Assert.IsTrue(result.Succeeded);

            return await roleManager.FindByIdAsync(role.Id);
        }

        /// <summary>
        /// Adds then removes a claim for a role and verifies claim counts during the sequence.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task Consolidated_ClaimsAsync_Tests(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);
            var claim = new Claim(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

            // Act - Add a claim
            var result1 = await roleManager.AddClaimAsync(role, claim);

            // Assert - Add a claim
            Assert.IsTrue(result1.Succeeded, $"Failed for provider: {provider.DisplayName}");
            var result2 = await roleManager.GetClaimsAsync(role);
            Assert.AreEqual(1, result2?.Count, $"Failed for provider: {provider.DisplayName}");

            // Act - Remove a claim
            var result3 = await roleManager.RemoveClaimAsync(role, claim);

            // Assert
            Assert.IsTrue(result3.Succeeded, $"Failed for provider: {provider.DisplayName}");
            var result4 = await roleManager.GetClaimsAsync(role);
            Assert.AreEqual(0, result4.Count, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Creates a role via RoleManager.CreateAsync and asserts the call succeeds.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task CreateAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName);
            using var roleManager = GetTestRoleManager(roleStore);
            var role = new IdentityRole();
            role.Name = Guid.NewGuid().ToString();
            role.NormalizedName = role.Name.ToLowerInvariant();
            role.Id = Guid.NewGuid().ToString();

            // Act
            var result1 = await roleManager.CreateAsync(role);

            // Assert
            Assert.IsTrue(result1.Succeeded, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Deletes a role and verifies it can no longer be found by id.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task DeleteAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);
            var id = role.Id;

            // Act
            var result1 = await roleManager.DeleteAsync(role);

            // Assert
            Assert.IsTrue(result1.Succeeded, $"Failed for provider: {provider.DisplayName}");
            var result2 = await roleManager.FindByIdAsync(id);
            Assert.IsNull(result2, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Finds a role by id and asserts the returned role matches the created role.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task FindByIdAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);
            var id = role.Id;

            // Act
            var result = await roleManager.FindByIdAsync(id);

            // Assert
            Assert.IsNotNull(result, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(id, result.Id, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Finds a role by name and asserts the returned role has the expected name.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task FindByNameAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);
            var name = role.Name;

            // Act
            var result = await roleManager.FindByNameAsync(name);

            // Assert
            Assert.IsNotNull(result, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(name, result.Name, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Adds multiple claims to a role and verifies GetClaimsAsync returns them.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task GetClaimsAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);
            var claim1 = new Claim(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            var claim2 = new Claim(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            var result1 = await roleManager.AddClaimAsync(role, claim1);
            var result2 = await roleManager.AddClaimAsync(role, claim2);
            Assert.IsTrue(result1.Succeeded, $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(result2.Succeeded, $"Failed for provider: {provider.DisplayName}");

            // Act
            var result = await roleManager.GetClaimsAsync(role);

            // Assert
            Assert.AreEqual(2, result.Count, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Retrieves the role id using the RoleManager API and asserts it matches.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task GetRoleIdAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);

            // Act
            var result = await roleManager.GetRoleIdAsync(role);

            // Assert
            Assert.AreEqual(role.Id, result, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Retrieves the role name using the RoleManager API and asserts it matches.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task GetRoleNameAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);

            // Act
            var result = await roleManager.GetRoleNameAsync(role);

            // Assert
            Assert.AreEqual(role.Name, result, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Verifies the RoleManager.NormalizeKey function returns an upper-case key.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task NormalizeKeyTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);
            var key = Guid.NewGuid().ToString();

            // Act
            var result = roleManager.NormalizeKey(key);

            // Assert
            Assert.AreEqual(key.ToUpperInvariant(), result, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Checks RoleExistsAsync returns true for a role that was created.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task RoleExistsAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);

            // Act
            var result = await roleManager.RoleExistsAsync(role.Name);

            // Assert
            Assert.IsTrue(result, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Sets a role's name via RoleManager and verifies the persisted value.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task SetRoleNameAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);
            var name = Guid.NewGuid().ToString();

            // Act
            var result = await roleManager.SetRoleNameAsync(role, name);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");
            var result2 = await roleManager.FindByIdAsync(role.Id);
            Assert.AreEqual(name, result2.Name, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Calls UpdateAsync on a role and asserts the operation succeeds.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task UpdateAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);

            // Act
            var result = await roleManager.UpdateAsync(role);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Updates a role's normalized name and verifies the normalized value persisted.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task UpdateNormalizedRoleNameAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);
            var name = Guid.NewGuid().ToString();
            var result = await roleManager.SetRoleNameAsync(role, name);
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");
            var result2 = await roleManager.FindByIdAsync(role.Id);
            Assert.AreEqual(name, result2.Name, $"Failed for provider: {provider.DisplayName}");

            // Act
            await roleManager.UpdateNormalizedRoleNameAsync(role);

            // Assert
            var result3 = await roleManager.FindByIdAsync(role.Id);
            Assert.AreEqual(name.ToUpperInvariant(), result3.NormalizedName, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Mutates a role's name and normalized name, updates it, and verifies changes persisted.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders), DynamicDataSourceType.Method)]
        public async Task UpdateRoleAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var roleManager = GetTestRoleManager(_testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName));
            var role = await GetTestRole(roleManager);
            role.Name = role.Name + "-A";
            role.NormalizedName = role.NormalizedName + "-A";
            var n = role.Name;
            var nn = role.NormalizedName;

            // Act
            var result1 = await roleManager.UpdateAsync(role);

            // Assert
            Assert.IsTrue(result1.Succeeded, $"Failed for provider: {provider.DisplayName}");
            var result2 = await roleManager.FindByIdAsync(role.Id);
            Assert.AreEqual(n, result2.Name, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(nn, result2.NormalizedName, $"Failed for provider: {provider.DisplayName}");
        }
    }
}
