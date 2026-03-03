// <copyright file="Mediator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Shared;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Simple mediator implementation using service provider for handler resolution.
/// </summary>
/// <remarks>
/// This implementation uses reflection and the dependency injection container to dynamically
/// resolve and invoke the appropriate command or query handlers at runtime.
/// The mediator follows the CQRS pattern by providing separate methods for commands and queries.
/// </remarks>
public class Mediator : IMediator
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<Mediator> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve command and query handlers.</param>
    /// <param name="logger">Optional logger for diagnostics and error tracking.</param>
    public Mediator(IServiceProvider serviceProvider, ILogger<Mediator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        this.serviceProvider = serviceProvider;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Mediator>.Instance;
    }

    /// <inheritdoc/>
    public async Task<TResult> SendAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResult));

        logger.LogDebug(
            "Attempting to resolve command handler: {CommandType} -> {HandlerType}",
            commandType.Name,
            handlerType.Name);

        object handler;
        try
        {
            handler = serviceProvider.GetRequiredService(handlerType);
            logger.LogDebug("Successfully resolved handler for command: {CommandType}", commandType.Name);
        }
        catch (InvalidOperationException ex)
        {
            var errorMessage = $"No command handler registered for '{commandType.FullName}'. " +
                             $"Expected handler type: '{handlerType.FullName}'. " +
                             $"Ensure the handler is registered in the DI container using " +
                             $"'services.AddScoped<{handlerType.Name}, YourHandlerImplementation>()' or use " +
                             $"'services.AddMediatorHandlers()' for automatic registration.";

            logger.LogError(ex, "Command handler resolution failed: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage, ex);
        }

        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResult>, TResult>.HandleAsync));

        if (method == null)
        {
            var errorMessage = $"Handler method 'HandleAsync' not found on handler type '{handlerType.FullName}' " +
                             $"for command '{commandType.Name}'. This indicates a framework error or interface mismatch.";

            logger.LogError("Command handler method not found: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        try
        {
            logger.LogDebug("Invoking handler for command: {CommandType}", commandType.Name);

            var result = method.Invoke(handler, [command, cancellationToken]);

            if (result is Task<TResult> task)
            {
                var commandResult = await task;
                logger.LogDebug("Command completed successfully: {CommandType}", commandType.Name);
                return commandResult;
            }

            var returnTypeError = $"Handler for command '{commandType.Name}' did not return expected type 'Task<{typeof(TResult).Name}>'. " +
                                $"Actual return type: {result?.GetType().FullName ?? "null"}";

            logger.LogError("Command handler return type mismatch: {ErrorMessage}", returnTypeError);
            throw new InvalidOperationException(returnTypeError);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Unwrap TargetInvocationException if present (from reflection)
            var actualException = ex.InnerException ?? ex;

            logger.LogError(
                actualException,
                "Command handler threw an exception: {CommandType}, Handler: {HandlerType}",
                commandType.Name,
                handlerType.Name);

            throw actualException;
        }
    }

    /// <inheritdoc/>
    public async Task<TResult> QueryAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));

        logger.LogDebug(
            "Attempting to resolve query handler: {QueryType} -> {HandlerType}",
            queryType.Name,
            handlerType.Name);

        object handler;
        try
        {
            handler = serviceProvider.GetRequiredService(handlerType);
            logger.LogDebug("Successfully resolved handler for query: {QueryType}", queryType.Name);
        }
        catch (InvalidOperationException ex)
        {
            var errorMessage = $"No query handler registered for '{queryType.FullName}'. " +
                             $"Expected handler type: '{handlerType.FullName}'. " +
                             $"Ensure the handler is registered in the DI container using " +
                             $"'services.AddScoped<{handlerType.Name}, YourHandlerImplementation>()' or use " +
                             $"'services.AddMediatorHandlers()' for automatic registration.";

            logger.LogError(ex, "Query handler resolution failed: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage, ex);
        }

        var method = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResult>, TResult>.HandleAsync));

        if (method == null)
        {
            var errorMessage = $"Handler method 'HandleAsync' not found on handler type '{handlerType.FullName}' " +
                             $"for query '{queryType.Name}'. This indicates a framework error or interface mismatch.";

            logger.LogError("Query handler method not found: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        try
        {
            logger.LogDebug("Invoking handler for query: {QueryType}", queryType.Name);

            var result = method.Invoke(handler, [query, cancellationToken]);

            if (result is Task<TResult> task)
            {
                var queryResult = await task;
                logger.LogDebug("Query completed successfully: {QueryType}", queryType.Name);
                return queryResult;
            }

            var returnTypeError = $"Handler for query '{queryType.Name}' did not return expected type 'Task<{typeof(TResult).Name}>'. " +
                                $"Actual return type: {result?.GetType().FullName ?? "null"}";

            logger.LogError("Query handler return type mismatch: {ErrorMessage}", returnTypeError);
            throw new InvalidOperationException(returnTypeError);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Unwrap TargetInvocationException if present (from reflection)
            var actualException = ex.InnerException ?? ex;

            logger.LogError(
                actualException,
                "Query handler threw an exception: {QueryType}, Handler: {HandlerType}",
                queryType.Name,
                handlerType.Name);

            throw actualException;
        }
    }
}
