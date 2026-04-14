// <copyright file="Unit.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Shared
{
    /// <summary>
    /// Represents a void/no-value result for commands that don't return specific data.
    /// Used with <see cref="ICommand{T}"/> for fire-and-forget command patterns.
    /// </summary>
    public sealed class Unit
    {
        /// <summary>
        /// Gets the singleton instance of Unit.
        /// </summary>
        public static readonly Unit Value = new Unit();

        private Unit()
        {
        }
    }
}
