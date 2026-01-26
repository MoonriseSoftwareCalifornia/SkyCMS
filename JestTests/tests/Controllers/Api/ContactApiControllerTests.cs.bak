// <copyright file="ContactApiControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers.Api
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Antiforgery;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Api.Shared.Controllers;
    using Sky.Cms.Api.Shared.Features.ContactForm.Submit;
    using Sky.Cms.Api.Shared.Features.ContactForm.ValidateCaptcha;
    using Sky.Cms.Api.Shared.Features.Shared;
    using Sky.Cms.Api.Shared.Models;

    /// <summary>
    /// Unit tests for the <see cref="ContactApiController"/> class.
    /// </summary>
    [TestClass]
    public class ContactApiControllerTests
    {
        private Mock<IMediator> _mockMediator;
        private Mock<IAntiforgery> _mockAntiforgery;
        private Mock<ILogger<ContactApiController>> _mockLogger;
        private ContactApiConfig _config;
        private ContactApiController _controller;
        private DefaultHttpContext _httpContext;

        /// <summary>
        /// Initializes the test class before each test method runs.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            _mockMediator = new Mock<IMediator>();
            _mockAntiforgery = new Mock<IAntiforgery>();
            _mockLogger = new Mock<ILogger<ContactApiController>>();
            
            _config = new ContactApiConfig
            {
                AdminEmail = "admin@test.com",
                MaxMessageLength = 5000,
                RequireCaptcha = false
            };

            var options = Options.Create(_config);

            _controller = new ContactApiController(
                _mockMediator.Object,
                _mockAntiforgery.Object,
                _mockLogger.Object,
                options);

            // Setup HttpContext
            _httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = _httpContext
            };
        }

        #region Constructor Tests

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_WithNullMediator_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new ContactApiController(
                    null!,
                    _mockAntiforgery.Object,
                    _mockLogger.Object,
                    Options.Create(_config)));
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_WithNullAntiforgery_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new ContactApiController(
                    _mockMediator.Object,
                    null!,
                    _mockLogger.Object,
                    Options.Create(_config)));
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new ContactApiController(
                    _mockMediator.Object,
                    _mockAntiforgery.Object,
                    null!,
                    Options.Create(_config)));
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_WithNullConfig_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new ContactApiController(
                    _mockMediator.Object,
                    _mockAntiforgery.Object,
                    _mockLogger.Object,
                    null!));
        }

        #endregion

        #region GetContactScript Tests

        [TestMethod]
        [TestCategory("GetContactScript")]
        public void GetContactScript_WithValidRequest_ReturnsJavaScript()
        {
            // Arrange
            var tokens = new AntiforgeryTokenSet("request-token", "cookie-token", "form-field", "header");
            _mockAntiforgery
                .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
                .Returns(tokens);

            // Act
            var result = _controller.GetContactScript();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ContentResult));
            var contentResult = (ContentResult)result;
            Assert.AreEqual("application/javascript", contentResult.ContentType);
            StringAssert.Contains(contentResult.Content, "SkyCmsContact");
            StringAssert.Contains(contentResult.Content, "request-token");
        }

        [TestMethod]
        [TestCategory("GetContactScript")]
        public void GetContactScript_WithCaptchaDisabled_GeneratesScriptWithoutCaptcha()
        {
            // Arrange
            _config.RequireCaptcha = false;
            var tokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
            _mockAntiforgery
                .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
                .Returns(tokens);

            // Act
            var result = _controller.GetContactScript();

            // Assert
            var contentResult = (ContentResult)result;
            StringAssert.Contains(contentResult.Content, "requireCaptcha: false");
        }

        [TestMethod]
        [TestCategory("GetContactScript")]
        public void GetContactScript_WithTurnstileEnabled_GeneratesTurnstileScript()
        {
            // Arrange
            _config.RequireCaptcha = true;
            _config.CaptchaProvider = "turnstile";
            _config.CaptchaSiteKey = "test-site-key";
            
            var tokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
            _mockAntiforgery
                .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
                .Returns(tokens);

            // Act
            var result = _controller.GetContactScript();

            // Assert
            var contentResult = (ContentResult)result;
            StringAssert.Contains(contentResult.Content, "requireCaptcha: true");
            StringAssert.Contains(contentResult.Content, "captchaProvider: 'turnstile'");
            StringAssert.Contains(contentResult.Content, "test-site-key");
            StringAssert.Contains(contentResult.Content, "challenges.cloudflare.com/turnstile");
        }

        [TestMethod]
        [TestCategory("GetContactScript")]
        public void GetContactScript_WithReCaptchaEnabled_GeneratesReCaptchaScript()
        {
            // Arrange
            _config.RequireCaptcha = true;
            _config.CaptchaProvider = "recaptcha";
            _config.CaptchaSiteKey = "recaptcha-key";
            
            var tokens = new AntiforgeryTokenSet("token", "cookie", "field", "header");
            _mockAntiforgery
                .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
                .Returns(tokens);

            // Act
            var result = _controller.GetContactScript();

            // Assert
            var contentResult = (ContentResult)result;
            StringAssert.Contains(contentResult.Content, "requireCaptcha: true");
            StringAssert.Contains(contentResult.Content, "captchaProvider: 'recaptcha'");
            StringAssert.Contains(contentResult.Content, "recaptcha-key");
            StringAssert.Contains(contentResult.Content, "google.com/recaptcha");
        }

        [TestMethod]
        [TestCategory("GetContactScript")]
        public void GetContactScript_WhenExceptionOccurs_Returns500()
        {
            // Arrange
            _mockAntiforgery
                .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
                .Throws(new InvalidOperationException("Antiforgery error"));

            // Act
            var result = _controller.GetContactScript();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(500, objectResult.StatusCode);
        }

        [TestMethod]
        [TestCategory("GetContactScript")]
        public void GetContactScript_WhenExceptionOccurs_LogsError()
        {
            // Arrange
            _mockAntiforgery
                .Setup(x => x.GetAndStoreTokens(It.IsAny<HttpContext>()))
                .Throws(new InvalidOperationException("Antiforgery error"));

            // Act
            _controller.GetContactScript();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error generating contact form JavaScript")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Submit Tests - Validation

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "invalid-email", // Invalid email format
                Message = "Test message"
            };

            _controller.ModelState.AddModelError("Email", "Invalid email format");

            // Act
            var result = await _controller.Submit(request);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = (BadRequestObjectResult)result;
            var response = badRequestResult.Value as ContactFormResponse;
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Success);
            StringAssert.Contains(response.Message, "Validation failed");
        }

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithMissingCaptchaToken_WhenCaptchaRequired_ReturnsBadRequest()
        {
            // Arrange
            _config.RequireCaptcha = true;
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test message",
                CaptchaToken = null // Missing CAPTCHA token
            };

            // Act
            var result = await _controller.Submit(request);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = (BadRequestObjectResult)result;
            var response = badRequestResult.Value as ContactFormResponse;
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Success);
            StringAssert.Contains(response.Message, "CAPTCHA validation is required");
        }

        #endregion

        #region Submit Tests - CAPTCHA Validation

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithInvalidCaptcha_ReturnsBadRequest()
        {
            // Arrange
            _config.RequireCaptcha = true;
            _config.CaptchaProvider = "turnstile";
            
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test message",
                CaptchaToken = "invalid-token"
            };

            _mockMediator
                .Setup(x => x.QueryAsync<bool>(
                    It.IsAny<ValidateCaptchaQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Submit(request);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = (BadRequestObjectResult)result;
            var response = badRequestResult.Value as ContactFormResponse;
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Success);
            StringAssert.Contains(response.Message, "CAPTCHA validation failed");
        }

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithValidCaptcha_CallsMediatorWithCorrectQuery()
        {
            // Arrange
            _config.RequireCaptcha = true;
            _httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
            
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test message",
                CaptchaToken = "valid-token"
            };

            _mockMediator
                .Setup(x => x.QueryAsync<bool>(
                    It.IsAny<ValidateCaptchaQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var successResponse = new ContactFormResponse
            {
                Success = true,
                Message = "Thank you for your message"
            };

            _mockMediator
                .Setup(x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<ContactFormResponse>.Success(successResponse));

            // Act
            await _controller.Submit(request);

            // Assert
            _mockMediator.Verify(
                x => x.QueryAsync<bool>(
                    It.Is<ValidateCaptchaQuery>(q =>
                        q.Token == "valid-token" &&
                        q.RemoteIpAddress == "192.168.1.1"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Submit Tests - Success Cases

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithValidRequest_CallsMediatorWithCorrectCommand()
        {
            // Arrange
            _httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");
            
            var request = new ContactFormRequest
            {
                Name = "John Doe",
                Email = "john@example.com",
                Message = "This is a test message"
            };

            var successResponse = new ContactFormResponse
            {
                Success = true,
                Message = "Thank you for your message"
            };

            _mockMediator
                .Setup(x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<ContactFormResponse>.Success(successResponse));

            // Act
            await _controller.Submit(request);

            // Assert
            _mockMediator.Verify(
                x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.Is<SubmitContactFormCommand>(c =>
                        c.Request == request &&
                        c.RemoteIpAddress == "10.0.0.1"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithSuccessfulSubmission_ReturnsOk()
        {
            // Arrange
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test message"
            };

            var successResponse = new ContactFormResponse
            {
                Success = true,
                Message = "Thank you for your message. We'll get back to you soon!"
            };

            _mockMediator
                .Setup(x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<ContactFormResponse>.Success(successResponse));

            // Act
            var result = await _controller.Submit(request);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var okResult = (OkObjectResult)result;
            var response = okResult.Value as ContactFormResponse;
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Success);
            Assert.AreEqual("Thank you for your message. We'll get back to you soon!", response.Message);
        }

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithSuccessfulSubmission_LogsInformation()
        {
            // Arrange
            _httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
            
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test message"
            };

            var successResponse = new ContactFormResponse { Success = true };

            _mockMediator
                .Setup(x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<ContactFormResponse>.Success(successResponse));

            // Act
            await _controller.Submit(request);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Contact form submitted successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Submit Tests - Failure Cases

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithFailedSubmission_ReturnsBadRequest()
        {
            // Arrange
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test message"
            };

            _mockMediator
                .Setup(x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<ContactFormResponse>.Failure("Email sending failed"));

            // Act
            var result = await _controller.Submit(request);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = (BadRequestObjectResult)result;
            var response = badRequestResult.Value as ContactFormResponse;
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Success);
            StringAssert.Contains(response.Message, "Email sending failed");
        }

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WhenExceptionOccurs_Returns500()
        {
            // Arrange
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test message"
            };

            _mockMediator
                .Setup(x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            // Act
            var result = await _controller.Submit(request);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(500, objectResult.StatusCode);
            
            var response = objectResult.Value as ContactFormResponse;
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Success);
            StringAssert.Contains(response.Message, "unexpected error occurred");
        }

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WhenExceptionOccurs_LogsError()
        {
            // Arrange
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test message"
            };

            _mockMediator
                .Setup(x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            // Act
            await _controller.Submit(request);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing contact form")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Submit Tests - Edge Cases

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithNullRemoteIpAddress_UsesUnknown()
        {
            // Arrange
            _httpContext.Connection.RemoteIpAddress = null; // Null IP
            
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test message"
            };

            var successResponse = new ContactFormResponse { Success = true };

            _mockMediator
                .Setup(x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<ContactFormResponse>.Success(successResponse));

            // Act
            await _controller.Submit(request);

            // Assert
            _mockMediator.Verify(
                x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.Is<SubmitContactFormCommand>(c => c.RemoteIpAddress == "unknown"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithCancellationToken_PassesToMediator()
        {
            // Arrange
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = "Test message"
            };

            var successResponse = new ContactFormResponse { Success = true };
            var cancellationToken = new CancellationToken();

            _mockMediator
                .Setup(x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    cancellationToken))
                .ReturnsAsync(CommandResult<ContactFormResponse>.Success(successResponse));

            // Act
            await _controller.Submit(request, cancellationToken);

            // Assert
            _mockMediator.Verify(
                x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    cancellationToken),
                Times.Once);
        }

        [TestMethod]
        [TestCategory("Submit")]
        public async Task Submit_WithMaxMessageLength_AcceptsRequest()
        {
            // Arrange
            _config.MaxMessageLength = 100;
            var request = new ContactFormRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Message = new string('A', 100) // Exactly max length
            };

            var successResponse = new ContactFormResponse { Success = true };

            _mockMediator
                .Setup(x => x.SendAsync<CommandResult<ContactFormResponse>>(
                    It.IsAny<SubmitContactFormCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<ContactFormResponse>.Success(successResponse));

            // Act
            var result = await _controller.Submit(request);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        }

        #endregion
    }
}