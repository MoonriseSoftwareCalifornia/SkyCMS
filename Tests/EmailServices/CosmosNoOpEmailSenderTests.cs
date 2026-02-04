// <copyright file="CosmosNoOpEmailSenderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.Net;
using System.Threading.Tasks;
using Cosmos.EmailServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sky.Tests.EmailServices
{
    /// <summary>
    /// Priority 5 tests for CosmosNoOpEmailSender.
    /// Verifies no-op behavior - methods complete without sending actual emails.
    /// </summary>
    [TestClass]
    public class CosmosNoOpEmailSenderTests
    {
        #region Constructor Tests

        [TestMethod]
        public void Constructor_CreatesInstance_Successfully()
        {
            // Act
            var sender = new CosmosNoOpEmailSender();

            // Assert
            Assert.IsNotNull(sender, "NoOpEmailSender should be created");
        }

        [TestMethod]
        public void SendResult_IsAccessible_AfterConstruction()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act
            var result = sender.SendResult;

            // Assert
            Assert.IsNotNull(result, "SendResult should not be null");
        }

        [TestMethod]
        public void SendResult_HasOkStatus_ByDefault()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act
            var result = sender.SendResult;

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode,
                "SendResult should have OK status by default");
        }

        [TestMethod]
        public void SendResult_HasNoOpMessage()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act
            var result = sender.SendResult;

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(result.Message),
                "SendResult should have a message");
            Assert.IsTrue(result.Message.Contains("NoOp", StringComparison.OrdinalIgnoreCase),
                "Message should indicate this is a NoOp sender");
        }

        #endregion

        #region SendEmailAsync(string, string, string) Tests

        [TestMethod]
        public async Task SendEmailAsync_ThreeParameters_CompletesImmediately()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();
            var email = "test@example.com";
            var subject = "Test Subject";
            var htmlMessage = "<p>Test Message</p>";

            // Act
            var task = sender.SendEmailAsync(email, subject, htmlMessage);

            // Assert
            Assert.IsNotNull(task, "Should return a task");
            Assert.IsTrue(task.IsCompleted, "Task should complete immediately");
            await task; // Should not throw
        }

        [TestMethod]
        public async Task SendEmailAsync_ThreeParameters_DoesNotThrow()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act & Assert - Should not throw
            await sender.SendEmailAsync("test@example.com", "Test", "<p>Test</p>");
            Assert.IsTrue(true, "Method should complete without throwing");
        }

        [TestMethod]
        public async Task SendEmailAsync_ThreeParameters_WithNullEmail_DoesNotThrow()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act & Assert - Should not validate or throw
            await sender.SendEmailAsync(null, "Test", "<p>Test</p>");
            Assert.IsTrue(true, "NoOp should accept null email");
        }

        [TestMethod]
        public async Task SendEmailAsync_ThreeParameters_WithEmptySubject_DoesNotThrow()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act & Assert
            await sender.SendEmailAsync("test@example.com", string.Empty, "<p>Test</p>");
            Assert.IsTrue(true, "NoOp should accept empty subject");
        }

        [TestMethod]
        public async Task SendEmailAsync_ThreeParameters_WithNullMessage_DoesNotThrow()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act & Assert
            await sender.SendEmailAsync("test@example.com", "Test", null);
            Assert.IsTrue(true, "NoOp should accept null message");
        }

        [TestMethod]
        public async Task SendEmailAsync_ThreeParameters_MultipleCallsSucceed()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act
            await sender.SendEmailAsync("test1@example.com", "Test 1", "<p>Test 1</p>");
            await sender.SendEmailAsync("test2@example.com", "Test 2", "<p>Test 2</p>");
            await sender.SendEmailAsync("test3@example.com", "Test 3", "<p>Test 3</p>");

            // Assert
            Assert.IsTrue(true, "Multiple calls should succeed");
        }

        #endregion

        #region SendEmailAsync(string, string, string, string, string) Tests

        [TestMethod]
        public async Task SendEmailAsync_FiveParameters_CompletesImmediately()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();
            var emailTo = "test@example.com";
            var subject = "Test Subject";
            var textVersion = "Plain text message";
            var htmlVersion = "<p>HTML message</p>";
            var emailFrom = "sender@example.com";

            // Act
            var task = sender.SendEmailAsync(emailTo, subject, textVersion, htmlVersion, emailFrom);

            // Assert
            Assert.IsNotNull(task, "Should return a task");
            Assert.IsTrue(task.IsCompleted, "Task should complete immediately");
            await task; // Should not throw
        }

        [TestMethod]
        public async Task SendEmailAsync_FiveParameters_DoesNotThrow()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act & Assert
            await sender.SendEmailAsync("test@example.com", "Test", "Text", "<p>HTML</p>", "from@example.com");
            Assert.IsTrue(true, "Method should complete without throwing");
        }

        [TestMethod]
        public async Task SendEmailAsync_FiveParameters_WithNullFromEmail_DoesNotThrow()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act & Assert
            await sender.SendEmailAsync("test@example.com", "Test", "Text", "<p>HTML</p>", null);
            Assert.IsTrue(true, "NoOp should accept null from email");
        }

        [TestMethod]
        public async Task SendEmailAsync_FiveParameters_WithNullTextVersion_DoesNotThrow()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act & Assert
            await sender.SendEmailAsync("test@example.com", "Test", null, "<p>HTML</p>", "from@example.com");
            Assert.IsTrue(true, "NoOp should accept null text version");
        }

        [TestMethod]
        public async Task SendEmailAsync_FiveParameters_WithNullHtmlVersion_DoesNotThrow()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act & Assert
            await sender.SendEmailAsync("test@example.com", "Test", "Text", null, "from@example.com");
            Assert.IsTrue(true, "NoOp should accept null HTML version");
        }

        [TestMethod]
        public async Task SendEmailAsync_FiveParameters_MultipleCallsSucceed()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act
            await sender.SendEmailAsync("test1@example.com", "Test 1", "Text 1", "<p>HTML 1</p>", "from@example.com");
            await sender.SendEmailAsync("test2@example.com", "Test 2", "Text 2", "<p>HTML 2</p>", "from@example.com");
            await sender.SendEmailAsync("test3@example.com", "Test 3", "Text 3", "<p>HTML 3</p>", "from@example.com");

            // Assert
            Assert.IsTrue(true, "Multiple calls should succeed");
        }

        #endregion

        #region SendResult Consistency Tests

        [TestMethod]
        public async Task SendResult_RemainsConsistent_AfterSending()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();
            var initialResult = sender.SendResult;

            // Act
            await sender.SendEmailAsync("test@example.com", "Test", "<p>Test</p>");
            var resultAfterSend = sender.SendResult;

            // Assert
            Assert.AreEqual(initialResult.StatusCode, resultAfterSend.StatusCode,
                "Status code should remain consistent");
            Assert.AreEqual(initialResult.Message, resultAfterSend.Message,
                "Message should remain consistent");
        }

        [TestMethod]
        public async Task SendResult_AlwaysReturnsOK_AfterMultipleSends()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act
            await sender.SendEmailAsync("test1@example.com", "Test 1", "<p>Test 1</p>");
            var result1 = sender.SendResult;

            await sender.SendEmailAsync("test2@example.com", "Test 2", "<p>Test 2</p>");
            var result2 = sender.SendResult;

            await sender.SendEmailAsync("test3@example.com", "Test 3", "<p>Test 3</p>");
            var result3 = sender.SendResult;

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, result1.StatusCode, "Should always return OK");
            Assert.AreEqual(HttpStatusCode.OK, result2.StatusCode, "Should always return OK");
            Assert.AreEqual(HttpStatusCode.OK, result3.StatusCode, "Should always return OK");
        }

        #endregion

        #region No-Op Behavior Verification Tests

        [TestMethod]
        public async Task SendEmailAsync_DoesNotActuallySendEmail()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();

            // Act
            await sender.SendEmailAsync("test@example.com", "Test", "<p>Test</p>");

            // Assert
            // This is a no-op sender, so it should complete instantly without any actual email sending
            // We verify this by checking that the SendResult indicates success without any delay
            Assert.AreEqual(HttpStatusCode.OK, sender.SendResult.StatusCode,
                "NoOp sender should report success without actually sending");
        }

        [TestMethod]
        public async Task SendEmailAsync_WithInvalidEmailFormat_StillSucceeds()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();
            var invalidEmail = "not-a-valid-email";

            // Act & Assert - Should not validate
            await sender.SendEmailAsync(invalidEmail, "Test", "<p>Test</p>");
            Assert.AreEqual(HttpStatusCode.OK, sender.SendResult.StatusCode,
                "NoOp sender should succeed even with invalid email format");
        }

        [TestMethod]
        public async Task SendEmailAsync_CompletesInstantly()
        {
            // Arrange
            var sender = new CosmosNoOpEmailSender();
            var startTime = DateTime.UtcNow;

            // Act
            await sender.SendEmailAsync("test@example.com", "Test", "<p>Test</p>");
            var endTime = DateTime.UtcNow;
            var elapsed = endTime - startTime;

            // Assert
            Assert.IsTrue(elapsed.TotalMilliseconds < 100,
                "NoOp sender should complete nearly instantly (< 100ms)");
        }

        #endregion
    }
}
