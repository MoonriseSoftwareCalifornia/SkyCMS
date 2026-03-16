// <copyright file="DomainEventDispatcher.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Domain.Events
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Central dispatcher that sends <see cref="IDomainEvent"/> instances to all
    /// registered <see cref="IDomainEventHandler{TDomainEvent}"/> implementations
    /// resolved at runtime.
    /// </summary>
    /// <remarks>
    /// Responsibilities:
    /// - Discovers matching handlers for a given event type via a supplied resolver function.
    /// - Invokes each handler in either sequential or parallel mode (configurable).
    /// - Aggregates and rethrows exceptions (never swallows handler faults).
    /// - Supports cancellation tokens where handler signatures accept them.
    ///
    /// Performance characteristics:
    /// - Reflection is performed only once per concrete event type; invocation
    ///   delegates are cached in a static <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// - Delegate cache lookup is O(1) and thread-safe.
    ///
    /// Thread-safety:
    /// - Safe for concurrent calls to any public <c>DispatchAsync</c> overload.
    /// - Cache population for a new event type may occur concurrently; the underlying
    ///   <see cref="ConcurrentDictionary{TKey, TValue}"/> ensures only one compiled delegate
    ///   list is retained.
    ///
    /// Parallel dispatch:
    /// - When constructed with <c>parallel = true</c>, handlers for a single event are fired concurrently.
    /// - Ordering between handlers is NOT guaranteed in parallel mode.
    /// - Sequential mode preserves registration order (subject to the enumeration order
    ///   returned by the resolver).
    ///
    /// Exception semantics:
    /// - All handler exceptions are collected.
    /// - A single exception is rethrown directly.
    /// - Multiple exceptions are wrapped in an <see cref="AggregateException"/>.
    ///
    /// Cancellation:
    /// - A supplied <see cref="CancellationToken"/> is passed only to handlers whose
    ///   <c>HandleAsync</c> signature includes it.
    /// - Cancellation triggers pre-dispatch checks in multi-event sequences.
    ///
    /// Handler resolution:
    /// - The resolver is invoked with the closed generic handler interface
    ///   (e.g., <c>IDomainEventHandler&lt;UserCreatedEvent&gt;</c>).
    /// - Duplicate handler object instances are deduplicated defensively.
    ///
    /// Backward compatibility:
    /// - Explicit interface implementations provide legacy overloads without
    ///   cancellation tokens; they delegate internally to the newer APIs.
    ///
    /// Example:
    /// <example>
    /// <![CDATA[
    /// var dispatcher = new DomainEventDispatcher(type =>
    ///     serviceProvider.GetServices(type)); // Using DI container
    ///
    /// await dispatcher.DispatchAsync(new UserRegisteredEvent(userId), cancellationToken);
    /// ]]>
    /// </example>
    /// </remarks>
    public sealed class DomainEventDispatcher : IDomainEventDispatcher
    {
        /// <summary>
        /// Resolves handler instances for a requested closed generic handler interface type.
        /// </summary>
        private readonly Func<Type, IEnumerable<object>> handlerResolver;

        /// <summary>
        /// Indicates whether handlers for a single event should be invoked in parallel.
        /// </summary>
        private readonly bool parallel;

        /// <summary>
        /// Cache mapping event CLR types to compiled invocation delegates for each handler.
        /// Each dispatcher instance has its own cache to prevent cross-instance contamination.
        /// </summary>
        private readonly ConcurrentDictionary<Type, List<Func<IDomainEvent, CancellationToken, Task>>> delegateCache = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainEventDispatcher"/> class
        /// with a fixed set of handler instances (no filtering by event type).
        /// </summary>
        /// <param name="handlers">Concrete handler instances (any object implementing one or more domain handler interfaces).</param>
        /// <param name="parallel">Whether to invoke handlers concurrently per event.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handlers"/> is null.</exception>
        public DomainEventDispatcher(IEnumerable<object> handlers, bool parallel = false)
        {
            if (handlers == null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            handlerResolver = _ => handlers;
            this.parallel = parallel;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainEventDispatcher"/> class
        /// using a custom resolver invoked per event type.
        /// </summary>
        /// <param name="handlerResolver">
        /// Function receiving a closed generic <c>IDomainEventHandler&lt;TEvent&gt;</c> type
        /// and returning zero or more handler objects.
        /// </param>
        /// <param name="parallel">Whether to invoke handlers concurrently per event.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handlerResolver"/> is null.</exception>
        public DomainEventDispatcher(
            Func<Type, IEnumerable<object>> handlerResolver,
            bool parallel = false)
        {
            this.handlerResolver = handlerResolver ?? throw new ArgumentNullException(nameof(handlerResolver));
            this.parallel = parallel;
        }

        /// <summary>
        /// Dispatches a single domain event to all registered handlers matching the event's runtime type.
        /// </summary>
        /// <param name="domainEvent">The event instance to dispatch.</param>
        /// <param name="cancellationToken">A token observing caller cancellation.</param>
        /// <remarks>
        /// No-op when <paramref name="domainEvent"/> is null or no handlers are registered.
        /// Exceptions from handlers are aggregated (see class remarks).
        /// </remarks>
        public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            if (domainEvent == null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var delegates = GetOrCreateDelegates(domainEvent.GetType());
            if (delegates.Count == 0)
            {
                return;
            }

            var failures = new List<Exception>();

            if (parallel && delegates.Count > 1)
            {
                var tasks = delegates
                    .Select(d => d(domainEvent, cancellationToken))
                    .ToArray();

                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch
                {
                    // Collect all faults without losing any.
                    foreach (var t in tasks.Where(t => t.IsFaulted && t.Exception != null))
                    {
                        // Task.Exception is always an AggregateException; extract the inner exceptions
                        if (t.Exception is AggregateException ae)
                        {
                            failures.AddRange(ae.InnerExceptions);
                        }
                        else
                        {
                            failures.Add(t.Exception);
                        }
                    }
                }
            }
            else
            {
                foreach (var d in delegates)
                {
                    try
                    {
                        await d(domainEvent, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(ex);
                    }
                }
            }

            if (failures.Count == 1)
            {
                throw failures[0];
            }

            if (failures.Count > 1)
            {
                throw new AggregateException("One or more domain event handlers failed.", failures);
            }
        }

        /// <summary>
        /// Dispatches a sequence of domain events in order; each dispatch is awaited before the next begins.
        /// </summary>
        /// <param name="events">The ordered collection of events to dispatch.</param>
        /// <param name="cancellationToken">A token observing caller cancellation.</param>
        /// <remarks>
        /// Null collections result in a no-op. Cancellation is checked before each event dispatch.
        /// </remarks>
        public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            if (events == null)
            {
                return;
            }

            foreach (var e in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DispatchAsync(e, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        async Task IDomainEventDispatcher.DispatchAsync(IDomainEvent @event) =>
            await DispatchAsync(@event, CancellationToken.None).ConfigureAwait(false);

        /// <inheritdoc />
        async Task IDomainEventDispatcher.DispatchAsync(IEnumerable<IDomainEvent> events) =>
            await DispatchAsync(events, CancellationToken.None).ConfigureAwait(false);

        /// <summary>
        /// Retrieves cached handler delegates for the given event type, or builds and caches them if absent.
        /// </summary>
        /// <param name="eventType">Concrete CLR type of the domain event.</param>
        private List<Func<IDomainEvent, CancellationToken, Task>> GetOrCreateDelegates(Type eventType) =>
            delegateCache.GetOrAdd(eventType, BuildDelegatesForType);

        /// <summary>
        /// Uses reflection to discover and wrap matching handler method invocations for a specific event type.
        /// </summary>
        /// <param name="eventType">Concrete event type for which delegates are constructed.</param>
        /// <returns>List of compiled delegates invoking <c>HandleAsync</c> on each handler instance.</returns>
        /// <remarks>
        /// A handler is considered valid if it exposes a public instance method named
        /// <c>HandleAsync</c> with signature:
        ///  - Task HandleAsync(TEvent)
        ///  - Task HandleAsync(TEvent, CancellationToken)
        /// Non-conforming handlers are skipped silently.
        /// </remarks>
        private List<Func<IDomainEvent, CancellationToken, Task>> BuildDelegatesForType(Type eventType)
        {
            var handlerInterface = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

            var instances = handlerResolver(handlerInterface)
                ?.Distinct()
                ?.ToList() ?? new List<object>();

            var list = new List<Func<IDomainEvent, CancellationToken, Task>>(instances.Count);

            foreach (var instance in instances)
            {
                var instanceType = instance.GetType();
                MethodInfo? method = null;
                bool supportsCancellation = false;

                // Get all public HandleAsync methods
                // Note: We only support PUBLIC methods, not explicit interface implementations
                // This is by design - handlers must have public methods to be invoked
                var allMethods = instanceType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "HandleAsync" && typeof(Task).IsAssignableFrom(m.ReturnType))
                    .ToList();

                if (allMethods.Count == 0)
                {
                    continue;
                }

                // Prefer the 2-parameter version (with CancellationToken)
                method = allMethods.FirstOrDefault(m =>
                {
                    var parameters = m.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType.IsAssignableFrom(eventType) &&
                           parameters[1].ParameterType == typeof(CancellationToken);
                });

                if (method != null)
                {
                    supportsCancellation = true;
                }
                else
                {
                    // Fall back to 1-parameter version
                    method = allMethods.FirstOrDefault(m =>
                    {
                        var parameters = m.GetParameters();
                        return parameters.Length == 1 &&
                               parameters[0].ParameterType.IsAssignableFrom(eventType);
                    });

                    if (method != null)
                    {
                        supportsCancellation = false;
                    }
                }

                // If no method found, skip this handler
                if (method == null)
                {
                    continue;
                }

                // Capture the method and instance for the delegate
                var capturedMethod = method;
                var capturedInstance = instance;
                var capturedSupportsCancellation = supportsCancellation;

                Task Invoke(IDomainEvent ev, CancellationToken ct)
                {
                    try
                    {
                        object? result = capturedSupportsCancellation
                            ? capturedMethod.Invoke(capturedInstance, new object[] { ev, ct })
                            : capturedMethod.Invoke(capturedInstance, new object[] { ev });

                        return result is Task t ? t : Task.CompletedTask;
                    }
                    catch (TargetInvocationException tie) when (tie.InnerException != null)
                    {
                        // Return a faulted task instead of throwing synchronously
                        // This ensures parallel execution can collect all exceptions
                        return Task.FromException(tie.InnerException);
                    }
                    catch (Exception ex)
                    {
                        // Handle any other reflection exceptions
                        return Task.FromException(ex);
                    }
                }

                list.Add(Invoke);
            }

            return list;
        }
    }
}
