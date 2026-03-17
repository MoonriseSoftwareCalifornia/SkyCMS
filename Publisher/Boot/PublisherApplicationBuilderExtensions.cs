// <copyright file="PublisherApplicationBuilderExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Cosmos.Publisher.Configuration;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Extension methods for configuring middleware in the Publisher application.
    /// Centralizes middleware setup to reduce duplication and improve maintainability.
    /// </summary>
    public static class PublisherApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds Publisher core middleware in the correct order for HTTP request pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="configuration">The configuration provider.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UsePublisherCoreMiddleware(this IApplicationBuilder app, IConfiguration configuration)
        {
            app.UseForwardedHeaders();

            if (app.ApplicationServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            return app;
        }

        /// <summary>
        /// Adds Publisher routing and CORS middleware.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="configuration">The configuration provider.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UsePublisherRouting(this IApplicationBuilder app, IConfiguration configuration)
        {
            app.UseRouting();
            app.UseRateLimiter();

            var corsOrigins = configuration.GetValue<string>(PublisherConfigurationKeys.CorsAllowedOrigins);
            if (string.IsNullOrEmpty(corsOrigins))
            {
                app.UseCors();
            }
            else
            {
                app.UseCors(PublisherConfigurationKeys.CorsPolicyName);
            }

            return app;
        }

        /// <summary>
        /// Adds Publisher authentication and authorization middleware.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UsePublisherAuthentication(this IApplicationBuilder app)
        {
            app.UseResponseCaching();
            app.UseAuthentication();
            app.UseAuthorization();

            return app;
        }

        /// <summary>
        /// Maps the antiforgery token endpoint for XSRF protection.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The endpoint route builder for further configuration.</returns>
        public static IEndpointRouteBuilder MapPublisherAntiforgeryEndpoint(this IEndpointRouteBuilder app)
        {
            app.MapGet("ccms__antiforgery/token", (IAntiforgery forgeryService, HttpContext context) =>
            {
                var tokens = forgeryService.GetAndStoreTokens(context);
                context.Response.Headers["XSRF-TOKEN"] = tokens.RequestToken;
                return Results.Ok();
            });

            return app;
        }

        /// <summary>
        /// Maps the health check endpoint for load balancer health checks.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The endpoint route builder for further configuration.</returns>
        public static IEndpointRouteBuilder MapPublisherHealthEndpoint(this IEndpointRouteBuilder app)
        {
            app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
            return app;
        }

        /// <summary>
        /// Adds static asset caching headers middleware.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UsePublisherStaticAssetCaching(this IApplicationBuilder app)
        {
            app.Use(async (context, next) =>
            {
                context.Response.GetTypedHeaders().CacheControl =
                    new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = PublisherConfigurationKeys.ResponseHeaders.StaticAssetsCacheControl,
                    };
                context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] =
                    new string[] { "Accept-Encoding" };

                await next();
            });

            return app;
        }
    }
}
