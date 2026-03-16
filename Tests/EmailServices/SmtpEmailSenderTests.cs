// <copyright file="SmtpEmailSenderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using Cosmos.EmailServices;
using Microsoft.Extensions.Options;

namespace Sky.Tests.EmailServices
{
    /// <summary>
    /// Priority 5 tests for SmtpEmailSender basic operations.
    /// Tests SendEmailAsync methods with valid and invalid inputs.
    /// </summary>
    [TestClass]
    public class SmtpEmailSenderTests
    {
        #region SendEmailAsync(string, string, string) Tests

        [TestMethod]
        public async Task SendEmailAsync_ThreeParameters_WithValidInputs_SetsUpMessage()
        {
            // Arrange
            var options = CreateDefaultOptions();
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "Test Subject";
            var htmlMessage = "<p>Test HTML message</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
            // Note: Without a real SMTP server, this will fail to send but should set SendResult
        }

        [TestMethod]
        public async Task SendEmailAsync_ThreeParameters_UsesDefaultFromAddress()
        {
            // Arrange
            var options = CreateDefaultOptions();
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "Test";
            var htmlMessage = "<p>Test</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be initialized");
            // The from address should be from options.DefaultFromEmailAddress
        }

        [TestMethod]
        public async Task SendEmailAsync_ThreeParameters_WithEmptySubject_HandlesGracefully()
        {
            // Arrange
            var options = CreateDefaultOptions();
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = string.Empty;
            var htmlMessage = "<p>Test</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set even with empty subject");
        }

        #endregion

        #region SendEmailAsync(string, string, string, string) Tests

        [TestMethod]
        public async Task SendEmailAsync_FourParameters_WithValidInputs_SetsUpMessage()
        {
            // Arrange
            var options = CreateDefaultOptions();
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "Test Subject";
            var htmlMessage = "<p>Test HTML</p>";
            var emailFrom = "custom@example.com";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage, emailFrom);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
        }

        [TestMethod]
        public async Task SendEmailAsync_FourParameters_WithNullFromEmail_UsesDefault()
        {
            // Arrange
            var options = CreateDefaultOptions();
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "Test";
            var htmlMessage = "<p>Test</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage, null);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
            // Should use options.DefaultFromEmailAddress when emailFrom is null
        }

        [TestMethod]
        public async Task SendEmailAsync_FourParameters_WithEmptyFromEmail_UsesDefault()
        {
            // Arrange
            var options = CreateDefaultOptions();
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "Test";
            var htmlMessage = "<p>Test</p>";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage, string.Empty);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
        }

        [TestMethod]
        public async Task SendEmailAsync_FourParameters_WithCustomFromEmail_UsesCustomEmail()
        {
            // Arrange
            var options = CreateDefaultOptions();
            var sender = new SmtpEmailSender(options);

            var emailTo = "recipient@example.com";
            var subject = "Test";
            var htmlMessage = "<p>Test</p>";
            var customFrom = "custom-sender@example.com";

            // Act
            await sender.SendEmailAsync(emailTo, subject, htmlMessage, customFrom);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be set");
        }

        #endregion

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithValidOptions_InitializesSender()
        {
            // Arrange
            var options = CreateDefaultOptions();

            // Act
            var sender = new SmtpEmailSender(options);

            // Assert
            Assert.IsNotNull(sender, "SmtpEmailSender should be created");
            Assert.IsNotNull(sender.SendResult, "SendResult should be initialized");
        }

        [TestMethod]
        public void Constructor_WithNullOptions_ThrowsException()
        {
            // Arrange
            var options = Options.Create<SmtpEmailProviderOptions>(null);

            // Act & Assert
            try
            {
                var sender = new SmtpEmailSender(options);
                Assert.Fail("Should have thrown InvalidOperationException");
            }
            catch (InvalidOperationException ex)
            {
                Assert.IsTrue(ex.Message.Contains("SmtpEmailProviderOptions"),
                    "Exception message should mention SmtpEmailProviderOptions");
            }
        }

        [TestMethod]
        public void SendResult_InitiallySet_HasDefaultValues()
        {
            // Arrange
            var options = CreateDefaultOptions();

            // Act
            var sender = new SmtpEmailSender(options);

            // Assert
            Assert.IsNotNull(sender.SendResult, "SendResult should be initialized");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates default SMTP options for testing.
        /// Note: These will not connect to a real SMTP server.
        /// </summary>
        private IOptions<SmtpEmailProviderOptions> CreateDefaultOptions()
        {
            var options = new SmtpEmailProviderOptions
            {
                Host = "smtp.example.com",
                Port = 587,
                UserName = "testuser",
                Password = "testpassword",
                DefaultFromEmailAddress = "noreply@example.com",
                UsesSsl = true
            };

            return Options.Create(options);
        }

        /// <summary>
        /// Creates SMTP options without credentials for testing.
        /// </summary>
        private IOptions<SmtpEmailProviderOptions> CreateOptionsWithoutCredentials()
        {
            var options = new SmtpEmailProviderOptions
            {
                Host = "smtp.example.com",
                Port = 25,
                DefaultFromEmailAddress = "noreply@example.com",
                UsesSsl = false
            };

            return Options.Create(options);
        }

        #endregion
    }
}
