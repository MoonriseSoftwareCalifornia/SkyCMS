// <copyright file="DynamicPublisherWebsite.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>
using System.Text.RegularExpressions;
using AspNetCore.Identity.FlexDb.Extensions;
using Azure.Identity;
using Cosmos.BlobService;
using Cosmos.Common.Data;
using Cosmos.EmailServices;
using Cosmos.MicrosoftGraph;
using Cosmos.Publisher.Configuration;
using Microsoft.AspNetCore.Identity;

namespace Cosmos.Publisher.Boot
{
    /// <summary>
    /// Configures and initializes the web application with the necessary services, middleware, and settings.
    /// </summary>
    /// <remarks>This method sets up the application by registering essential services and configuring
    /// middleware. It includes support for memory caching, Azure credentials, CORS policies,
    /// Cosmos DB integration, identity management, OAuth providers, distributed caching, rate limiting,
    /// and more. The method also configures the HTTP request pipeline and maps routes for controllers
    /// and Razor Pages.
    ///
    /// Key features include:
    /// - Integration with Cosmos DB for data storage and caching.
    /// - Support for OAuth authentication with Google and Microsoft providers.
    /// - Middleware configuration for HTTPS redirection, static files, response caching, and rate limiting.
    /// - Customizable CORS policies and cookie settings.
    /// - Support for distributed environments with data protection and caching mechanisms.
    ///
    /// This method is intended to be called during the application's startup process to ensure all
    /// required dependencies and configurations are in place before the application begins handling requests.
    /// </remarks>
    public static class DynamicPublisherWebsite
    {
        /// <summary>
        /// Boots the web application by configuring services, middleware, and settings.
        /// </summary>
        /// <param name="builder">Web application service builder.</param>
        /// <returns>Task.</returns>
        public static async Task Boot(WebApplicationBuilder builder)
        {
            // Configure core services
            builder.Services.AddPublisherCoreServices();
            builder.Services.AddPublisherCors(builder.Configuration);
            builder.Services.AddPublisherDatabase(builder.Configuration);
            builder.Services.AddPublisherMediator();
            builder.Services.AddPublisherStorageContext(builder.Configuration);
            builder.Services.AddPublisherRateLimiting();
            builder.Services.AddPublisherJsonSerialization();
            builder.Services.AddPublisherResponseCaching();
            builder.Services.AddPublisherDistributedCache(builder.Configuration);
            builder.Services.AddPublisherForwardedHeaders();
            builder.Services.AddPublisherOptions(builder.Configuration);
            builder.Services.AddPublisherGraphIntegration();
            builder.Services.AddPublisherCaching();

            // Create one instance of the DefaultAzureCredential to be used throughout the application.
            var defaultAzureCredential = new DefaultAzureCredential();
            builder.Services.AddSingleton(defaultAzureCredential);

            // Add Cosmos Identity
            builder.Services.AddCosmosIdentity<ApplicationDbContext, IdentityUser, IdentityRole, string>(
                options => options.SignIn.RequireConfirmedAccount = PublisherConfigurationKeys.Authentication.RequireConfirmedAccount)
                .AddDefaultUI()
                .AddDefaultTokenProviders();

            // Configure authentication cookie
            builder.Services.ConfigureApplicationCookie(o =>
            {
                o.Cookie.Name = PublisherConfigurationKeys.Authentication.CookieName;
                o.ExpireTimeSpan = PublisherConfigurationKeys.Authentication.DefaultExpireTimeSpan;
                o.SlidingExpiration = PublisherConfigurationKeys.Authentication.SlidingExpirationEnabled;
            });

            // Add data protection
            var containerClient = Cosmos.BlobService.ServiceCollectionExtensions.GetBlobContainerClient(
                builder.Configuration, defaultAzureCredential, "dataprotection");
            containerClient.CreateIfNotExists();
            builder.Services.AddCosmosCmsDataProtection(builder.Configuration, defaultAzureCredential);

            // Configure OAuth providers
            ConfigureOAuthProviders(builder);

            // Add Email services
            builder.Services.AddCosmosEmailServices(builder.Configuration);
            builder.Services.AddControllersWithViews();

            // Build application
            var app = builder.Build();

            // Configure middleware pipeline
            app.UsePublisherCoreMiddleware(builder.Configuration);
            app.UsePublisherRouting(builder.Configuration);
            app.UsePublisherAuthentication();

            // Map endpoints
            app.MapControllerRoute(
                name: "pub",
                pattern: "pub/{*index}",
                defaults: new { controller = "Pub", action = "Index" });

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapControllerRoute(
                "MsValidation",
                ".well-known/microsoft-identity-association.json",
                new { controller = "Home", action = "GetMicrosoftIdentityAssociation" });

            app.MapControllerRoute(
                "MyArea",
                "{area:exists}/{controller=Home}/{action=Index}/{id?}");

            app.MapPublisherAntiforgeryEndpoint();
            app.MapPublisherHealthEndpoint();

            app.MapFallbackToController("Index", "Home");
            app.MapRazorPages();

            await app.RunAsync();
        }

        /// <summary>
        /// Configures OAuth authentication providers (Google and Microsoft).
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        private static void ConfigureOAuthProviders(WebApplicationBuilder builder)
        {
            // Add Google if keys are present
            var googleOAuth = builder.Configuration.GetSection(PublisherConfigurationKeys.OAuth.GoogleSection)
                .Get<Cosmos.Common.Services.Configurations.OAuth>();

            if (googleOAuth?.IsConfigured() == true)
            {
                builder.Services.AddAuthentication().AddGoogle(options =>
                {
                    options.ClientId = googleOAuth.ClientId;
                    options.ClientSecret = googleOAuth.ClientSecret;
                });
            }

            // Add Microsoft if keys are present
            var entraIdOAuth = builder.Configuration.GetSection(PublisherConfigurationKeys.OAuth.MicrosoftSection)
                .Get<Cosmos.Common.Services.Configurations.OAuth>();

            if (entraIdOAuth?.IsConfigured() == true)
            {
                builder.Services.AddScoped<MsGraphService>();
                builder.Services.AddScoped<MsGraphClaimsTransformation>();

                builder.Services.AddAuthentication().AddMicrosoftAccount(options =>
                {
                    options.ClientId = entraIdOAuth.ClientId;
                    options.ClientSecret = entraIdOAuth.ClientSecret;

                    if (!string.IsNullOrEmpty(entraIdOAuth.TenantId))
                    {
                        options.AuthorizationEndpoint = $"https://login.microsoftonline.com/{entraIdOAuth.TenantId}/oauth2/v2.0/authorize";
                        options.TokenEndpoint = $"https://login.microsoftonline.com/{entraIdOAuth.TenantId}/oauth2/v2.0/token";
                    }

                    if (!string.IsNullOrEmpty(entraIdOAuth.CallbackDomain))
                    {
                        options.Events.OnRedirectToAuthorizationEndpoint = context =>
                        {
                            var redirectUrl = Regex.Replace(
                                context.RedirectUri,
                                "redirect_uri=(.)+%2Fsignin-",
                                $"redirect_uri=https%3A%2F%2F{entraIdOAuth.CallbackDomain}%2Fsignin-");
                            context.Response.Redirect(redirectUrl);
                            return Task.CompletedTask;
                        };
                    }
                });
            }
        }
    }
}