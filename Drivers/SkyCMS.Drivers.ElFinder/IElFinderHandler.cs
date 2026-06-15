// <copyright file="IElFinderHandler.cs" company="Moonrise Software, LLC">
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
/// Handles a specific elFinder command request and returns a response.
/// </summary>
/// <typeparam name="TRequest">The elFinder command type.</typeparam>
public interface IElFinderHandler<TRequest>
    where TRequest : IElFinderRequest
{
    /// <summary>
    /// Handles the given elFinder request.
    /// </summary>
    /// <param name="request">The elFinder command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The elFinder response.</returns>
    Task<IElFinderResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
