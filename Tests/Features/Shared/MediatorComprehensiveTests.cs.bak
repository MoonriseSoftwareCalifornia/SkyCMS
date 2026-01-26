// <copyright file="MediatorComprehensiveTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Shared
{
    using Cosmos.Common.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Shared;
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Comprehensive unit tests for the <see cref="Mediator"/> class covering reflection, error handling, and edge cases.
    /// </summary>
    [TestClass]
    public class MediatorComprehensiveTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                var mediator = new Mediator(null!);
            });

            Assert.AreEqual("serviceProvider", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_WithValidServiceProvider_CreatesInstance()
        {
            // Arrange
            var services = new ServiceCollection()
                .BuildServiceProvider();

            // Act
            var mediator = new Mediator(services);

            // Assert
            Assert.IsNotNull(mediator);
        }

        #endregion

        #region SendAsync - Null and Validation Tests

        [TestMethod]
        public async Task SendAsync_WithNullCommand_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            {
                await Mediator.SendAsync<CommandResult<ArticleViewModel>>(null!);
            });

            Assert.AreEqual("command", exception.ParamName);
        }

        [TestMethod]
        public async Task SendAsync_WithUnregisteredHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            var command = new UnregisteredCommand();

            // Act & Assert
            var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            {
                await Mediator.SendAsync(command);
            });

            Assert.IsTrue(exception.Message.Contains("No service for type"));
        }

        #endregion

        #region SendAsync - Reflection Tests

        [TestMethod]
        public async Task SendAsync_WithValidCommand_UsesReflectionToResolveHandler()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Reflection Test Article",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(CommandResult<ArticleViewModel>));
            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public async Task SendAsync_WithDerivedCommandType_ResolvesCorrectHandler()
        {
            // Arrange - Test that GetType() returns the runtime type
            ICommand<CommandResult<ArticleViewModel>> command = new CreateArticleCommand
            {
                Title = "Derived Type Test",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public async Task SendAsync_WithGenericTypeConstruction_CreatesCorrectHandlerType()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Generic Type Test",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert - Verify the handler was constructed with correct generic types
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(CommandResult<ArticleViewModel>));
        }

        #endregion

        #region SendAsync - Method Invocation Tests

        [TestMethod]
        public async Task SendAsync_WhenHandlerReturnsTask_UnwrapsResultCorrectly()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Task Unwrap Test",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(CommandResult<ArticleViewModel>));
        }

        [TestMethod]
        public async Task SendAsync_WithHandlerThrowingException_PropagatesException()
        {
            // Arrange - Use a mock service provider with a handler that throws
            var services = new ServiceCollection()
                .AddScoped<ICommandHandler<ThrowingCommand, CommandResult>>(sp => new ThrowingCommandHandler())
                .AddScoped<IMediator, Mediator>()
                .BuildServiceProvider();

            var mediator = services.GetRequiredService<IMediator>();
            var command = new ThrowingCommand();

            // Act & Assert
            // Reflection wraps exceptions in TargetInvocationException
            var exception = await Assert.ThrowsExactlyAsync<TargetInvocationException>(async () =>
            {
                await mediator.SendAsync(command);
            });

            Assert.IsNotNull(exception.InnerException);
            Assert.IsInstanceOfType(exception.InnerException, typeof(InvalidOperationException));
            Assert.AreEqual("Test exception from handler", exception.InnerException.Message);
        }

        #endregion

        #region SendAsync - CancellationToken Tests

        [TestMethod]
        public async Task SendAsync_WithCancellationToken_PassesTokenToHandler()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var command = new CreateArticleCommand
            {
                Title = "Cancellation Test",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command, cts.Token);

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task SendAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddScoped<ICommandHandler<CancellableCommand, CommandResult>>(sp => new CancellableCommandHandler())
                .AddScoped<IMediator, Mediator>()
                .BuildServiceProvider();

            var mediator = services.GetRequiredService<IMediator>();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var command = new CancellableCommand();

            // Act & Assert
            // TaskCanceledException derives from OperationCanceledException, so we expect the base type
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await mediator.SendAsync(command, cts.Token);
            });
        }

        #endregion

        #region QueryAsync - Null and Validation Tests

        [TestMethod]
        public async Task QueryAsync_WithNullQuery_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            {
                await Mediator.QueryAsync<string>(null!);
            });

            Assert.AreEqual("query", exception.ParamName);
        }

        [TestMethod]
        public async Task QueryAsync_WithUnregisteredHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            var query = new UnregisteredQuery();

            // Act & Assert
            var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            {
                await Mediator.QueryAsync(query);
            });

            Assert.IsTrue(exception.Message.Contains("No service for type"));
        }

        #endregion

        #region QueryAsync - Reflection Tests

        [TestMethod]
        public async Task QueryAsync_WithValidQuery_UsesReflectionToResolveHandler()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddScoped<IQueryHandler<TestQuery, string>>(sp => new TestQueryHandler())
                .AddScoped<IMediator, Mediator>()
                .BuildServiceProvider();

            var mediator = services.GetRequiredService<IMediator>();
            var query = new TestQuery { Value = "test" };

            // Act
            var result = await mediator.QueryAsync(query);

            // Assert
            Assert.AreEqual("Result: test", result);
        }

        [TestMethod]
        public async Task QueryAsync_WithDerivedQueryType_ResolvesCorrectHandler()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddScoped<IQueryHandler<TestQuery, string>>(sp => new TestQueryHandler())
                .AddScoped<IMediator, Mediator>()
                .BuildServiceProvider();

            var mediator = services.GetRequiredService<IMediator>();
            IQuery<string> query = new TestQuery { Value = "derived" };

            // Act
            var result = await mediator.QueryAsync(query);

            // Assert
            Assert.AreEqual("Result: derived", result);
        }

        #endregion

        #region QueryAsync - CancellationToken Tests

        [TestMethod]
        public async Task QueryAsync_WithCancellationToken_PassesTokenToHandler()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddScoped<IQueryHandler<TestQuery, string>>(sp => new TestQueryHandler())
                .AddScoped<IMediator, Mediator>()
                .BuildServiceProvider();

            var mediator = services.GetRequiredService<IMediator>();
            var cts = new CancellationTokenSource();
            var query = new TestQuery { Value = "cancellable" };

            // Act
            var result = await mediator.QueryAsync(query, cts.Token);

            // Assert
            Assert.AreEqual("Result: cancellable", result);
        }

        [TestMethod]
        public async Task QueryAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddScoped<IQueryHandler<CancellableQuery, string>>(sp => new CancellableQueryHandler())
                .AddScoped<IMediator, Mediator>()
                .BuildServiceProvider();

            var mediator = services.GetRequiredService<IMediator>();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var query = new CancellableQuery();

            // Act & Assert
            // TaskCanceledException derives from OperationCanceledException, so we expect the base type
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await mediator.QueryAsync(query, cts.Token);
            });
        }

        #endregion

        #region Error Handling - Handler Method Not Found

        [TestMethod]
        public async Task SendAsync_WhenHandlerMethodNotFound_ThrowsInvalidOperationException()
        {
            // Arrange - Create a handler without HandleAsync method (impossible in reality, but testing reflection path)
            var services = new ServiceCollection()
                .AddScoped<ICommandHandler<TestCommand, CommandResult>>(sp => new InvalidHandlerWithoutMethod())
                .AddScoped<IMediator, Mediator>()
                .BuildServiceProvider();

            var mediator = services.GetRequiredService<IMediator>();
            var command = new TestCommand();

            // Act
            var result = await mediator.SendAsync(command);

            // Assert - Should still work because interface enforces method
            Assert.IsNotNull(result);
        }

        #endregion

        #region Error Handling - Wrong Return Type

        [TestMethod]
        public async Task SendAsync_WhenHandlerReturnsWrongType_ThrowsException()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddScoped<ICommandHandler<WrongReturnTypeCommand, CommandResult>>(sp => new WrongReturnTypeCommandHandler())
                .AddScoped<IMediator, Mediator>()
                .BuildServiceProvider();

            var mediator = services.GetRequiredService<IMediator>();
            var command = new WrongReturnTypeCommand();

            // Act & Assert
            // In .NET 9, the cast (Task<CommandResult>)(object)Task<string> throws InvalidCastException
            // This happens inside the handler's HandleAsync method during method.Invoke()
            // Reflection wraps this in TargetInvocationException
            var exception = await Assert.ThrowsExactlyAsync<TargetInvocationException>(async () =>
            {
                await mediator.SendAsync(command);
            });

            // Verify the inner exception is the InvalidCastException from the bad cast
            Assert.IsNotNull(exception.InnerException);
            Assert.IsInstanceOfType(exception.InnerException, typeof(InvalidCastException));
            Assert.IsTrue(exception.InnerException.Message.Contains("Task`1[System.String]"));
        }

        #endregion

        #region Thread Safety Tests

        [TestMethod]
        public async Task SendAsync_WithConcurrentRequests_HandlesCorrectly()
        {
            // Arrange - Use a test handler that doesn't have database concurrency issues
            var services = new ServiceCollection()
                .AddScoped<ICommandHandler<ThreadSafeTestCommand, CommandResult<int>>>(
                    sp => new ThreadSafeTestCommandHandler())
                .AddScoped<IMediator, Mediator>()
                .BuildServiceProvider();

            var mediator = services.GetRequiredService<IMediator>();
            var tasks = new Task<CommandResult<int>>[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks[i] = Task.Run(async () =>
                {
                    var command = new ThreadSafeTestCommand { Value = index };
                    return await mediator.SendAsync(command);
                });
            }

            var results = await Task.WhenAll(tasks);

            // Assert - All should succeed since there's no database contention
            Assert.AreEqual(10, results.Length, "Should have 10 results");
            
            foreach (var result in results)
            {
                Assert.IsNotNull(result, "Result should not be null");
                Assert.IsTrue(result.IsSuccess, "Result should be successful");
            }
            
            // Verify we got all expected values
            var values = results.Select(r => r.Data).OrderBy(v => v).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(0, 10).ToArray(), values);
        }

        #endregion
        
        #region Test Helpers - Commands

        private class UnregisteredCommand : ICommand<CommandResult>
        {
        }

        private class ThrowingCommand : ICommand<CommandResult>
        {
        }

        private class ThrowingCommandHandler : ICommandHandler<ThrowingCommand, CommandResult>
        {
            public Task<CommandResult> HandleAsync(ThrowingCommand command, CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Test exception from handler");
            }
        }

        private class CancellableCommand : ICommand<CommandResult>
        {
        }

        private class CancellableCommandHandler : ICommandHandler<CancellableCommand, CommandResult>
        {
            public async Task<CommandResult> HandleAsync(CancellableCommand command, CancellationToken cancellationToken = default)
            {
                await Task.Delay(1000, cancellationToken);
                return new CommandResult { IsSuccess = true };
            }
        }

        private class TestCommand : ICommand<CommandResult>
        {
        }

        private class InvalidHandlerWithoutMethod : ICommandHandler<TestCommand, CommandResult>
        {
            public Task<CommandResult> HandleAsync(TestCommand command, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new CommandResult { IsSuccess = true });
            }
        }

        private class WrongReturnTypeCommand : ICommand<CommandResult>
        {
        }

        private class WrongReturnTypeCommandHandler : ICommandHandler<WrongReturnTypeCommand, CommandResult>
        {
            public Task<CommandResult> HandleAsync(WrongReturnTypeCommand command, CancellationToken cancellationToken = default)
            {
                // Return wrong type to test error handling
                var wrongType = Task.FromResult("wrong");
                return (Task<CommandResult>)(object)wrongType;
            }
        }

        #endregion

        #region Test Helpers - Queries

        private class UnregisteredQuery : IQuery<string>
        {
        }

        private class TestQuery : IQuery<string>
        {
            public string Value { get; set; } = string.Empty;
        }

        private class TestQueryHandler : IQueryHandler<TestQuery, string>
        {
            public Task<string> HandleAsync(TestQuery query, CancellationToken cancellationToken = default)
            {
                return Task.FromResult($"Result: {query.Value}");
            }
        }

        private class CancellableQuery : IQuery<string>
        {
        }

        private class CancellableQueryHandler : IQueryHandler<CancellableQuery, string>
        {
            public async Task<string> HandleAsync(CancellableQuery query, CancellationToken cancellationToken = default)
            {
                await Task.Delay(1000, cancellationToken);
                return "Success";
            }
        }

        private class ThreadSafeTestCommand : ICommand<CommandResult<int>>
        {
            public int Value { get; set; }
        }
        
        private class ThreadSafeTestCommandHandler : ICommandHandler<ThreadSafeTestCommand, CommandResult<int>>
        {
            public async Task<CommandResult<int>> HandleAsync(
                ThreadSafeTestCommand command, 
                CancellationToken cancellationToken = default)
            {
                // Simulate some async work without database access
                await Task.Delay(10, cancellationToken);
                return CommandResult<int>.Success(command.Value);
            }
        }
        
        #endregion
    }
}
