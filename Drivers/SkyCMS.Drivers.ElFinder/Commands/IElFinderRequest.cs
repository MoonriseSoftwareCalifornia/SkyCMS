// <copyright file="IElFinderRequest.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder.Commands
{
    using MediatR;
    using SkyCMS.Drivers.ElFinder.Responses;

    /// <summary>
    /// Base interface for all elFinder CQRS request commands.
    /// </summary>
    /// <remarks>
    /// All elFinder commands inherit from this interface and return an <see cref="IElFinderResponse"/>.
    /// This enables centralized command handling, validation, and response formatting through MediatR.
    /// </remarks>
    public interface IElFinderRequest : IRequest<IElFinderResponse>
    {
        /// <summary>
        /// Gets the elFinder command name (e.g., "open", "tree", "mkdir").
        /// </summary>
        string Command { get; }

        /// <summary>
        /// Gets the volume ID (typically "l1_" for the primary volume).
        /// </summary>
        string? VolumeId { get; }
    }
}
