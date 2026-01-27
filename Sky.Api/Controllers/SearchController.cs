// <copyright file="SearchController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Api.Controllers;

using Cosmos.Common.Features.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sky.Cms.Api.Shared.Controllers;

/// <summary>
/// API search controller that inherits from the shared implementation.
/// This controller provides the search endpoints for the Sky.Api project.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SearchController : SearchApiController
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchController"/> class.
    /// </summary>
    /// <param name="mediator">Mediator for CQRS commands and queries.</param>
    /// <param name="logger">Logger instance.</param>
    public SearchController(
        IMediator mediator,
        ILogger<SearchController> logger)
        : base(mediator, logger)
    {
        // This controller inherits all functionality from SearchApiController
        // The endpoints will be available at:
        // - GET /api/search?query=...
        // - GET /api/search/suggest?query=...
        // - GET /api/search/health
    }
}