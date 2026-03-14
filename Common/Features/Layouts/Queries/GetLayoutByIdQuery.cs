// <copyright file="GetLayoutByIdQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Layouts.Queries;

using System;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;

/// <summary>
/// Query to get a layout by its unique identifier.
/// Replaces LayoutHelper.GetLayoutByIdAsync() method.
/// </summary>
/// <param name="LayoutId">The layout ID to find.</param>
public record GetLayoutByIdQuery(Guid LayoutId) : IQuery<Layout?>;
