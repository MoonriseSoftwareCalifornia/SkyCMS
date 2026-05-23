// <copyright file="IElFinderDispatcher.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder;

using System.Threading;
using System.Threading.Tasks;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

/// <summary>
/// Dispatches elFinder commands to their registered handlers.
/// Replaces the MediatR dependency for elFinder CQRS routing.
/// </summary>
public interface IElFinderDispatcher
{
    /// <summary>
    /// Dispatches an elFinder command to its handler and returns the response.
    /// </summary>
    /// <typeparam name="TRequest">The elFinder command type.</typeparam>
    /// <param name="request">The elFinder command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The elFinder response.</returns>
    Task<IElFinderResponse> SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IElFinderRequest;
}
