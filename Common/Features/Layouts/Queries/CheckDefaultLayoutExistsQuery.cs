// <copyright file="CheckDefaultLayoutExistsQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Layouts.Queries;

using Cosmos.Common.Features.Shared;

/// <summary>
/// Query to check if any default layout exists in the database.
/// Replaces LayoutHelper.HasDefaultLayoutAsync() method.
/// </summary>
public record CheckDefaultLayoutExistsQuery : IQuery<bool>;
