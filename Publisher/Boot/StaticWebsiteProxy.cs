// <copyright file="StaticWebsiteProxy.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>
using Azure.Identity;
using Cosmos.Publisher.Configuration;

namespace Cosmos.Publisher.Boot
{
    /// <summary>
    /// Configures and starts a web application to serve static files from the "wwwroot" directory.
    /// </summary>
    /// <remarks>This method sets up the application to serve static files and enables default file handling,
    /// such as serving "index.html" when a directory is accessed. Directory browsing is also enabled to allow users to
    /// view the contents of directories if no default file is present.
    /// </remarks>
    public static class StaticWebsiteProxy
    {
        /// <summary>
        /// Configures and starts the web application with support for serving static files.
        /// </summary>
        /// <remarks>This method enables serving static files from the "wwwroot" directory and configures
        /// the application to look for default files such as "index.html". Directory browsing is also enabled to allow
        /// users to view directory contents if no default file is present. Once configured, the application is
        /// started.
        /// </remarks>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> used to configure and build the application.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Boot(WebApplicationBuilder builder)
        {
            // Configure core services
            builder.Services.AddPublisherCoreServices();
            builder.Services.AddPublisherCors(builder.Configuration);
            builder.Services.AddPublisherStorageContext(builder.Configuration);
            builder.Services.AddPublisherRateLimiting();
            builder.Services.AddPublisherJsonSerialization();
            builder.Services.AddPublisherResponseCaching(isStaticSite: true);

            // Create one instance of the DefaultAzureCredential to be used throughout the application.
            var defaultAzureCredential = new DefaultAzureCredential();
            builder.Services.AddSingleton(defaultAzureCredential);

            // Add services for controllers with views
            builder.Services.AddControllersWithViews();

            // Build application
            var app = builder.Build();

            // Configure middleware pipeline
            app.UseForwardedHeaders();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseRouting();

            // Add Rate Limiter to prevent abuse.
            app.UseRateLimiter();

            var corsOrigins = builder.Configuration.GetValue<string>(PublisherConfigurationKeys.CorsAllowedOrigins);
            if (string.IsNullOrEmpty(corsOrigins))
            {
                app.UseCors();
            }
            else
            {
                app.UseCors(PublisherConfigurationKeys.CorsPolicyName);
            }

            app.UseResponseCaching();

            // Add static asset caching
            app.UsePublisherStaticAssetCaching();

            // Map health and antiforgery endpoints
            var endpointRouteBuilder = app.MapWhen(
                context => !context.Request.Path.StartsWithSegments("/healthz") &&
                           !context.Request.Path.StartsWithSegments("/ccms__antiforgery"),
                appBuilder =>
                {
                    appBuilder.Run(async context =>
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.CompleteAsync();
                    });
                });

            app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

            app.MapGet("ccms__antiforgery/token", (Microsoft.AspNetCore.Antiforgery.IAntiforgery forgeryService, HttpContext context) =>
            {
                var tokens = forgeryService.GetAndStoreTokens(context);
                context.Response.Headers["XSRF-TOKEN"] = tokens.RequestToken;
                return Results.Ok();
            });

            app.MapControllerRoute(
                name: "catchall",
                pattern: "{*path}",
                defaults: new { controller = "StaticProxy", action = "Index" });

            await app.RunAsync();
        }
    }
}
