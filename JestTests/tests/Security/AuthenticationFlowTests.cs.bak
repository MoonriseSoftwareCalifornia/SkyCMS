// <copyright file="AuthenticationFlowTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Security
{
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Comprehensive authentication flow tests for SkyCMS.
    /// Tests local accounts, OAuth providers, session management, and security policies.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class AuthenticationFlowTests : SkyCmsTestBase
    {
        private Mock<IAuthenticationSchemeProvider> mockSchemeProvider;
        private Mock<IOptions<IdentityOptions>> mockIdentityOptions;
        private Mock<ILogger<SignInManager<IdentityUser>>> mockSignInLogger;
        private Mock<IUserConfirmation<IdentityUser>> mockUserConfirmation;
        private SignInManager<IdentityUser> signInManager;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext();

            // Setup authentication mocks
            mockSchemeProvider = new Mock<IAuthenticationSchemeProvider>();
            mockIdentityOptions = new Mock<IOptions<IdentityOptions>>();
            mockSignInLogger = new Mock<ILogger<SignInManager<IdentityUser>>>();
            mockUserConfirmation = new Mock<IUserConfirmation<IdentityUser>>();

            // Configure Identity options
            mockIdentityOptions.Setup(o => o.Value).Returns(new IdentityOptions
            {
                Password = new PasswordOptions
                {
                    RequireDigit = true,
                    RequireLowercase = true,
                    RequireUppercase = true,
                    RequireNonAlphanumeric = true,
                    RequiredLength = 8
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
                }
            });

            // Setup user confirmation mock - default to allowing sign-in
            mockUserConfirmation.Setup(c => c.IsConfirmedAsync(It.IsAny<UserManager<IdentityUser>>(), It.IsAny<IdentityUser>()))
                .ReturnsAsync((UserManager<IdentityUser> um, IdentityUser user) => user.EmailConfirmed);

            // Create HttpContext with authentication services
            var httpContext = new DefaultHttpContext();
            var mockServiceProvider = new Mock<IServiceProvider>();
            
            // Setup authentication service
            var mockAuthService = new Mock<IAuthenticationService>();
            mockAuthService
                .Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);
            mockAuthService
                .Setup(a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);

            mockServiceProvider.Setup(sp => sp.GetService(typeof(IAuthenticationService)))
                .Returns(mockAuthService.Object);
            
            httpContext.RequestServices = mockServiceProvider.Object;

            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

            var mockClaimsFactory = new Mock<IUserClaimsPrincipalFactory<IdentityUser>>();
            mockClaimsFactory
                .Setup(f => f.CreateAsync(It.IsAny<IdentityUser>()))
                .ReturnsAsync((IdentityUser user) => new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.NameIdentifier, user.Id)
                }, IdentityConstants.ApplicationScheme)));

            // Create SignInManager with proper dependencies
            signInManager = new SignInManager<IdentityUser>(
                UserManager,
                mockHttpContextAccessor.Object,
                mockClaimsFactory.Object,
                mockIdentityOptions.Object,
                mockSignInLogger.Object,
                mockSchemeProvider.Object,
                mockUserConfirmation.Object);
        }

        #region Local Account Authentication Tests

        [TestMethod]
        public async Task Login_ValidCredentials_Succeeds()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "testuser@example.com",
                Email = "testuser@example.com",
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var password = "Test@1234";
            var createResult = await UserManager.CreateAsync(user, password);
            Assert.IsTrue(createResult.Succeeded, $"User creation failed: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

            // Act
            var result = await signInManager.CheckPasswordSignInAsync(
                user,
                password,
                lockoutOnFailure: true);

            // Assert
            Assert.IsTrue(result.Succeeded, "Login should succeed with valid credentials");
        }

        [TestMethod]
        public async Task Login_InvalidPassword_Fails()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "testuser@example.com",
                Email = "testuser@example.com",
                EmailConfirmed = true
            };

            await UserManager.CreateAsync(user, "Test@1234");

            // Act
            var result = await signInManager.CheckPasswordSignInAsync(
                user,
                "WrongPassword123!",
                lockoutOnFailure: true);

            // Assert
            Assert.IsFalse(result.Succeeded, "Login should fail with invalid password");
        }

        [TestMethod]
        public async Task Login_NonExistentUser_ReturnsFailure()
        {
            // Arrange
            var user = await UserManager.FindByNameAsync("nonexistent@example.com");

            // Assert
            Assert.IsNull(user, "Non-existent user should return null");
        }

        [TestMethod]
        public async Task Login_UnconfirmedEmail_RequiresConfirmation()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "unconfirmed@example.com",
                Email = "unconfirmed@example.com",
                EmailConfirmed = false // Not confirmed
            };

            var password = "Test@1234";
            await UserManager.CreateAsync(user, password);

            // Act
            var canSignIn = await signInManager.CanSignInAsync(user);

            // Assert
            Assert.IsFalse(canSignIn, "User with unconfirmed email should not be able to sign in");
        }

        [TestMethod]
        public async Task Login_MaxFailedAttempts_LocksAccount()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "locktest@example.com",
                Email = "locktest@example.com",
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            await UserManager.CreateAsync(user, "Test@1234");

            // Act - Make 5 failed attempts
            for (int i = 0; i < 5; i++)
            {
                await signInManager.CheckPasswordSignInAsync(
                    user,
                    "WrongPassword123!",
                    lockoutOnFailure: true);
            }

            // Refresh user from database
            var lockedUser = await UserManager.FindByNameAsync(user.UserName);
            var isLockedOut = await UserManager.IsLockedOutAsync(lockedUser);

            // Assert
            Assert.IsTrue(isLockedOut, "Account should be locked after max failed attempts");
        }

        [TestMethod]
        public async Task Login_LockedAccount_ReturnsLockedOut()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "locked@example.com",
                Email = "locked@example.com",
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            await UserManager.CreateAsync(user, "Test@1234");
            await UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5));

            // Refresh user
            user = await UserManager.FindByNameAsync(user.UserName);

            // Act
            var result = await signInManager.CheckPasswordSignInAsync(
                user,
                "Test@1234",
                lockoutOnFailure: false);

            // Assert
            Assert.IsTrue(result.IsLockedOut, "Login should indicate account is locked");
        }

        #endregion

        #region Password Policy Tests

        [TestMethod]
        public async Task CreateUser_WeakPassword_Fails()
        {
            // Arrange
            var weakPasswords = new[]
            {
                ("password", "No uppercase, no digits, no special chars"),
                ("Password", "No digits, no special chars"),
                ("Pass123", "Too short, no special chars"),
                ("PASSWORD123!", "No lowercase")
            };

            foreach (var (weakPassword, reason) in weakPasswords)
            {
                var user = new IdentityUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = $"weakpass{Guid.NewGuid()}@example.com",
                    Email = $"weakpass{Guid.NewGuid()}@example.com"
                };

                // Act
                var result = await UserManager.CreateAsync(user, weakPassword);

                // Assert
                Assert.IsFalse(result.Succeeded, $"Weak password '{weakPassword}' should be rejected ({reason})");
            }
        }

        [TestMethod]
        public async Task CreateUser_StrongPassword_Succeeds()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "strongpass@example.com",
                Email = "strongpass@example.com"
            };

            // Act
            var result = await UserManager.CreateAsync(user, "StrongP@ss123");

            // Assert
            Assert.IsTrue(result.Succeeded, $"Strong password should be accepted. Errors: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        [TestMethod]
        public async Task ChangePassword_RequiresCurrentPassword()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "changepass@example.com",
                Email = "changepass@example.com"
            };

            await UserManager.CreateAsync(user, "OldP@ss123");

            // Act
            var result = await UserManager.ChangePasswordAsync(
                user,
                "WrongOldP@ss123",
                "NewP@ss456");

            // Assert
            Assert.IsFalse(result.Succeeded, "Password change should fail with incorrect current password");
        }

        [TestMethod]
        public async Task ChangePassword_ValidCurrentPassword_Succeeds()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "changepass2@example.com",
                Email = "changepass2@example.com",
                EmailConfirmed = true
            };

            await UserManager.CreateAsync(user, "OldP@ss123");

            // Act
            var changeResult = await UserManager.ChangePasswordAsync(
                user,
                "OldP@ss123",
                "NewP@ss456");

            // Assert
            Assert.IsTrue(changeResult.Succeeded, $"Password change should succeed. Errors: {string.Join(", ", changeResult.Errors.Select(e => e.Description))}");

            // Refresh user from database
            user = await UserManager.FindByNameAsync(user.UserName);

            // Verify old password no longer works
            var oldPasswordWorks = await UserManager.CheckPasswordAsync(user, "OldP@ss123");
            Assert.IsFalse(oldPasswordWorks, "Old password should no longer work");

            // Verify new password works
            var newPasswordWorks = await UserManager.CheckPasswordAsync(user, "NewP@ss456");
            Assert.IsTrue(newPasswordWorks, "New password should work");
        }

        #endregion

        #region Email Confirmation Tests

        [TestMethod]
        public async Task EmailConfirmation_ValidToken_Succeeds()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "confirm@example.com",
                Email = "confirm@example.com",
                EmailConfirmed = false
            };

            await UserManager.CreateAsync(user, "Test@1234");
            var token = await UserManager.GenerateEmailConfirmationTokenAsync(user);

            // Act
            var result = await UserManager.ConfirmEmailAsync(user, token);

            // Assert
            Assert.IsTrue(result.Succeeded, $"Email confirmation should succeed. Errors: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            
            var updatedUser = await UserManager.FindByEmailAsync(user.Email);
            Assert.IsTrue(updatedUser.EmailConfirmed, "Email should be marked as confirmed");
        }

        [TestMethod]
        public async Task EmailConfirmation_InvalidToken_Fails()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "confirm2@example.com",
                Email = "confirm2@example.com",
                EmailConfirmed = false
            };

            await UserManager.CreateAsync(user, "Test@1234");

            // Act
            var result = await UserManager.ConfirmEmailAsync(user, "InvalidToken123");

            // Assert
            Assert.IsFalse(result.Succeeded, "Email confirmation should fail with invalid token");
        }

        [TestMethod]
        public async Task EmailConfirmation_ExpiredToken_Fails()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "expired@example.com",
                Email = "expired@example.com",
                EmailConfirmed = false
            };

            await UserManager.CreateAsync(user, "Test@1234");
            
            // Generate token
            var token = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            
            // Simulate token expiration by changing security stamp
            await UserManager.UpdateSecurityStampAsync(user);

            // Act
            var result = await UserManager.ConfirmEmailAsync(user, token);

            // Assert
            Assert.IsFalse(result.Succeeded, "Email confirmation should fail with expired token");
        }

        #endregion

        #region Password Reset Tests

        [TestMethod]
        public async Task PasswordReset_ValidToken_Succeeds()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "reset@example.com",
                Email = "reset@example.com",
                EmailConfirmed = true
            };

            await UserManager.CreateAsync(user, "OldP@ss123");
            var resetToken = await UserManager.GeneratePasswordResetTokenAsync(user);

            // Act
            var result = await UserManager.ResetPasswordAsync(user, resetToken, "NewP@ss456");

            // Assert
            Assert.IsTrue(result.Succeeded, $"Password reset should succeed. Errors: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            
            // Refresh user and verify new password works
            user = await UserManager.FindByNameAsync(user.UserName);
            var passwordWorks = await UserManager.CheckPasswordAsync(user, "NewP@ss456");
            Assert.IsTrue(passwordWorks, "New password should work after reset");
        }

        [TestMethod]
        public async Task PasswordReset_InvalidToken_Fails()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "reset2@example.com",
                Email = "reset2@example.com"
            };

            await UserManager.CreateAsync(user, "OldP@ss123");

            // Act
            var result = await UserManager.ResetPasswordAsync(user, "InvalidToken", "NewP@ss456");

            // Assert
            Assert.IsFalse(result.Succeeded, "Password reset should fail with invalid token");
        }

        [TestMethod]
        public async Task PasswordReset_TokenReuse_Fails()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "reuse@example.com",
                Email = "reuse@example.com"
            };

            await UserManager.CreateAsync(user, "OldP@ss123");
            var resetToken = await UserManager.GeneratePasswordResetTokenAsync(user);

            // First reset
            await UserManager.ResetPasswordAsync(user, resetToken, "NewP@ss456");

            // Refresh user
            user = await UserManager.FindByNameAsync(user.UserName);

            // Act - Try to reuse the same token
            var result = await UserManager.ResetPasswordAsync(user, resetToken, "AnotherP@ss789");

            // Assert
            Assert.IsFalse(result.Succeeded, "Token reuse should fail");
        }

        #endregion

        #region Two-Factor Authentication Tests

        [TestMethod]
        public async Task TwoFactorAuth_EnableForUser_Succeeds()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "2fa@example.com",
                Email = "2fa@example.com",
                EmailConfirmed = true
            };

            await UserManager.CreateAsync(user, "Test@1234");

            // Act
            var result = await UserManager.SetTwoFactorEnabledAsync(user, true);

            // Assert
            Assert.IsTrue(result.Succeeded, "Enabling 2FA should succeed");
            
            var updatedUser = await UserManager.FindByEmailAsync(user.Email);
            Assert.IsTrue(updatedUser.TwoFactorEnabled, "2FA should be enabled");
        }

        #endregion

        #region OAuth Provider Tests

        [TestMethod]
        public async Task OAuthLogin_ExternalProvider_CreatesLocalAccount()
        {
            // Arrange & Act
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "oauth@example.com",
                Email = "oauth@example.com",
                EmailConfirmed = true // OAuth providers verify email
            };

            var createResult = await UserManager.CreateAsync(user);
            var addLoginResult = await UserManager.AddLoginAsync(
                user,
                new UserLoginInfo("Google", "google-user-id-123", "Google"));

            // Assert
            Assert.IsTrue(createResult.Succeeded, "User creation should succeed");
            Assert.IsTrue(addLoginResult.Succeeded, "External login should be added");
            
            var logins = await UserManager.GetLoginsAsync(user);
            Assert.AreEqual(1, logins.Count, "User should have one external login");
            Assert.AreEqual("Google", logins[0].LoginProvider);
        }

        #endregion

        #region Security Stamp Tests

        [TestMethod]
        public async Task SecurityStamp_ChangedOnPasswordChange_InvalidatesSessions()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "stamp@example.com",
                Email = "stamp@example.com"
            };

            await UserManager.CreateAsync(user, "OldP@ss123");
            var oldStamp = await UserManager.GetSecurityStampAsync(user);

            // Act
            await UserManager.ChangePasswordAsync(user, "OldP@ss123", "NewP@ss456");
            
            // Refresh user
            user = await UserManager.FindByNameAsync(user.UserName);
            var newStamp = await UserManager.GetSecurityStampAsync(user);

            // Assert
            Assert.AreNotEqual(oldStamp, newStamp, "Security stamp should change on password change");
        }

        [TestMethod]
        public async Task SecurityStamp_ManualUpdate_InvalidatesTokens()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "stamp2@example.com",
                Email = "stamp2@example.com"
            };

            await UserManager.CreateAsync(user, "Test@1234");
            var resetToken = await UserManager.GeneratePasswordResetTokenAsync(user);

            // Act
            await UserManager.UpdateSecurityStampAsync(user);
            
            // Refresh user
            user = await UserManager.FindByNameAsync(user.UserName);

            // Try to use old token
            var result = await UserManager.ResetPasswordAsync(user, resetToken, "NewP@ss456");

            // Assert
            Assert.IsFalse(result.Succeeded, "Old token should be invalid after security stamp update");
        }

        #endregion

        #region Account Lockout Recovery Tests

        [TestMethod]
        public async Task AccountLockout_AutomaticallyExpires()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "lockexpire@example.com",
                Email = "lockexpire@example.com",
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            await UserManager.CreateAsync(user, "Test@1234");
            await UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(1));

            // Verify locked
            user = await UserManager.FindByNameAsync(user.UserName);
            var isLocked = await UserManager.IsLockedOutAsync(user);
            Assert.IsTrue(isLocked, "Account should be locked initially");

            // Act - Wait for lockout to expire
            await Task.Delay(1100); // Wait 1.1 seconds

            // Assert
            user = await UserManager.FindByNameAsync(user.UserName);
            isLocked = await UserManager.IsLockedOutAsync(user);
            Assert.IsFalse(isLocked, "Account lockout should have expired");
        }

        [TestMethod]
        public async Task AccountLockout_ManualUnlock_Succeeds()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "manualunlock@example.com",
                Email = "manualunlock@example.com",
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            await UserManager.CreateAsync(user, "Test@1234");
            await UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5));

            // Act
            var result = await UserManager.SetLockoutEndDateAsync(user, null);

            // Assert
            Assert.IsTrue(result.Succeeded, "Manual unlock should succeed");
            
            user = await UserManager.FindByNameAsync(user.UserName);
            var isLocked = await UserManager.IsLockedOutAsync(user);
            Assert.IsFalse(isLocked, "Account should be unlocked");
        }

        #endregion

        #region Role-Based Authentication Tests

        [TestMethod]
        public async Task UserInRole_Administrator_HasAccess()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "admin@example.com",
                Email = "admin@example.com"
            };

            await UserManager.CreateAsync(user, "Test@1234");
            
            // Ensure Administrator role exists
            if (!await RoleManager.RoleExistsAsync("Administrators"))
            {
                await RoleManager.CreateAsync(new IdentityRole("Administrators"));
            }

            // Act
            await UserManager.AddToRoleAsync(user, "Administrators");

            // Refresh user
            user = await UserManager.FindByNameAsync(user.UserName);

            // Assert
            var isInRole = await UserManager.IsInRoleAsync(user, "Administrators");
            Assert.IsTrue(isInRole, "User should be in Administrator role");
        }

        [TestMethod]
        public async Task UserInRole_MultipleRoles_AllAssigned()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "multirole@example.com",
                Email = "multirole@example.com"
            };

            await UserManager.CreateAsync(user, "Test@1234");
            
            // Ensure roles exist
            var roles = new[] { "Administrators", "Editors", "Authors" };
            foreach (var role in roles)
            {
                if (!await RoleManager.RoleExistsAsync(role))
                {
                    await RoleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Act
            foreach (var role in roles)
            {
                await UserManager.AddToRoleAsync(user, role);
            }

            // Refresh user
            user = await UserManager.FindByNameAsync(user.UserName);

            // Assert
            var userRoles = await UserManager.GetRolesAsync(user);
            Assert.AreEqual(3, userRoles.Count, "User should have 3 roles");
            CollectionAssert.AreEquivalent(roles, userRoles.ToArray(), "All roles should be assigned");
        }

        #endregion
    }
}
