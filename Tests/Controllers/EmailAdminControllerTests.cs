// <copyright file="EmailAdminControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.EmailServices;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Controllers;
    using Sky.Editor.Models;
    using System;
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for the <see cref="EmailAdminController"/> class.
    /// </summary>
    [TestClass]
    public class EmailAdminControllerTests
    {
        private EmailAdminController controller = null!;
        private Mock<ICosmosEmailSender> mockEmailSender = null!;
        private SendResult sendResult = null!;

        [TestInitialize]
        public void Setup()
        {
            sendResult = new SendResult
            {
                StatusCode = HttpStatusCode.OK,
                Message = "Sent",
            };

            mockEmailSender = new Mock<ICosmosEmailSender>();
            mockEmailSender.SetupGet(x => x.SendResult).Returns(() => sendResult);
            mockEmailSender
                .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            controller = new EmailAdminController(new NullLogger<EmailAdminController>(), mockEmailSender.Object);
        }

        /// <summary>
        /// Tests that GET Index returns view with default model.
        /// </summary>
        [TestMethod]
        public void Index_Get_ReturnsViewWithModel()
        {
            // Act
            var result = controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsInstanceOfType(viewResult.Model, typeof(TestEmailMessageViewModel));
        }

        /// <summary>
        /// Tests that POST Index sends email and reports success when sender succeeds.
        /// </summary>
        [TestMethod]
        public async Task Index_Post_ValidModel_SendsEmailAndReturnsSuccessModel()
        {
            // Arrange
            var model = new TestEmailMessageViewModel
            {
                To = "recipient@example.com",
                Subject = "subject",
                Body = "body",
            };

            sendResult.StatusCode = HttpStatusCode.Accepted;
            sendResult.Message = "Accepted";

            // Act
            var result = await controller.Index(model);

            // Assert
            mockEmailSender.Verify(
                x => x.SendEmailAsync(model.To, model.Subject, model.Body, model.Body, It.IsAny<string>()),
                Times.Once);

            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var resultModel = viewResult.Model as TestEmailMessageViewModel;
            Assert.IsNotNull(resultModel);
            Assert.AreEqual(true, resultModel.Success);
            Assert.AreEqual("Accepted", resultModel.ErrorMessage);
        }

        /// <summary>
        /// Tests that POST Index skips send when model state is invalid.
        /// </summary>
        [TestMethod]
        public async Task Index_Post_InvalidModel_DoesNotSendEmail()
        {
            // Arrange
            var model = new TestEmailMessageViewModel
            {
                To = "bad-email",
                Subject = "subject",
                Body = "body",
            };
            controller.ModelState.AddModelError("To", "Invalid email");

            // Act
            var result = await controller.Index(model);

            // Assert
            mockEmailSender.Verify(
                x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);

            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreSame(model, viewResult.Model);
        }

        /// <summary>
        /// Tests that POST Index returns failure info when sender throws.
        /// </summary>
        [TestMethod]
        public async Task Index_Post_SenderThrows_ReturnsFailureModel()
        {
            // Arrange
            var model = new TestEmailMessageViewModel
            {
                To = "recipient@example.com",
                Subject = "subject",
                Body = "body",
            };

            mockEmailSender
                .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Simulated send failure"));

            // Act
            var result = await controller.Index(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var resultModel = viewResult.Model as TestEmailMessageViewModel;
            Assert.IsNotNull(resultModel);
            Assert.AreEqual(false, resultModel.Success);
            Assert.AreEqual("Simulated send failure", resultModel.ErrorMessage);
        }
    }
}
