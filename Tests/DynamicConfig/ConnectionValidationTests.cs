// <copyright file="ConnectionValidationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.DynamicConfig
{
    using Cosmos.DynamicConfig;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Unit tests for <see cref="Connection"/> entity validation attributes.
    /// Ensures data integrity and validation rules are properly enforced.
    /// </summary>
    [TestClass]
    public class ConnectionValidationTests
    {
        #region PublisherMode Validation Tests

        /// <summary>
        /// CRITICAL: Tests that PublisherMode accepts valid values.
        /// </summary>
        [TestMethod]
        [DataRow("Static")]
        [DataRow("Decoupled")]
        [DataRow("Headless")]
        [DataRow("Hybrid")]
        [DataRow("Static-dynamic")]
        [DataRow("")] // Empty string is allowed
        public void PublisherMode_WithValidValue_PassesValidation(string mode)
        {
            // Arrange
            var connection = new Connection
            {
                OwnerEmail = "foo1@acme.com",
                DomainNames = new[] { "test.com" },
                DbConn = "Server=test;",
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://test.com",
                ResourceGroup = "test-rg", // REQUIRED: Added missing required field
                PublisherMode = mode
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsTrue(isValid, $"PublisherMode '{mode}' should be valid. Errors: {string.Join(", ", results)}");
        }

        /// <summary>
        /// CRITICAL: Tests that PublisherMode rejects invalid values.
        /// </summary>
        [TestMethod]
        [DataRow("Invalid")]
        [DataRow("Dynamic")]
        [DataRow("Unsupported")]
        public void PublisherMode_WithInvalidValue_FailsValidation(string mode)
        {
            // Arrange
            var connection = new Connection
            {
                DomainNames = new[] { "test.com" },
                DbConn = "Server=test;",
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://test.com",
                ResourceGroup = "test-rg", // REQUIRED: Added missing required field
                PublisherMode = mode
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsFalse(isValid, $"PublisherMode '{mode}' should fail validation");
            Assert.IsTrue(results.Count > 0, "Should have validation errors");
        }

        #endregion

        #region Required Field Validation Tests

        /// <summary>
        /// Tests that DbConn is required.
        /// </summary>
        [TestMethod]
        public void DbConn_WhenNull_FailsValidation()
        {
            // Arrange
            var connection = new Connection
            {
                DomainNames = new[] { "test.com" },
                DbConn = null, // Required field
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://test.com"
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsFalse(isValid, "DbConn is required and should fail validation when null");
            Assert.IsTrue(results.Exists(r => r.MemberNames.Contains("DbConn")), "Should have DbConn validation error");
        }

        /// <summary>
        /// Tests that StorageConn is required.
        /// </summary>
        [TestMethod]
        public void StorageConn_WhenNull_FailsValidation()
        {
            // Arrange
            var connection = new Connection
            {
                DomainNames = new[] { "test.com" },
                DbConn = "Server=test;",
                StorageConn = null, // Required field
                WebsiteUrl = "https://test.com"
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsFalse(isValid, "StorageConn is required and should fail validation when null");
            Assert.IsTrue(results.Exists(r => r.MemberNames.Contains("StorageConn")), "Should have StorageConn validation error");
        }

        /// <summary>
        /// Tests that WebsiteUrl is required.
        /// </summary>
        [TestMethod]
        public void WebsiteUrl_WhenNull_FailsValidation()
        {
            // Arrange
            var connection = new Connection
            {
                DomainNames = new[] { "test.com" },
                DbConn = "Server=test;",
                StorageConn = "Storage=test;",
                WebsiteUrl = null // Required field
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsFalse(isValid, "WebsiteUrl is required and should fail validation when null");
            Assert.IsTrue(results.Exists(r => r.MemberNames.Contains("WebsiteUrl")), "Should have WebsiteUrl validation error");
        }

        /// <summary>
        /// Tests that ResourceGroup is required.
        /// </summary>
        [TestMethod]
        public void ResourceGroup_WhenNull_FailsValidation()
        {
            // Arrange
            var connection = new Connection
            {
                DomainNames = new[] { "test.com" },
                DbConn = "Server=test;",
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://test.com",
                ResourceGroup = null // Required field
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsFalse(isValid, "ResourceGroup is required and should fail validation when null");
            Assert.IsTrue(results.Exists(r => r.MemberNames.Contains("ResourceGroup")), "Should have ResourceGroup validation error");
        }

        #endregion

        #region Email Validation Tests

        /// <summary>
        /// Tests that OwnerEmail accepts valid email addresses.
        /// </summary>
        [TestMethod]
        [DataRow("user@example.com")]
        [DataRow("admin+tag@domain.co.uk")]
        [DataRow("test.user@sub.domain.com")]
        public void OwnerEmail_WithValidEmail_PassesValidation(string email)
        {
            // Arrange
            var connection = new Connection
            {
                DomainNames = new[] { "test.com" },
                DbConn = "Server=test;",
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://test.com",
                ResourceGroup = "test-rg", // REQUIRED: Added missing required field
                OwnerEmail = email
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsTrue(isValid, $"Email '{email}' should be valid. Errors: {string.Join(", ", results)}");
        }

        /// <summary>
        /// Tests that OwnerEmail allows null (optional field).
        /// </summary>
        [TestMethod]
        public void OwnerEmail_WhenNull_PassesValidation()
        {
            // Arrange
            var connection = new Connection
            {
                DomainNames = new[] { "test.com" },
                DbConn = "Server=test;",
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://test.com",
                ResourceGroup = "test-rg", // REQUIRED: Added missing required field
                OwnerEmail = null
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsTrue(isValid, "OwnerEmail should allow null (optional field)");
        }

        #endregion

        #region URL Validation Tests

        /// <summary>
        /// Tests that WebsiteUrl accepts valid URLs.
        /// </summary>
        [TestMethod]
        [DataRow("https://example.com")]
        [DataRow("http://www.example.com")]
        [DataRow("https://sub.domain.example.com:8080")]
        public void WebsiteUrl_WithValidUrl_PassesValidation(string url)
        {
            // Arrange
            var connection = new Connection
            {
                DomainNames = new[] { "test.com" },
                DbConn = "Server=test;",
                StorageConn = "Storage=test;",
                ResourceGroup = "test-rg", // REQUIRED: Added missing required field
                WebsiteUrl = url
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsTrue(isValid, $"URL '{url}' should be valid. Errors: {string.Join(", ", results)}");
        }

        /// <summary>
        /// Tests that WebsiteUrl rejects invalid URLs.
        /// </summary>
        [TestMethod]
        [DataRow("not-a-url")]
        [DataRow("ftp://invalid")]
        [DataRow("example.com")] // Missing protocol
        public void WebsiteUrl_WithInvalidUrl_FailsValidation(string url)
        {
            // Arrange
            var connection = new Connection
            {
                DomainNames = new[] { "test.com" },
                DbConn = "Server=test;",
                StorageConn = "Storage=test;",
                ResourceGroup = "test-rg", // REQUIRED: Added missing required field
                WebsiteUrl = url
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsFalse(isValid, $"URL '{url}' should fail validation");
            Assert.IsTrue(results.Count > 0, "Should have validation errors");
        }

        #endregion

        #region Domain Name Array Validation Tests

        /// <summary>
        /// Tests that empty DomainNames array is handled.
        /// </summary>
        [TestMethod]
        public void DomainNames_WhenEmpty_PassesValidation()
        {
            // Arrange
            var connection = new Connection
            {
                DomainNames = Array.Empty<string>(),
                DbConn = "Server=test;",
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://example.com",
                ResourceGroup = "test-rg" // REQUIRED: Added missing required field
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsTrue(isValid, "Empty DomainNames array should be valid");
        }

        #endregion

        #region Complete Entity Validation Tests

        /// <summary>
        /// Tests that a fully populated connection passes validation.
        /// </summary>
        [TestMethod]
        public void Connection_WithAllValidData_PassesValidation()
        {
            // Arrange
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                AllowSetup = true,
                DomainNames = new[] { "example.com", "www.example.com" },
                DbConn = "Server=myserver;Database=mydb;",
                StorageConn = "DefaultEndpointsProtocol=https;AccountName=myaccount;",
                Customer = "Test Customer",
                ResourceGroup = "test-rg",
                PublisherMode = "Static",
                BlobPublicUrl = "/",
                MicrosoftAppId = "app-id-123",
                PublisherRequiresAuthentication = false,
                WebsiteUrl = "https://example.com",
                OwnerEmail = "owner@example.com"
            };

            // Act
            var validationContext = new ValidationContext(connection);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(connection, validationContext, results, true);

            // Assert
            Assert.IsTrue(isValid, $"Fully populated connection should be valid. Errors: {string.Join(", ", results)}");
            Assert.AreEqual(0, results.Count, "Should have no validation errors");
        }

        #endregion
    }
}
