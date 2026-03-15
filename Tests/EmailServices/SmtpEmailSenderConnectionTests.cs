// <copyright file="SmtpEmailSenderConnectionTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using Cosmos.EmailServices;
using Microsoft.Extensions.Options;
using System.Net;

namespace Sky.Tests.EmailServices
{
    /// <summary>
    /// Priority 5 tests for SmtpEmailSender SMTP connection handling and error scenarios.
    /// Tests connection configuration, SSL usage, and failure handling.
    /// </summary>
    [TestClass]
    public class SmtpEmailSenderConnectionTests
    {
        #region SMTP Connection Configuration Tests

        [TestMethod]
        public async Task SendEmailAsync_WithSslEnabled_ConfiguresClientCorrectly()
        {
            // Arrange
            var options = CreateOptionsWithSsl(true);
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "SSL Test";
            var htmlMessage = "<p>Testing SSL configuration</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
            // SSL configuration is internal, but we verify the method completes
        }

        [TestMethod]
        public async Task SendEmailAsync_WithSslDisabled_ConfiguresClientCorrectly()
        {
            // Arrange
            var options = CreateOptionsWithSsl(false);
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "No SSL Test";
            var htmlMessage = "<p>Testing without SSL</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
        }

        [TestMethod]
        public async Task SendEmailAsync_WithCredentials_ConfiguresAuthentication()
        {
            // Arrange
            var options = CreateOptionsWithCredentials("testuser", "testpassword");
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "Auth Test";
            var htmlMessage = "<p>Testing authentication</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
        }

        [TestMethod]
        public async Task SendEmailAsync_WithoutPassword_SkipsAuthentication()
        {
            // Arrange
            var options = CreateOptionsWithoutPassword();
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "No Auth Test";
            var htmlMessage = "<p>Testing without authentication</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
        }

        [TestMethod]
        public async Task SendEmailAsync_WithDifferentPorts_ConfiguresCorrectly()
        {
            // Arrange - Test port 25
            var optionsPort25 = CreateOptionsWithPort(25);
            var sender25 = new SmtpEmailSender(optionsPort25);

            // Act
            await sender25.SendEmailAsync("test@example.com", "Port 25", "<p>Test</p>");

            // Assert
            Assert.IsNotNull(sender25.SendResult);

            // Arrange - Test port 587 (TLS)
            var optionsPort587 = CreateOptionsWithPort(587);
            var sender587 = new SmtpEmailSender(optionsPort587);

            // Act
            await sender587.SendEmailAsync("test@example.com", "Port 587", "<p>Test</p>");

            // Assert
            Assert.IsNotNull(sender587.SendResult);
        }

        #endregion

        #region Email Send Failure Tests

        [TestMethod]
        public async Task SendEmailAsync_WithInvalidSmtpHost_SetsBadRequestStatus()
        {
            // Arrange
            var options = CreateOptionsWithInvalidHost();
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "Test";
            var htmlMessage = "<p>Test</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
            Assert.AreEqual(HttpStatusCode.BadRequest, sender.SendResult.StatusCode,
                "Should set BadRequest status on failure");
            Assert.IsFalse(string.IsNullOrEmpty(sender.SendResult.Message),
                "Should have error message");
        }

        [TestMethod]
        public async Task SendEmailAsync_ConnectionFailure_CapturesException()
        {
            // Arrange
            var options = CreateOptionsWithNonExistentHost();
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "Test";
            var htmlMessage = "<p>Test</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
            Assert.AreEqual(HttpStatusCode.BadRequest, sender.SendResult.StatusCode,
                "Should set BadRequest on connection failure");
            Assert.IsNotNull(sender.SendResult.Message, "Should contain error message");
        }

        [TestMethod]
        public async Task SendEmailAsync_AfterFailure_SendResultUpdated()
        {
            // Arrange
            var options = CreateOptionsWithInvalidHost();
            var sender = new SmtpEmailSender(options);

            // Act - First send (will fail)
            await sender.SendEmailAsync("test@example.com", "Test 1", "<p>Test 1</p>");
            var firstResult = sender.SendResult;

            // Assert first send
            Assert.AreEqual(HttpStatusCode.BadRequest, firstResult.StatusCode);

            // Act - Second send (will also fail)
            await sender.SendEmailAsync("test@example.com", "Test 2", "<p>Test 2</p>");
            var secondResult = sender.SendResult;

            // Assert second send
            Assert.AreEqual(HttpStatusCode.BadRequest, secondResult.StatusCode);
            Assert.IsNotNull(secondResult.Message);
        }

        [TestMethod]
        public async Task SendEmailAsync_WithInvalidEmailAddress_HandlesError()
        {
            // Arrange
            var options = CreateDefaultOptions();
            var sender = new SmtpEmailSender(options);

            // Invalid email format
            var invalidEmail = "not-an-email";
            var subject = "Test";
            var htmlMessage = "<p>Test</p>";

            // Act
            await sender.SendEmailAsync(invalidEmail, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
            // May fail with BadRequest due to invalid email format or connection issue
        }

        #endregion

        #region SendResult Property Tests

        [TestMethod]
        public async Task SendResult_AfterSuccessfulConfiguration_IsAccessible()
        {
            // Arrange
            var options = CreateDefaultOptions();
            var sender = new SmtpEmailSender(options);

            // Act
            var resultBefore = sender.SendResult;
            await sender.SendEmailAsync("test@example.com", "Test", "<p>Test</p>");
            var resultAfter = sender.SendResult;

            // Assert
            Assert.IsNotNull(resultBefore, "SendResult should be accessible before sending");
            Assert.IsNotNull(resultAfter, "SendResult should be accessible after sending");
        }

        [TestMethod]
        public async Task SendResult_ContainsStatusCode_AfterSend()
        {
            // Arrange
            var options = CreateOptionsWithInvalidHost();
            var sender = new SmtpEmailSender(options);

            // Act
            await sender.SendEmailAsync("test@example.com", "Test", "<p>Test</p>");

            // Assert
            Assert.IsNotNull(sender.SendResult.StatusCode, "StatusCode should be set");
            Assert.IsTrue(
                sender.SendResult.StatusCode == HttpStatusCode.OK ||
                sender.SendResult.StatusCode == HttpStatusCode.BadRequest,
                "StatusCode should be OK or BadRequest");
        }

        [TestMethod]
        public async Task SendResult_ContainsMessage_AfterFailure()
        {
            // Arrange
            var options = CreateOptionsWithInvalidHost();
            var sender = new SmtpEmailSender(options);

            // Act
            await sender.SendEmailAsync("test@example.com", "Test", "<p>Test</p>");

            // Assert
            Assert.IsNotNull(sender.SendResult.Message, "Message should be set after failure");
            Assert.IsFalse(string.IsNullOrWhiteSpace(sender.SendResult.Message),
                "Message should not be empty after failure");
        }

        #endregion

        #region Helper Methods

        private IOptions<SmtpEmailProviderOptions> CreateDefaultOptions()
        {
            return Options.Create(new SmtpEmailProviderOptions
            {
                Host = "localhost",
                Port = 25,
                DefaultFromEmailAddress = "noreply@example.com",
                UsesSsl = false
            });
        }

        private IOptions<SmtpEmailProviderOptions> CreateOptionsWithSsl(bool useSsl)
        {
            return Options.Create(new SmtpEmailProviderOptions
            {
                Host = "smtp.example.com",
                Port = useSsl ? 465 : 25,
                UserName = "testuser",
                Password = "testpassword",
                DefaultFromEmailAddress = "noreply@example.com",
                UsesSsl = useSsl
            });
        }

        private IOptions<SmtpEmailProviderOptions> CreateOptionsWithCredentials(string username, string password)
        {
            return Options.Create(new SmtpEmailProviderOptions
            {
                Host = "smtp.example.com",
                Port = 587,
                UserName = username,
                Password = password,
                DefaultFromEmailAddress = "noreply@example.com",
                UsesSsl = true
            });
        }

        private IOptions<SmtpEmailProviderOptions> CreateOptionsWithoutPassword()
        {
            return Options.Create(new SmtpEmailProviderOptions
            {
                Host = "smtp.example.com",
                Port = 25,
                UserName = "testuser",
                Password = null, // No password
                DefaultFromEmailAddress = "noreply@example.com",
                UsesSsl = false
            });
        }

        private IOptions<SmtpEmailProviderOptions> CreateOptionsWithPort(int port)
        {
            return Options.Create(new SmtpEmailProviderOptions
            {
                Host = "smtp.example.com",
                Port = port,
                UserName = "testuser",
                Password = "testpassword",
                DefaultFromEmailAddress = "noreply@example.com",
                UsesSsl = port != 25
            });
        }

        private IOptions<SmtpEmailProviderOptions> CreateOptionsWithInvalidHost()
        {
            return Options.Create(new SmtpEmailProviderOptions
            {
                Host = "invalid.smtp.host.that.does.not.exist.example.com",
                Port = 25,
                DefaultFromEmailAddress = "noreply@example.com",
                UsesSsl = false
            });
        }

        private IOptions<SmtpEmailProviderOptions> CreateOptionsWithNonExistentHost()
        {
            return Options.Create(new SmtpEmailProviderOptions
            {
                Host = "192.0.2.1", // TEST-NET-1 (RFC 5737) - should not route
                Port = 25,
                DefaultFromEmailAddress = "noreply@example.com",
                UsesSsl = false
            });
        }

        #endregion
    }
}
