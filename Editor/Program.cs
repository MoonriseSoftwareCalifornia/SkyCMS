// <copyright file="Program.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using System.Web;
using AspNetCore.Identity.FlexDb.Extensions;
using Azure.Identity;
using Cosmos.BlobService;
using Cosmos.Cms.Common.Services.Configurations;
using Cosmos.Common;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Articles.Shared;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services;
using Cosmos.Common.Services.BlogPublishing;
using Cosmos.Common.Services.Caching;
using Cosmos.Common.Services.Configurations;
using Cosmos.Common.Services.Email;
using Cosmos.Common.Services.PublishedBlog;
using Cosmos.DynamicConfig;
using Cosmos.DynamicConfig.Configurations;
using Cosmos.EmailServices;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using Sky.Cms.Api.Shared.Extensions;
using Sky.Cms.Hubs;
using Sky.Cms.Services;
using Sky.Editor.Boot;
using Sky.Editor.Data.Logic;
using Sky.Editor.Domain.Events;
using Sky.Editor.Extensions;
using Sky.Editor.Hubs;
using Sky.Editor.Infrastructure.SignalR;
using Sky.Editor.Infrastructure.Time;
using Sky.Editor.Middleware;
using Sky.Editor.Services.Authors;
using Sky.Editor.Services.Catalog;
using Sky.Editor.Services.CDN;
using Sky.Editor.Services.Copilot;
using Sky.Editor.Services.Diagnostics;
using Sky.Editor.Services.EditorSettings;
using Sky.Editor.Services.Email;
using Sky.Editor.Services.Html;
using Sky.Editor.Services.Layout;
using Sky.Editor.Services.Layouts;
using Sky.Editor.Services.Migrations;
using Sky.Editor.Services.Publishing;
using Sky.Editor.Services.Redirects;
using Sky.Editor.Services.ReservedPaths;
using Sky.Editor.Services.Scheduling;
using Sky.Editor.Services.Setup;
using Sky.Editor.Services.Slugs;
using Sky.Editor.Services.Templates;
using Sky.Editor.Services.Titles;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------
// CONFIGURATION CONSTANTS
// ---------------------------------------------------------------
const string CONFIG_MULTI_TENANT = "MultiTenantEditor";
const string CONFIG_ALLOW_SETUP = "CosmosAllowSetup";
const string CONFIG_ENABLE_DIAGNOSTICS = "EnableDiagnostics";
const string CONFIG_REQUIRES_AUTH = "CosmosRequiresAuthentication";
const string CONFIG_ALLOW_LOCAL_ACCOUNTS = "AllowLocalAccounts";
const string CONFIG_PASSKEY_SERVER_DOMAIN = "IdentityPasskey:ServerDomain";
const string CONNECTIONSTRING_APP_DB = "ApplicationDbContextConnection";
const string CONNECTIONSTRING_CONFIG_DB = "ConfigDbConnectionString";

// ---------------------------------------------------------------
// CHECK FOR MINIMAL CONFIGURATION (EARLY EXIT IF NOT MET)
// ---------------------------------------------------------------
var isMultiTenantEditor = builder.Configuration.GetValue<bool?>(CONFIG_MULTI_TENANT) ?? false;

if (isMultiTenantEditor)
{
    var configDbConnectionString = builder.Configuration.GetConnectionString(CONNECTIONSTRING_CONFIG_DB);

    if (string.IsNullOrWhiteSpace(configDbConnectionString))
    {
        throw new InvalidOperationException($"Connection string '{CONNECTIONSTRING_CONFIG_DB}' is required for multi-tenant mode. Please provide a valid connection string in the configuration.");
    }
}
else
{
    var applicationDbContextConnection = builder.Configuration.GetConnectionString(CONNECTIONSTRING_APP_DB);
    if (string.IsNullOrWhiteSpace(applicationDbContextConnection))
    {
        throw new InvalidOperationException($"Connection string '{CONNECTIONSTRING_APP_DB}' is required for single-tenant mode. Please provide a valid connection string in the configuration.");
    }
}

// Helper method for extracting hostname from request (used by cookie configuration)
static string GetHostname(HttpContext context)
{
    var hostname = context.Request.Headers["x-origin-hostname"].ToString().ToLowerInvariant();
    return string.IsNullOrWhiteSpace(hostname)
        ? context.Request.Host.Host.ToLowerInvariant()
        : hostname;
}

// ---------------------------------------------------------------
// STEP 1: DETERMINE DEPLOYMENT MODE
// ---------------------------------------------------------------
var allowSetup = builder.Configuration.GetValue<bool?>(CONFIG_ALLOW_SETUP) ?? false;
var enableDiagnostics = builder.Configuration.GetValue<bool?>(CONFIG_ENABLE_DIAGNOSTICS) ?? false;
var versionNumber = Assembly.GetExecutingAssembly().GetName().Version.ToString();

if (enableDiagnostics)
{
    System.Console.WriteLine("⚠️ DIAGNOSTIC MODE ENABLED - Normal startup will be bypassed");
}
else
{
    System.Console.WriteLine($"Starting Cosmos CMS Editor in {(isMultiTenantEditor ? "Multi-Tenant" : "Single-Tenant")} Mode (v.{versionNumber})");
}

// ---------------------------------------------------------------
// STEP 1.5: ENTER DIAGNOSTIC MODE IF INDICATED
// ---------------------------------------------------------------
if (enableDiagnostics)
{
    ValidationResult? earlyValidationResult = null;

    System.Console.WriteLine("Diagnostic mode is enabled - performing early configuration validation...");

    // Perform synchronous validation WITHOUT requiring any services
    var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
    var logger = loggerFactory.CreateLogger<ConfigurationValidator>();
    var validator = new ConfigurationValidator(builder.Configuration, logger);

    // Run validation synchronously at startup
    earlyValidationResult = validator.ValidateAsync().GetAwaiter().GetResult();
    bool configurationValid = earlyValidationResult.IsValid;

    if (!configurationValid)
    {
        System.Console.WriteLine("⚠️ Configuration validation FAILED - starting in diagnostic-only mode");
        System.Console.WriteLine($"   Errors: {earlyValidationResult.Checks.Count(c => c.Status == CheckStatus.Error)}");
        System.Console.WriteLine($"   Warnings: {earlyValidationResult.Checks.Count(c => c.Status == CheckStatus.Warning)}");
    }
    else
    {
        System.Console.WriteLine("✅ Configuration validation passed");
    }

    // DIAGNOSTIC-ONLY MODE: Minimal services to show diagnostic page
    // Note: This branch exits early and never reaches the normal service registration below
    System.Console.WriteLine("Registering minimal services for diagnostic-only mode...");

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ConfigurationValidator>();
    builder.Services.AddRazorPages();
    builder.Services.AddControllersWithViews(); // Minimal - no API controllers

    // Build minimal app
    var diagnosticApp = builder.Build();

    // Configure minimal middleware pipeline
    diagnosticApp.UseRouting();
    diagnosticApp.UseStaticFiles();

    // Redirect ALL requests to diagnostic page
    diagnosticApp.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/___diagnostics") &&
            !context.Request.Path.StartsWithSegments("/lib") &&
            !context.Request.Path.StartsWithSegments("/css") &&
            !context.Request.Path.StartsWithSegments("/js") &&
            !context.Request.Path.Value.EndsWith(".css") &&
            !context.Request.Path.Value.EndsWith(".js"))
        {
            context.Response.Redirect("/___diagnostics");
            return;
        }

        await next();
    });

    diagnosticApp.MapRazorPages();

    System.Console.WriteLine("🔧 Application started in DIAGNOSTIC-ONLY mode");
    System.Console.WriteLine("   Navigate to: /___diagnostics");
    System.Console.WriteLine("   Fix configuration issues and restart the application");

    await diagnosticApp.RunAsync();
    return; // ← EXIT - Normal startup below NEVER runs
}

// ---------------------------------------------------------------
// STEP 1.6: RUN CUSTOM MIGRATION SERVICE (SCHEMA + DATA)
// ---------------------------------------------------------------
// This step runs the custom migration service that handles both schema migrations
// (e.g., adding columns, creating indexes) and data migrations (e.g., populating
// LayoutNumber values). The service automatically detects the database provider
// (Cosmos DB, SQL Server, MySQL, SQLite) and applies provider-specific migrations.
//
// Migration Flow:
//   1. Detect database provider using CosmosDbOptionsBuilder strategies
//   2. Run schema migrations (M001_AddLayoutNumber, etc.)
//   3. Run data migrations (LayoutMigrationService)
//
// Supported Modes:
//   - Single-Tenant: Migrates a single database
//   - Multi-Tenant: Discovers all tenants via DynamicConfigurationProvider
//                    and migrates each tenant's database independently
// ---------------------------------------------------------------
// The following gets the proxy settings for multi-tenant scenarios.
// These settings are used by the DynamicConfigurationProvider.
builder.Services.Configure<ProxySettings>(builder.Configuration.GetSection("ProxySettings"));
if (allowSetup)
{
    var migrationSummary = await StartupMigrationService.RunMigrationsAsync(
        builder.Configuration,
        isMultiTenantEditor);

    migrationSummary.LogResults();

    // Halt startup if single-tenant migration failed (multi-tenant continues)
    if (!migrationSummary.IsSuccess && !isMultiTenantEditor)
    {
        throw new InvalidOperationException("Application startup halted due to migration failure. Please fix the database configuration and restart.");
    }
}

// ---------------------------------------------------------------
// STEP 2: Register Core Infrastructure (Common to Both Modes)
// ---------------------------------------------------------------
builder.Services.AddMemoryCache();
builder.Services.AddScoped(typeof(ICacheService<>), typeof(CacheService<>)); // Scoped to allow tenant-specific caching, and prevents potential issues with singleton cache in multi-tenant scenarios.
builder.Services.AddSingleton<ICacheKeyProvider, CacheKeyProvider>(); // Stateless and thread-safe, can be singleton.
var defaultAzureCredential = new DefaultAzureCredential();
builder.Services.AddSingleton(defaultAzureCredential);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// ---------------------------------------------------------------
// STEP 3: Configure ApplicationDbContext and other services based
// on deployment mode.
// ---------------------------------------------------------------
if (isMultiTenantEditor)
{
    // Multi-tenant: Dynamic configuration from database
    builder.Services.AddSingleton<IDynamicConfigurationProvider, DynamicConfigurationProvider>();
    builder.Services.AddScoped<IMultiTenantSetupService, MultiTenantSetupService>();
    MultiTenant.Configure(builder, defaultAzureCredential);
}
else
{
    // Single-tenant: Configuration from environment variables/appsettings
    builder.Services.AddSingleton<IDynamicConfigurationProvider, SingleTenantConfigurationProvider>();
    SingleTenant.Configure(builder);
}

// ---------------------------------------------------------------
// STEP 4: Register Site Settings
// ---------------------------------------------------------------
builder.Services.Configure<SiteSettings>(settings =>
{
    settings.MultiTenantEditor = isMultiTenantEditor;
    settings.AllowSetup = allowSetup;
    settings.CosmosRequiresAuthentication = builder.Configuration.GetValue<bool?>(CONFIG_REQUIRES_AUTH) ?? false;
    settings.AllowLocalAccounts = builder.Configuration.GetValue<bool?>(CONFIG_ALLOW_LOCAL_ACCOUNTS) ?? true;
    settings.AllowedFileTypes = ".js,.css,.htm,.html,.mov,.webm,.avi,.mp4,.mpeg,.ts,.svg,.json";
    settings.MultiTenantRedirectUrl = builder.Configuration.GetValue<string>("MultiTenantRedirectUrl") ?? string.Empty;
    settings.MicrosoftAppId = builder.Configuration.GetValue<string>("MicrosoftAppId") ?? string.Empty;
});

// ---------------------------------------------------------------
// STEP 5: Register OAuth Configuration (if available)
// ---------------------------------------------------------------
var microsoftAuth = builder.Configuration.GetSection("MicrosoftOAuth").Get<AzureAD>()
                    ?? builder.Configuration.GetSection("AzureAD").Get<AzureAD>();
if (microsoftAuth != null)
{
    builder.Services.AddSingleton(microsoftAuth);
}

// ---------------------------------------------------------------
// STEP 6: Register Email Services
// ---------------------------------------------------------------
// Register Cosmos Email Services (SMTP, Azure Communication, SendGrid, or NoOp fallback)
// This automatically selects the appropriate provider based on configuration
builder.Services.AddCosmosEmailServices(builder.Configuration);

// ---------------------------------------------------------------
// STEP 7: Register Application Services
// ---------------------------------------------------------------
// Scoped services (per-request lifecycle, can access HttpContext)
builder.Services.AddTransient<IDatabaseConnectionTester, DatabaseConnectionTester>();
builder.Services.AddScoped<IStorageConnectionTester>(sp => new StorageConnectionTester(sp.GetRequiredService<IMemoryCache>()));
builder.Services.AddTransient<ISendGridEmailTester, SendGridEmailTester>();
builder.Services.AddTransient<ISmtpEmailTester, SmtpEmailTester>();
builder.Services.AddScoped<ISetupService, SetupService>();
builder.Services.AddScoped<ILayoutFamilyService, LayoutFamilyService>();
builder.Services.AddScoped<IStorageContext, StorageContext>();
builder.Services.AddScoped<IEditorSettings, EditorSettings>();
builder.Services.AddScoped<ICopilotProxyOptionsService, CopilotProxyOptionsService>();
builder.Services.AddScoped<ILayoutTemplateService, LayoutTemplateService>();
builder.Services.AddScoped<ILayoutVersioningService, LayoutVersioningService>();
builder.Services.AddScoped<IViewRenderService, ViewRenderService>();

// Register shared query services (Common namespace - used by both Editor and Publisher)
builder.Services.AddScoped<IArticleCatalogQueryService>(sp =>
    new ArticleCatalogQueryService(
        sp.GetRequiredService<ApplicationDbContext>(),
        builder.Configuration.GetValue<string>("CosmosPublisherUrl") ?? string.Empty,
        builder.Configuration.GetValue<string>("SiteSettings:BlobPublicUrl") ?? string.Empty));
builder.Services.AddScoped<IPublishedPageQueryService, PublishedPageQueryService>();
builder.Services.AddScoped<IBlogNavigationService, BlogNavigationService>();
builder.Services.AddScoped<IPublishedBlogService, PublishedBlogService>();
builder.Services.AddScoped<IContactManagementService, ContactManagementService>();

// Register SINGLE mediator (Common namespace) with multi-tenant security decorator
builder.Services.AddCosmosMediator();

// Auto-register all command and query handlers from Editor and Common assemblies
builder.Services.AddMediatorHandlers();

builder.Services.AddScoped<ILayoutImportService, LayoutImportService>();

builder.Services.AddScoped<IArticleViewModelBuilder>(sp =>
    new ArticleViewModelBuilder(
        sp.GetRequiredService<IMediator>(),
        sp.GetRequiredService<ApplicationDbContext>(),
        sp.GetRequiredService<IMemoryCache>(),
        builder.Configuration.GetValue<string>("CosmosPublisherUrl") ?? string.Empty,
        isEditor: true));

// Transient services (stateless operations, created each time)
builder.Services.AddTransient<ICdnServiceFactory, CdnServiceFactory>();
builder.Services.AddTransient<ITemplateService, TemplateService>();
builder.Services.AddTransient<IArticleHtmlService, ArticleHtmlService>();
builder.Services.AddTransient<IAuthorInfoService, AuthorInfoService>();
builder.Services.AddTransient<ICatalogService, CatalogService>();
builder.Services.AddTransient<IClock, Sky.Editor.Infrastructure.Time.SystemClock>();
builder.Services.AddTransient<IDomainEventDispatcher>(sp => new DomainEventDispatcher(type => sp.GetServices(type)));
builder.Services.AddTransient<IPublishingService, PublishingService>();
builder.Services.AddTransient<IRedirectService, RedirectService>();
builder.Services.AddTransient<IReservedPaths, ReservedPaths>();
builder.Services.AddTransient<ISlugService, SlugService>();
builder.Services.AddTransient<ITitleChangeService, TitleChangeService>();
builder.Services.AddTransient<IBlogStreamRenderingService, BlogStreamRenderingService>();
builder.Services.AddTransient<IEmailConfigurationService, EmailConfigurationService>();
builder.Services.AddTransient<IArticleScheduler, ArticleScheduler>();
builder.Services.AddTransient<ArticleEditLogic>();
builder.Services.AddTransient<ISetupCheckService, SetupCheckService>();

// Register Contact API services (required for /_api/contact endpoints)
builder.Services.AddContactApi(builder.Configuration);

// Register validator for diagnostic page (if setup allowed)
if (allowSetup)
{
    builder.Services.AddScoped<ConfigurationValidator>();
}

// Additional authorization policies
builder.Services.AddAuthorization();

// ---------------------------------------------------------------
// STEP 8: Register Background Services
// ---------------------------------------------------------------
builder.Services.AddHostedService<ArticleSchedulerBackgroundService>();

// ---------------------------------------------------------------
// STEP 9: Register Data Protection & SignalR
// ---------------------------------------------------------------
builder.Services.AddFlexDbDataProtection(builder.Configuration);

// Add SignalR with tenant isolation
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 102400; // 100 KB
    options.StreamBufferCapacity = 10;
});

// Custom user ID provider for tenant isolation - ensures SignalR connections are scoped to the current tenant's user
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();

// Progress reporter (SCOPED for tenant isolation) - allows per-user progress updates via SignalR
builder.Services.AddScoped<IPublishingProgressReporter, PublishingProgressReporter>();

// ---------------------------------------------------------------
// STEP 10: Register MVC & Razor Pages
// ---------------------------------------------------------------
// Normal mode: Full controller registration with API support
// Api route is /_api/*
builder.Services.AddControllersWithViews()
    .AddApplicationPart(typeof(Sky.Cms.Api.Shared.Controllers.ContactApiController).Assembly);

builder.Services.AddCosmosIdentity<ApplicationDbContext, IdentityUser, IdentityRole, string>(
      options => options.SignIn.RequireConfirmedAccount = true)
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    options.AuthenticatorTimeout = TimeSpan.FromMinutes(3);
    options.ChallengeSize = 64;

    // Single-tenant: allow explicit RP ID configuration.
    // Multi-tenant: leave unset so Identity derives host per request.
    if (!isMultiTenantEditor)
    {
        var configuredServerDomain = builder.Configuration.GetValue<string>(CONFIG_PASSKEY_SERVER_DOMAIN);
        if (!string.IsNullOrWhiteSpace(configuredServerDomain))
        {
            options.ServerDomain = configuredServerDomain.Trim().ToLowerInvariant();
        }
    }
});

// ---------------------------------------------------------------
// STEP 11: Configure OAuth Providers
// ---------------------------------------------------------------
var googleOAuth = builder.Configuration.GetSection("GoogleOAuth").Get<OAuth>();
if (googleOAuth != null && googleOAuth.IsConfigured())
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleOAuth.ClientId;
        options.ClientSecret = googleOAuth.ClientSecret;
    });
}

var entraIdOAuth = builder.Configuration.GetSection("MicrosoftOAuth").Get<OAuth>();
if (entraIdOAuth != null && entraIdOAuth.IsConfigured())
{
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
                var redirectUrl = Regex.Replace(context.RedirectUri, "redirect_uri=(.)+%2Fsignin-", $"redirect_uri=https%3A%2F%2F{entraIdOAuth.CallbackDomain}%2Fsignin-");
                context.Response.Redirect(redirectUrl);
                return Task.CompletedTask;
            };
        }
    });
}

// ---------------------------------------------------------------
// STEP 12: Configure Session & Razor Pages Routes
// ---------------------------------------------------------------
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(3600);
    options.Cookie.IsEssential = true;
});

builder.Services.AddRazorPages(options =>
{
    if (!isMultiTenantEditor)
    {
        // Single-tenant setup wizard pages
        options.Conventions.AddAreaPageRoute("Setup", "/Index", "___setup");
        options.Conventions.AddAreaPageRoute("Setup", "/Step1_Mode", "___setup/mode");
        options.Conventions.AddAreaPageRoute("Setup", "/Step2_Storage", "___setup/storage");
        options.Conventions.AddAreaPageRoute("Setup", "/Step3_AdminAccount", "___setup/admin");
        options.Conventions.AddAreaPageRoute("Setup", "/Step4_Publisher", "___setup/publisher");
        options.Conventions.AddAreaPageRoute("Setup", "/Step5_Email", "___setup/email");
        options.Conventions.AddAreaPageRoute("Setup", "/Step6_Review", "___setup/review");
        options.Conventions.AddAreaPageRoute("Setup", "/Complete", "___setup/complete");
    }
    else
    {
        // Multi-tenant setup pages (simplified)
        options.Conventions.AddAreaPageRoute("Setup", "/Tenant/Index", "___setup");
        options.Conventions.AddAreaPageRoute("Setup", "/Tenant/Index", "___setup/tenant");
        options.Conventions.AddAreaPageRoute("Setup", "/Tenant/Admin", "___setup/tenant/admin");
        options.Conventions.AddAreaPageRoute("Setup", "/Tenant/Complete", "___setup/tenant/complete");
    }
});

builder.Services.AddRazorPages();

builder.Services.AddMvc()
    .AddNewtonsoftJson(options =>
        options.SerializerSettings.ContractResolver = new DefaultContractResolver())
    .AddRazorPagesOptions(options =>
    {
        options.Conventions.AuthorizeAreaFolder("Identity", "/Account/Manage");
        options.Conventions.AuthorizeAreaPage("Identity", "/Account/Logout");
    });

// ---------------------------------------------------------------
// STEP 13: Configure CORS & Security
// ---------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllCors", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod();
    });
});

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "CosmosAuthCookie";
    options.ExpireTimeSpan = TimeSpan.FromDays(5);
    options.SlidingExpiration = true;

    // For multi-tenant: dynamically set cookie domain based on x-origin-hostname header
    if (isMultiTenantEditor)
    {
        options.Events.OnValidatePrincipal = async context =>
        {
            var currentDomain = GetHostname(context.HttpContext);

            if (context.Principal?.Identity?.IsAuthenticated == true)
            {
                var cookieDomainClaim = context.Principal.FindFirst("CookieDomain");
                if (cookieDomainClaim != null && !cookieDomainClaim.Value.Equals(currentDomain, StringComparison.OrdinalIgnoreCase))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            }
        };

        options.Events.OnSigningIn = context =>
        {
            var currentDomain = GetHostname(context.HttpContext);

            var identity = (System.Security.Claims.ClaimsIdentity)context.Principal.Identity;
            identity.AddClaim(new System.Security.Claims.Claim("CookieDomain", currentDomain));

            return Task.CompletedTask;
        };

        options.Cookie.Domain = null;
    }

    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;

    options.Events.OnRedirectToLogin = x =>
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(x.Request.QueryString.Value);

        // Remove internal query parameters before redirecting
        queryParams.Remove("ccmswebsite");
        queryParams.Remove("ccmsopt");
        queryParams.Remove("ccmsemail");
        var queryString = HttpUtility.UrlEncode(queryParams.ToString());

        if (x.Request.Path.Equals("/Preview", StringComparison.InvariantCultureIgnoreCase))
        {
            x.Response.Redirect($"/Identity/Account/Login?returnUrl=/Home/Preview?{queryString}");
            return Task.CompletedTask;
        }

        x.Response.Redirect($"/Identity/Account/Login?returnUrl={x.Request.Path}&{queryString}");
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToLogout = x =>
    {
        x.Response.Redirect("/Identity/Account/Logout");
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = x =>
    {
        x.Response.Redirect("/Identity/Account/AccessDenied");
        return Task.CompletedTask;
    };
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---------------------------------------------------------------
// STEP 14: Configure Rate Limiting
// ---------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    // Fixed window limiter for general use
    options.AddFixedWindowLimiter(policyName: "fixed", opt =>
    {
        opt.PermitLimit = 4;
        opt.Window = TimeSpan.FromSeconds(8);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });

    // Deployment API rate limiter
    options.AddFixedWindowLimiter("deployment", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(5);
        opt.PermitLimit = 10;  // Max 10 deployments per 5 minutes per IP
        opt.QueueLimit = 0;    // No queuing
    });

    // Docs import API rate limiter
    var docsImportPermitLimit = builder.Configuration.GetValue<int?>("DocsImport:RateLimit:PermitLimit") ?? 300;
    var docsImportWindowMinutes = builder.Configuration.GetValue<int?>("DocsImport:RateLimit:WindowMinutes") ?? 5;
    docsImportPermitLimit = Math.Max(1, docsImportPermitLimit);
    docsImportWindowMinutes = Math.Max(1, docsImportWindowMinutes);

    options.AddFixedWindowLimiter("docs-import", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(docsImportWindowMinutes);
        opt.PermitLimit = docsImportPermitLimit;
        opt.QueueLimit = 0;
    });

    // Contact form submission rate limiter (for Sky.Cms.Api.Shared)
    // Environment-aware: relaxed in development, strict in production
    options.AddFixedWindowLimiter("contact-form", opt =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development: Relaxed limits for testing
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 20;  // Allow more frequent testing
        }
        else
        {
            // Production: Strict anti-spam protection
            opt.Window = TimeSpan.FromMinutes(5);
            opt.PermitLimit = 3;
        }

        opt.QueueLimit = 0;   // Reject immediately when limit reached
    });

    // Copilot inline completion endpoint limiter
    options.AddFixedWindowLimiter("copilot-inline", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = builder.Environment.IsDevelopment() ? 120 : 60;
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("copilot-chat", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = builder.Environment.IsDevelopment() ? 30 : 15;
        opt.QueueLimit = 0;
    });

    // Add a global rate limiter for general API protection
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Exempt deployment endpoint from global limits (it has its own)
        if (context.Request.Path.StartsWithSegments("/api/deployment"))
        {
            return RateLimitPartition.GetNoLimiter("deployment-exempt");
        }

        // Apply general rate limiting to other API endpoints
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 100
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                success = false,
                error = "Too many requests. Please try again later."
            }, cancellationToken);
    };
});

// ---------------------------------------------------------------
// BUILD APPLICATION
// ---------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------
// CONFIGURE MIDDLEWARE PIPELINE
// ---------------------------------------------------------------

// Multi-tenant middleware (must run early in pipeline)
if (isMultiTenantEditor)
{
    app.UseMiddleware<DomainMiddleware>();
    app.UseTenantSetupRedirect();
}

// ---------------------------------------------------------------
// UNIFIED SETUP DETECTION MIDDLEWARE (Both Single & Multi-Tenant)
// ---------------------------------------------------------------
if (allowSetup)
{
    app.UseSetupDetection(isMultiTenantEditor);
}

app.UseCosmosCmsDataProtection();

// CloudFront protocol mapping (before UseForwardedHeaders)
app.Use(async (context, next) =>
{
    var headers = context.Request.Headers;
    if (headers.TryGetValue("CloudFront-Forwarded-Proto", out var cfProto))
    {
        headers["X-Forwarded-Proto"] = cfProto;
    }

    await next();
});

app.UseForwardedHeaders();

// Setup wizard access control
if (allowSetup)
{
    app.UseSetupAccessControl(isMultiTenantEditor);
}

// Environment-specific middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Handle unhandled exceptions (500, etc.)
    app.UseExceptionHandler("/Error"); // Use your generic error page

    // Handle status codes (404, 403, etc.) by redirecting to the same error page
    app.UseStatusCodePages(context =>
    {
        context.HttpContext.Response.Redirect("/Error");
        return Task.CompletedTask;
    });

    app.UseHsts();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".js"))
        {
            ctx.Context.Response.Headers.Append("Content-Type", "application/javascript");
        }
    }
});

app.UseRouting();
app.UseCors();
app.UseResponseCaching();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ---------------------------------------------------------------
// CONFIGURE ENDPOINTS
// ---------------------------------------------------------------
app.MapGet("/___healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet("ccms__antiforgery/token", (IAntiforgery forgeryService, HttpContext context) =>
{
    var tokens = forgeryService.GetAndStoreTokens(context);
    context.Response.Headers["XSRF-TOKEN"] = tokens.RequestToken;
    return Results.Ok();
});
app.MapControllerRoute("MsValidation", ".well-known/microsoft-identity-association.json", new { controller = "Home", action = "GetMicrosoftIdentityAssociation" }).AllowAnonymous();
app.MapControllerRoute("MyArea", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(name: "pub", pattern: "pub/{*index}", defaults: new { controller = "Pub", action = "Index" });
app.MapControllerRoute(name: "blog_rss", pattern: "blog/rss", defaults: new { controller = "Blog", action = "Rss" });
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.MapAreaControllerRoute(
    name: "setup",
    areaName: "Setup",
    pattern: "___setup/{*pathInfo}",
    defaults: new { page = "/Index" });

// Map endpoints AFTER tenant context is established
app.MapRazorPages();
app.MapControllers();
app.MapHub<PublishingProgressHub>("/hubs/publishing-progress");
app.MapHub<LiveEditorHub>("/___cwps_hubs_live_editor");

// Add this catch-all route for dynamic pages (blog streams, articles, etc.)
app.MapFallbackToController("Index", "Home");

await app.RunAsync();
