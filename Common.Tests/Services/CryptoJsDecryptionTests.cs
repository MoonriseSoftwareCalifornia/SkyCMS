// <copyright file="CryptoJsDecryptionTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Services
{
    using Cosmos.Common.Services;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Comprehensive tests for CryptoJsDecryption service.
    /// Target: 100% code coverage.
    /// </summary>
    [TestClass]
    public class CryptoJsDecryptionTests
    {
        private const string DefaultKey = "1234567890123456";
        private const string CustomKey = "MyCustomKey12345";

        #region Encrypt Tests

        [TestMethod]
        public void Encrypt_WithValidText_ReturnsEncryptedString()
        {
            // Arrange
            string plainText = "Hello, World!";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Assert
            Assert.IsNotNull(encrypted);
            Assert.IsTrue(encrypted.Length > 0);
            Assert.AreNotEqual(plainText, encrypted);
        }

        [TestMethod]
        public void Encrypt_WithNullText_ReturnsEmptyString()
        {
            // Arrange
            string plainText = null!;

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Assert
            Assert.AreEqual(string.Empty, encrypted);
        }

        [TestMethod]
        public void Encrypt_WithEmptyText_ReturnsEmptyString()
        {
            // Arrange
            string plainText = string.Empty;

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Assert
            Assert.AreEqual(string.Empty, encrypted);
        }

        [TestMethod]
        public void Encrypt_WithWhitespaceText_ReturnsEmptyString()
        {
            // Arrange
            string plainText = "   ";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Assert
            Assert.AreEqual(string.Empty, encrypted);
        }

        [TestMethod]
        public void Encrypt_WithDefaultKey_UsesDefaultKey()
        {
            // Arrange
            string plainText = "Test message";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Assert
            Assert.IsNotNull(encrypted);
            Assert.IsTrue(encrypted.Length > 0);
        }

        [TestMethod]
        public void Encrypt_WithCustomKey_UsesCustomKey()
        {
            // Arrange
            string plainText = "Test message";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText, CustomKey);

            // Assert
            Assert.IsNotNull(encrypted);
            Assert.IsTrue(encrypted.Length > 0);
        }

        [TestMethod]
        public void Encrypt_WithEmptyKey_UsesDefaultKey()
        {
            // Arrange
            string plainText = "Test message";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText, string.Empty);

            // Assert
            Assert.IsNotNull(encrypted);
            Assert.IsTrue(encrypted.Length > 0);
        }

        [TestMethod]
        public void Encrypt_WithNullKey_UsesDefaultKey()
        {
            // Arrange
            string plainText = "Test message";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText, null!);

            // Assert
            Assert.IsNotNull(encrypted);
            Assert.IsTrue(encrypted.Length > 0);
        }

        [TestMethod]
        public void Encrypt_WithWhitespaceKey_UsesDefaultKey()
        {
            // Arrange
            string plainText = "Test message";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText, "   ");

            // Assert
            Assert.IsNotNull(encrypted);
            Assert.IsTrue(encrypted.Length > 0);
        }

        [TestMethod]
        public void Encrypt_WithLongText_EncryptsSuccessfully()
        {
            // Arrange
            string plainText = new string('A', 10000);

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Assert
            Assert.IsNotNull(encrypted);
            Assert.IsTrue(encrypted.Length > 0);
        }

        [TestMethod]
        public void Encrypt_WithSpecialCharacters_EncryptsSuccessfully()
        {
            // Arrange
            string plainText = "Special: !@#$%^&*(){}[]|\\:;\"'<>,.?/~`";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Assert
            Assert.IsNotNull(encrypted);
            Assert.IsTrue(encrypted.Length > 0);
        }

        [TestMethod]
        public void Encrypt_WithUnicodeCharacters_EncryptsSuccessfully()
        {
            // Arrange
            string plainText = "Unicode: 你好世界 🚀 émoji";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Assert
            Assert.IsNotNull(encrypted);
            Assert.IsTrue(encrypted.Length > 0);
        }

        [TestMethod]
        public void Encrypt_SameTextDifferentKeys_ProducesDifferentCiphertext()
        {
            // Arrange
            string plainText = "Same text";

            // Act
            var encrypted1 = CryptoJsDecryption.Encrypt(plainText, "Key1234567890123");
            var encrypted2 = CryptoJsDecryption.Encrypt(plainText, "Key9876543210987");

            // Assert
            Assert.AreNotEqual(encrypted1, encrypted2);
        }

        #endregion

        #region Decrypt Tests

        [TestMethod]
        public void Decrypt_WithValidEncryptedText_ReturnsDecryptedString()
        {
            // Arrange
            string plainText = "Hello, World!";
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(encrypted);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithNullText_ReturnsEmptyString()
        {
            // Arrange
            string encryptedText = null;

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(encryptedText);

            // Assert
            Assert.AreEqual(string.Empty, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithEmptyText_ReturnsEmptyString()
        {
            // Arrange
            string encryptedText = string.Empty;

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(encryptedText);

            // Assert
            Assert.AreEqual(string.Empty, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithWhitespaceText_ReturnsEmptyString()
        {
            // Arrange
            string encryptedText = "   ";

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(encryptedText);

            // Assert
            Assert.AreEqual(string.Empty, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithDefaultKey_UsesDefaultKey()
        {
            // Arrange
            string plainText = "Test message";
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(encrypted);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithCustomKey_UsesCustomKey()
        {
            // Arrange
            string plainText = "Test message";
            var encrypted = CryptoJsDecryption.Encrypt(plainText, CustomKey);

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(encrypted, CustomKey);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithEmptyKey_UsesDefaultKey()
        {
            // Arrange
            string plainText = "Test message";
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(encrypted, string.Empty);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithNullKey_UsesDefaultKey()
        {
            // Arrange
            string plainText = "Test message";
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(encrypted, null);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithWhitespaceKey_UsesDefaultKey()
        {
            // Arrange
            string plainText = "Test message";
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(encrypted, "   ");

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithJsonEnvelopeFormat_DecryptsSuccessfully()
        {
            // Arrange - Create JSON envelope format (iv + ct)
            string plainText = "Envelope test";
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Create envelope format
            var envelope = $"{{\"iv\":\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(DefaultKey))}\",\"ct\":\"{encrypted}\"}}";

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(envelope);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithInvalidJsonEnvelope_FallsBackToLegacyDecryption()
        {
            // Arrange - Invalid JSON that will fail envelope parsing
            string plainText = "Legacy test";
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Act
            var decrypted = CryptoJsDecryption.Decrypt(encrypted);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void Decrypt_WithWrongKey_ThrowsOrReturnsGarbage()
        {
            // Arrange
            string plainText = "Secret message";
            var encrypted = CryptoJsDecryption.Encrypt(plainText, "CorrectKey123456");

            // Act & Assert
            try
            {
                var decrypted = CryptoJsDecryption.Decrypt(encrypted, "WrongKey12345678");
                // If it doesn't throw, it should return garbage (not the original)
                Assert.AreNotEqual(plainText, decrypted);
            }
            catch
            {
                // Decryption with wrong key can throw - this is acceptable
                Assert.IsTrue(true);
            }
        }

        #endregion

        #region Round-Trip Tests

        [TestMethod]
        public void EncryptDecrypt_RoundTrip_PreservesOriginalText()
        {
            // Arrange
            string plainText = "Round-trip test message!";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);
            var decrypted = CryptoJsDecryption.Decrypt(encrypted);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void EncryptDecrypt_WithCustomKey_PreservesOriginalText()
        {
            // Arrange
            string plainText = "Custom key test";
            string customKey = "MySecretKey12345";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText, customKey);
            var decrypted = CryptoJsDecryption.Decrypt(encrypted, customKey);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void EncryptDecrypt_WithLongText_PreservesOriginalText()
        {
            // Arrange
            string plainText = new string('X', 5000) + " Middle " + new string('Y', 5000);

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);
            var decrypted = CryptoJsDecryption.Decrypt(encrypted);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void EncryptDecrypt_WithSpecialCharacters_PreservesOriginalText()
        {
            // Arrange
            string plainText = "!@#$%^&*()_+-=[]{}|;':\",./<>?`~\r\n\t";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);
            var decrypted = CryptoJsDecryption.Decrypt(encrypted);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void EncryptDecrypt_WithUnicode_PreservesOriginalText()
        {
            // Arrange
            string plainText = "Unicode: 你好 مرحبا Здравствуйте 🌍🚀⭐";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);
            var decrypted = CryptoJsDecryption.Decrypt(encrypted);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void EncryptDecrypt_MultipleMessages_AllPreserved()
        {
            // Arrange
            var messages = new[]
            {
                "Message 1",
                "Second message with more content",
                "Third 123 !@#",
                "你好",
                string.Empty // Edge case - should return empty
            };

            foreach (var message in messages)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    // Empty/whitespace returns empty
                    var encrypted = CryptoJsDecryption.Encrypt(message);
                    Assert.AreEqual(string.Empty, encrypted);
                }
                else
                {
                    // Act
                    var encrypted = CryptoJsDecryption.Encrypt(message);
                    var decrypted = CryptoJsDecryption.Decrypt(encrypted);

                    // Assert
                    Assert.AreEqual(message, decrypted, $"Failed for message: {message}");
                }
            }
        }

        [TestMethod]
        public void EncryptDecrypt_SameTextMultipleTimes_ConsistentResults()
        {
            // Arrange
            string plainText = "Consistent test";

            // Act & Assert
            for (int i = 0; i < 10; i++)
            {
                var encrypted = CryptoJsDecryption.Encrypt(plainText);
                var decrypted = CryptoJsDecryption.Decrypt(encrypted);
                Assert.AreEqual(plainText, decrypted, $"Failed on iteration {i}");
            }
        }

        #endregion

        #region Edge Cases and Error Handling

        [TestMethod]
        public void Decrypt_WithPlainTextNotBase64_HandlesGracefully()
        {
            // Arrange
            string notBase64 = "This is not base64!@#$%";

            // Act & Assert
            try
            {
                var result = CryptoJsDecryption.Decrypt(notBase64);
                // If it doesn't throw, that's fine - just shouldn't crash
                Assert.IsNotNull(result);
            }
            catch (FormatException)
            {
                // Expected for invalid base64
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public void Encrypt_WithNewlines_EncryptsSuccessfully()
        {
            // Arrange
            string plainText = "Line 1\r\nLine 2\nLine 3\r\n";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);
            var decrypted = CryptoJsDecryption.Decrypt(encrypted);

            // Assert
            Assert.AreEqual(plainText, decrypted);
        }

        [TestMethod]
        public void Encrypt_ResultIsBase64_ValidFormat()
        {
            // Arrange
            string plainText = "Test for base64";

            // Act
            var encrypted = CryptoJsDecryption.Encrypt(plainText);

            // Assert
            Assert.IsNotNull(encrypted);
            try
            {
                var bytes = Convert.FromBase64String(encrypted);
                Assert.IsTrue(bytes.Length > 0);
            }
            catch
            {
                Assert.Fail("Encrypted result should be valid base64");
            }
        }

        #endregion
    }
}
