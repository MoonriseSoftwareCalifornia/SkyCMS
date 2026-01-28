# Cosmos.ConnectionStrings — Developer README

Purpose
- Contains helpers for loading and managing Cosmos DB connection strings and configuration providers.

Quick start
```powershell
dotnet build SkyCMS.sln
```

Where to look
- Providers and configuration classes: `Cosmos.ConnectionStrings/*`.
- Single-tenant vs multi-tenant configuration implementations.

Notes
- Changing connection string handling affects all Cosmos consumers; coordinate changes and add tests.
# Cosmos.DynamicConfig - Multi-Tenant Configuration Provider

> **Note:** This project is located in the `Cosmos.ConnectionStrings/` folder but the project file is `Cosmos.DynamicConfig.csproj`. Both names refer to the same package - the namespace and assembly name is `Cosmos.DynamicConfig`.

## Overview

Cosmos.DynamicConfig is a configuration management system for multi-tenant applications. It provides dynamic, domain-based configuration and connection string resolution so a single application instance can serve multiple tenants with different database and storage settings based on the incoming request's domain name.

## Features

### Multi-Tenant Support

- **Domain-Based Routing**: Automatically resolves configuration based on request domain
- **Dynamic Connection Strings**: Per-tenant database and storage connection strings
- **Runtime Configuration**: Configuration values resolved at runtime without application restart
- **Tenant Isolation**: Complete separation of tenant data and resources

### Configuration Management

- **Database Connections**: Per-tenant database connection string management
- **Storage Connections**: Per-tenant blob storage connection string management
- **Custom Configuration**: Key-value configuration pairs per tenant
- **Memory Caching**: Efficient caching of configuration data for performance (10-second TTL)

### Advanced Features

- **Domain Validation**: Validate domain names against configured tenants via `ValidateDomainName()`
- **Metrics Collection**: Built-in metrics tracking for tenant resource usage
- **Entity Framework Integration**: Works with Cosmos DB, SQL Server, and MySQL via FlexDb provider detection
- **HTTP Context Integration**: Seamless integration with ASP.NET Core pipeline

## Architecture

### Core Components

#### IDynamicConfigurationProvider Interface

Defines the contract for dynamic configuration services, providing methods for retrieving connection strings and configuration values based on domain context.

**Properties:**
- `IsMultiTenantConfigured`: Returns `true` if any tenant connection is configured

**Methods:**
- `GetDatabaseConnectionString(domainName)`: Retrieves tenant database connection
- `GetStorageConnectionString(domainName)`: Retrieves tenant storage connection
- `GetConfigurationValue(key)`: Gets standard configuration values
- `GetConnectionStringByName(name)`: Gets named connection strings

#### DynamicConfigurationProvider

The main implementation that handles configuration resolution, caching, and database interactions for multi-tenant scenarios.

**Key Features:**
- Requires `IHttpContextAccessor` - throws `ArgumentNullException` if not available
- Validates `ConfigDbConnectionString` on construction
- Caches tenant connections for 10 seconds using `IMemoryCache`
- Uses FlexDb for automatic database provider detection

**Important Notes:**
- The constructor **requires** an active HTTP context. If your code runs outside an HTTP request (e.g., background services), you **must** pass the domain explicitly to the API methods.
- If `HttpContext` is null during construction, an `ArgumentNullException` is thrown.
- Configuration database connection string is mandatory and validated on startup.

#### DynamicConfigDbContext

Entity Framework DbContext for managing configuration data, connections, and metrics. The backing database is determined by your `ConfigDbConnectionString` (Cosmos DB, SQL Server, or MySQL).

**Containers/Tables:**
- `Connections`: Tenant configuration entities (container: "config")
- `Metrics`: Usage metrics entities (container: "Metrics")

#### Connection Entity

Represents a tenant configuration with domain mappings, connection strings, and metadata.

**Properties:**
- `Id`: Unique identifier (Guid)
- `DomainNames`: Array of domain names for tenant routing
- `DbConn`: Tenant database connection string
- `StorageConn`: Tenant storage connection string
- `Customer`: Owner/tenant name
- `ResourceGroup`: Azure resource group
- `PublisherMode`: Publishing mode (Static, Decoupled, Headless, Hybrid, Static-dynamic)
- `WebsiteUrl`: Primary website URL
- `OwnerEmail`: Owner email address

#### Metric Entity

Tracks resource usage per tenant.

**Properties:**
- `Id`: Unique identifier (Guid)
- `ConnectionId`: Associated tenant connection ID
- `TimeStamp`: Metric timestamp (DateTimeOffset)
- `BlobStorageBytes`: Total blob storage usage (double?)
- `BlobStorageEgressBytes`: Outbound bandwidth (double?)
- `BlobStorageIngressBytes`: Inbound bandwidth (double?)
- `BlobStorageTransactions`: Storage transaction count (double?)
- `DatabaseDataUsageBytes`: Database data usage (double?)
- `DatabaseIndexUsageBytes`: Database index usage (double?)
- `DatabaseRuUsage`: Request Units consumed (double?)
- `FrontDoorRequestBytes`: Front Door request bytes (long?)
- `FrontDoorResponseBytes`: Front Door response bytes (long?)

#### DomainMiddleware

ASP.NET Core middleware that captures the current request's domain and stores it in `HttpContext.Items["Domain"]` for downstream use.

## Installation

This package is part of the SkyCMS solution. You can obtain it by cloning the [SkyCMS GitHub repository](https://github.com/CWALabs/SkyCMS).

## Configuration

### Database Setup

Configure the main configuration database connection string (required key: `ConnectionStrings:ConfigDbConnectionString`).

```json
{
  "ConnectionStrings": {
    "ConfigDbConnectionString": "AccountEndpoint=https://your-cosmos-account.documents.azure.com:443/;AccountKey=your-key;Database=your-config-db"
  }
}
```

Alternative examples:

SQL Server:

```json
{
    "ConnectionStrings": {
        "ConfigDbConnectionString": "Server=tcp:your-sql.database.windows.net,1433;Initial Catalog=YourConfigDb;User ID=youruser;Password=yourpassword;"
    }
}
```

MySQL:

```json
{
    "ConnectionStrings": {
        "ConfigDbConnectionString": "Server=your-mysql;Port=3306;uid=youruser;pwd=yourpassword;database=YourConfigDb;"
    }
}
```

### Multi-Tenant Configuration

Each tenant is configured with a Connection entity that includes:

- **Domain Names**: Array of domain names for the tenant
- **Database Connection**: Tenant-specific database connection string
- **Storage Connection**: Tenant-specific storage connection string
- **Publisher Mode**: Website publishing mode (Static, Decoupled, Headless, etc.)
- **Website URL**: Primary website URL
- **Customer Information**: Owner name, email, and resource group

## Usage

### Service Registration

In your `Program.cs` or `Startup.cs`:

```csharp
using Cosmos.DynamicConfig;

// Register HTTP context accessor
builder.Services.AddHttpContextAccessor();

// Register memory cache
builder.Services.AddMemoryCache();

// Register dynamic configuration provider
builder.Services.AddScoped<IDynamicConfigurationProvider, DynamicConfigurationProvider>();

// Add domain middleware to capture request domain
app.UseMiddleware<DomainMiddleware>();

Note: The provider requires an active HTTP request context to infer the tenant domain. For background services or out-of-band operations, pass the domain explicitly to the API methods (see below).
```

### Basic Usage

```csharp
using Cosmos.DynamicConfig;

public class TenantService
{
    private readonly IDynamicConfigurationProvider _configProvider;

    public TenantService(IDynamicConfigurationProvider configProvider)
    {
        _configProvider = configProvider;
    }

    public async Task<string> GetTenantData()
    {
        // Get current tenant's database connection
        var dbConnection = _configProvider.GetDatabaseConnectionString();
        
        // Get current tenant's storage connection
        var storageConnection = _configProvider.GetStorageConnectionString();
        
        // Get custom configuration value
        var customSetting = _configProvider.GetConfigurationValue("CustomSetting");
        
        return $"DB: {dbConnection}, Storage: {storageConnection}";
    }
}
```

Explicit domain usage (no HTTP context required):

```csharp
var dbForFoo = _configProvider.GetDatabaseConnectionString("www.foo.com");
var storageForFoo = _configProvider.GetStorageConnectionString("www.foo.com");
```

### Explicit Domain Configuration Access

For scenarios where HTTP context is unavailable (background jobs, console apps, unit tests), explicitly pass the domain parameter:

**Background Service Example:**

```csharp
public class TenantBackgroundService : BackgroundService
{
    private readonly IDynamicConfigurationProvider _configProvider;
    private readonly ILogger<TenantBackgroundService> _logger;
    
    public TenantBackgroundService(
        IDynamicConfigurationProvider configProvider,
        ILogger<TenantBackgroundService> logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tenantDomains = new[] { "tenant1.example.com", "tenant2.example.com" };
        
        foreach (var domain in tenantDomains)
        {
            // Pass domain explicitly - no HTTP context needed
            var dbConnection = _configProvider.GetDatabaseConnectionString(domain);
            var storageConnection = _configProvider.GetStorageConnectionString(domain);
            
            _logger.LogInformation("Processing tenant: {Domain}", domain);
            
            // Process tenant data...
            await ProcessTenantDataAsync(domain, dbConnection, storageConnection);
        }
    }
}
```

**Console Application Example:**

```csharp
class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        
        // Configure services without HTTP context
        services.AddDbContext<DynamicConfigDbContext>(options =>
            options.UseCosmos(configConnectionString, "ConfigDb"));
        
        services.AddMemoryCache();
        services.AddSingleton<IDynamicConfigurationProvider, DynamicConfigurationProvider>();
        
        var provider = services.BuildServiceProvider();
        var configProvider = provider.GetRequiredService<IDynamicConfigurationProvider>();
        
        // Explicitly specify domain
        var domain = "client.example.com";
        var dbConn = configProvider.GetDatabaseConnectionString(domain);
        
        Console.WriteLine($"Database connection for {domain}: {dbConn}");
    }
}
```

**Unit Test Example:**

```csharp
[TestMethod]
public void GetDatabaseConnectionString_WithExplicitDomain_ReturnsCorrectConnection()
{
    // Arrange
    var mockCache = new MemoryCache(new MemoryCacheOptions());
    var configProvider = new DynamicConfigurationProvider(
        configuration: mockConfig,
        httpContextAccessor: null,  // No HTTP context
        memoryCache: mockCache,
        logger: mockLogger);
    
    // Act - Pass domain explicitly
    var result = configProvider.GetDatabaseConnectionString("test.example.com");
    
    // Assert
    Assert.IsNotNull(result);
    Assert.IsTrue(result.Contains("Database=TestDb"));
}
```

**API Methods Supporting Explicit Domain:**

```csharp
// All methods accept optional domain parameter
string GetDatabaseConnectionString(string? domain = null);
string GetStorageConnectionString(string? domain = null);
string GetConfigurationValue(string key, string? domain = null);
Task<bool> ValidateDomainName(string domain);
Task<Connection?> GetConnectionByDomainAsync(string domain);
```

**When to Use Explicit Domain:**

- Background services / hosted services
- Console applications
- Unit tests
- Scheduled jobs (Hangfire, Quartz)
- Azure Functions (when not triggered by HTTP)
- Regular HTTP requests (use middleware - automatic)

### Metrics Collection

The Metrics entity tracks resource consumption per tenant for billing, monitoring, and capacity planning.

**Recording Metrics:**

```csharp
public class MetricsService
{
    private readonly DynamicConfigDbContext _dbContext;
    
    public async Task RecordTenantMetricsAsync(
        Guid connectionId,
        double blobStorageBytes,
        double blobTransactions,
        double databaseRUs)
    {
        var metric = new Metric
        {
            Id = Guid.NewGuid(),
            ConnectionId = connectionId,
            TimeStamp = DateTimeOffset.UtcNow,
            BlobStorageBytes = blobStorageBytes,
            BlobStorageTransactions = blobTransactions,
            DatabaseRequestUnits = databaseRUs
        };
        
        _dbContext.Metrics.Add(metric);
        await _dbContext.SaveChangesAsync();
    }
}
```

**Querying Metrics:**

```csharp
public async Task<double> GetMonthlyStorageUsageAsync(Guid connectionId)
{
    var startOfMonth = new DateTimeOffset(
        DateTime.UtcNow.Year,
        DateTime.UtcNow.Month,
        1, 0, 0, 0, TimeSpan.Zero);
    
    var totalBytes = await _dbContext.Metrics
        .Where(m => m.ConnectionId == connectionId && m.TimeStamp >= startOfMonth)
        .SumAsync(m => m.BlobStorageBytes ?? 0);
    
    return totalBytes;
}
```

**Automated Metrics Collection:**

```csharp
public class MetricsCollectorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Collect metrics for all tenants every hour
            await CollectAllTenantMetricsAsync();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
    
    private async Task CollectAllTenantMetricsAsync()
    {
        var connections = await _dbContext.Connections.ToListAsync();
        
        foreach (var connection in connections)
        {
            // Query Azure/AWS APIs for actual usage
            var metrics = await FetchTenantResourceMetricsAsync(connection);
            await RecordTenantMetricsAsync(connection.Id, metrics);
        }
    }
}
```

### Domain-Specific Configuration

```csharp
public class ConfigurationController : ControllerBase
{
    private readonly IDynamicConfigurationProvider _configProvider;

    public ConfigurationController(IDynamicConfigurationProvider configProvider)
    {
        _configProvider = configProvider;
    }

    [HttpGet("tenant-info")]
    public async Task<IActionResult> GetTenantInfo()
    {
        // Check if multi-tenant is configured (any tenant connection present)
        if (!_configProvider.IsMultiTenantConfigured)
        {
            return BadRequest("Multi-tenant not configured");
        }

        // Get configuration for current domain
        var dbConnection = _configProvider.GetDatabaseConnectionString();
        var storageConnection = _configProvider.GetStorageConnectionString();

        return Ok(new
        {
            DatabaseConfigured = !string.IsNullOrEmpty(dbConnection),
            StorageConfigured = !string.IsNullOrEmpty(storageConnection)
        });
    }

    [HttpGet("validate-domain/{domain}")]
    public async Task<IActionResult> ValidateDomain(string domain)
    {
        var isValid = await _configProvider.ValidateDomainName(domain);
        return Ok(new { IsValid = isValid });
    }
}
```

### Connection Management

```csharp
public class TenantManagementService
{
    private readonly IDynamicConfigurationProvider _configProvider;

    public TenantManagementService(IDynamicConfigurationProvider configProvider)
    {
        _configProvider = configProvider;
    }

    public async Task<Connection> CreateTenant(TenantRequest request)
    {
        var connection = new Connection
        {
            DomainNames = request.Domains,
            DbConn = request.DatabaseConnectionString,
            StorageConn = request.StorageConnectionString,
            Customer = request.CustomerName,
            ResourceGroup = request.ResourceGroup,
            PublisherMode = request.PublisherMode,
            WebsiteUrl = request.WebsiteUrl,
            OwnerEmail = request.OwnerEmail
        };

        // Save to configuration database
        using var context = new DynamicConfigDbContext(options);
        context.Connections.Add(connection);
        await context.SaveChangesAsync();

        return connection;
    }
}
```

### Metrics Integration

```csharp
public class MetricsService
{
    private readonly DynamicConfigDbContext _context;

    public MetricsService(DynamicConfigDbContext context)
    {
        _context = context;
    }

    public async Task RecordUsageMetrics(Guid connectionId, UsageData usage)
    {
        var metric = new Metric
        {
            ConnectionId = connectionId,
            TimeStamp = DateTimeOffset.UtcNow,
            BlobStorageBytes = usage.StorageBytes,
            BlobStorageEgressBytes = usage.EgressBytes,
            BlobStorageIngressBytes = usage.IngressBytes,
            BlobStorageTransactions = usage.Transactions,
            DatabaseDataUsageBytes = usage.DatabaseBytes,
            DatabaseRuUsage = usage.RequestUnits,
            FrontDoorRequestBytes = usage.RequestBytes,
            FrontDoorResponseBytes = usage.ResponseBytes
        };

        _context.Metrics.Add(metric);
        await _context.SaveChangesAsync();
    }
}
```

## API Reference

### IDynamicConfigurationProvider Methods

| Method | Description | Parameters |
|--------|-------------|------------|
| `GetDatabaseConnectionString` | Get tenant database connection | `domainName: string` (optional) |
| `GetStorageConnectionString` | Get tenant storage connection | `domainName: string` (optional) |
| `GetConfigurationValue` | Get configuration value by key | `key: string` |
| `GetConnectionStringByName` | Get connection string by name | `name: string` |
| `ValidateDomainName` | Validate if domain is configured | `domainName: string` |

### Connection Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Unique connection identifier |
| `DomainNames` | `string[]` | Array of associated domain names |
| `DbConn` | `string` | Database connection string |
| `StorageConn` | `string` | Storage connection string |
| `Customer` | `string` | Customer/tenant name |
| `ResourceGroup` | `string` | Azure resource group |
| `PublisherMode` | `string` | Publishing mode |
| `WebsiteUrl` | `string` | Primary website URL |
| `OwnerEmail` | `string` | Owner email address |

### Metric Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Unique metric identifier |
| `ConnectionId` | `Guid` | Associated connection ID |
| `TimeStamp` | `DateTimeOffset` | Metric timestamp |
| `BlobStorageBytes` | `double?` | Blob storage usage in bytes |
| `BlobStorageEgressBytes` | `double?` | Outbound bandwidth usage |
| `BlobStorageIngressBytes` | `double?` | Inbound bandwidth usage |
| `BlobStorageTransactions` | `double?` | Storage transaction count |
| `DatabaseDataUsageBytes` | `double?` | Database data usage |
| `DatabaseIndexUsageBytes` | `double?` | Database index usage |
| `DatabaseRuUsage` | `double?` | Request Units consumed |

## Publisher Modes

The system supports various publishing modes for different tenant requirements:

- **Static**: Static website hosting
- **Decoupled**: Decoupled CMS architecture
- **Headless**: Headless CMS mode
- **Hybrid**: Hybrid static/dynamic content
- **Static-dynamic**: Mixed static and dynamic content

## Dependencies

### Core Dependencies

- **.NET 9.0**: Modern .NET framework
- **Microsoft.EntityFrameworkCore**: Entity Framework Core
- **Microsoft.EntityFrameworkCore.Cosmos**: Cosmos DB provider
- **Microsoft.AspNetCore.Http.Abstractions**: HTTP abstractions
- **Microsoft.Extensions.Configuration**: Configuration management
- **Microsoft.Extensions.Caching.Memory**: Memory caching

### Project References

- **AspNetCore.Identity.FlexDb**: Flexible identity provider integration

## Multi-Tenant Architecture Benefits

1. **Resource Isolation**: Complete separation of tenant data and resources
2. **Scalability**: Support for unlimited number of tenants
3. **Cost Efficiency**: Shared application infrastructure with isolated data
4. **Customization**: Per-tenant configuration and feature flags
5. **Security**: Tenant isolation and secure configuration management

## Performance Considerations

- **Memory Caching**: Configuration data is cached for 10 seconds to reduce database calls
- **Efficient Queries**: Optimized Entity Framework queries for configuration lookup
- **Connection Pooling**: Efficient database connection management
- **Lazy Loading**: Configuration loaded only when needed

## Security Features

- **Domain Validation**: Prevents unauthorized domain access
- **Connection String Security**: Secure storage of sensitive connection information
- **Tenant Isolation**: Complete separation of tenant configurations
- **HTTP Context Integration**: Secure domain resolution from request context

## Monitoring and Metrics

The system includes comprehensive metrics collection for:

- **Storage Usage**: Blob storage bytes, ingress/egress bandwidth
- **Database Usage**: Data and index usage, Request Units consumption
- **Transaction Tracking**: Storage and database transaction counts
- **Network Traffic**: Front Door request/response bytes

## License

Licensed under the GNU Public License, Version 3.0. See the LICENSE file for details.

## Contributing

This project is part of the SkyCMS ecosystem. For contribution guidelines and more information, visit the [SkyCMS GitHub repository](https://github.com/CWALabs/SkyCMS).

## Troubleshooting

- HTTP context is not available
    - Ensure `AddHttpContextAccessor()` is registered and the code runs within an HTTP request. For background jobs, pass a domain string to the API.
- Missing `ConfigDbConnectionString`
    - The provider requires `ConnectionStrings:ConfigDbConnectionString`. Add it to configuration or environment variables.
- Domain not found
    - `ValidateDomainName` returns false if the domain is not configured. Add the domain to a `Connection` entity in the configuration database.
- Stale configuration
    - Results are cached for 10 seconds. Wait for cache expiry or adjust code to refresh if you just updated tenant settings.

## See Also

### Related Documentation
- [Editor Documentation](../Editor/README.md) - Content management interface using dynamic configuration
- [Publisher Documentation](../Publisher/README.md) - Public website using multi-tenant support
- [Database Configuration Guide](https://cwalabs.github.io/SkyCMS/DatabaseConfig.html) - Database provider setup instructions
- [Storage Configuration Guide](https://cwalabs.github.io/SkyCMS/StorageConfig.html) - Storage provider setup instructions
- [Azure Installation Guide](https://cwalabs.github.io/SkyCMS/AzureInstall.html) - Azure deployment and configuration

### Configuration Files
- [appsettings.json](../launchSettings.json) - Application configuration examples
- [docker-compose.yml](../docker-compose.yml) - Docker container configuration

