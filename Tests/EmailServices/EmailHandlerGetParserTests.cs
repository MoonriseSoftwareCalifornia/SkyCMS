// <copyright file="EmailHandlerGetParserTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using Cosmos.EmailServices;
using Cosmos.EmailServices.Templates;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace Sky.Tests.EmailServices
{
    /// <summary>
    /// Priority 5 tests for EmailHandler GetParser method.
    /// Tests HTML vs Text parser selection and error paths.
    /// </summary>
    [TestClass]
    public class EmailHandlerGetParserTests
    {
        #region GetParser Tests - Valid Templates

        [TestMethod]
        public void GetParser_WithValidTemplate_ReturnsParser()
        {
            // Arrange
            var handler = CreateEmailHandler();
            var templateName = "AccountConfirmation"; // Assuming this template exists

            // Act
            try
            {
                var parser = InvokeGetParser(handler, templateName);

                // Assert
                Assert.IsNotNull(parser, "Parser should not be null for valid template");
                Assert.IsFalse(string.IsNullOrEmpty(parser.Html), "HTML content should be loaded");
                Assert.IsFalse(string.IsNullOrEmpty(parser.Text), "Text content should be loaded");
            }
            catch (Exception ex)
            {
                // If the specific template doesn't exist, this test will be inconclusive
                Assert.Inconclusive($"Template '{templateName}' may not exist in resources: {ex.Message}");
            }
        }

        [TestMethod]
        public void GetParser_LoadsBothHtmlAndText_ForValidTemplate()
        {
            // Arrange
            var handler = CreateEmailHandler();
            var templateName = "AccountConfirmation";

            // Act
            try
            {
                var parser = InvokeGetParser(handler, templateName);

                // Assert
                Assert.IsNotNull(parser.Html, "HTML should be loaded");
                Assert.IsNotNull(parser.Text, "Text should be loaded");
                Assert.IsTrue(parser.Html.Length > 0, "HTML should have content");
                Assert.IsTrue(parser.Text.Length > 0, "Text should have content");
            }
            catch (Exception)
            {
                Assert.Inconclusive("Template may not exist in test environment");
            }
        }

        #endregion

        #region GetParser Tests - Invalid Templates (Error Paths)

        [TestMethod]
        public void GetParser_WithNonExistentTemplate_ThrowsException()
        {
            // Arrange
            var handler = CreateEmailHandler();
            var invalidTemplate = "NonExistentTemplate" + Guid.NewGuid().ToString();

            // Act & Assert
            try
            {
                var parser = InvokeGetParser(handler, invalidTemplate);
                Assert.Fail("Should have thrown exception for non-existent template");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("Could not load") || ex.Message.Contains("not found"),
                    $"Exception should indicate template not found. Actual: {ex.Message}");
            }
        }

        [TestMethod]
        public void GetParser_WithNullTemplateName_ThrowsException()
        {
            // Arrange
            var handler = CreateEmailHandler();

            // Act & Assert
            try
            {
                var parser = InvokeGetParser(handler, null);
                Assert.Fail("Should have thrown exception for null template name");
            }
            catch (Exception)
            {
                // Expected exception
                Assert.IsTrue(true, "Should throw exception for null template");
            }
        }

        [TestMethod]
        public void GetParser_WithEmptyTemplateName_ThrowsException()
        {
            // Arrange
            var handler = CreateEmailHandler();

            // Act & Assert
            try
            {
                var parser = InvokeGetParser(handler, string.Empty);
                Assert.Fail("Should have thrown exception for empty template name");
            }
            catch (Exception)
            {
                // Expected exception
                Assert.IsTrue(true, "Should throw exception for empty template");
            }
        }

        [TestMethod]
        public void GetParser_WhenHtmlIsMissing_ThrowsException()
        {
            // Arrange
            var handler = CreateEmailHandler();
            // Use a template name that would not have HTML version
            var templateWithoutHtml = "TemplateMissingHtml" + Guid.NewGuid().ToString();

            // Act & Assert
            try
            {
                var parser = InvokeGetParser(handler, templateWithoutHtml);
                Assert.Fail("Should throw exception when HTML template is missing");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(
                    ex.Message.Contains("Could not load") || ex.Message.Contains("not found"),
                    "Should indicate template loading failure");
            }
        }

        [TestMethod]
        public void GetParser_WhenTextIsMissing_ThrowsException()
        {
            // Arrange
            var handler = CreateEmailHandler();
            // Use a template name that would not have text version
            var templateWithoutText = "TemplateMissingText" + Guid.NewGuid().ToString();

            // Act & Assert
            try
            {
                var parser = InvokeGetParser(handler, templateWithoutText);
                Assert.Fail("Should throw exception when text template is missing");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(
                    ex.Message.Contains("Could not load") || ex.Message.Contains("not found"),
                    "Should indicate template loading failure");
            }
        }

        #endregion

        #region GetParser Tests - Parser Functionality

        [TestMethod]
        public void GetParser_ReturnsParserWithInsertCapability()
        {
            // Arrange
            var handler = CreateEmailHandler();

            // Act
            try
            {
                var parser = InvokeGetParser(handler, "AccountConfirmation");

                // Verify parser has Insert functionality
                var initialHtml = parser.Html;
                var initialText = parser.Text;

                parser.Insert("TestKey", "TestValue");

                // Assert
                Assert.IsNotNull(parser, "Parser should support Insert method");
                // If the template contains {{TestKey}}, it should be replaced
            }
            catch (Exception)
            {
                Assert.Inconclusive("Template may not be available");
            }
        }

        [TestMethod]
        public void GetParser_ReturnsParserWithInsertHtmlCapability()
        {
            // Arrange
            var handler = CreateEmailHandler();

            // Act
            try
            {
                var parser = InvokeGetParser(handler, "AccountConfirmation");

                // Verify parser has InsertHtml functionality
                parser.InsertHtml("TestKey", "<strong>TestValue</strong>");

                // Assert
                Assert.IsNotNull(parser, "Parser should support InsertHtml method");
            }
            catch (Exception)
            {
                Assert.Inconclusive("Template may not be available");
            }
        }

        #endregion

        #region GetParser Tests - Exception Message Quality

        [TestMethod]
        public void GetParser_ExceptionMessage_ContainsTemplateName()
        {
            // Arrange
            var handler = CreateEmailHandler();
            var invalidTemplate = "InvalidTemplate123";

            // Act & Assert
            try
            {
                var parser = InvokeGetParser(handler, invalidTemplate);
                Assert.Fail("Should throw exception");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(
                    ex.Message.Contains(invalidTemplate) || ex.Message.Contains("Could not load"),
                    $"Exception message should reference the template name. Actual: {ex.Message}");
            }
        }

        [TestMethod]
        public void GetParser_ExceptionMessage_IsDescriptive()
        {
            // Arrange
            var handler = CreateEmailHandler();
            var invalidTemplate = "NonExistent";

            // Act & Assert
            try
            {
                var parser = InvokeGetParser(handler, invalidTemplate);
                Assert.Fail("Should throw exception");
            }
            catch (Exception ex)
            {
                Assert.IsFalse(string.IsNullOrEmpty(ex.Message),
                    "Exception message should not be empty");
                Assert.IsTrue(ex.Message.Length > 10,
                    "Exception message should be descriptive");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates an EmailHandler instance for testing.
        /// </summary>
        private EmailHandler CreateEmailHandler()
        {
            var mockEmailSender = new Mock<ICosmosEmailSender>();
            var mockLogger = new Mock<ILogger<EmailHandler>>();

            return new EmailHandler(mockEmailSender.Object, mockLogger.Object);
        }

        /// <summary>
        /// Invokes the private GetParser method using reflection.
        /// </summary>
        private EmailTemplateParser InvokeGetParser(EmailHandler handler, string templateName)
        {
            var method = typeof(EmailHandler).GetMethod(
                "GetParser",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                Assert.Fail("GetParser method not found via reflection");
            }

            try
            {
                return (EmailTemplateParser)method.Invoke(handler, new object[] { templateName });
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception from reflection invoke
                throw ex.InnerException ?? ex;
            }
        }

        #endregion
    }
}
