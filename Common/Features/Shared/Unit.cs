// <copyright file="Unit.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Cosmos.Common.Features.Shared
{
    /// <summary>
    /// Represents a void/no-value result for commands that don't return specific data.
    /// Used with ICommand<Unit> for fire-and-forget command patterns.
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
