// <copyright file="IMediator.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Shared;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Mediator for dispatching commands and queries to their handlers using the CQRS pattern.
/// </summary>
/// <remarks>
/// This interface defines the contract for a mediator implementation that separates commands (write operations)
/// from queries (read operations) following Command Query Responsibility Segregation (CQRS) principles.
/// The mediator pattern helps decouple the sender of a request from the handler that processes it,
/// enabling better separation of concerns and testability.
/// </remarks>
public interface IMediator
{
    /// <summary>
    /// Sends a command to its registered handler for processing.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the command handler.</typeparam>
    /// <param name="command">The command instance to be processed by its handler.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Commands typically represent write operations that modify application state (e.g., create, update, delete operations).
    /// The mediator will locate the appropriate <see cref="ICommandHandler{TCommand, TResult}"/> and invoke its handler method.
    /// </remarks>
    Task<TResult> SendAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query to its registered handler for processing.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the query handler.</typeparam>
    /// <param name="query">The query instance to be processed by its handler.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Queries typically represent read operations that retrieve data without modifying application state.
    /// The mediator will locate the appropriate <see cref="IQueryHandler{TQuery, TResult}"/> and invoke its handler method.
    /// </remarks>
    Task<TResult> QueryAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken cancellationToken = default);
}
