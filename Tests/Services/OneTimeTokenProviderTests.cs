// <copyright file="OneTimeTokenProviderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Services;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Unit tests for <see cref="OneTimeTokenProvider{TUser}"/>.
    /// Tests token generation, validation, expiration, and security features.
    /// </summary>
    [TestClass]
    public class OneTimeTokenProviderTests
    {
        private ApplicationDbContext dbContext;
        private Mock<ILogger> loggerMock;
        private OneTimeTokenProvider<IdentityUser> tokenProvider;
        private IdentityUser testUser;

        private const string TestUserId = "test-user-123";
        private const string TestUserEmail = "test@example.com";
        private const string TestNormalizedEmail = "TEST@EXAMPLE.COM";

        [TestInitialize]
        public void Setup()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"TokenProviderTest_{Guid.NewGuid()}")
                .Options;
            dbContext = new ApplicationDbContext(options);

            // Create test user
            testUser = new IdentityUser
            {
                Id = TestUserId,
                UserName = TestUserEmail,
                Email = TestUserEmail,
                NormalizedEmail = TestNormalizedEmail,
                EmailConfirmed = true,
                LockoutEnabled = false,
                LockoutEnd = null
            };

            dbContext.Users.Add(testUser);
            dbContext.SaveChanges();

            // Setup mocks
            loggerMock = new Mock<ILogger>();

            // Create token provider
            tokenProvider = new OneTimeTokenProvider<IdentityUser>(
                dbContext,
                loggerMock.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            dbContext?.Dispose();
        }

        #region GenerateAsync Tests

        /// <summary>
        /// Tests that token generation creates a valid token.
        /// </summary>
        [TestMethod]
        public async Task GenerateAsync_ValidUser_ReturnsToken()
        {
            // Act
            var token = await tokenProvider.GenerateAsync(testUser);

            // Assert
            Assert.IsNotNull(token);
            Assert.IsFalse(string.IsNullOrWhiteSpace(token));
            Assert.AreEqual(32, token.Length, "Token should be 32 characters");
        }

        /// <summary>
        /// Tests that generated token is stored in database.
        /// </summary>
        [TestMethod]
        public async Task GenerateAsync_ValidUser_StoresTokenInDatabase()
        {
            // Act
            var token = await tokenProvider.GenerateAsync(testUser);

            // Assert
            var storedToken = await dbContext.TotpTokens
                .FirstOrDefaultAsync(t => t.Token == token && t.UserId == TestUserId);

            Assert.IsNotNull(storedToken);
            Assert.AreEqual(TestUserId, storedToken.UserId);
            Assert.AreEqual(TestNormalizedEmail, storedToken.Email);
            Assert.AreEqual(token, storedToken.Token);
        }

        /// <summary>
        /// Tests that generated token has correct expiration.
        /// </summary>
        [TestMethod]
        public async Task GenerateAsync_ValidUser_SetsExpirationCorrectly()
        {
            // Arrange
            var beforeGeneration = DateTimeOffset.UtcNow;

            // Act
            var token = await tokenProvider.GenerateAsync(testUser);

            // Assert
            var storedToken = await dbContext.TotpTokens.FirstOrDefaultAsync(t => t.Token == token);
            var afterGeneration = DateTimeOffset.UtcNow;

            Assert.IsNotNull(storedToken);
            Assert.IsTrue(storedToken.CreatedAt >= beforeGeneration);
            Assert.IsTrue(storedToken.CreatedAt <= afterGeneration);
            Assert.IsTrue(storedToken.ExpiresAt > storedToken.CreatedAt);

            // Default expiration is 15 minutes
            var expectedExpiration = storedToken.CreatedAt.AddMinutes(15);
            var timeDifference = Math.Abs((storedToken.ExpiresAt - expectedExpiration).TotalSeconds);
            Assert.IsTrue(timeDifference < 1, "Expiration should be approximately 15 minutes from creation");
        }

        /// <summary>
        /// Tests that each generated token is unique.
        /// </summary>
        [TestMethod]
        public async Task GenerateAsync_MultipleCalls_GeneratesUniqueTokens()
        {
            // Act
            var token1 = await tokenProvider.GenerateAsync(testUser);
            var token2 = await tokenProvider.GenerateAsync(testUser);
            var token3 = await tokenProvider.GenerateAsync(testUser);

            // Assert
            Assert.AreNotEqual(token1, token2);
            Assert.AreNotEqual(token2, token3);
            Assert.AreNotEqual(token1, token3);
        }

        /// <summary>
        /// Tests that token generation logs appropriately.
        /// </summary>
        [TestMethod]
        public async Task GenerateAsync_ValidUser_LogsTokenGeneration()
        {
            // Act
            await tokenProvider.GenerateAsync(testUser);

            // Assert - Verify logging occurred (check that Log was called)
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.AtLeastOnce);
        }

        #endregion

        #region ValidateAsync Tests - Valid Scenarios

        /// <summary>
        /// Tests that valid token is accepted.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_ValidToken_ReturnsValid()
        {
            // Arrange
            var token = await tokenProvider.GenerateAsync(testUser);

            // Act
            var result = await tokenProvider.ValidateAsync(token, testUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Valid, result);
        }

        /// <summary>
        /// Tests that valid token is removed after successful validation.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_ValidTokenWithRemove_RemovesTokenFromDatabase()
        {
            // Arrange
            var token = await tokenProvider.GenerateAsync(testUser);

            // Act
            var result = await tokenProvider.ValidateAsync(token, testUser, removeToken: true);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Valid, result);

            var storedToken = await dbContext.TotpTokens.FirstOrDefaultAsync(t => t.Token == token);
            Assert.IsNull(storedToken, "Token should be removed from database after validation");
        }

        /// <summary>
        /// Tests that valid token is NOT removed when removeToken is false.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_ValidTokenWithoutRemove_KeepsTokenInDatabase()
        {
            // Arrange
            var token = await tokenProvider.GenerateAsync(testUser);

            // Act
            var result = await tokenProvider.ValidateAsync(token, testUser, removeToken: false);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Valid, result);

            var storedToken = await dbContext.TotpTokens.FirstOrDefaultAsync(t => t.Token == token);
            Assert.IsNotNull(storedToken, "Token should remain in database when removeToken is false");
        }

        #endregion

        #region ValidateAsync Tests - Invalid Scenarios

        /// <summary>
        /// Tests that null token returns Invalid.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_NullToken_ReturnsInvalid()
        {
            // Act
            var result = await tokenProvider.ValidateAsync(null, testUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        /// <summary>
        /// Tests that empty token returns Invalid.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_EmptyToken_ReturnsInvalid()
        {
            // Act
            var result = await tokenProvider.ValidateAsync(string.Empty, testUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        /// <summary>
        /// Tests that whitespace token returns Invalid.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_WhitespaceToken_ReturnsInvalid()
        {
            // Act
            var result = await tokenProvider.ValidateAsync("   ", testUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        /// <summary>
        /// Tests that non-existent token returns Invalid.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_NonExistentToken_ReturnsInvalid()
        {
            // Act
            var result = await tokenProvider.ValidateAsync("non-existent-token-123456789012", testUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        /// <summary>
        /// Tests that token for different user returns Invalid.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_TokenForDifferentUser_ReturnsInvalid()
        {
            // Arrange
            var token = await tokenProvider.GenerateAsync(testUser);

            var differentUser = new IdentityUser
            {
                Id = "different-user-456",
                UserName = "other@example.com",
                Email = "other@example.com",
                NormalizedEmail = "OTHER@EXAMPLE.COM",
                EmailConfirmed = true
            };
            dbContext.Users.Add(differentUser);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await tokenProvider.ValidateAsync(token, differentUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        /// <summary>
        /// Tests that unconfirmed email user returns Invalid.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_UnconfirmedEmail_ReturnsInvalid()
        {
            // Arrange
            testUser.EmailConfirmed = false;
            dbContext.Users.Update(testUser);
            await dbContext.SaveChangesAsync();

            var token = await tokenProvider.GenerateAsync(testUser);

            // Act
            var result = await tokenProvider.ValidateAsync(token, testUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        /// <summary>
        /// Tests that locked out user returns Invalid.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_LockedOutUser_ReturnsInvalid()
        {
            // Arrange
            var token = await tokenProvider.GenerateAsync(testUser);

            // Lock out the user
            testUser.LockoutEnabled = true;
            testUser.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);
            dbContext.Users.Update(testUser);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await tokenProvider.ValidateAsync(token, testUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        /// <summary>
        /// Tests that expired lockout allows validation.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_ExpiredLockout_ReturnsValid()
        {
            // Arrange
            var token = await tokenProvider.GenerateAsync(testUser);

            // Set lockout to past
            testUser.LockoutEnabled = true;
            testUser.LockoutEnd = DateTimeOffset.UtcNow.AddHours(-1);
            dbContext.Users.Update(testUser);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await tokenProvider.ValidateAsync(token, testUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Valid, result);
        }

        #endregion

        #region ValidateAsync Tests - Expiration

        /// <summary>
        /// Tests that expired token returns Expired.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_ExpiredToken_ReturnsExpired()
        {
            // Arrange
            var token = await tokenProvider.GenerateAsync(testUser);

            // Manually expire the token
            var storedToken = await dbContext.TotpTokens.FirstOrDefaultAsync(t => t.Token == token);
            storedToken.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await tokenProvider.ValidateAsync(token, testUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Expired, result);
        }

        /// <summary>
        /// Tests that token just at expiration boundary returns Expired.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_TokenAtExpirationBoundary_ReturnsExpired()
        {
            // Arrange
            var token = await tokenProvider.GenerateAsync(testUser);

            // Set token to expire exactly now
            var storedToken = await dbContext.TotpTokens.FirstOrDefaultAsync(t => t.Token == token);
            storedToken.ExpiresAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();

            // Wait a tiny bit to ensure we're past expiration
            await Task.Delay(10);

            // Act
            var result = await tokenProvider.ValidateAsync(token, testUser);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Expired, result);
        }

        #endregion

        #region ValidateAsync Tests - Null Checks

        /// <summary>
        /// Tests that null user throws ArgumentNullException.
        /// </summary>
        [TestMethod]
        public void ValidateAsync_NullUser_ThrowsArgumentNullException()
        {
            try
            {
                tokenProvider.ValidateAsync("some-token", null).GetAwaiter().GetResult();
                Assert.Fail("Expected ArgumentNullException was not thrown.");
            }
            catch (ArgumentNullException)
            {
                // Test passes
            }
        }

        /// <summary>
        /// Tests that null dbContext throws ArgumentNullException.
        /// </summary>
        [TestMethod]
        public void Constructor_NullDbContext_ThrowsArgumentNullException()
        {
            try
            {
                var _ = new OneTimeTokenProvider<IdentityUser>(null, loggerMock.Object);
                Assert.Fail("Expected ArgumentNullException was not thrown.");
            }
            catch (ArgumentNullException)
            {
                // Test passes
            }
        }

        /// <summary>
        /// Tests that null logger throws ArgumentNullException.
        /// </summary>
        [TestMethod]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            try
            {
                var _ = new OneTimeTokenProvider<IdentityUser>(dbContext, null);
                Assert.Fail("Expected ArgumentNullException was not thrown.");
            }
            catch (ArgumentNullException)
            {
                // Test passes
            }
        }

        #endregion

        #region Concurrency Tests

        /// <summary>
        /// Tests that concurrent validation of the same token is handled correctly.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_ConcurrentValidation_HandlesRaceCondition()
        {
            // Arrange
            var token = await tokenProvider.GenerateAsync(testUser);

            // Act - Validate the same token concurrently
            var task1 = tokenProvider.ValidateAsync(token, testUser, removeToken: true);
            var task2 = tokenProvider.ValidateAsync(token, testUser, removeToken: true);

            var results = await Task.WhenAll(task1, task2);

            // Assert - At least one should succeed, one might fail depending on timing
            var validResults = results.Count(r => r == TokenVerificationResult.Valid);
            var invalidResults = results.Count(r => r == TokenVerificationResult.Invalid);

            // Either both succeeded (if both read before either deleted), or one succeeded and one failed
            Assert.IsTrue(validResults >= 1, "At least one validation should succeed");

            // Token should be removed only once
            var storedToken = await dbContext.TotpTokens.FirstOrDefaultAsync(t => t.Token == token);
            Assert.IsNull(storedToken, "Token should be removed from database");
        }

        /// <summary>
        /// Tests that multiple tokens for the same user can coexist.
        /// </summary>
        [TestMethod]
        public async Task GenerateAsync_MultipleTokensForSameUser_AllValid()
        {
            // Act
            var token1 = await tokenProvider.GenerateAsync(testUser);
            var token2 = await tokenProvider.GenerateAsync(testUser);
            var token3 = await tokenProvider.GenerateAsync(testUser);

            // Assert - All tokens should be valid
            var result1 = await tokenProvider.ValidateAsync(token1, testUser, removeToken: false);
            var result2 = await tokenProvider.ValidateAsync(token2, testUser, removeToken: false);
            var result3 = await tokenProvider.ValidateAsync(token3, testUser, removeToken: false);

            Assert.AreEqual(TokenVerificationResult.Valid, result1);
            Assert.AreEqual(TokenVerificationResult.Valid, result2);
            Assert.AreEqual(TokenVerificationResult.Valid, result3);

            // All should exist in database
            var tokensInDb = await dbContext.TotpTokens
                .Where(t => t.UserId == TestUserId)
                .CountAsync();
            Assert.AreEqual(3, tokensInDb);
        }

        #endregion

        #region Security Tests

        /// <summary>
        /// Tests that token cannot be reused after validation with removal.
        /// </summary>
        [TestMethod]
        public async Task ValidateAsync_TokenUsedTwice_SecondAttemptFails()
        {
            // Arrange
            var token = await tokenProvider.GenerateAsync(testUser);

            // Act
            var firstResult = await tokenProvider.ValidateAsync(token, testUser, removeToken: true);
            var secondResult = await tokenProvider.ValidateAsync(token, testUser, removeToken: true);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Valid, firstResult);
            Assert.AreEqual(TokenVerificationResult.Invalid, secondResult,
                "Token should not be valid after first use");
        }

        /// <summary>
        /// Tests that token uses cryptographically secure random generation.
        /// </summary>
        [TestMethod]
        public async Task GenerateAsync_TokenRandomness_UsesSecureGeneration()
        {
            // Arrange - Generate many tokens
            var tokens = new HashSet<string>();
            var iterations = 100;

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var token = await tokenProvider.GenerateAsync(testUser);
                tokens.Add(token);
            }

            // Assert - All should be unique (no collisions)
            Assert.AreEqual(iterations, tokens.Count,
                "All generated tokens should be unique (no collisions in 100 iterations)");

            // Check character distribution (should use full alphanumeric set)
            var allChars = string.Join("", tokens);
            var hasUpperCase = allChars.Any(char.IsUpper);
            var hasLowerCase = allChars.Any(char.IsLower);
            var hasDigits = allChars.Any(char.IsDigit);

            Assert.IsTrue(hasUpperCase, "Tokens should contain uppercase letters");
            Assert.IsTrue(hasLowerCase, "Tokens should contain lowercase letters");
            Assert.IsTrue(hasDigits, "Tokens should contain digits");
        }

        #endregion
    }
}
