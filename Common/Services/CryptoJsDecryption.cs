// <copyright file="CryptoJsDecryption.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// AesDecryption utility for CryptoJS.
    /// </summary>
    public static class CryptoJsDecryption
    {
        private sealed class CryptoEnvelope
        {
            [System.Text.Json.Serialization.JsonPropertyName("iv")]
            public string Iv { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("ct")]
            public string Ct { get; set; }
        }

        /// <summary>
        /// Decrypts the encrypted text if not null or empty.
        /// </summary>
        /// <param name="encryptedText">Encrypted text.</param>
        /// <param name="keyText">Key text.</param>
        /// <returns>Decripted text.</returns>
        public static string Decrypt(string encryptedText, string keyText = "")
        {
            if (string.IsNullOrWhiteSpace(encryptedText))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(keyText))
            {
                keyText = "1234567890123456";
            }

            byte[] key = Encoding.UTF8.GetBytes(keyText);

            try
            {
                var envelope = JsonSerializer.Deserialize<CryptoEnvelope>(encryptedText);
                if (envelope != null && !string.IsNullOrWhiteSpace(envelope.Ct) && !string.IsNullOrWhiteSpace(envelope.Iv))
                {
                    var encryptedBytes = Convert.FromBase64String(envelope.Ct);
                    var ivBytes = Convert.FromBase64String(envelope.Iv);
                    return DecryptInternal(encryptedBytes, key, ivBytes);
                }
            }
            catch (JsonException)
            {
            }

            byte[] legacyEncryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] legacyIv = Encoding.UTF8.GetBytes(keyText);
            return DecryptInternal(legacyEncryptedBytes, key, legacyIv);
        }

        /// <summary>
        /// Encrypts the plain text using AES encryption with a specified key.
        /// </summary>
        /// <param name="plainText">Unencrypted text.</param>
        /// <param name="keyText">Key text.</param>
        /// <returns>Encrypted text.</returns>
        public static string Encrypt(string plainText, string keyText = "")
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(keyText))
            {
                keyText = "1234567890123456";
            }

            // Generate the key and IV using the passphrase
            byte[] key = Encoding.UTF8.GetBytes(keyText);
            byte[] iv = Encoding.UTF8.GetBytes(keyText);

            // Encrypt the data
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                    sw.Flush();
                    cs.FlushFinalBlock();
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private static string DecryptInternal(byte[] encryptedBytes, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(encryptedBytes))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}
