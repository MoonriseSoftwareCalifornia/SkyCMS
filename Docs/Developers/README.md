---
title: Developer Documentation
description: Architecture, shared components, and extension points for SkyCMS development
keywords: developers, architecture, components, extension-points, API
audience: [developers, architects]
---

# SkyCMS Developer Documentation

Welcome to the developer documentation area. This section complements the feature guides under `Docs/` by describing the architecture, shared components, and extension points used across SkyCMS projects.

## Overview

SkyCMS is a cloud-native content management system built on .NET 9.0 with a modular architecture that separates concerns across multiple projects. The system supports multiple database providers (Cosmos DB, SQL Server, MySQL) and multiple storage providers (Azure Blob Storage, Amazon S3, Cloudflare R2).

## Architecture

### Core Projects

- **Sky.Editor** (`Editor/`): Content authoring and administration application
- **Sky.Publisher** (`Publisher/`): Public-facing website renderer and content delivery
- **Cosmos.Common** (`Common/`): Shared library with business logic, data models, and base controllers
- **Cosmos.BlobService** (`Cosmos.BlobService/`): Multi-cloud storage abstraction layer
- **Cosmos.DynamicConfig** (`Cosmos.ConnectionStrings/`): Multi-tenant dynamic configuration management
- **AspNetCore.Identity.FlexDb** (`AspNetCore.Identity.FlexDb/`): Flexible multi-database identity provider

### Technology Stack

- **Runtime**: .NET 9.0, ASP.NET Core
- **Data Access**: Entity Framework Core 9.0 with multi-provider support
- **Authentication**: ASP.NET Core Identity with external providers (Google, Microsoft)
- **Frontend**: Bootstrap 5, CKEditor 5, GrapesJS, Monaco Editor, FilePond
- **Storage**: Azure Blob Storage, AWS S3, Cloudflare R2 (via abstraction layer)
- **Databases**: Azure Cosmos DB, SQL Server, MySQL

## Sections

- **Controllers**
  - [HomeControllerBase](Controllers/HomeControllerBase.md): Shared base controller used by Publisher and Editor
  - [PubControllerBase](Controllers/PubControllerBase.md): Secure file access controller for Publisher

- **Editor Widgets**
  - [Image Widget](ImageWidget.md): Per‑widget attributes (including `data-ccms-enable-alt-editor`) and developer hooks

## Key Concepts

### Multi-Database Support

SkyCMS automatically detects and configures the appropriate database provider based on connection string patterns:

- **Cosmos DB**: `AccountEndpoint=...`
- **SQL Server**: `Server=...` or `Data Source=...`
- **MySQL**: `server=...` (lowercase)

See [Database configuration reference](../Configuration/Database-Configuration-Reference.md) for detailed settings and connection strings.

### Multi-Cloud Storage

The Cosmos.BlobService provides a unified interface for multiple storage providers:

- **Azure Blob Storage**: `DefaultEndpointsProtocol=https;AccountName=...`
- **Amazon S3**: `Bucket=...;Region=...;KeyId=...;Key=...`
- **Cloudflare R2**: `AccountId=...;Bucket=...;KeyId=...;Key=...`

See [Storage configuration reference](../Configuration/Storage-Configuration-Reference.md) for detailed settings and connection strings.

### Multi-Tenancy

The Cosmos.DynamicConfig project enables multi-tenant configurations where a single application instance can serve multiple customers with isolated databases and storage. For implementation details, see the `Cosmos.ConnectionStrings` component in the source repository.

## Extending SkyCMS

### Adding Custom Controllers

Extend base controllers for common functionality:

```csharp
public class MyController : HomeControllerBase
{
    public MyController(
        ArticleLogic articleLogic,
        ApplicationDbContext dbContext,
        StorageContext storageContext,
        ILogger<MyController> logger,
        IEmailSender emailSender)
        : base(articleLogic, dbContext, storageContext, logger, emailSender)
    {
    }
}
```

### Custom Database Providers

To add a new database provider, implement a strategy in AspNetCore.Identity.FlexDb:

1. Create a class implementing `IDatabaseConfigurationStrategy`
2. Define connection string detection logic
3. Configure EF Core options for the provider
4. Register the strategy with appropriate priority

### Custom Storage Providers

To add a new storage provider to Cosmos.BlobService:

1. Implement the `ICosmosStorage` interface
2. Add detection logic in `StorageContext`
3. Handle provider-specific operations
4. Test with multi-tenant scenarios

## Development Guidelines

### Code Standards

- Follow .NET coding conventions
- Use StyleCop for code style enforcement
- Add XML documentation for public APIs
- Write unit tests for new functionality
- Use async/await for I/O operations

### Configuration

- Use `appsettings.json` for single-tenant setups
- Use Cosmos.DynamicConfig for multi-tenant deployments
- Never commit secrets to source control
- Use Azure Key Vault or User Secrets for sensitive data

### Performance

- Use caching where appropriate (memory cache for configuration)
- Optimize database queries (avoid N+1 problems)
- Use streaming for large file operations
- Consider CDN integration for static assets

## Metrics and Monitoring

### Built-in Metrics Classes

SkyCMS provides comprehensive metrics collection for monitoring resource usage, performance, and costs.

#### CosmosDBMetrics

Tracks Cosmos DB consumption and performance.

**Usage:**
```csharp
using Cosmos.Common.Metrics;

public class CosmosMetricsService
{
    public async Task<CosmosDBMetrics> GetDatabaseMetricsAsync(string accountName)
    {
        var metrics = new CosmosDBMetrics
        {
            AccountName = accountName,
            DatabaseName = "SkyCMSDb",
            RequestUnits = 450.5,
            DataUsageBytes = 1024 * 1024 * 500, // 500 MB
            IndexUsageBytes = 1024 * 1024 * 50,  // 50 MB
            DocumentCount = 10000,
            CollectionCount = 5,
            TimeStamp = DateTimeOffset.UtcNow
        };
        
        return metrics;
    }
}
```

#### StorageAccountMetrics

Monitors blob storage consumption, bandwidth, and operations.

**Usage:**
```csharp
using Cosmos.Common.Metrics;

public class StorageMetricsService
{
    public async Task<StorageAccountMetrics> GetStorageMetricsAsync(string accountName)
    {
        var metrics = new StorageAccountMetrics
        {
            AccountName = accountName,
            BlobStorageBytes = 1024L * 1024 * 1024 * 10, // 10 GB
            EgressBytes = 1024L * 1024 * 500,            // 500 MB
            IngressBytes = 1024L * 1024 * 200,           // 200 MB
            Transactions = 150000,
            TimeStamp = DateTimeOffset.UtcNow
        };
        
        return metrics;
    }
}
```

#### FrontDoorProfileMetrics

Tracks Azure Front Door CDN metrics.

**Usage:**
```csharp
using Cosmos.Common.Metrics;

public class CdnMetricsService
{
    public async Task<FrontDoorProfileMetrics> GetCdnMetricsAsync()
    {
        var metrics = new FrontDoorProfileMetrics
        {
            ProfileName = "SkyCMS-FrontDoor",
            RequestCount = 1000000,
            RequestBytes = 1024L * 1024 * 1024 * 50,  // 50 GB
            ResponseBytes = 1024L * 1024 * 1024 * 100, // 100 GB
            CacheHitRatio = 0.85, // 85% cache hit rate
            TimeStamp = DateTimeOffset.UtcNow
        };
        
        return metrics;
    }
}
```

### Exporting Metrics to Azure Application Insights

**Setup:**
```csharp
// In Program.cs
builder.Services.AddApplicationInsightsTelemetry();

// Custom metrics tracking
public class MetricsExporter
{
    private readonly TelemetryClient _telemetryClient;
    
    public void TrackCosmosMetrics(CosmosDBMetrics metrics)
    {
        _telemetryClient.TrackMetric("CosmosDB.RequestUnits", metrics.RequestUnits);
        _telemetryClient.TrackMetric("CosmosDB.DataUsage", metrics.DataUsageBytes);
        _telemetryClient.TrackMetric("CosmosDB.DocumentCount", metrics.DocumentCount);
    }
    
    public void TrackStorageMetrics(StorageAccountMetrics metrics)
    {
        _telemetryClient.TrackMetric("Storage.TotalBytes", metrics.BlobStorageBytes);
        _telemetryClient.TrackMetric("Storage.Egress", metrics.EgressBytes);
        _telemetryClient.TrackMetric("Storage.Transactions", metrics.Transactions);
    }
}
```

### Grafana Dashboard Integration

**Sample Prometheus Exporter:**
```csharp
using Prometheus;

public class MetricsExporter
{
    private static readonly Gauge CosmosRUs = Metrics.CreateGauge(
        "skycms_cosmos_request_units",
        "Cosmos DB Request Units consumed",
        new GaugeConfiguration { LabelNames = new[] { "database", "account" } });
    
    private static readonly Gauge StorageBytes = Metrics.CreateGauge(
        "skycms_storage_bytes",
        "Total storage bytes used",
        new GaugeConfiguration { LabelNames = new[] { "account" } });
    
    public void UpdateMetrics(CosmosDBMetrics cosmosMetrics, StorageAccountMetrics storageMetrics)
    {
        CosmosRUs.WithLabels(cosmosMetrics.DatabaseName, cosmosMetrics.AccountName)
            .Set(cosmosMetrics.RequestUnits);
        
        StorageBytes.WithLabels(storageMetrics.AccountName)
            .Set(storageMetrics.BlobStorageBytes);
    }
}

// Expose metrics endpoint
app.MapMetrics(); // Exposes /metrics endpoint for Prometheus
```

**Grafana Query Examples:**
```promql
# Average Cosmos DB RU consumption over 5 minutes
rate(skycms_cosmos_request_units[5m])

# Total storage growth rate
deriv(skycms_storage_bytes[1h])

# CDN cache hit ratio
sum(skycms_cdn_cache_hits) / sum(skycms_cdn_total_requests)
```

### Recommended Dashboards

**Application Performance:**
- Request duration (p50, p95, p99)
- Error rate by endpoint
- Database query performance
- Cache hit ratios

**Resource Consumption:**
- Cosmos DB RU/s usage and throttling
- Storage account capacity and growth
- CDN bandwidth and cache efficiency
- Application memory and CPU

**Business Metrics:**
- Active tenants
- Content publish rate
- User sessions
- Page views by tenant

### Automated Alerts

**Azure Monitor Alert Example:**
```json
{
  "name": "High Cosmos DB RU Usage",
  "condition": {
    "metric": "CosmosDB.RequestUnits",
    "operator": "GreaterThan",
    "threshold": 1000,
    "timeAggregation": "Average",
    "windowSize": "PT5M"
  },
  "actions": [
    {
      "actionGroupId": "/subscriptions/.../actionGroups/OnCall",
      "severity": "2"
    }
  ]
}
```

## Related Documentation

- **Project READMEs**: Each project has detailed documentation in its folder
- **Configuration Guides**: [Database](../Configuration/Database-Configuration-Reference.md) | [Storage](../Configuration/Storage-Configuration-Reference.md)
- **Deployment**: [Azure Installation](../Installation/AzureInstall.md) | [AWS S3 Static Hosting](../S3StaticWebsite.md)
- **User Guides**: [Templates](../Templates/Readme.md) | [Editors](../Editors/) | [File Management](../FileManagement/)

> Tip: If you introduce new shared services or base classes, add corresponding docs here to keep the codebase discoverable.

Last updated: November 2025 (.NET 9.0 update)

