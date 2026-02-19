---
title: Application Startup Lifecycle
description: ASP.NET Core application startup sequence and initialization process in SkyCMS
keywords: startup, lifecycle, initialization, ASP.NET-Core, architecture
audience: [developers, architects]
---

# Application Startup Lifecycle

This guide documents the startup sequence, initialization order, and lifecycle events in SkyCMS. Understanding this helps with debugging startup issues, extending the startup process, and optimizing initialization.

## Table of Contents
- [Startup Overview](#startup-overview)
- [Startup Sequence](#startup-sequence)
- [Dependency Injection Setup](#dependency-injection-setup)
- [Single-Tenant vs Multi-Tenant Initialization](#single-tenant-vs-multi-tenant-initialization)
- [Database Initialization](#database-initialization)
- [Background Services](#background-services)
- [Configuration Loading](#configuration-loading)
- [Middleware Pipeline](#middleware-pipeline)
- [Troubleshooting Startup Issues](#troubleshooting-startup-issues)

## Startup Overview

The SkyCMS startup process follows ASP.NET Core conventions:

1. **WebApplicationBuilder Creation** - Configure services
2. **Service Registration** - Register DI services
3. **Application Building** - Create WebApplication
4. **Middleware Configuration** - Setup request pipeline
5. **Application Start** - Run app.Run()

### Key Configuration Points

| Point | Responsibility |
|-------|-----------------|
| **Startup** | Entry point (Program.cs) |
| **Service Registration** | Dependency Injection setup |
| **Configuration** | Load app settings, env vars |
| **Database Setup** | Initialize DbContexts, migrations |
| **Middleware** | Request pipeline setup |
| **Background Jobs** | Hangfire scheduling, cron tasks |

## Startup Sequence

### Phase 1: Builder Initialization (Pre-Services)

```csharp
var builder = WebApplication.CreateBuilder(args);
```

**Occurs:**
- Configuration files loaded (appsettings.json, user secrets, env vars)
- Logging infrastructure initialized
- Default services registered

### Phase 2: Setup Wizard Services

```csharp
builder.Services.AddScoped<ISetupService, SetupService>();
```

**Purpose:**
- Register setup wizard service
- Setup state now persists to the main ApplicationDbContext using the Settings table
- Setup configuration serialized as JSON in Settings (group="SYSTEM", name="SETUP_WIZARD_STATE")
- This approach ensures setup state is shared across multiple instances for scalable deployments
- Temporary SQLite database for setup state

### Phase 3: Base Services Registration

Common services registered for both single-tenant and multi-tenant modes:

```csharp
builder.Services.AddMemoryCache();
var defaultAzureCredential = new DefaultAzureCredential();
builder.Services.AddSingleton(defaultAzureCredential);
builder.Services.AddApplicationInsightsTelemetry();

// SiteSettings configuration
builder.Services.Configure<SiteSettings>(settings => { ... });

// OAuth providers (if configured)
if (googleOAuth?.IsConfigured()) 
    builder.Services.AddAuthentication().AddGoogle(...);
if (entraIdOAuth?.IsConfigured())
    builder.Services.AddAuthentication().AddMicrosoftAccount(...);
```

**Services Registered:**
- Memory cache
- Azure credential provider (singleton)
- Application Insights telemetry
- Site settings configuration
- OAuth providers (conditional)

### Phase 4: Single-Tenant or Multi-Tenant Configuration

Branching logic based on configuration:

```csharp
var isMultiTenantEditor = builder.Configuration.GetValue<bool?>("MultiTenantEditor") ?? false;

if (isMultiTenantEditor)
{
    Console.WriteLine($"Starting Cosmos CMS Editor in Multi-Tenant Mode");
    MultiTenant.Configure(builder, defaultAzureCredential);
}
else
{
    Console.WriteLine($"Starting Cosmos CMS Editor in Single-Tenant Mode");
    SingleTenant.Configure(builder);
}
```

**Single-Tenant Configuration** (`SingleTenant.Configure`):
- Registers application DbContext with main database
- Uses environment variables for database connection
- Single database for all operations

**Multi-Tenant Configuration** (`MultiTenant.Configure`):
- Registers dynamic configuration provider
- Registers central config database
- Sets up tenant context for request scoping
- Registers connection string resolution logic

### Phase 5: Transient and Scoped Services

Services for content, publishing, and features:

```csharp
// Transient services (new instance per request)
builder.Services.AddTransient<ICdnServiceFactory, CdnServiceFactory>();
builder.Services.AddTransient<ITemplateService, TemplateService>();
builder.Services.AddTransient<IPublishingService, PublishingService>();
// ... more services

// Scoped services (one per request)
builder.Services.AddScoped<ILayoutImportService, LayoutImportService>();
builder.Services.AddScoped<IMediator, Mediator>();
```

### Phase 6: Hangfire Background Job Scheduling

```csharp
builder.Services.AddHangFireScheduling(builder.Configuration);
builder.Logging.AddFilter("Hangfire", LogLevel.Debug);
```

**Purpose:**
- Register Hangfire for scheduled tasks
- Setup job persistence database
- Enable logging for job execution

### Phase 7: Identity and Authentication

```csharp
builder.Services.AddCosmosIdentity<ApplicationDbContext, IdentityUser, IdentityRole, string>(
    options => options.SignIn.RequireConfirmedAccount = true)
    .AddDefaultUI()
    .AddDefaultTokenProviders();

// Session configuration
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(3600);
    options.Cookie.IsEssential = true;
});

// Configure cookie options (including multi-tenant specific handling)
builder.Services.ConfigureApplicationCookie(options => { ... });
```

**Key Setup:**
- Identity DbContext configuration
- Token providers (email confirmation, password reset)
- Session state
- Authentication cookie configuration
- Multi-tenant cookie domain handling (if enabled)

### Phase 8: Razor Pages and MVC

```csharp
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddAreaPageRoute("Setup", "/Index", "___setup");
    options.Conventions.AddAreaPageRoute("Setup", "/Step1_Mode", "___setup/mode");
    // ... more route mappings
});

builder.Services.AddMvc()
    .AddNewtonsoftJson(options => ...)
    .AddRazorPagesOptions(options => ...);
```

**Configuration:**
- Setup wizard routes mapped
- Identity pages authorized
- JSON serialization settings
- Razor page options

### Phase 9: CORS, HSTS, and Security

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllCors", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod());
});

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.Configure<ForwardedHeadersOptions>(options => { ... });
```

### Phase 10: Application Building

```csharp
var app = builder.Build();
```

**What happens:**
- Service provider created
- All registered services now available
- Middleware pipeline ready to be configured

### Phase 11: Middleware Pipeline Configuration

```csharp
// Multi-tenant middleware (if enabled)
if (isMultiTenantEditor)
    app.UseMiddleware<DomainMiddleware>();

// Data protection
app.UseCosmosCmsDataProtection();

// Forwarded headers (for proxies)
app.UseForwardedHeaders();

// Setup redirect detection
app.Use(async (context, next) => { ... });
app.UseMiddleware<SetupRedirectMiddleware>();

// Error handling
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/Home/Error");

// Static files
app.UseStaticFiles();

// Routing and authentication
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
```

### Phase 12: Application Running

```csharp
app.MapControllers();
app.MapRazorPages();
app.MapHub<LiveEditorHub>("/hubs/live-editor");
app.Run();
```

**Application is now:**
- Listening for HTTP requests
- Ready to process requests through middleware pipeline
- Ready to serve pages and API endpoints

## Dependency Injection Setup

### Service Lifetimes

| Lifetime | Behavior | Use Case |
|----------|----------|----------|
| **Singleton** | Created once, shared across all requests | Configuration, factories, shared state |
| **Scoped** | Created once per request | DbContext, request-specific services |
| **Transient** | Created every time | Stateless utilities, logic services |

### Key Singletons

```csharp
builder.Services.AddSingleton(defaultAzureCredential);
builder.Services.AddSingleton(microsoftAuth);
```

**Note:** DefaultAzureCredential is created once at startup and reused for all Azure authentication.

### Key Scoped Services

```csharp
builder.Services.AddScoped<ApplicationDbContext>(...);
builder.Services.AddScoped<IMediator, Mediator>();
builder.Services.AddScoped<ILayoutImportService, LayoutImportService>();
```

**ApplicationDbContext** is scoped so each request gets a fresh database connection.

### Service Resolution Order

Services are resolved in the order they're registered. If Service A depends on Service B:

1. Container checks if Service B is registered
2. Resolves all dependencies of Service B recursively
3. Creates instance of Service B
4. Creates instance of Service A with Service B injected

## Single-Tenant vs Multi-Tenant Initialization

### Single-Tenant Initialization

```csharp
// SingleTenant.Configure(builder)
// 1. Registers ApplicationDbContext with direct connection string
// 2. Uses CONNECTIONSTRINGS_COSMOS environment variable
// 3. All requests use same database
```

**Startup message:**
```
Starting Cosmos CMS Editor in Single-Tenant Mode (v.2.x.x)
```

### Multi-Tenant Initialization

```csharp
// MultiTenant.Configure(builder, credential)
// 1. Registers central configuration database
// 2. Registers DynamicConfigurationProvider
// 3. Registers TenantContext for request scoping
// 4. Middleware extracts domain from request
// 5. Database context uses domain-specific connection
```

**Startup message:**
```
Starting Cosmos CMS Editor in Multi-Tenant Mode (v.2.x.x)
```

**Key Difference:** Multi-tenant resolves connection strings at runtime based on request domain.

## Database Initialization

### Automatic Migration

Entity Framework Core applies pending migrations on startup (if configured):

```csharp
// In SingleTenant.Configure or MultiTenant.Configure
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate(); // Applies pending migrations
}
```

### First-Run Initialization

On first run, migrations create:
- Identity tables (Users, Roles, Claims)
- Article/Page tables
- Template tables
- Publishing tables

### Seeding Data

If needed, seeding occurs after migrations:

```csharp
// Example: Ensure required roles exist
await SetupNewAdministrator.Ensure_Roles_Exists(roleManager);
```

## Background Services

### Hangfire Job Scheduling

Hangfire is configured during startup:

1. **Hangfire Database** initialized with job tables
2. **Job Processors** registered to execute queued jobs
3. **Recurring Jobs** scheduled based on configuration

**Startup logging:**
```
Hangfire dashboard available at /Editor/CCMS___PageScheduler
```

### Cron Tasks

Scheduled tasks run at specified intervals:
- Page publication scheduler
- Backup jobs
- Maintenance tasks

## Configuration Loading

### Configuration Sources (In Order)

1. **appsettings.json** - Default configuration
2. **appsettings.{Environment}.json** - Environment-specific overrides
3. **User secrets** (Development only) - Developer's local secrets
4. **Environment variables** - Runtime overrides
5. **Command-line arguments** - CLI overrides

### Configuration Access in Startup

```csharp
// Access configuration during startup
var isMultiTenant = builder.Configuration.GetValue<bool?>("MultiTenantEditor") ?? false;
var connectionString = builder.Configuration.GetConnectionString("cosmos");
```

### Configuration at Runtime

```csharp
// Access configuration in running app
var config = context.RequestServices.GetRequiredService<IConfiguration>();
var allowSetup = config.GetValue<bool?>("CosmosAllowSetup") ?? false;
```

## Middleware Pipeline

### Middleware Execution Order

Middleware executes in the order registered. **Order matters!**

```
Request comes in
    ↓
[1] Forwarded Headers (proxy detection)
    ↓
[2] Domain Middleware (multi-tenant only - extract domain)
    ↓
[3] Data Protection
    ↓
[4] Setup Check (redirect to setup if needed)
    ↓
[5] Exception Handling
    ↓
[6] Static Files
    ↓
[7] Routing
    ↓
[8] Authentication
    ↓
[9] Authorization
    ↓
[10] Endpoints (controller action)
    ↓
Response sent back
```

### Critical Middleware Notes

- **Forwarded Headers** must come early (proxy detection)
- **Domain Middleware** (multi-tenant) must come before DbContext usage
- **Authentication** must come before **Authorization**
- **Static Files** should come after **Routing** (or you need separate route)

## Troubleshooting Startup Issues

### Application Won't Start

**Symptoms:** Application crashes immediately on startup

**Debug steps:**
1. Check console output for error messages
2. Look for exception stack trace in logs
3. Check appsettings.json for syntax errors
4. Verify database connection string

**Common causes:**
- Invalid connection string
- Database unreachable
- Corrupted configuration file
- Missing environment variables

### Setup Wizard Not Available

**Symptoms:** Accessing `/___setup` returns 404 or redirects

**Causes:**
- `CosmosAllowSetup` environment variable is false
- `SetupRedirectMiddleware` is redirecting
- Running in multi-tenant mode (setup wizard not supported)

**Fix:**
```bash
export CosmosAllowSetup=true
```

### Database Connection Timeout

**Symptoms:** Application hangs during startup

**Causes:**
- Database server unreachable
- Firewall blocking connection
- Incorrect connection string

**Fix:**
1. Test connection string independently
2. Verify network access to database
3. Check firewall rules

### Services Not Resolving

**Symptoms:** "No service for type X has been registered"

**Causes:**
- Service registration missing in startup
- Incorrect service lifetime
- Circular dependency

**Fix:**
1. Check service is registered in SingleTenant/MultiTenant.Configure
2. Verify lifetimes are correct
3. Review dependency graph

### Migrations Not Applied

**Symptoms:** Schema mismatch errors at runtime

**Causes:**
- Migrations not run at startup
- Database in read-only mode
- Entity Framework error

**Fix:**
1. Check for migration errors in logs
2. Manually run migrations
3. Verify database permissions

### Multi-Tenant Not Routing

**Symptoms:** All requests return 404 for invalid domains

**Causes:**
- Domain not in Connections table
- DomainMiddleware not registered
- TenantContext not set properly

**Fix:**
1. Verify Connection record exists for domain
2. Check DomainMiddleware is registered
3. Review domain validation logic

## Related Documentation

- [Middleware Pipeline](./Middleware-Pipeline.md) - Detailed middleware documentation
- [Configuration Overview](../Configuration/Database-Configuration-Reference.md) - Configuration reference
- [Multi-Tenant Configuration](../Configuration/Multi-Tenant-Configuration.md) - Multi-tenant setup
- [Post-Installation](../Installation/Post-Installation.md) - After startup verification

## Code References

> **Note**: The following source code files are located in the SkyCMS project repository, not in the published documentation.

- **Main Startup**: `Editor/Program.cs` (495 lines)
- **Single-Tenant Config**: `Editor/Boot/SingleTenant.cs`
- **Multi-Tenant Config**: `Editor/Boot/MultiTenant.cs`
- **Setup Service**: `Editor/Services/Setup/SetupService.cs`
- **Domain Middleware**: `Cosmos.ConnectionStrings/DomainMiddleware.cs`
