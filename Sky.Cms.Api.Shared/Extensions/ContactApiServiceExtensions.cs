// <copyright file="ContactApiServiceExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Extensions;

using Cosmos.Common.Features.Shared;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sky.Cms.Api.Shared.Features.ContactForm.Submit;
using Sky.Cms.Api.Shared.Features.ContactForm.ValidateCaptcha;
using Sky.Cms.Api.Shared.Models;
using Sky.Cms.Api.Shared.Services;
using Sky.Cms.Api.Shared.Services.Captcha;
using System.Threading.RateLimiting;

/// <summary>
/// Extension methods for configuring the Contact API services.
/// </summary>
public static class ContactApiServiceExtensions
{
    /// <summary>
    /// Adds Contact API services to the service collection.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddContactApi(this IServiceCollection services, IConfiguration configuration)
    {
        // Register configuration
        services.Configure<ContactApiConfig>(configuration.GetSection("ContactApi"));

        // Ensure HTTP client factory is available for CAPTCHA validation
        services.AddHttpClient();

        // Register mediator for CQRS pattern
        services.AddScoped<IMediator, Mediator>();

        // Register services
        services.AddScoped<IContactService, ContactService>();

        // Register CAPTCHA validator (default to NoOp)
        services.AddScoped<ICaptchaValidator, NoOpCaptchaValidator>();

        // Register CQRS handlers for contact form
        services.AddScoped<ICommandHandler<SubmitContactFormCommand, CommandResult<ContactFormResponse>>, SubmitContactFormHandler>();
        services.AddScoped<IQueryHandler<ValidateCaptchaQuery, bool>, ValidateCaptchaHandler>();

        return services;
    }

    /// <summary>
    /// Configures rate limiting for the Contact API.
    /// </summary>
    /// <param name="options">Rate limiter options.</param>
    public static void ConfigureContactApiRateLimiting(RateLimiterOptions options)
    {
        options.AddPolicy("contact-form", context =>
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ipAddress,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });
    }
}