// <copyright file="DomainEventDispatcherTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

#nullable enable

namespace Sky.Tests.Editor.Domain.Events
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Domain.Events;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="DomainEventDispatcher"/> event dispatching, handler registration, and error handling.
    /// Tests are designed to execute in parallel where independent of one another.
    /// </summary>
    [TestClass]
    public class DomainEventDispatcherTests
    {
        #region Test Events and Handlers

        /// <summary>
        /// Concrete test event for dispatching scenarios.
        /// </summary>
        private sealed class TestEvent : DomainEventBase
        {
            /// <summary>
            /// Gets the event payload.
            /// </summary>
            public string Payload { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="TestEvent"/> class.
            /// </summary>
            /// <param name="payload">Event payload.</param>
            public TestEvent(string payload = "test")
            {
                this.Payload = payload;
            }
        }

        /// <summary>
        /// Alternative test event for multi-event dispatching.
        /// </summary>
        private sealed class AnotherTestEvent : DomainEventBase
        {
            /// <summary>
            /// Gets the event value.
            /// </summary>
            public int Value { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="AnotherTestEvent"/> class.
            /// </summary>
            /// <param name="value">Event value.</param>
            public AnotherTestEvent(int value = 42)
            {
                this.Value = value;
            }
        }

        /// <summary>
        /// Handler tracking invocations for testing.
        /// </summary>
        private sealed class TrackingHandler : IDomainEventHandler<TestEvent>
        {
            /// <summary>
            /// Gets the invocation count.
            /// </summary>
            public int InvokeCount { get; private set; }

            /// <summary>
            /// Gets the last handled event.
            /// </summary>
            public TestEvent? LastEvent { get; private set; }

            /// <summary>
            /// Gets the last cancellation token received.
            /// </summary>
            public CancellationToken LastCancellationToken { get; private set; }

            /// <summary>
            /// Gets or sets a delay for simulating async work.
            /// </summary>
            public int DelayMs { get; set; }

            /// <summary>
            /// Handles the event without cancellation token.
            /// </summary>
            public Task HandleAsync(TestEvent @event)
            {
                return HandleAsync(@event, CancellationToken.None);
            }

            /// <summary>
            /// Handles the event with cancellation support.
            /// </summary>
            public async Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
            {
                this.LastEvent = @event;
                this.LastCancellationToken = cancellationToken;
                this.InvokeCount++;

                if (this.DelayMs > 0)
                {
                    await Task.Delay(this.DelayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Handler that throws exceptions for error handling tests.
        /// </summary>
        private sealed class FailingHandler : IDomainEventHandler<TestEvent>
        {
            /// <summary>
            /// Gets the exception to throw.
            /// </summary>
            public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("Handler failed");

            /// <summary>
            /// Gets the invocation count before throwing.
            /// </summary>
            public int InvokeCount { get; private set; }

            /// <summary>
            /// Handles the event without cancellation token.
            /// </summary>
            public Task HandleAsync(TestEvent @event)
            {
                return HandleAsync(@event, CancellationToken.None);
            }

            /// <summary>
            /// Handles the event and throws an exception.
            /// </summary>
            public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
            {
                this.InvokeCount++;
                throw this.ExceptionToThrow;
            }
        }

        /// <summary>
        /// Handler for AnotherTestEvent type.
        /// </summary>
        private sealed class AnotherEventHandler : IDomainEventHandler<AnotherTestEvent>
        {
            /// <summary>
            /// Gets the last handled event.
            /// </summary>
            public AnotherTestEvent? LastEvent { get; private set; }

            /// <summary>
            /// Handles the event without cancellation token.
            /// </summary>
            public Task HandleAsync(AnotherTestEvent @event)
            {
                return HandleAsync(@event, CancellationToken.None);
            }

            /// <summary>
            /// Handles the event with cancellation support.
            /// </summary>
            public Task HandleAsync(AnotherTestEvent @event, CancellationToken cancellationToken)
            {
                this.LastEvent = @event;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Handler that supports cancellation for testing.
        /// </summary>
        private sealed class CancellableHandler : IDomainEventHandler<TestEvent>
        {
            /// <summary>
            /// Gets a value indicating whether cancellation was observed.
            /// </summary>
            public bool CancellationObserved { get; private set; }

            /// <summary>
            /// Gets or sets the delay time.
            /// </summary>
            public int DelayMs { get; set; } = 100;

            /// <summary>
            /// Handles the event without cancellation token.
            /// </summary>
            public Task HandleAsync(TestEvent @event)
            {
                return HandleAsync(@event, CancellationToken.None);
            }

            /// <summary>
            /// Handles the event with cancellation support.
            /// </summary>
            public async Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
            {
                try
                {
                    await Task.Delay(this.DelayMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.CancellationObserved = true;
                    throw;
                }
            }
        }

        #endregion

        #region Handler Registration Tests

        /// <summary>
        /// Test: Dispatcher constructs with handler enumerable.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.Construction")]
        public async Task Constructor_WithHandlers_AcceptsHandlerList()
        {
            // Arrange
            var handler = new TrackingHandler();
            var handlers = new object[] { handler };

            // Act
            var dispatcher = new DomainEventDispatcher(handlers);

            // Assert
            Assert.IsNotNull(dispatcher);
            await dispatcher.DispatchAsync(new TestEvent("test"));
            Assert.AreEqual(1, handler.InvokeCount);
        }

        /// <summary>
        /// Test: Dispatcher constructs with resolver function.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.Construction")]
        public async Task Constructor_WithResolver_AcceptsResolverFunction()
        {
            // Arrange
            var handler = new TrackingHandler();
            Func<Type, IEnumerable<object>> resolver = (type) =>
            {
                if (type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>) &&
                    type.GetGenericArguments()[0] == typeof(TestEvent))
                {
                    return new[] { (object)handler };
                }

                return Array.Empty<object>();
            };

            // Act
            var dispatcher = new DomainEventDispatcher(resolver);

            // Assert
            Assert.IsNotNull(dispatcher);
            await dispatcher.DispatchAsync(new TestEvent("test"));
            Assert.AreEqual(1, handler.InvokeCount);
        }

        /// <summary>
        /// Test: Dispatcher throws on null resolver.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.Construction")]
        public void Constructor_WithNullResolver_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new DomainEventDispatcher((Func<Type, IEnumerable<object>>)null!));
        }

        /// <summary>
        /// Test: Dispatcher throws on null handler enumerable.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.Construction")]
        public void Constructor_WithNullHandlers_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new DomainEventDispatcher((IEnumerable<object>)null!));
        }

        /// <summary>
        /// Test: Multiple handlers registered for same event type.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.HandlerRegistration")]
        public async Task DispatchAsync_MultipleHandlers_AllInvoked()
        {
            // Arrange
            var handler1 = new TrackingHandler();
            var handler2 = new TrackingHandler();
            var handler3 = new TrackingHandler();
            var handlers = new object[] { handler1, handler2, handler3 };
            var dispatcher = new DomainEventDispatcher(handlers);
            var @event = new TestEvent("multi");

            // Act
            await dispatcher.DispatchAsync(@event);

            // Assert
            Assert.AreEqual(1, handler1.InvokeCount);
            Assert.AreEqual(1, handler2.InvokeCount);
            Assert.AreEqual(1, handler3.InvokeCount);
            Assert.AreEqual("multi", handler1.LastEvent?.Payload);
            Assert.AreEqual("multi", handler2.LastEvent?.Payload);
            Assert.AreEqual("multi", handler3.LastEvent?.Payload);
        }

        /// <summary>
        /// Test: Handlers deduplicated when resolver returns same instance multiple times.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.HandlerRegistration")]
        public async Task DispatchAsync_DuplicateHandlerInstances_Deduplicated()
        {
            // Arrange
            var handler = new TrackingHandler();
            Func<Type, IEnumerable<object>> resolver = (_) =>
            {
                // Return same handler instance three times
                return new[] { (object)handler, handler, handler };
            };
            var dispatcher = new DomainEventDispatcher(resolver);

            // Act
            await dispatcher.DispatchAsync(new TestEvent("dup"));

            // Assert - Distinct() in BuildDelegatesForType should reduce to 1 invocation
            Assert.AreEqual(1, handler.InvokeCount);
        }

        #endregion

        #region Event Dispatching - Sequential Mode Tests

        /// <summary>
        /// Test: Null event is dispatched without error (no-op).
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.DispatchSequential")]
        public async Task DispatchAsync_NullEvent_NoOp()
        {
            // Arrange
            var handler = new TrackingHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler });

            // Act
            await dispatcher.DispatchAsync((IDomainEvent)null!);

            // Assert
            Assert.AreEqual(0, handler.InvokeCount);
        }

        /// <summary>
        /// Test: Event with no registered handlers is dispatched without error (no-op).
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.DispatchSequential")]
        public async Task DispatchAsync_NoHandlers_NoOp()
        {
            // Arrange
            var dispatcher = new DomainEventDispatcher(Array.Empty<object>());

            // Act & Assert - should not throw
            await dispatcher.DispatchAsync(new TestEvent("orphan"));
        }

        /// <summary>
        /// Test: Single handler receives event in sequential mode.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.DispatchSequential")]
        public async Task DispatchAsync_SingleHandler_ReceivesEvent()
        {
            // Arrange
            var handler = new TrackingHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler }, parallel: false);
            var @event = new TestEvent("single");

            // Act
            await dispatcher.DispatchAsync(@event);

            // Assert
            Assert.AreEqual(1, handler.InvokeCount);
            Assert.IsNotNull(handler.LastEvent);
            Assert.AreEqual("single", handler.LastEvent.Payload);
        }

        /// <summary>
        /// Test: Multiple handlers invoked sequentially in registration order.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.DispatchSequential")]
        public async Task DispatchAsync_MultipleHandlers_SequentialOrder()
        {
            // Arrange
            var handler1 = new TrackingHandler { DelayMs = 10 };
            var handler2 = new TrackingHandler { DelayMs = 10 };
            var handler3 = new TrackingHandler { DelayMs = 10 };

            var resolver = new MockResolver(
                typeof(TestEvent),
                new[] { (object)handler1, handler2, handler3 });

            var dispatcher = new DomainEventDispatcher(resolver.Resolve, parallel: false);

            // Act
            await dispatcher.DispatchAsync(new TestEvent("seq"));

            // Assert - all should be invoked
            Assert.AreEqual(1, handler1.InvokeCount);
            Assert.AreEqual(1, handler2.InvokeCount);
            Assert.AreEqual(1, handler3.InvokeCount);
        }

        #endregion

        #region Event Dispatching - Parallel Mode Tests

        /// <summary>
        /// Test: Single handler in parallel mode operates as sequential.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.DispatchParallel")]
        public async Task DispatchAsync_SingleHandlerParallel_Executes()
        {
            // Arrange
            var handler = new TrackingHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler }, parallel: true);

            // Act
            await dispatcher.DispatchAsync(new TestEvent("single-par"));

            // Assert
            Assert.AreEqual(1, handler.InvokeCount);
        }

        /// <summary>
        /// Test: Multiple handlers invoked in parallel with reduced total time.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.DispatchParallel")]
        public async Task DispatchAsync_MultipleHandlersParallel_ConcurrentExecution()
        {
            // Arrange
            var handler1 = new TrackingHandler { DelayMs = 50 };
            var handler2 = new TrackingHandler { DelayMs = 50 };
            var handler3 = new TrackingHandler { DelayMs = 50 };
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler1, handler2, handler3 }, parallel: true);

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await dispatcher.DispatchAsync(new TestEvent("par"));
            stopwatch.Stop();

            // Assert - all handlers should complete
            Assert.AreEqual(1, handler1.InvokeCount);
            Assert.AreEqual(1, handler2.InvokeCount);
            Assert.AreEqual(1, handler3.InvokeCount);

            // Parallel execution should be noticeably faster than sequential
            // Sequential would be ~150ms, parallel should be ~50-80ms
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 150,
                $"Parallel execution took {stopwatch.ElapsedMilliseconds}ms (expected <150ms for parallel)");
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Test: Single handler exception is rethrown directly.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.ErrorHandling")]
        public async Task DispatchAsync_SingleHandlerThrows_ExceptionRethrown()
        {
            // Arrange
            var failingHandler = new FailingHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)failingHandler });

            // Act & Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                await dispatcher.DispatchAsync(new TestEvent("fail")));
        }

        /// <summary>
        /// Test: Multiple handler exceptions aggregated into AggregateException.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.ErrorHandling")]
        public async Task DispatchAsync_MultipleHandlersThrow_AggregateExceptionThrown()
        {
            // Arrange
            var failingHandler1 = new FailingHandler { ExceptionToThrow = new InvalidOperationException("Error 1") };
            var failingHandler2 = new FailingHandler { ExceptionToThrow = new InvalidOperationException("Error 2") };
            var dispatcher = new DomainEventDispatcher(new[] { (object)failingHandler1, failingHandler2 }, parallel: false);

            // Act & Assert
            try
            {
                await dispatcher.DispatchAsync(new TestEvent("agg"));
                Assert.Fail("Should have thrown AggregateException");
            }
            catch (AggregateException ex)
            {
                Assert.AreEqual(2, ex.InnerExceptions.Count);
                Assert.IsTrue(ex.InnerExceptions.Any(e => e.Message.Contains("Error 1")));
                Assert.IsTrue(ex.InnerExceptions.Any(e => e.Message.Contains("Error 2")));
            }
        }

        /// <summary>
        /// Test: Successful handler invoked before failing handler; exception still thrown.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.ErrorHandling")]
        public async Task DispatchAsync_PartialFailure_ExceptionThrownWithPriorCompletion()
        {
            // Arrange
            var successHandler = new TrackingHandler();
            var failingHandler = new FailingHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)successHandler, failingHandler }, parallel: false);

            // Act & Assert
            try
            {
                await dispatcher.DispatchAsync(new TestEvent("partial"));
                Assert.Fail("Should have thrown exception");
            }
            catch (InvalidOperationException)
            {
                // Expected
                Assert.AreEqual(1, successHandler.InvokeCount, "Success handler should have completed");
                Assert.AreEqual(1, failingHandler.InvokeCount, "Failing handler should have attempted");
            }
        }

        /// <summary>
        /// Test: In parallel mode, all handler exceptions collected into AggregateException.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.ErrorHandling")]
        public async Task DispatchAsync_ParallelHandlersThrow_AllExceptionsCollected()
        {
            // Arrange
            var failingHandler1 = new FailingHandler { ExceptionToThrow = new InvalidOperationException("Parallel 1") };
            var failingHandler2 = new FailingHandler { ExceptionToThrow = new InvalidOperationException("Parallel 2") };
            var failingHandler3 = new FailingHandler { ExceptionToThrow = new InvalidOperationException("Parallel 3") };
            var dispatcher = new DomainEventDispatcher(
                new[] { (object)failingHandler1, failingHandler2, failingHandler3 },
                parallel: true);

            // Act & Assert
            try
            {
                await dispatcher.DispatchAsync(new TestEvent("par-fail"));
                Assert.Fail("Should have thrown AggregateException");
            }
            catch (AggregateException ex)
            {
                Assert.AreEqual(3, ex.InnerExceptions.Count);
                Assert.IsTrue(ex.InnerExceptions.Any(e => e.Message.Contains("Parallel 1")));
                Assert.IsTrue(ex.InnerExceptions.Any(e => e.Message.Contains("Parallel 2")));
                Assert.IsTrue(ex.InnerExceptions.Any(e => e.Message.Contains("Parallel 3")));
            }
        }

        #endregion

        #region Cancellation Token Tests

        /// <summary>
        /// Test: Cancellation token passed to handler accepting it.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.Cancellation")]
        public async Task DispatchAsync_WithCancellationToken_PassedToHandler()
        {
            // Arrange
            var handler = new TrackingHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler });
            var cts = new CancellationTokenSource();

            // Act
            await dispatcher.DispatchAsync(new TestEvent("cancel"), cts.Token);

            // Assert
            Assert.AreEqual(cts.Token, handler.LastCancellationToken);
        }

        /// <summary>
        /// Test: Handler receives default CancellationToken when none supplied.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.Cancellation")]
        public async Task DispatchAsync_NoToken_HandlerReceivesDefault()
        {
            // Arrange
            var handler = new TrackingHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler });

            // Act
            await dispatcher.DispatchAsync(new TestEvent("no-token"));

            // Assert
            Assert.AreEqual(default, handler.LastCancellationToken);
        }

        /// <summary>
        /// Test: Cancellation before dispatch throws OperationCanceledException.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.Cancellation")]
        public async Task DispatchAsync_PreCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var handler = new CancellableHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler });
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
                await dispatcher.DispatchAsync(new TestEvent("pre-cancel"), cts.Token));
        }

        #endregion

        #region Multi-Event Sequence Tests

        /// <summary>
        /// Test: Null event sequence is no-op.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.MultiEvent")]
        public async Task DispatchAsync_NullEventSequence_NoOp()
        {
            // Arrange
            var handler = new TrackingHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler });

            // Act
            await dispatcher.DispatchAsync((IEnumerable<IDomainEvent>)null!);

            // Assert
            Assert.AreEqual(0, handler.InvokeCount);
        }

        /// <summary>
        /// Test: Empty event sequence is no-op.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.MultiEvent")]
        public async Task DispatchAsync_EmptyEventSequence_NoOp()
        {
            // Arrange
            var handler = new TrackingHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler });

            // Act
            await dispatcher.DispatchAsync(Array.Empty<IDomainEvent>());

            // Assert
            Assert.AreEqual(0, handler.InvokeCount);
        }

        /// <summary>
        /// Test: Multiple events dispatched sequentially in order.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.MultiEvent")]
        public async Task DispatchAsync_MultipleEvents_DispatchedInOrder()
        {
            // Arrange
            var handler = new TrackingHandler();
            var events = new IDomainEvent[]
            {
                new TestEvent("first"),
                new TestEvent("second"),
                new TestEvent("third"),
            };
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler });

            // Act
            await dispatcher.DispatchAsync(events);

            // Assert
            Assert.AreEqual(3, handler.InvokeCount);
            Assert.AreEqual("third", handler.LastEvent?.Payload);
        }

        /// <summary>
        /// Test: Multiple different event types dispatched to appropriate handlers.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.MultiEvent")]
        public async Task DispatchAsync_DifferentEventTypes_RoutedToCorrectHandlers()
        {
            // Arrange
            var testHandler = new TrackingHandler();
            var anotherHandler = new AnotherEventHandler();
            var resolver = new MockResolver(
                new Dictionary<Type, object[]>
                {
                    { typeof(TestEvent), new[] { (object)testHandler } },
                    { typeof(AnotherTestEvent), new[] { (object)anotherHandler } },
                });

            var events = new IDomainEvent[]
            {
                new TestEvent("test1"),
                new AnotherTestEvent(100),
                new TestEvent("test2"),
            };
            var dispatcher = new DomainEventDispatcher(resolver.Resolve);

            // Act
            await dispatcher.DispatchAsync(events);

            // Assert
            Assert.AreEqual(2, testHandler.InvokeCount);
            Assert.IsNotNull(anotherHandler.LastEvent);
            Assert.AreEqual(100, anotherHandler.LastEvent.Value);
        }

        /// <summary>
        /// Test: Cancellation token checked before each event dispatch.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.MultiEvent")]
        public async Task DispatchAsync_EventSequenceCancelled_ThrowsBeforeSecondEvent()
        {
            // Arrange - Use a pre-cancelled token to guarantee cancellation is checked
            var handler = new TrackingHandler();
            var events = new IDomainEvent[]
            {
                new TestEvent("first"),
                new TestEvent("second"),
                new TestEvent("third"),
            };
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler });

            // Cancel the token BEFORE starting dispatch
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            // Should throw immediately when checking cancellation before first event
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await dispatcher.DispatchAsync(events, cts.Token));

            // Verify no events were handled since token was pre-cancelled
            Assert.AreEqual(0, handler.InvokeCount, "No events should be handled when token is pre-cancelled");
        }

        #endregion

        #region Interface Implementation Tests

        /// <summary>
        /// Test: Explicit interface method DispatchAsync(IDomainEvent) without token.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.InterfaceImplementation")]
        public async Task IDispatcher_DispatchAsync_SingleEvent_Invoked()
        {
            // Arrange
            var handler = new TrackingHandler();
            IDomainEventDispatcher dispatcher = new DomainEventDispatcher(new[] { (object)handler });

            // Act
            await dispatcher.DispatchAsync(new TestEvent("interface"));

            // Assert
            Assert.AreEqual(1, handler.InvokeCount);
        }

        /// <summary>
        /// Test: Explicit interface method DispatchAsync(IEnumerable) without token.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.InterfaceImplementation")]
        public async Task IDispatcher_DispatchAsync_EventSequence_Invoked()
        {
            // Arrange
            var handler = new TrackingHandler();
            IDomainEventDispatcher dispatcher = new DomainEventDispatcher(new[] { (object)handler });
            var events = new IDomainEvent[]
            {
                new TestEvent("evt1"),
                new TestEvent("evt2"),
            };

            // Act
            await dispatcher.DispatchAsync(events);

            // Assert
            Assert.AreEqual(2, handler.InvokeCount);
        }

        #endregion

        #region Handler Method Signature Support Tests

        /// <summary>
        /// Test: Handler with only single-parameter HandleAsync(TEvent) method works.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.MethodSignatures")]
        public async Task DispatchAsync_HandlerWithSingleParamMethod_Invoked()
        {
            // Arrange
            var handler = new SingleParamOnlyHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler });

            // Act
            await dispatcher.DispatchAsync(new TestEvent("single-param"));

            // Assert
            Assert.AreEqual(1, handler.InvokeCount);
        }

        /// <summary>
        /// Test: Handler without public HandleAsync method is skipped.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.MethodSignatures")]
        public async Task DispatchAsync_HandlerWithoutPublicMethod_Skipped()
        {
            // Arrange
            var handler = new NoPublicMethodHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler });

            // Act & Assert - should not throw
            await dispatcher.DispatchAsync(new TestEvent("skip"));
        }

        #endregion

        #region Delegate Caching Tests

        /// <summary>
        /// Test: Reflection is performed once per event type; delegates are cached.
        /// </summary>
        [TestMethod]
        [TestCategory("DomainEventDispatcher.Caching")]
        public async Task DispatchAsync_SameEventType_DelegatesCached()
        {
            // Arrange
            var handler1 = new TrackingHandler();
            var handler2 = new TrackingHandler();
            var dispatcher = new DomainEventDispatcher(new[] { (object)handler1, handler2 });

            // Act - dispatch same event type multiple times
            await dispatcher.DispatchAsync(new TestEvent("cache1"));
            await dispatcher.DispatchAsync(new TestEvent("cache2"));
            await dispatcher.DispatchAsync(new TestEvent("cache3"));

            // Assert
            Assert.AreEqual(3, handler1.InvokeCount);
            Assert.AreEqual(3, handler2.InvokeCount);
            // Cache should be hit for 2nd and 3rd dispatch
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Handler with only single-parameter method for testing signature support.
        /// </summary>
        private sealed class SingleParamOnlyHandler : IDomainEventHandler<TestEvent>
        {
            /// <summary>
            /// Gets the invocation count.
            /// </summary>
            public int InvokeCount { get; private set; }

            /// <summary>
            /// Handles the event without cancellation token.
            /// </summary>
            public Task HandleAsync(TestEvent @event)
            {
                this.InvokeCount++;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Handler without public HandleAsync method to test skipping.
        /// </summary>
        private sealed class NoPublicMethodHandler : IDomainEventHandler<TestEvent>
        {
            /// <summary>
            /// Private HandleAsync method that should not be invoked.
            /// </summary>
            private Task HandleAsync(TestEvent @event)
            {
                throw new InvalidOperationException("Should not be called");
            }

            /// <summary>
            /// Required by interface but not public.
            /// </summary>
            Task IDomainEventHandler<TestEvent>.HandleAsync(TestEvent @event)
            {
                throw new InvalidOperationException("Interface implementation should not be called");
            }
        }

        /// <summary>
        /// Mock resolver for testing different event type scenarios.
        /// </summary>
        private sealed class MockResolver
        {
            private readonly Dictionary<Type, object[]> _handlers;

            /// <summary>
            /// Initializes a new instance with a specific event type.
            /// </summary>
            public MockResolver(Type targetType, object[] handlers)
            {
                this._handlers = new Dictionary<Type, object[]>
                {
                    { MakeHandlerInterface(targetType), handlers },
                };
            }

            /// <summary>
            /// Initializes a new instance with multiple event types.
            /// </summary>
            public MockResolver(Dictionary<Type, object[]> handlers)
            {
                this._handlers = handlers
                    .ToDictionary(
                        kvp => MakeHandlerInterface(kvp.Key),
                        kvp => kvp.Value);
            }

            /// <summary>
            /// Resolves handlers for the given type.
            /// </summary>
            public IEnumerable<object> Resolve(Type handlerType)
            {
                return this._handlers.TryGetValue(handlerType, out var handlers) ? handlers : Array.Empty<object>();
            }

            private static Type MakeHandlerInterface(Type eventType) =>
                typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        }

        #endregion
    }
}
