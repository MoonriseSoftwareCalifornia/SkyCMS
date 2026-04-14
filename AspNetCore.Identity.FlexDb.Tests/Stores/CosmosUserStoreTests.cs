using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Identity.CosmosDb.Tests.Net9.Stores
{
    /// <summary>
    /// Tests user store implementations across supported providers.
    /// Includes CRUD operations, login/role/claim management, and various getters/setters
    /// to validate that user-related persistence behaves consistently.
    /// </summary>
    [TestClass()]
    [DoNotParallelize]
    public class CosmosUserStoreTests : CosmosIdentityTestsBase
    {
        private static string phoneNumber = "0000000000";

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

        /// <summary>
        /// Create an IdentityUser test
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task CreateAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);

            // Create a bunch of users in rapid succession
            for (int i = 0; i < 35; i++)
            {
                var r = await GetMockRandomUserAsync(userStore);
            }

            // Arrange - setup the new user with UNIQUE email
            var uniqueEmail = $"test_{Guid.NewGuid():N}@testdomain.com";
            var user = new IdentityUser(uniqueEmail) { Email = uniqueEmail };
            user.NormalizedUserName = user.UserName.ToUpper();
            user.NormalizedEmail = user.Email.ToUpper();
            user.Id = Guid.NewGuid().ToString(); // Use unique ID per test run

            // Act - create the user
            var result = await userStore.CreateAsync(user);

            // Assert - User should have been created
            Assert.IsNotNull(result, $"Failed for provider: {provider.DisplayName}");

            // IMPROVED: Show actual error messages if creation fails
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                Assert.Fail($"Failed for provider: {provider.DisplayName}. Errors: {errors}");
            }

            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");

            var user2 = await userStore.FindByIdAsync(user.Id);

            Assert.IsNotNull(user2, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user2.UserName, uniqueEmail, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user2.Email, uniqueEmail, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user2.NormalizedUserName, uniqueEmail.ToUpper(), $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user2.NormalizedEmail, uniqueEmail.ToUpper(), $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Deletes a user and verifies related user claims, logins, and role links are removed.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task DeleteAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange - setup the new user
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName);
            using var dbContext = _testUtilities.GetDbContext(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var userId = user.Id;
            var role = await GetMockRandomRoleAsync(roleStore);
            var claim = GetMockClaim();
            var login = GetMockLoginInfoAsync();
            await userStore.AddClaimsAsync(user, new[] { claim });
            await userStore.AddLoginAsync(user, login);
            await userStore.AddToRoleAsync(user, role.NormalizedName);

            // Act
            var result = await userStore.DeleteAsync(user);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(await dbContext.Users.Where(a => a.Id == userId).CountAsync() == 0, $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(await dbContext.UserClaims.Where(a => a.UserId == userId).CountAsync() == 0, $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(await dbContext.UserLogins.Where(a => a.UserId == userId).CountAsync() == 0, $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(await dbContext.UserRoles.Where(a => a.UserId == userId).CountAsync() == 0, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Finds a user by email (case-insensitive via normalized email) and asserts match.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByEmailAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var user1 = await userStore.FindByEmailAsync(user.Email.ToUpper());

            // Assert
            Assert.IsNotNull(user1, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user.Email, user1.Email, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Finds a user by id and asserts the returned user matches the created user.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByIdAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var user1 = await userStore.FindByIdAsync(user.Id);

            // Assert
            Assert.IsNotNull(user1, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual(user.Id, user1.Id, $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Finds a user by normalized username and asserts the returned user matches.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByNameAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var user1 = await userStore.FindByNameAsync(user.UserName.ToUpper());

            // Assert
            Assert.IsNotNull(user);
            Assert.AreEqual(user.UserName, user1.UserName);
        }

        /// <summary>
        /// Finds a user by normalized email (via FindByNameAsync in this test) and asserts username match.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByNameEmailAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var user1 = await userStore.FindByNameAsync(user.Email.ToUpper());

            // Assert
            Assert.IsNotNull(user);
            Assert.AreEqual(user.UserName, user1.UserName);
        }

        /// <summary>
        /// Retrieves a user's email via the store API and asserts it matches the persisted email.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetEmailAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var result = await userStore.GetEmailAsync(user);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(user.Email, result);
        }

        /// <summary>
        /// Verifies the email confirmed flag can be read and set via the store API.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetEmailConfirmedAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var result = await userStore.GetEmailConfirmedAsync(user);
            Assert.IsFalse(result);

            // Arrange - user name and email are the same with this test
            await userStore.SetEmailConfirmedAsync(user, true);

            // Act
            result = await userStore.GetEmailConfirmedAsync(user);

            // Assert
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Duplicate/emphasis test for email confirmed behavior; verifies set and get semantics.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetEmailConfirmedAsyncTestFail(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var result = await userStore.GetEmailConfirmedAsync(user);
            Assert.IsFalse(result);
            await userStore.SetEmailConfirmedAsync(user, true);

            // Act
            result = await userStore.GetEmailConfirmedAsync(user);

            // Assert
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Retrieves the normalized email for a user and asserts it matches the expected value.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetNormalizedEmailAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var result = await userStore.GetNormalizedEmailAsync(user);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(user.NormalizedEmail, result);
        }

        /// <summary>
        /// Retrieves the normalized username for a user and asserts it matches the expected value.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetNormalizedUserNameAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var result = await userStore.GetNormalizedUserNameAsync(user);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(user.NormalizedUserName, result);
        }

        /// <summary>
        /// Verifies get/set behavior for password hash on the user store.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetPasswordHashAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var hash = await userStore.GetPasswordHashAsync(user); // Should be no hash now
            Assert.IsTrue(string.IsNullOrEmpty(hash));
            var password = Guid.NewGuid().ToString(); // Now add hash
            await userStore.SetPasswordHashAsync(user, password);

            // Act
            hash = await userStore.GetPasswordHashAsync(user);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(hash));
            Assert.AreSame(password, hash); // The hash should be different than original
        }

        /// <summary>
        /// Sets and retrieves a user's phone number, asserting persisted equality.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetPhoneNumberAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var phoneNumber = "1234567899";
            await userStore.SetPhoneNumberAsync(user, phoneNumber);
            //user = await userStore.FindByIdAsync(user.Id);

            // Act
            user = await userStore.FindByIdAsync(user.Id);
            var result2 = await userStore.GetPhoneNumberAsync(user);

            // Assert
            Assert.AreSame(phoneNumber, result2);
        }

        /// <summary>
        /// Verifies phone number confirmed flag can be set and retrieved.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetPhoneNumberConfirmedAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            await userStore.SetPhoneNumberAsync(user, phoneNumber);
            //user = await userStore.FindByIdAsync(user.Id);
            await userStore.SetPhoneNumberConfirmedAsync(user, true);
            //user = await userStore.FindByIdAsync(user.Id);

            // Act
            var result = await userStore.GetPhoneNumberConfirmedAsync(user);

            // Assert
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Retrieves a user's id via the store API and asserts it matches the created user's id.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetUserIdAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var result = await userStore.GetUserIdAsync(user);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(user.Id, result);
        }

        /// <summary>
        /// Retrieves a user's username via the store API and asserts equality.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetUserNameAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var result = await userStore.GetUserNameAsync(user);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(user.UserName, result);
        }

        /// <summary>
        /// Verifies HasPasswordAsync returns true after setting a password hash.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task HasPasswordAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var hash = await userStore.GetPasswordHashAsync(user); // Should be no hash now
            Assert.IsTrue(string.IsNullOrEmpty(hash));
            var password = Guid.NewGuid().ToString(); // Now add hash

            await userStore.SetPasswordHashAsync(user, password);

            // Act
            var result1 = await userStore.HasPasswordAsync(user);

            // Assert
            Assert.IsTrue(result1);
        }

        /// <summary>
        /// Sets a user's email via the store and verifies persistence.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetEmailAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            await userStore.SetEmailAsync(user, TestUtilities.IDENUSER2EMAIL);

            // Assert
            var user2 = await userStore.FindByIdAsync(user.Id);

            Assert.IsNotNull(user2);
            Assert.AreEqual(TestUtilities.IDENUSER2EMAIL, user2.Email);

            Assert.AreEqual(user.UserName, user2.UserName);
        }

        /// <summary>
        /// Sets the email confirmed flag and verifies it via the store API.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetEmailConfirmedAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            Assert.IsFalse(user.EmailConfirmed);

            // Act
            await userStore.SetEmailConfirmedAsync(user, true);

            // Assert
            var result = await userStore.GetEmailConfirmedAsync(user);
            user = await userStore.FindByIdAsync(user.Id);
            Assert.IsTrue(user.EmailConfirmed);
            Assert.IsTrue(result);
        }

        // This function is tested with SetEmailAsync().
        /// <summary>
        /// Sets a user's normalized email and verifies the persisted normalized value.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetNormalizedEmailAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var newEmail = $"A{GetNextRandomNumber(111, 9999).ToString()}@foo.com";

            // Act
            await userStore.SetNormalizedEmailAsync(user, newEmail.ToUpper());

            // Assert
            user = await userStore.FindByIdAsync(user.Id);
            Assert.AreEqual(newEmail.ToUpper(), user.NormalizedEmail);
        }

        // This method is tested with SetUserNameAsync().
        /// <summary>
        /// Sets a user's normalized username and verifies the persisted normalized value.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetNormalizedUserNameAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var newEmail = $"A{GetNextRandomNumber(111, 9999).ToString()}@foo.com";

            // Act
            await userStore.SetNormalizedUserNameAsync(user, newEmail.ToUpper());

            // Assert
            var user2 = await userStore.FindByIdAsync(user.Id);
            Assert.AreEqual(newEmail.ToUpper(), user2.NormalizedUserName);
        }

        /// <summary>
        /// Sets a user's password hash and verifies it is persisted.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetPasswordHashAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            Assert.IsTrue(string.IsNullOrEmpty(user.PasswordHash));

            // Act
            await userStore.SetPasswordHashAsync(user, Guid.NewGuid().ToString());

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(user.PasswordHash));


        }

        /// <summary>
        /// Sets a user's phone number and verifies persistence via GetPhoneNumberAsync.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetPhoneNumberAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            Assert.IsTrue(string.IsNullOrEmpty(user.PhoneNumber));

            // Act
            await userStore.SetPhoneNumberAsync(user, phoneNumber);

            // Assert
            var user2 = await userStore.FindByIdAsync(user.Id);
            Assert.AreEqual(phoneNumber, user2.PhoneNumber);
        }

        /// <summary>
        /// Sets the phone number confirmed flag and verifies it via the store API.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetPhoneNumberConfirmedAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            Assert.IsFalse(user.PhoneNumberConfirmed);

            // Act
            await userStore.SetPhoneNumberConfirmedAsync(user, true);

            // Assert
            var result = await userStore.GetPhoneNumberConfirmedAsync(user);
            user = await userStore.FindByIdAsync(user.Id);
            Assert.IsTrue(user.PhoneNumberConfirmed);
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Sets a user's username and verifies the persisted value.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetUserNameAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var newUserName = "A" + user.UserName;

            // Act
            await userStore.SetUserNameAsync(user, newUserName);

            // Assert
            user = await userStore.FindByIdAsync(user.Id);
            Assert.AreEqual(newUserName, user.UserName);

        }

        // This method tested with SetPasswordHashAsyncTest() | UserManager.AddPasswordAsync()
        /// <summary>
        /// Updates several user fields (email, normalized email, phone) and verifies persistence.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task UpdateAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var phoneNumber = "1234567890";

            // Act
            user.Email = TestUtilities.IDENUSER1EMAIL;
            user.NormalizedEmail = TestUtilities.IDENUSER1EMAIL.ToUpper();
            user.PhoneNumber = phoneNumber;

            var result = await userStore.UpdateAsync(user);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Succeeded);

            var user1 = await userStore.FindByIdAsync(user.Id);

            Assert.AreEqual(TestUtilities.IDENUSER1EMAIL, user1.Email);
            Assert.AreEqual(TestUtilities.IDENUSER1EMAIL.ToUpper(), user1.NormalizedEmail);
            Assert.AreEqual(phoneNumber, user1.PhoneNumber);

        }

        /// <summary>
        /// Adds an external login to a user and verifies it appears in GetLoginsAsync.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task AddLoginAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            var loginInfo = GetMockLoginInfoAsync();
            await userStore.AddLoginAsync(user, loginInfo);

            // Assert
            var logins = await userStore.GetLoginsAsync(user);
            Assert.AreEqual(1, logins.Count);
            Assert.IsTrue(logins.Any(a => a.LoginProvider.Equals("Twitter")));

        }

        /// <summary>
        /// Removes an external login from a user and verifies it no longer appears in GetLoginsAsync.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task RemoveLoginAsyncTest(TestDatabaseProvider provider)
        {

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var loginInfo = GetMockLoginInfoAsync();
            await userStore.AddLoginAsync(user, loginInfo);
            var logins = await userStore.GetLoginsAsync(user);
            Assert.AreEqual(1, logins.Count);
            Assert.IsTrue(logins.Any(a => a.LoginProvider.Equals("Twitter")));

            // Act
            await userStore.RemoveLoginAsync(user, "Twitter", loginInfo.ProviderKey);

            // Assert
            logins = await userStore.GetLoginsAsync(user);
            Assert.AreEqual(0, logins.Count);

        }

        /// <summary>
        /// Retrieves external logins for a user and asserts expected providers are present.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetLoginsAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var loginInfo = GetMockLoginInfoAsync();
            await userStore.AddLoginAsync(user, loginInfo);

            // Act
            var logins = await userStore.GetLoginsAsync(user);

            // Assert
            Assert.AreEqual(1, logins.Count);
            Assert.IsTrue(logins.Any(a => a.LoginProvider.Equals("Twitter")));
        }

        /// <summary>
        /// Finds a user by external login provider and provider key, asserting the correct user is returned.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task FindByLoginAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var loginInfo = GetMockLoginInfoAsync();
            await userStore.AddLoginAsync(user, loginInfo);
            var logins = await userStore.GetLoginsAsync(user);
            Assert.AreEqual(1, logins.Count);
            Assert.IsTrue(logins.Any(a => a.LoginProvider.Equals("Twitter")));

            // Arrange
            var user2 = await userStore.FindByLoginAsync("Twitter", loginInfo.ProviderKey);

            // Assert
            Assert.AreEqual(user.Id, user2.Id);
        }

        /// <summary>
        /// Adds a user to a role and verifies membership and users-in-role behavior using normalized names.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task AddToRoleAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var role = await GetMockRandomRoleAsync(roleStore);
            var users = await userStore.GetUsersInRoleAsync(role.NormalizedName); // Use NormalizedName
            Assert.AreEqual(0, users.Count, $"Failed for provider: {provider.DisplayName}"); // Should be no users

            // Act - FIXED: Use role.NormalizedName instead of role.Name
            await userStore.AddToRoleAsync(user, role.NormalizedName);

            // Assert
            Assert.IsTrue(await userStore.IsInRoleAsync(user, role.NormalizedName), $"Failed for provider: {provider.DisplayName}"); // Use NormalizedName
            users = await userStore.GetUsersInRoleAsync(role.NormalizedName); // Use NormalizedName
            Assert.AreEqual(1, users.Count, $"Failed for provider: {provider.DisplayName}"); // Should be one user
            Assert.IsTrue(users.Any(u => u.Id == user.Id), $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Removes a user from a role and verifies the user is no longer reported in that role.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task RemoveFromRoleAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var role = await GetMockRandomRoleAsync(roleStore);
            var users = await userStore.GetUsersInRoleAsync(role.NormalizedName);
            Assert.AreEqual(0, users.Count); // Should be no users
            await userStore.AddToRoleAsync(user, role.NormalizedName);
            Assert.IsTrue(await userStore.IsInRoleAsync(user, role.NormalizedName));
            users = await userStore.GetUsersInRoleAsync(role.NormalizedName);
            Assert.AreEqual(1, users.Count); // Should be one user
            Assert.IsTrue(users.Any(u => u.Id == user.Id));

            // Act
            await userStore.RemoveFromRoleAsync(user, role.NormalizedName);

            // Assert
            users = await userStore.GetUsersInRoleAsync(role.NormalizedName);
            Assert.AreEqual(0, users.Count); // Should be no users

        }

        /// <summary>
        /// Retrieves roles assigned to a user and asserts expected role names are returned.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetRolesAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var role1 = await GetMockRandomRoleAsync(roleStore);
            var role2 = await GetMockRandomRoleAsync(roleStore);

            // FIX: Use NormalizedName for lookups
            var users1 = await userStore.GetUsersInRoleAsync(role1.NormalizedName);
            Assert.AreEqual(0, users1.Count, $"Failed for provider: {provider.DisplayName}"); // Should be no users
            var users2 = await userStore.GetUsersInRoleAsync(role2.NormalizedName);
            Assert.AreEqual(0, users2.Count, $"Failed for provider: {provider.DisplayName}"); // Should be no users

            // FIX: Use NormalizedName instead of Name
            await userStore.AddToRoleAsync(user, role1.NormalizedName);
            await userStore.AddToRoleAsync(user, role2.NormalizedName);

            // FIX: Use NormalizedName for checks
            Assert.IsTrue(await userStore.IsInRoleAsync(user, role1.NormalizedName), $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(await userStore.IsInRoleAsync(user, role2.NormalizedName), $"Failed for provider: {provider.DisplayName}");

            // Act
            var roles = await userStore.GetRolesAsync(user);

            // Assert
            Assert.AreEqual(2, roles.Count, $"Failed for provider: {provider.DisplayName}"); // Should be two
            Assert.IsTrue(roles.Contains(role1.Name), $"Failed for provider: {provider.DisplayName}");
            Assert.IsTrue(roles.Contains(role2.Name), $"Failed for provider: {provider.DisplayName}");
        }

        /// <summary>
        /// Verifies IsInRoleAsync returns true for a user after adding to the role.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task IsInRoleAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);
            var role = await GetMockRandomRoleAsync(roleStore);
            var users = await userStore.GetUsersInRoleAsync(role.NormalizedName);
            Assert.AreEqual(0, users.Count); // Should be no users
            await userStore.AddToRoleAsync(user, role.NormalizedName);

            // Act
            var result = await userStore.IsInRoleAsync(user, role.NormalizedName);

            // Assert
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Verifies GetUsersInRoleAsync returns all users assigned to a given normalized role name.
        /// </summary>
        [TestMethod()]
        [DynamicData(nameof(GetTestProviders))]
        public async Task GetUsersInRoleAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            using var roleStore = _testUtilities.GetRoleStore(provider.ConnectionString, provider.DatabaseName);
            var user1 = await GetMockRandomUserAsync(userStore);
            var user2 = await GetMockRandomUserAsync(userStore);
            var role = await GetMockRandomRoleAsync(roleStore);
            await userStore.AddToRoleAsync(user1, role.NormalizedName);
            await userStore.AddToRoleAsync(user2, role.NormalizedName);

            // Act
            var result = await userStore.GetUsersInRoleAsync(role.NormalizedName);

            // Assert
            Assert.IsTrue(result.Count == 2);
            Assert.IsTrue(result.Any(r => r.Id == user1.Id));
            Assert.IsTrue(result.Any(r => r.Id == user2.Id));

        }

        /// <summary>
        /// Queries the user set and verifies the store exposes an IQueryable and returns results.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task QueryUsersTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user1 = await GetMockRandomUserAsync(userStore);

            // Act
            var result = await userStore.Users.ToListAsync();

            // Assert
            Assert.IsInstanceOfType(userStore.Users, typeof(IQueryable<IdentityUser>));
            Assert.IsTrue(result.Count > 0);
        }

        /// <summary>
        /// Sets and updates the authenticator key for a user and verifies the latest value is persisted.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetTestProviders))]
        public async Task SetAndGetAuthenticatorKeyAsyncTest(TestDatabaseProvider provider)
        {
            InitializeForProvider(provider);

            // Arrange
            using var userStore = _testUtilities.GetUserStore(provider.ConnectionString, provider.DatabaseName);
            var user = await GetMockRandomUserAsync(userStore);

            // Act
            await userStore.SetAuthenticatorKeyAsync(user, "AuthenticatorKey_1", default);
            var firstCode = await userStore.GetAuthenticatorKeyAsync(user, default);

            await userStore.SetAuthenticatorKeyAsync(user, "AuthenticatorKey_2", default);
            var updatedCode = await userStore.GetAuthenticatorKeyAsync(user, default);

            // Assert
            Assert.IsNotNull(firstCode, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual("AuthenticatorKey_1", firstCode, $"Failed for provider: {provider.DisplayName}");
            Assert.AreEqual("AuthenticatorKey_2", updatedCode, $"Failed for provider: {provider.DisplayName}");
        }
    }
}
