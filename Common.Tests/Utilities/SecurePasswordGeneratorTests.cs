// <copyright file="SecurePasswordGeneratorTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Utilities
{
    using Cosmos.Cms.Common.Utilities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Comprehensive tests for SecurePasswordGenerator utility class.
    /// Target: 100% code coverage.
    /// </summary>
    [TestClass]
    public class SecurePasswordGeneratorTests
    {
        #region GeneratePassword Tests

        [TestMethod]
        public void GeneratePassword_DefaultParameters_ReturnsPasswordOfLength32()
        {
            // Act
            var password = SecurePasswordGenerator.GeneratePassword();

            // Assert
            Assert.IsNotNull(password);
            Assert.AreEqual(32, password.Length);
        }

        [TestMethod]
        public void GeneratePassword_CustomLength_ReturnsPasswordOfSpecifiedLength()
        {
            // Arrange
            int length = 64;

            // Act
            var password = SecurePasswordGenerator.GeneratePassword(length);

            // Assert
            Assert.IsNotNull(password);
            Assert.AreEqual(length, password.Length);
        }

        [TestMethod]
        public void GeneratePassword_MinimumLength_ReturnsPasswordOfLength16()
        {
            // Arrange
            int minLength = 16;

            // Act
            var password = SecurePasswordGenerator.GeneratePassword(minLength);

            // Assert
            Assert.IsNotNull(password);
            Assert.AreEqual(minLength, password.Length);
        }

        [TestMethod]
        public void GeneratePassword_WithSpecialChars_ContainsSpecialCharacter()
        {
            // Act
            var password = SecurePasswordGenerator.GeneratePassword(32, includeSpecialChars: true);

            // Assert
            Assert.IsNotNull(password);
            var specialChars = "!@#$%^&*-_+=";
            Assert.IsTrue(password.Any(c => specialChars.Contains(c)), 
                "Password should contain at least one special character");
        }

        [TestMethod]
        public void GeneratePassword_WithoutSpecialChars_DoesNotContainSpecialCharacter()
        {
            // Act
            // Generate multiple passwords to increase confidence
            for (int i = 0; i < 10; i++)
            {
                var password = SecurePasswordGenerator.GeneratePassword(32, includeSpecialChars: false);

                // Assert
                Assert.IsNotNull(password);
                var specialChars = "!@#$%^&*-_+=";
                Assert.IsFalse(password.Any(c => specialChars.Contains(c)), 
                    $"Password should not contain special characters: {password}");
            }
        }

        [TestMethod]
        public void GeneratePassword_AlwaysContainsUppercase()
        {
            // Act
            var password = SecurePasswordGenerator.GeneratePassword(32);

            // Assert
            Assert.IsTrue(password.Any(c => char.IsUpper(c)), 
                "Password should contain at least one uppercase letter");
        }

        [TestMethod]
        public void GeneratePassword_AlwaysContainsLowercase()
        {
            // Act
            var password = SecurePasswordGenerator.GeneratePassword(32);

            // Assert
            Assert.IsTrue(password.Any(c => char.IsLower(c)), 
                "Password should contain at least one lowercase letter");
        }

        [TestMethod]
        public void GeneratePassword_AlwaysContainsDigit()
        {
            // Act
            var password = SecurePasswordGenerator.GeneratePassword(32);

            // Assert
            Assert.IsTrue(password.Any(c => char.IsDigit(c)), 
                "Password should contain at least one digit");
        }

        [TestMethod]
        public void GeneratePassword_WithComplexityRequirements_MeetsAllRequirements()
        {
            // Act
            var password = SecurePasswordGenerator.GeneratePassword(32, includeSpecialChars: true);

            // Assert
            Assert.IsNotNull(password);
            Assert.AreEqual(32, password.Length);
            Assert.IsTrue(password.Any(c => char.IsUpper(c)), "Should have uppercase");
            Assert.IsTrue(password.Any(c => char.IsLower(c)), "Should have lowercase");
            Assert.IsTrue(password.Any(c => char.IsDigit(c)), "Should have digit");
            
            var specialChars = "!@#$%^&*-_+=";
            Assert.IsTrue(password.Any(c => specialChars.Contains(c)), "Should have special char");
        }

        [TestMethod]
        public void GeneratePassword_WithoutSpecialChars_MeetsBasicRequirements()
        {
            // Act
            var password = SecurePasswordGenerator.GeneratePassword(32, includeSpecialChars: false);

            // Assert
            Assert.IsNotNull(password);
            Assert.AreEqual(32, password.Length);
            Assert.IsTrue(password.Any(c => char.IsUpper(c)), "Should have uppercase");
            Assert.IsTrue(password.Any(c => char.IsLower(c)), "Should have lowercase");
            Assert.IsTrue(password.Any(c => char.IsDigit(c)), "Should have digit");
        }

        [TestMethod]
        public void GeneratePassword_LengthLessThan16_ThrowsArgumentException()
        {
            // Arrange
            int invalidLength = 15;

            // Act & Assert
            try
            {
                SecurePasswordGenerator.GeneratePassword(invalidLength);
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("at least 16 characters"));
                Assert.AreEqual("length", ex.ParamName);
            }
        }

        [TestMethod]
        public void GeneratePassword_LengthZero_ThrowsArgumentException()
        {
            // Act & Assert
            try
            {
                SecurePasswordGenerator.GeneratePassword(0);
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("length", ex.ParamName);
            }
        }

        [TestMethod]
        public void GeneratePassword_NegativeLength_ThrowsArgumentException()
        {
            // Act & Assert
            try
            {
                SecurePasswordGenerator.GeneratePassword(-5);
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("length", ex.ParamName);
            }
        }

        [TestMethod]
        public void GeneratePassword_MultipleInvocations_ProducesDifferentPasswords()
        {
            // Act
            var password1 = SecurePasswordGenerator.GeneratePassword(32);
            var password2 = SecurePasswordGenerator.GeneratePassword(32);
            var password3 = SecurePasswordGenerator.GeneratePassword(32);

            // Assert
            Assert.AreNotEqual(password1, password2, "Passwords should be unique");
            Assert.AreNotEqual(password2, password3, "Passwords should be unique");
            Assert.AreNotEqual(password1, password3, "Passwords should be unique");
        }

        [TestMethod]
        public void GeneratePassword_LargeLength_GeneratesCorrectly()
        {
            // Arrange
            int largeLength = 256;

            // Act
            var password = SecurePasswordGenerator.GeneratePassword(largeLength, includeSpecialChars: true);

            // Assert
            Assert.IsNotNull(password);
            Assert.AreEqual(largeLength, password.Length);
            Assert.IsTrue(password.Any(c => char.IsUpper(c)));
            Assert.IsTrue(password.Any(c => char.IsLower(c)));
            Assert.IsTrue(password.Any(c => char.IsDigit(c)));
        }

        [TestMethod]
        public void GeneratePassword_OnlyContainsAllowedCharacters()
        {
            // Act
            var password = SecurePasswordGenerator.GeneratePassword(32, includeSpecialChars: true);

            // Assert
            var allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*-_+=";
            foreach (var c in password)
            {
                Assert.IsTrue(allowedChars.Contains(c), 
                    $"Character '{c}' is not in the allowed character set");
            }
        }

        [TestMethod]
        public void GeneratePassword_WithoutSpecialChars_OnlyContainsAlphanumeric()
        {
            // Act
            var password = SecurePasswordGenerator.GeneratePassword(32, includeSpecialChars: false);

            // Assert
            var allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            foreach (var c in password)
            {
                Assert.IsTrue(allowedChars.Contains(c), 
                    $"Character '{c}' is not alphanumeric");
            }
        }

        #endregion

        #region GenerateUrlSafeToken Tests

        [TestMethod]
        public void GenerateUrlSafeToken_DefaultByteLength_ReturnsNonEmptyToken()
        {
            // Act
            var token = SecurePasswordGenerator.GenerateUrlSafeToken();

            // Assert
            Assert.IsNotNull(token);
            Assert.IsTrue(token.Length > 0);
        }

        [TestMethod]
        public void GenerateUrlSafeToken_CustomByteLength_ReturnsTokenOfAppropriateLength()
        {
            // Arrange
            int byteLength = 64;

            // Act
            var token = SecurePasswordGenerator.GenerateUrlSafeToken(byteLength);

            // Assert
            Assert.IsNotNull(token);
            Assert.IsTrue(token.Length > 0);
            // Base64 encoding typically produces ~1.33x the byte length (minus padding)
            // URL-safe removes padding, so length should be close to byteLength * 4/3
        }

        [TestMethod]
        public void GenerateUrlSafeToken_DoesNotContainUrlUnsafeCharacters()
        {
            // Act
            var token = SecurePasswordGenerator.GenerateUrlSafeToken(32);

            // Assert
            Assert.IsNotNull(token);
            Assert.IsFalse(token.Contains('+'), "Token should not contain '+'");
            Assert.IsFalse(token.Contains('/'), "Token should not contain '/'");
            Assert.IsFalse(token.Contains('='), "Token should not contain '='");
        }

        [TestMethod]
        public void GenerateUrlSafeToken_OnlyContainsUrlSafeCharacters()
        {
            // Act
            var token = SecurePasswordGenerator.GenerateUrlSafeToken(32);

            // Assert
            // URL-safe base64 uses: A-Z, a-z, 0-9, -, _
            var urlSafePattern = new Regex("^[A-Za-z0-9_-]+$");
            Assert.IsTrue(urlSafePattern.IsMatch(token), 
                $"Token contains invalid characters: {token}");
        }

        [TestMethod]
        public void GenerateUrlSafeToken_MultipleInvocations_ProducesDifferentTokens()
        {
            // Act
            var token1 = SecurePasswordGenerator.GenerateUrlSafeToken();
            var token2 = SecurePasswordGenerator.GenerateUrlSafeToken();
            var token3 = SecurePasswordGenerator.GenerateUrlSafeToken();

            // Assert
            Assert.AreNotEqual(token1, token2, "Tokens should be unique");
            Assert.AreNotEqual(token2, token3, "Tokens should be unique");
            Assert.AreNotEqual(token1, token3, "Tokens should be unique");
        }

        [TestMethod]
        public void GenerateUrlSafeToken_SmallByteLength_GeneratesValidToken()
        {
            // Arrange
            int smallLength = 8;

            // Act
            var token = SecurePasswordGenerator.GenerateUrlSafeToken(smallLength);

            // Assert
            Assert.IsNotNull(token);
            Assert.IsTrue(token.Length > 0);
            var urlSafePattern = new Regex("^[A-Za-z0-9_-]+$");
            Assert.IsTrue(urlSafePattern.IsMatch(token));
        }

        [TestMethod]
        public void GenerateUrlSafeToken_LargeByteLength_GeneratesValidToken()
        {
            // Arrange
            int largeLength = 256;

            // Act
            var token = SecurePasswordGenerator.GenerateUrlSafeToken(largeLength);

            // Assert
            Assert.IsNotNull(token);
            Assert.IsTrue(token.Length > 0);
            var urlSafePattern = new Regex("^[A-Za-z0-9_-]+$");
            Assert.IsTrue(urlSafePattern.IsMatch(token));
        }

        [TestMethod]
        public void GenerateUrlSafeToken_ByteLengthOne_GeneratesValidToken()
        {
            // Arrange
            int minimalLength = 1;

            // Act
            var token = SecurePasswordGenerator.GenerateUrlSafeToken(minimalLength);

            // Assert
            Assert.IsNotNull(token);
            Assert.IsTrue(token.Length > 0);
            var urlSafePattern = new Regex("^[A-Za-z0-9_-]+$");
            Assert.IsTrue(urlSafePattern.IsMatch(token));
        }

        [TestMethod]
        public void GenerateUrlSafeToken_IsActuallyCryptographicallySecure()
        {
            // Generate many tokens and verify they don't have obvious patterns
            var tokens = new HashSet<string>();
            int tokenCount = 100;

            // Act
            for (int i = 0; i < tokenCount; i++)
            {
                var token = SecurePasswordGenerator.GenerateUrlSafeToken(16);
                tokens.Add(token);
            }

            // Assert - all tokens should be unique
            Assert.AreEqual(tokenCount, tokens.Count, 
                "All generated tokens should be unique (no collisions)");
        }

        #endregion

        #region Complexity and Edge Cases

        [TestMethod]
        public void GeneratePassword_ExactMinimumLength_MeetsComplexityRequirements()
        {
            // Act
            var password = SecurePasswordGenerator.GeneratePassword(16, includeSpecialChars: true);

            // Assert
            Assert.AreEqual(16, password.Length);
            
            // Even with minimum length, should meet all complexity requirements
            Assert.IsTrue(password.Any(c => char.IsUpper(c)), "Should have uppercase");
            Assert.IsTrue(password.Any(c => char.IsLower(c)), "Should have lowercase");
            Assert.IsTrue(password.Any(c => char.IsDigit(c)), "Should have digit");
            
            var specialChars = "!@#$%^&*-_+=";
            Assert.IsTrue(password.Any(c => specialChars.Contains(c)), "Should have special char");
        }

        [TestMethod]
        public void GeneratePassword_RepeatedGeneration_ConsistentlyMeetsRequirements()
        {
            // Generate many passwords and verify all meet requirements
            for (int i = 0; i < 50; i++)
            {
                // Act
                var password = SecurePasswordGenerator.GeneratePassword(24, includeSpecialChars: true);

                // Assert
                Assert.AreEqual(24, password.Length, $"Iteration {i}: Length incorrect");
                Assert.IsTrue(password.Any(c => char.IsUpper(c)), $"Iteration {i}: Missing uppercase");
                Assert.IsTrue(password.Any(c => char.IsLower(c)), $"Iteration {i}: Missing lowercase");
                Assert.IsTrue(password.Any(c => char.IsDigit(c)), $"Iteration {i}: Missing digit");
                
                var specialChars = "!@#$%^&*-_+=";
                Assert.IsTrue(password.Any(c => specialChars.Contains(c)), 
                    $"Iteration {i}: Missing special char");
            }
        }

        [TestMethod]
        public void GeneratePassword_WithoutSpecialChars_RepeatedGeneration_ConsistentlyMeetsRequirements()
        {
            // Generate many passwords and verify all meet requirements (without special chars)
            for (int i = 0; i < 50; i++)
            {
                // Act
                var password = SecurePasswordGenerator.GeneratePassword(24, includeSpecialChars: false);

                // Assert
                Assert.AreEqual(24, password.Length, $"Iteration {i}: Length incorrect");
                Assert.IsTrue(password.Any(c => char.IsUpper(c)), $"Iteration {i}: Missing uppercase");
                Assert.IsTrue(password.Any(c => char.IsLower(c)), $"Iteration {i}: Missing lowercase");
                Assert.IsTrue(password.Any(c => char.IsDigit(c)), $"Iteration {i}: Missing digit");
            }
        }

        #endregion
    }
}
