// <copyright file="ElFinderDispatcher.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

/// <summary>
/// Default implementation of <see cref="IElFinderDispatcher"/>.
/// Resolves the appropriate <see cref="IElFinderHandler{TRequest}"/> from the DI container.
/// </summary>
public sealed class ElFinderDispatcher : IElFinderDispatcher
{
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElFinderDispatcher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The DI service provider used to resolve handlers.</param>
    public ElFinderDispatcher(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public Task<IElFinderResponse> SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IElFinderRequest
    {
        ArgumentNullException.ThrowIfNull(request);
        var handler = this.serviceProvider.GetRequiredService<IElFinderHandler<TRequest>>();
        return handler.HandleAsync(request, cancellationToken);
    }
}
