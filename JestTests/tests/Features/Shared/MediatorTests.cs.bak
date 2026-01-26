// <copyright file="MediatorTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Shared
{
    using Cosmos.Common.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Shared;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for the <see cref="Mediator"/> class.
    /// </summary>
    [TestClass]
    public class MediatorTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext();
        }

        #region SendAsync Tests

        [TestMethod]
        public async Task SendAsync_ValidCommand_CallsHandler()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Test Article",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public async Task SendAsync_InvalidCommand_ReturnsFailure()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = string.Empty, // Invalid
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        public async Task SendAsync_NullCommand_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            {
                await Mediator.SendAsync<CommandResult<ArticleViewModel>>(null!);
            });
        }

        [TestMethod]
        public async Task SendAsync_UnregisteredHandler_ThrowsException()
        {
            // Arrange - Create a command without a registered handler
            var unregisteredCommand = new UnregisteredCommand();

            // Act & Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            {
                await Mediator.SendAsync(unregisteredCommand);
            });
        }

        #endregion

        #region Handler Resolution Tests

        [TestMethod]
        public async Task SendAsync_MultipleHandlerTypes_ResolvesCorrectHandler()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Test Article",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsInstanceOfType(result, typeof(CommandResult<ArticleViewModel>));
        }

        #endregion

        #region Dummy Command for Testing

        private class UnregisteredCommand : ICommand<CommandResult>
        {
        }

        #endregion
    }
}
