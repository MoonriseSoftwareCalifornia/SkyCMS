// <copyright file="OneTimeTokenProviderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Services
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Services;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Comprehensive tests for OneTimeTokenProvider service.
    /// Target: 100% code coverage.
    /// </summary>
    [TestClass]
    public class OneTimeTokenProviderTests : CommonTestsBase
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            InitializeContextPool(context);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CleanupContextPool();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();

            // Act & Assert
            try
            {
                var provider = new OneTimeTokenProvider<IdentityUser>(null, mockLogger.Object);
                Assert.Fail("Expected ArgumentNullException was not thrown.");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var context = GetIsolatedContext();

            // Act & Assert
            try
            {
                var provider = new OneTimeTokenProvider<IdentityUser>(context, null);
                Assert.Fail("Expected ArgumentNullException was not thrown.");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("logger", ex.ParamName);
            }
        }

        [TestMethod]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();

            // Act
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);

            // Assert
            Assert.IsNotNull(provider);
        }

        #endregion

        #region GenerateAsync Tests

        [TestMethod]
        public async Task GenerateAsync_WithValidUser_ReturnsToken()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var token = await provider.GenerateAsync(user);

            // Assert
            Assert.IsNotNull(token);
            Assert.IsTrue(token.Length > 0);
        }

        [TestMethod]
        public async Task GenerateAsync_SavesTokenToDatabase()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var token = await provider.GenerateAsync(user);

            // Assert
            var savedToken = await context.TotpTokens.FirstOrDefaultAsync(t => t.Token == token);
            Assert.IsNotNull(savedToken);
            Assert.AreEqual(user.Id, savedToken.UserId);
            Assert.AreEqual(user.NormalizedEmail, savedToken.Email);
        }

        [TestMethod]
        public async Task GenerateAsync_TokenHasCorrectLength()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var token = await provider.GenerateAsync(user);

            // Assert
            Assert.AreEqual(32, token.Length); // Default length is 32
        }

        [TestMethod]
        public async Task GenerateAsync_TokenIsAlphanumeric()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var token = await provider.GenerateAsync(user);

            // Assert
            Assert.IsTrue(token.All(c => char.IsLetterOrDigit(c)), 
                "Token should only contain alphanumeric characters");
        }

        [TestMethod]
        public async Task GenerateAsync_MultipleCalls_ProduceDifferentTokens()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var token1 = await provider.GenerateAsync(user);
            var token2 = await provider.GenerateAsync(user);
            var token3 = await provider.GenerateAsync(user);

            // Assert
            Assert.AreNotEqual(token1, token2);
            Assert.AreNotEqual(token2, token3);
            Assert.AreNotEqual(token1, token3);
        }

        [TestMethod]
        public async Task GenerateAsync_LogsInformation()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            await provider.GenerateAsync(user);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Generated one-time token")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task GenerateAsync_SetsCreatedAtToUtcNow()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var beforeGenerate = DateTimeOffset.UtcNow;

            // Act
            var token = await provider.GenerateAsync(user);
            var afterGenerate = DateTimeOffset.UtcNow;

            // Assert
            var savedToken = await context.TotpTokens.FirstOrDefaultAsync(t => t.Token == token);
            Assert.IsTrue(savedToken.CreatedAt >= beforeGenerate);
            Assert.IsTrue(savedToken.CreatedAt <= afterGenerate);
        }

        #endregion

        #region ValidateAsync Tests - Valid Scenarios

        [TestMethod]
        public async Task ValidateAsync_WithValidToken_ReturnsValid()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var token = await provider.GenerateAsync(user);

            // Act
            var result = await provider.ValidateAsync(token, user, removeToken: false);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Valid, result);
        }

        [TestMethod]
        public async Task ValidateAsync_WithValidToken_RemovesToken()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var token = await provider.GenerateAsync(user);

            // Act
            var result = await provider.ValidateAsync(token, user, removeToken: true);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Valid, result);
            
            var tokenStillExists = await context.TotpTokens.AnyAsync(t => t.Token == token);
            Assert.IsFalse(tokenStillExists, "Token should be removed after validation");
        }

        [TestMethod]
        public async Task ValidateAsync_WithRemoveTokenFalse_DoesNotRemoveToken()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var token = await provider.GenerateAsync(user);

            // Act
            var result = await provider.ValidateAsync(token, user, removeToken: false);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Valid, result);
            
            var tokenStillExists = await context.TotpTokens.AnyAsync(t => t.Token == token);
            Assert.IsTrue(tokenStillExists, "Token should not be removed when removeToken is false");
        }

        #endregion

        #region ValidateAsync Tests - Invalid Scenarios

        [TestMethod]
        public async Task ValidateAsync_WithNullToken_ReturnsInvalid()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var result = await provider.ValidateAsync(null, user);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        [TestMethod]
        public async Task ValidateAsync_WithEmptyToken_ReturnsInvalid()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var result = await provider.ValidateAsync(string.Empty, user);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        [TestMethod]
        public async Task ValidateAsync_WithWhitespaceToken_ReturnsInvalid()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var result = await provider.ValidateAsync("   ", user);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        [TestMethod]
        public async Task ValidateAsync_WithNullUser_ThrowsArgumentNullException()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);

            // Act & Assert
            try
            {
                await provider.ValidateAsync("sometoken", null);
                Assert.Fail("Expected ArgumentNullException was not thrown.");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("user", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task ValidateAsync_WithNonExistentToken_ReturnsInvalid()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var result = await provider.ValidateAsync("NonExistentToken123456789012", user);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        [TestMethod]
        public async Task ValidateAsync_WithUnconfirmedEmail_ReturnsInvalid()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = false; // Email not confirmed
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var token = await provider.GenerateAsync(user);

            // Act
            var result = await provider.ValidateAsync(token, user);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        [TestMethod]
        public async Task ValidateAsync_WithLockedOutUser_ReturnsInvalid()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1); // Locked for 1 hour
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var token = await provider.GenerateAsync(user);

            // Act
            var result = await provider.ValidateAsync(token, user);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        [TestMethod]
        public async Task ValidateAsync_WithExpiredLockout_ReturnsValid()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(-1); // Lockout expired
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var token = await provider.GenerateAsync(user);

            // Act
            var result = await provider.ValidateAsync(token, user, removeToken: false);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Valid, result);
        }

        [TestMethod]
        public async Task ValidateAsync_WithTokenForDifferentUser_ReturnsInvalid()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user1 = TestDataBuilder.CreateUser();
            user1.EmailConfirmed = true;
            var user2 = TestDataBuilder.CreateUser();
            user2.EmailConfirmed = true;
            
            context.Users.Add(user1);
            context.Users.Add(user2);
            await context.SaveChangesAsync();

            var token = await provider.GenerateAsync(user1);

            // Act - Try to validate user1's token with user2
            var result = await provider.ValidateAsync(token, user2);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Invalid, result);
        }

        #endregion

        #region ValidateAsync Tests - Expired Token

        [TestMethod]
        public async Task ValidateAsync_WithExpiredToken_ReturnsExpired()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Create a token manually with past expiration
            var expiredToken = new TotpToken
            {
                UserId = user.Id,
                Email = user.NormalizedEmail,
                Token = "ExpiredToken123456789012345678",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) // Expired 1 hour ago
            };
            context.TotpTokens.Add(expiredToken);
            await context.SaveChangesAsync();

            // Act
            var result = await provider.ValidateAsync(expiredToken.Token, user);

            // Assert
            Assert.AreEqual(TokenVerificationResult.Expired, result);
        }

        #endregion

        #region Logging Tests

        [TestMethod]
        public async Task ValidateAsync_WithValidToken_LogsInformation()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var token = await provider.GenerateAsync(user);

            // Act
            await provider.ValidateAsync(token, user, removeToken: false);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("is valid")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ValidateAsync_WithUnconfirmedEmail_LogsWarning()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = false;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var token = await provider.GenerateAsync(user);

            // Act
            await provider.ValidateAsync(token, user);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("email is not confirmed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ValidateAsync_WithLockedOutUser_LogsWarning()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var token = await provider.GenerateAsync(user);

            // Act
            await provider.ValidateAsync(token, user);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("is locked out")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ValidateAsync_WithNonExistentToken_LogsWarning()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            await provider.ValidateAsync("NonExistent12345678901234567890", user);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("does not exist")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ValidateAsync_WithExpiredToken_LogsWarning()
        {
            // Arrange
            var context = GetIsolatedContext();
            var mockLogger = new Mock<ILogger>();
            var provider = new OneTimeTokenProvider<IdentityUser>(context, mockLogger.Object);
            
            var user = TestDataBuilder.CreateUser();
            user.EmailConfirmed = true;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var expiredToken = new TotpToken
            {
                UserId = user.Id,
                Email = user.NormalizedEmail,
                Token = "Expired123456789012345678901234",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
            };
            context.TotpTokens.Add(expiredToken);
            await context.SaveChangesAsync();

            // Act
            await provider.ValidateAsync(expiredToken.Token, user);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("has expired")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion
    }
}
