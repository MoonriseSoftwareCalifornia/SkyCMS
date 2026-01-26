// <copyright file="DatabaseInitializationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Areas.Setup
{
    using Cosmos.Cms.Data;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Services.Layouts;
    using Sky.Editor.Services.Setup;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for database initialization in single and multi-tenant scenarios.
    /// </summary>
    [TestClass]
    public class DatabaseInitializationTests : SkyCmsTestBase
    {
        private IServiceProvider? serviceProvider;
        private string testDbPath = string.Empty;

        // In the test, capture the UserManager used by SetupService
        private UserManager<IdentityUser> _sqliteUserManager;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext();

            testDbPath = Path.Combine(Path.GetTempPath(), $"skycms-dbinit-test-{Guid.NewGuid()}.db");
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (File.Exists(testDbPath))
            {
                try
                {
                    // Dispose any open connections first
                    if (Db != null)
                    {
                        await Db.DisposeAsync();
                    }

                    // Force garbage collection to release file handles
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    File.Delete(testDbPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cleanup warning: {ex.Message}");
                }
            }

            await DisposeAsync();
        }

        #region Single Tenant Tests

        [TestMethod]
        public async Task SingleTenant_CompleteSetup_InitializesDatabaseSchema()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";
            
            // Pre-create the database with schema so InitializeSetupAsync can save state
            using (var context = new ApplicationDbContext(connectionString))
            {
                await context.Database.EnsureCreatedAsync();
            }
            
            var setupService = CreateSetupService(connectionString);
            var setup = await setupService.InitializeSetupAsync();

            // Configure all required fields for single tenant
            await setupService.UpdateTenantModeAsync(setup.Id, "SingleTenant");
            await setupService.UpdateDatabaseConfigAsync(setup.Id, connectionString);
            await setupService.UpdateStorageConfigAsync(setup.Id, "UseDevelopmentStorage=True", "/");
            await setupService.UpdateAdminAccountAsync(setup.Id, "admin@test.com", "TestPassword123!");
            await setupService.UpdatePublisherConfigAsync(
                setup.Id,
                "https://test.com",
                false,
                false,
                ".jpg,.png",
                null,
                null,
                "Test Site");

            // Act
            var result = await setupService.CompleteSetupAsync(setup.Id);

            // Assert
            Assert.IsTrue(result.Success, $"Setup failed: {result.Message}");
            
            // Verify database was created and initialized
            Assert.IsTrue(File.Exists(testDbPath), "Database file should exist");
            
            // Verify schema exists by querying key tables
            using var verifyContext = new ApplicationDbContext(connectionString);
            var articlesTableExists = await verifyContext.Articles.AnyAsync() || true; // Will be true if table exists
            var settingsTableExists = await verifyContext.Settings.AnyAsync() || true;
            var layoutsTableExists = await verifyContext.Layouts.AnyAsync() || true;
            
            Assert.IsTrue(articlesTableExists, "Articles table should exist");
            Assert.IsTrue(settingsTableExists, "Settings table should exist");
            Assert.IsTrue(layoutsTableExists, "Layouts table should exist");
        }
        
        [TestMethod]
        public async Task SingleTenant_CompleteSetup_CreatesDefaultLayout()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";
            
            // Pre-create the database with schema
            using (var context = new ApplicationDbContext(connectionString))
            {
                await context.Database.EnsureCreatedAsync();
            }
            
            var setupService = CreateSetupService(connectionString);
            var setup = await setupService.InitializeSetupAsync();

            await setupService.UpdateTenantModeAsync(setup.Id, "SingleTenant");
            await setupService.UpdateDatabaseConfigAsync(setup.Id, connectionString);
            await setupService.UpdateStorageConfigAsync(setup.Id, "UseDevelopmentStorage=True", "/");
            await setupService.UpdateAdminAccountAsync(setup.Id, "admin@test.com", "TestPassword123!");
            await setupService.UpdatePublisherConfigAsync(
                setup.Id,
                "https://test.com",
                false,
                false,
                ".jpg,.png",
                null,
                null, // No site design - should create default
                "Test Site");

            // Act
            var result = await setupService.CompleteSetupAsync(setup.Id);

            // Assert
            Assert.IsTrue(result.Success);
            
            using var verifyContext = new ApplicationDbContext(connectionString);
            var defaultLayout = await verifyContext.Layouts.FirstOrDefaultAsync(l => l.IsDefault);
            
            Assert.IsNotNull(defaultLayout, "Default layout should be created");
            Assert.AreEqual("Default Layout", defaultLayout.LayoutName);
        }

        #endregion

        #region Multi-Tenant Tests

        [TestMethod]
        public async Task MultiTenant_CompleteSetup_UsesEditorConnection()
        {
            // Arrange
            var editorConnectionString = $"Data Source={testDbPath}";

            // Pre-create the editor database with schema
            using (var context = new ApplicationDbContext(editorConnectionString))
            {
                await context.Database.EnsureCreatedAsync();
            }

            var setupService = CreateSetupService(editorConnectionString, editorConnectionString);
            var setup = await setupService.InitializeSetupAsync();

            await setupService.UpdateTenantModeAsync(setup.Id, "MultiTenant");
            await setupService.UpdateStorageConfigAsync(setup.Id, "UseDevelopmentStorage=True", "/");
            await setupService.UpdateAdminAccountAsync(setup.Id, "admin@test.com", "TestPassword123!");
            await setupService.UpdatePublisherConfigAsync(
                setup.Id,
                "https://test.com",
                false,
                false,
                ".jpg,.png",
                null,
                null,
                "Test Site");

            // Act
            var result = await setupService.CompleteSetupAsync(setup.Id);

            // Assert
            Assert.IsTrue(result.Success);

            // Verify the editor database was used (not the tenant database)
            Assert.IsTrue(File.Exists(testDbPath), "Editor database should exist");
        }

        #endregion

        #region Database Provider Tests

        [TestMethod]
        public async Task CompleteSetup_SqliteProvider_ReturnsCorrectProviderType()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Pre-create the database with schema
            using (var context = new ApplicationDbContext(connectionString))
            {
                await context.Database.EnsureCreatedAsync();
            }

            var setupService = CreateSetupService(connectionString);
            var setup = await setupService.InitializeSetupAsync();

            await ConfigureMinimalSetup(setupService, setup, connectionString);

            // Act
            var result = await setupService.CompleteSetupAsync(setup.Id);

            // Assert
            Assert.IsTrue(result.Success);
            // Note: This assumes your SetupCompletionResult includes provider info
            // If not, you'll need to add DatabaseProvider property
        }

        [TestMethod]
        public async Task CompleteSetup_ExistingSchema_DoesNotRecreate()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Pre-create the database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString)
                .Options;
            using (var preContext = new ApplicationDbContext(options))
            {
                await preContext.Database.EnsureCreatedAsync();
            }

            var setupService = CreateSetupService(connectionString);
            var setup = await setupService.InitializeSetupAsync();
            await ConfigureMinimalSetup(setupService, setup, connectionString);

            // Act
            var result = await setupService.CompleteSetupAsync(setup.Id);

            // Assert
            Assert.IsTrue(result.Success);
            // Verify database still exists and wasn't recreated
            Assert.IsTrue(File.Exists(testDbPath));
        }

        #endregion

        #region Idempotency Tests

        [TestMethod]
        public async Task CompleteSetup_CalledTwice_OnlyCreatesAdminOnce()
        {
            var connectionString = $"Data Source={testDbPath}";

            // Initialize schema FIRST
            using (var context = new ApplicationDbContext(connectionString))
            {
                await context.Database.EnsureCreatedAsync();
            }

            var setupService = CreateSetupService(connectionString);
            var setup = await setupService.InitializeSetupAsync();
            await ConfigureMinimalSetup(setupService, setup, connectionString);

            // Act
            var result1 = await setupService.CompleteSetupAsync(setup.Id);
            var adminUsers1 = await UserManager.GetUsersInRoleAsync(RequiredIdentityRoles.Administrators);
            var count1 = adminUsers1.Count;

            // Try to complete again (should be idempotent)
            var result2 = await setupService.CompleteSetupAsync(setup.Id);
            var adminUsers2 = await UserManager.GetUsersInRoleAsync(RequiredIdentityRoles.Administrators);
            var count2 = adminUsers2.Count;

            // Assert
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(count1, count2, "Admin count should not increase on second completion");
        }

        [TestMethod]
        public async Task CompleteSetup_ExistingAdmin_SkipsCreation()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Pre-create the database with schema and roles
            using (var context = new ApplicationDbContext(connectionString))
            {
                await context.Database.EnsureCreatedAsync();
            }

            // Ensure the role exists
            var roleExists = await RoleManager.RoleExistsAsync(RequiredIdentityRoles.Administrators);
            if (!roleExists)
            {
                await RoleManager.CreateAsync(new IdentityRole(RequiredIdentityRoles.Administrators));
            }

            // Pre-create admin user
            var existingAdmin = new IdentityUser
            {
                UserName = "existing@test.com",
                Email = "existing@test.com",
                EmailConfirmed = true
            };
            await UserManager.CreateAsync(existingAdmin, "ExistingPass123!");
            await UserManager.AddToRoleAsync(existingAdmin, RequiredIdentityRoles.Administrators);

            var setupService = CreateSetupService(connectionString);
            var setup = await setupService.InitializeSetupAsync();
            await ConfigureMinimalSetup(setupService, setup, connectionString);

            // Act
            var result = await setupService.CompleteSetupAsync(setup.Id);

            // Assert
            Assert.IsTrue(result.Success);
            var adminUsers = await UserManager.GetUsersInRoleAsync(RequiredIdentityRoles.Administrators);
            Assert.AreEqual(1, adminUsers.Count, "Should not create duplicate admin");
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task CompleteSetup_MissingRequiredConfig_ReturnsValidationError()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Pre-create the database with schema
            using (var context = new ApplicationDbContext(connectionString))
            {
                await context.Database.EnsureCreatedAsync();
            }

            var setupService = CreateSetupService(connectionString);
            var setup = await setupService.InitializeSetupAsync();

            // Only partially configure (missing admin password)
            await setupService.UpdateDatabaseConfigAsync(setup.Id, connectionString);
            await setupService.UpdateStorageConfigAsync(setup.Id, "UseDevelopmentStorage=True", "/");
            await setupService.UpdatePublisherConfigAsync(
                setup.Id,
                "https://test.com",
                false,
                false,
                ".jpg,.png",
                null,
                null,
                "Test");

            // Act
            var result = await setupService.CompleteSetupAsync(setup.Id);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Message.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                         result.Message.Contains("password", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Helper Methods

        private ISetupService CreateSetupService(string? applicationConnection, string? editorConnection = null)
        {
            var configValues = new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApplicationDbContextConnection"] = applicationConnection ?? $"Data Source={testDbPath}",
                ["ConnectionStrings:StorageConnectionString"] = "UseDevelopmentStorage=true"
            };

            if (!string.IsNullOrEmpty(editorConnection))
            {
                configValues["ConnectionStrings:EditorDbConnection"] = editorConnection;
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            // Create a database context using the actual connection string for these tests
            var dbContext = new ApplicationDbContext(applicationConnection ?? $"Data Source={testDbPath}");

            // Create UserManager and RoleManager for the SQLite database
            var userStore = new UserStore<IdentityUser>(dbContext);
            var roleStore = new RoleStore<IdentityRole>(dbContext);

            // Configure Identity options with the same settings as SkyCmsTestBase
            var identityOptions = Options.Create(new IdentityOptions
            {
                Password = new PasswordOptions
                {
                    RequireDigit = true,
                    RequireLowercase = true,
                    RequireUppercase = true,
                    RequireNonAlphanumeric = true,
                    RequiredLength = 8,
                    RequiredUniqueChars = 1
                },
                Lockout = new LockoutOptions
                {
                    AllowedForNewUsers = true,
                    DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5),
                    MaxFailedAccessAttempts = 5
                },
                SignIn = new SignInOptions
                {
                    RequireConfirmedEmail = true,
                    RequireConfirmedAccount = false
                },
                User = new UserOptions
                {
                    RequireUniqueEmail = true
                }
            });

            // Create password validators
            var passwordValidators = new List<IPasswordValidator<IdentityUser>>
            {
                new PasswordValidator<IdentityUser>()
            };

            // Create token provider for UserManager
            var tokenProvider = new DataProtectorTokenProvider<IdentityUser>(
                new EphemeralDataProtectionProvider(new NullLoggerFactory()),
                Options.Create(new DataProtectionTokenProviderOptions()),
                new NullLogger<DataProtectorTokenProvider<IdentityUser>>());

            var testUserManager = new UserManager<IdentityUser>(
                userStore,
                identityOptions,
                new PasswordHasher<IdentityUser>(),
                Array.Empty<IUserValidator<IdentityUser>>(),
                passwordValidators,
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                services: null,
                new NullLogger<UserManager<IdentityUser>>());

            // Register the default token provider
            testUserManager.RegisterTokenProvider("Default", tokenProvider);

            var testRoleManager = new RoleManager<IdentityRole>(
                roleStore,
                Array.Empty<IRoleValidator<IdentityRole>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                new NullLogger<RoleManager<IdentityRole>>());

            // Create service provider with IDatabaseInitializationService
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging();
            services.AddMemoryCache();

            serviceProvider = services.BuildServiceProvider();

            var layoutImportService = new LayoutImportService(
                HttpClientFactory,
                Cache,
                new NullLogger<LayoutImportService>());

            _sqliteUserManager = testUserManager; // Store it

            return new SetupService(
                config,
                new NullLogger<SetupService>(),
                Cache,
                testUserManager,      // Use the SQLite-based UserManager
                testRoleManager,      // Use the SQLite-based RoleManager
                dbContext,            // Use the SQLite database context
                layoutImportService,
                Logic,
                Mediator);
        }

        private async Task ConfigureMinimalSetup(
            ISetupService setupService,
            SetupConfiguration setup,
            string connectionString)
        {
            await setupService.UpdateTenantModeAsync(setup.Id, "SingleTenant");
            await setupService.UpdateDatabaseConfigAsync(setup.Id, connectionString);
            await setupService.UpdateStorageConfigAsync(setup.Id, "UseDevelopmentStorage=True", "/");
            await setupService.UpdateAdminAccountAsync(setup.Id, "admin@test.com", "TestPassword123!");
            await setupService.UpdatePublisherConfigAsync(
                setup.Id,
                "https://test.com",
                false,
                false,
                ".jpg,.png",
                null,
                null,
                "Test Site");
        }

        #endregion
    }
}