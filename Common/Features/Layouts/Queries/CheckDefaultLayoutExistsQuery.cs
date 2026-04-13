// <copyright file="CheckDefaultLayoutExistsQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Layouts.Queries;

using System;
using Cosmos.Common.Features.Shared;

/// <summary>
/// Query to check if any default layout exists in the database.
/// Replaces LayoutHelper.HasDefaultLayoutAsync() method.
/// </summary>
public record CheckDefaultLayoutExistsQuery : IQuery<bool>
{
    /// <summary>
    /// Gets the optional cache duration for the layout existence check.
    /// </summary>
    /// <remarks>
    /// When set, the result will be cached for the specified duration.
    /// Recommended: 5-10 minutes since default layout existence rarely changes.
    /// Cache is automatically invalidated when layouts are published.
    /// </remarks>
    public TimeSpan? CacheDuration { get; init; }
}
