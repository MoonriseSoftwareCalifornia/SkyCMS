# Cosmos.DynamicConfig - Multi-Tenant Dynamic Configuration Provider

## Overview

Cosmos.DynamicConfig is a sophisticated configuration management system designed for multi-tenant applications. It provides dynamic, domain-based configuration and connection string resolution, allowing a single application instance to serve multiple tenants with different database and storage configurations based on the incoming request's domain name.

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
- **Memory Caching**: Efficient caching of configuration data for performance

### Advanced Features

- **Domain Validation**: Validate domain names against configured tenants
- **Metrics Collection**: Built-in metrics tracking for tenant resource usage
- **Entity Framework Integration**: Cosmos DB Entity Framework support
- **HTTP Context Integration**: Seamless integration with ASP.NET Core pipeline

## Architecture

### Core Components

#### IDynamicConfigurationProvider Interface

Defines the contract for dynamic configuration services, providing methods for retrieving connection strings and configuration values based on domain context.

#### DynamicConfigurationProvider

The main implementation that handles configuration resolution, caching, and database interactions for multi-tenant scenarios.

#### DynamicConfigDbContext

Entity Framework DbContext for managing configuration data, connections, and metrics in Cosmos DB.

#### Connection Entity

Represents a tenant configuration with domain mappings, connection strings, and metadata.

#### DomainMiddleware

ASP.NET Core middleware that captures and stores the current request's domain for configuration resolution.

## Installation

This package is part of the SkyCMS solution. You can obtain it by cloning the [SkyCMS GitHub repository](https://github.com/MoonriseSoftwareCalifornia/CosmosCMS).

## Configuration

### Database Setup

Configure the main configuration database connection string:

```json
{
  "ConnectionStrings": {
    "ConfigDbConnectionString": "AccountEndpoint=https://your-cosmos-account.documents.azure.com:443/;AccountKey=your-key;Database=your-config-db"
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
        // Check if multi-tenant is configured
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

This project is part of the SkyCMS ecosystem. For contribution guidelines and more information, visit the [SkyCMS GitHub repository](https://github.com/MoonriseSoftwareCalifornia/CosmosCMS).
