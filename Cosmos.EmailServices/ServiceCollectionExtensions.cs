// <copyright file="ServiceCollectionExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/MoonriseSoftwareCalifornia/CosmosCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.EmailServices
{
    using System.Configuration;
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// extension methods for <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the Cosmos Email Services to the services collection as a SCOPED service.
        /// Automatically resolves email provider at runtime for both single-tenant and multi-tenant scenarios.
        /// </summary>
        /// <param name="services">Startup services collection.</param>
        /// <param name="configuration">System configuration.</param>
        /// <remarks>
        /// Resolution order:
        /// - Multi-tenant: Load from tenant's database Settings table
        /// - Single-tenant with env vars: Load from IConfiguration (environment variables)
        /// - Single-tenant without env vars: Load from database Settings table
        /// - Fallback: NoOp sender (logs warning)
        /// </remarks>
        public static void AddCosmosEmailServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register DynamicEmailSender as SCOPED (supports per-request tenant resolution)
            services.AddScoped<IEmailSender, DynamicEmailSender>();
            services.AddScoped<ICosmosEmailSender>(sp => (ICosmosEmailSender)sp.GetRequiredService<IEmailSender>());
        }

        /// <summary>
        /// Adds the SendGrid Email Provider to the services collection.
        /// </summary>
        /// <param name="services">Startup services collection.</param>
        /// <param name="options">SendGrid provider options.</param>
        public static void AddSendGridEmailProvider(this IServiceCollection services, SendGridEmailProviderOptions options)
        {
            services.AddSingleton(Options.Create(options));
            services.AddTransient<IEmailSender, SendGridEmailSender>();
        }

        /// <summary>
        /// Adds the default Azure Email Communication Services.
        /// </summary>
        /// <param name="services">Startup services collection.</param>
        /// <param name="options">Azure Communications email provider options.</param>
        public static void AddAzureCommunicationEmailSenderProvider(this IServiceCollection services, AzureCommunicationEmailProviderOptions options)
        {
            services.AddSingleton(Options.Create(options));
            services.AddTransient<IEmailSender, AzureCommunicationEmailSender>();
        }

        /// <summary>
        /// Add SMTP EMail Provider.
        /// </summary>
        /// <param name="services">Startup services collection.</param>
        /// <param name="options">SMTP Email provider options.</param>
        public static void AddSmtpEmailProvider(this IServiceCollection services, SmtpEmailProviderOptions options)
        {
            services.AddSingleton(Options.Create(options));
            services.AddTransient<IEmailSender, SmtpEmailSender>();
        }

        /// <summary>
        /// Adds a NoOp Email Sender to the services collection.
        /// </summary>
        /// <param name="services">Startup services collection.</param>
        public static void AddNoOpEmailSender(this IServiceCollection services)
        {
            services.AddTransient<IEmailSender, CosmosNoOpEmailSender>();
        }
    }
}