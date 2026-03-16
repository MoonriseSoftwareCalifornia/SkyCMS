# AspNetCore.Identity.FlexDb — Developer README

Purpose
- Identity store implementation and related tests for Azure Cosmos DB, MySQL, SQL Server, and SQLite.

Quick start
```powershell
dotnet build SkyCMS.sln
dotnet test AspNetCore.Identity.FlexDb.Tests\AspNetCore.Identity.FlexDb.Tests.csproj
```

Where to look
- Store implementations: `AspNetCore.Identity.FlexDb/Stores`.
- Containers & utilities: `AspNetCore.Identity.FlexDb/Containers`.

Notes
- Identity changes are sensitive; add tests and check for compatibility with ASP.NET Identity abstractions.

# AspNetCore.Identity.FlexDb - Flexible Database Provider for ASP.NET Core Identity

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/AspNetCore.Identity.FlexDb.svg)](https://www.nuget.org/packages/AspNetCore.Identity.FlexDb/)

A flexible, multi-database implementation of ASP.NET Core Identity that **automatically selects the appropriate database provider** based on your connection string. Supports Azure Cosmos DB, SQL Server, MySQL, and SQLite with seamless switching between providers.

---

## Table of Contents

- [What's New](#whats-new)
- [Overview](#overview)
- [Supported Database Providers](#supported-database-providers)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Configuration Options](#configuration-options)
- [Security Features](#security-features)
- [Database Migration](#database-migration)
- [Performance Considerations](#performance-considerations)
- [Advanced Usage](#advanced-usage)
- [NuGet Package Information](#nuget-package-information)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

---

## What's New

### Version 9.0+

- **.NET 9** support with C# 13.0 features
- **Enhanced Strategy Pattern** with improved provider detection
- **Thread-safe** stateless strategy implementations
- **Improved documentation** with comprehensive XML comments
- **Performance optimizations** for all providers
- **Better error messages** with detailed provider information

---

## Overview

AspNetCore.Identity.FlexDb **eliminates the need to choose a specific database provider at compile time**. Simply provide a connection string, and the library automatically configures the correct Entity Framework provider using the **Strategy Pattern**.

### Why FlexDb?

- **Zero Code Changes**: Switch databases by changing connection strings only
- **Rapid Development**: No provider-specific configuration needed
- **Multi-Environment**: Use MySQL for dev, SQL Server for staging, Cosmos DB for production
- **Single Package**: All providers in one NuGet package
- **Extensible**: Add custom providers by implementing `IDatabaseConfigurationStrategy`
- **Secure**: Built-in personal data encryption and protection

### Key Features

| Feature | Description |
|---------|-------------|
| **Automatic Provider Detection** | Intelligently selects database provider from connection string patterns |
| **Multi-Database Support** | Cosmos DB, SQL Server, and MySQL out of the box |
| **Strategy Pattern** | Clean, extensible architecture for adding new providers |
| **Azure Integration** | Native support for Azure Cosmos DB and Azure SQL Database |
| **Backward Compatibility** | Supports legacy Cosmos DB configurations |
| **Personal Data Protection** | Built-in encryption for sensitive user data |
| **Thread-Safe** | All operations are thread-safe and concurrent-friendly |
| **Well Documented** | Comprehensive XML documentation for all public APIs |

---

## Supported Database Providers

### Azure Cosmos DB

**Best for:** Global-scale, cloud-native applications requiring low latency and high availability

| Aspect | Details |
|--------|---------|
| **Provider Priority** | 10 (highest) |
| **Detection Pattern** | `AccountEndpoint=` |
| **Features** | Multi-region replication, automatic scaling, NoSQL flexibility |
| **Connection String** | `AccountEndpoint=https://account.documents.azure.com:443/;AccountKey=key;Database=dbname;` |
| **Use Cases** | Global apps, serverless architectures, document-based data models |

**Optimizations:**
- Optimized partition key strategy for user data
- Minimized RU consumption
- Efficient batch operations

### SQL Server / Azure SQL Database

**Best for:** Enterprise applications requiring ACID compliance and relational integrity

| Aspect | Details |
|--------|---------|
| **Provider Priority** | 20 |
| **Detection Pattern** | `Server=` or `User ID=` |
| **Features** | Full ACID compliance, advanced indexing, enterprise features |
| **Connection String** | `Server=server;Initial Catalog=database;User ID=user;Password=password;` |
| **Use Cases** | Enterprise apps, complex reporting, existing SQL Server infrastructure |

### MySQL

**Best for:** Open-source projects, cost-effective hosting, LAMP stack integration

| Aspect | Details |
|--------|---------|
| **Provider Priority** | 30 |
| **Detection Pattern** | `uid=` or `user id=` (with `server=`) |
| **Features** | Open source, wide hosting support, good performance |
| **Connection String** | `Server=server;Port=3306;uid=user;pwd=password;database=dbname;` |
| **Use Cases** | Linux hosting, open-source projects, budget-conscious deployments |

### Provider Strategy Selection

FlexDb uses a sophisticated detection system to automatically select the appropriate database provider based on connection string patterns.

**Detection Algorithm:**

```csharp
public class ProviderDetection
{
    public static IDatabaseConfigurationStrategy DetectProvider(string connectionString)
    {
        // Priority 10: Cosmos DB (highest priority)
        if (connectionString.Contains("AccountEndpoint=", StringComparison.OrdinalIgnoreCase))
        {
            return new CosmosDbConfigurationStrategy();
        }
        
        // Priority 20: SQL Server
        if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("User ID=", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlServerConfigurationStrategy();
        }
        
        // Priority 30: MySQL
        if (connectionString.Contains("server=", StringComparison.OrdinalIgnoreCase) &&
            connectionString.Contains("database=", StringComparison.OrdinalIgnoreCase))
        {
            return new MySqlConfigurationStrategy();
        }
        
        // Priority 40: SQLite (lowest priority)
        if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &&
            connectionString.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            return new SqliteConfigurationStrategy();
        }
        
        throw new NotSupportedException($"Unable to detect database provider from connection string");
    }
}
```

**Strategy Interface:**

```csharp
public interface IDatabaseConfigurationStrategy
{
    int Priority { get; }  // Lower number = higher priority
    string ProviderName { get; }
    
    bool CanHandle(string connectionString);
    void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString);
    void ConfigureModel(ModelBuilder modelBuilder);
}
```

**Custom Strategy Example:**

```csharp
using AspNetCore.Identity.FlexDb.Strategies;

public class PostgreSqlConfigurationStrategy : IDatabaseConfigurationStrategy
{
    public int Priority => 25; // Between SQL Server and MySQL
    public string ProviderName => "PostgreSQL";
    
    public bool CanHandle(string connectionString)
    {
        return connectionString.Contains("Host=") &&
               connectionString.Contains("Username=");
    }
    
    public void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });
    }
    
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        // PostgreSQL-specific model configuration
        modelBuilder.HasDefaultSchema("identity");
    }
}

// Register custom strategy
services.AddCosmosIdentity<IdentityUser, IdentityRole, string>(
    connectionString,
    customStrategies: new[] { new PostgreSqlConfigurationStrategy() });
```

**Override Detection:**

```csharp
// Force a specific provider
services.AddDbContext<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>>(
    options =>
    {
        // Bypass detection, use SQL Server directly
        options.UseSqlServer(connectionString);
    });
```

**Strategy Selection Logging:**

```csharp
public class LoggingStrategySelector
{
    private readonly ILogger _logger;
    
    public IDatabaseConfigurationStrategy SelectStrategy(
        string connectionString,
        IEnumerable<IDatabaseConfigurationStrategy> strategies)
    {
        var selected = strategies
            .Where(s => s.CanHandle(connectionString))
            .OrderBy(s => s.Priority)
            .FirstOrDefault();
        
        if (selected == null)
        {
            _logger.LogError("No strategy found for connection string pattern");
            throw new InvalidOperationException("Unable to determine database provider");
        }
        
        _logger.LogInformation(
            "Selected {Provider} strategy (Priority: {Priority})",
            selected.ProviderName,
            selected.Priority);
        
        return selected;
    }
}
```

### Privacy and Retry Policies

**Personal Data Encryption:**

FlexDb includes `PersonalDataConverter` to automatically encrypt sensitive user information.

```csharp
using AspNetCore.Identity.FlexDb;
using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    [PersonalData]
    [ProtectedPersonalData]  // Automatically encrypted
    public string? PhoneNumber { get; set; }
    
    [PersonalData]
    [ProtectedPersonalData]
    public string? SocialSecurityNumber { get; set; }
}

// Configuration
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(@"./keys"))
            .SetApplicationName("SkyCMS");
        
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<CosmosIdentityDbContext<ApplicationUser, IdentityRole, string>>()
            .AddDefaultTokenProviders()
            .AddPersonalDataProtection<PersonalDataConverter, PersonalDataProtectionKeyProvider>();
    }
}
```

**How PersonalDataConverter Works:**

```csharp
public class PersonalDataConverter : IPersonalDataConverter
{
    private readonly IDataProtector _protector;
    
    public string? Protect(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return data;
        
        return _protector.Protect(data);
    }
    
    public string? Unprotect(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return data;
        
        return _protector.Unprotect(data);
    }
}
```

**Retry Policies:**

FlexDb includes built-in retry logic for transient failures.

```csharp
using AspNetCore.Identity.FlexDb;

public class RetryConfiguration
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        int delayMilliseconds = 100)
    {
        return await Retry.ExecuteAsync(
            operation,
            maxRetries,
            delayMilliseconds,
            onRetry: (ex, attempt) =>
            {
                Console.WriteLine($"Retry {attempt} after error: {ex.Message}");
            });
    }
}

// Usage in repository
public async Task<IdentityUser?> FindByIdAsync(string userId)
{
    return await RetryConfiguration.ExecuteWithRetryAsync(async () =>
    {
        return await _dbContext.Users.FindAsync(userId);
    });
}
```

**Cosmos DB Retry Policy:**

```csharp
services.AddDbContext<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>>(
    options => options.UseCosmos(
        connectionString,
        databaseName,
        cosmosOptions =>
        {
            cosmosOptions.MaxRetryCount(3);
            cosmosOptions.MaxRetryWaitTimeOnRateLimitedRequests(TimeSpan.FromSeconds(30));
        }));
```

**SQL Server Retry Policy:**

```csharp
services.AddDbContext<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>>(
    options => options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: new[] { 4060, 40197, 40501, 40613, 49918 });
        }));
```

**GDPR Compliance - Data Export:**

```csharp
public async Task<Dictionary<string, string>> ExportPersonalDataAsync(string userId)
{
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null)
        return new Dictionary<string, string>();
    
    var personalData = new Dictionary<string, string>();
    var personalDataProps = typeof(IdentityUser).GetProperties()
        .Where(prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
    
    foreach (var prop in personalDataProps)
    {
        var value = prop.GetValue(user)?.ToString() ?? "null";
        personalData.Add(prop.Name, value);
    }
    
    return personalData;
}
```

**Optimizations:**
- Connection pooling
- Optimized indexes on email and username
- Retry policies for transient failures

### SQLite

**Best for:** Lightweight, file-based storage for testing or small applications

| Aspect | Details |
|--------|---------|
| **Provider Priority** | 40 (lowest) |
| **Detection Pattern** | `Data Source=` |
| **Features** | Simple file-based storage, no server required |
| **Connection String** | `Data Source=app.db;` |
| **Use Cases** | Testing, development, small standalone apps |

---

## Quick Start

### Installation

Install the NuGet package:

```bash
dotnet add package AspNetCore.Identity.FlexDb
```

### Basic Configuration (Minimal hosting, .NET 9)

```csharp
using AspNetCore.Identity.FlexDb;
using AspNetCore.Identity.FlexDb.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Connection string determines the provider automatically
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    CosmosDbOptionsBuilder.ConfigureDbOptions(options, connectionString));

builder.Services.AddCosmosIdentity<ApplicationDbContext, IdentityUser, IdentityRole, string>(
    options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    },
    cookieExpireTimeSpan: TimeSpan.FromDays(30),
    slidingExpiration: true
);

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok("ok"));
app.Run();

public class ApplicationDbContext : CosmosIdentityDbContext<IdentityUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions options) : base(options) { }
}
```

Tip: The key name `DefaultConnection` is arbitrary—use any name, as long as you pass the same connection string into `ConfigureDbOptions`.

### Basic Configuration (Startup.cs pattern)

```csharp
using AspNetCore.Identity.FlexDb;
using AspNetCore.Identity.FlexDb.Extensions;
using Microsoft.AspNetCore.Identity;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Connection string determines the provider automatically
        var connectionString = Configuration.GetConnectionString("DefaultConnection");
        
        // Add FlexDb Identity with automatic provider detection
        services.AddDbContext<ApplicationDbContext>(options =>
            CosmosDbOptionsBuilder.ConfigureDbOptions(options, connectionString));
        
        services.AddCosmosIdentity<ApplicationDbContext, IdentityUser, IdentityRole, string>(
            options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            });
    }
}

// Your DbContext
public class ApplicationDbContext : CosmosIdentityDbContext<IdentityUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }
}
```

### Connection String Examples

```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://myaccount.documents.azure.com:443/;AccountKey=mykey;Database=MyDatabase;",
    "SqlServer": "Server=tcp:myserver.database.windows.net,1433;Initial Catalog=MyDatabase;User ID=myuser;Password=mypassword;",
    "MySQL": "Server=myserver;Port=3306;uid=myuser;pwd=mypassword;database=MyDatabase;",
    "SQLite": "Data Source=app.db;"
  }
}
```

## Architecture

### Core Components

#### CosmosIdentityDbContext

The main database context that extends Entity Framework's `IdentityDbContext`:

```csharp
public class CosmosIdentityDbContext<TUser, TRole, TKey> : IdentityDbContext<TUser, TRole, TKey>
    where TUser : IdentityUser<TKey>
    where TRole : IdentityRole<TKey>
    where TKey : IEquatable<TKey>
```

**Features:**

- **Provider-Specific Configuration**: Automatically adapts to database provider
- **Cosmos DB Optimizations**: Special handling for document database patterns
- **Backward Compatibility**: Support for legacy Cosmos DB configurations
- **Entity Configuration**: Optimized mappings for each database type

#### CosmosDbOptionsBuilder

Automatic database provider configuration utility:

```csharp
public static class CosmosDbOptionsBuilder
{
    public static DbContextOptions<TContext> GetDbOptions<TContext>(string connectionString)
    public static void ConfigureDbOptions(DbContextOptionsBuilder optionsBuilder, string connectionString)
}
```

**Provider Detection Logic:**

- **Cosmos DB**: Detects `AccountEndpoint=` pattern
- **SQL Server**: Detects `User ID` pattern
- **MySQL**: Detects `uid=` pattern
- **SQLite**: Detects `Data Source=` pattern

#### Identity Stores

Custom store implementations for multi-provider support:

- **CosmosUserStore**: User management with provider-specific optimizations
- **CosmosRoleStore**: Role management across database types
- **IdentityStoreBase**: Common functionality and error handling

#### Repository Pattern

Abstracted data access layer:

```csharp
public interface IRepository
{
    string ProviderName { get; }
    TEntity GetById<TEntity>(string id) where TEntity : class, new();
    IQueryable<TEntity> Find<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class, new();
    Task<int> SaveChangesAsync();
}
```

## Configuration Options

### Identity Configuration

```csharp
services.AddCosmosIdentity<ApplicationDbContext, IdentityUser, IdentityRole, string>(
    options =>
    {
        // Password requirements
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        
        // User settings
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        
        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        
        // Sign-in settings
        options.SignIn.RequireConfirmedEmail = true;
        options.SignIn.RequireConfirmedPhoneNumber = false;
    },
    cookieExpireTimeSpan: TimeSpan.FromDays(30),
    slidingExpiration: true
);
```

### Cosmos DB Specific Settings

```csharp
public class ApplicationDbContext : CosmosIdentityDbContext<IdentityUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions options) 
        : base(options, backwardCompatibility: false)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Additional custom configuration
        builder.Entity<IdentityUser>()
            .HasPartitionKey(u => u.Id);
    }
}
```

### Personal Data Protection

Enable encryption for sensitive user data:

```csharp
services.Configure<IdentityOptions>(options =>
{
    options.Stores.ProtectPersonalData = true;
});

services.AddSingleton<IPersonalDataProtector, MyPersonalDataProtector>();
```

## Security Features

### Data Protection

- **Personal Data Encryption**: Automatic encryption of PII fields
- **Secure Key Management**: Integration with ASP.NET Core Data Protection
- **Provider-Agnostic**: Works across all supported database types

### Authentication

- **Cookie Authentication**: Configurable expiration and sliding windows
- **Two-Factor Authentication**: Built-in 2FA support
- **External Providers**: OAuth integration ready

### Authorization

- **Role-Based Access**: Traditional role management
- **Claims-Based Security**: Fine-grained permission system
- **Policy-Based Authorization**: Flexible authorization policies

## Database Migration

### Switching Between Providers

FlexDb makes it easy to migrate between database providers:

1. **Update Connection String**: Change to target database format
2. **Migrate Data**: Use Entity Framework migrations or custom migration logic
3. **No Code Changes**: Application code remains unchanged

### Migration Example

```csharp
// From Cosmos DB to SQL Server
// Old: "AccountEndpoint=...;AccountKey=...;Database=MyDb;"
// New: "Server=...;Initial Catalog=MyDb;User ID=...;Password=...;"

// Entity Framework handles the provider switch automatically
await context.Database.MigrateAsync();
```

## Performance Considerations

### Cosmos DB Optimizations

- **Partition Key Strategy**: Optimized partitioning for user data
- **Query Efficiency**: Minimized RU consumption
- **Bulk Operations**: Efficient batch processing
- **Connection Pooling**: Optimized client connections

### Relational Database Optimizations

- **Indexing Strategy**: Appropriate indexes for Identity queries
- **Connection Pooling**: Efficient connection management
- **Query Optimization**: Optimized LINQ to SQL translations

## Advanced Usage

### Custom User and Role Types

```csharp
public class ApplicationUser : IdentityUser<string>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class ApplicationRole : IdentityRole<string>
{
    public string Description { get; set; }
}

public class ApplicationDbContext : CosmosIdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions options) : base(options) { }
}
```

### Repository Pattern Implementation

```csharp
public class CustomRepository : IRepository
{
    private readonly ApplicationDbContext _context;
    
    public CustomRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<ApplicationUser> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToUpper());
    }
}
```

### Multi-Tenant Support

```csharp
public class MultiTenantDbContext : CosmosIdentityDbContext<IdentityUser, IdentityRole, string>
{
    private readonly string _tenantId;
    
    public MultiTenantDbContext(DbContextOptions options, string tenantId) 
        : base(options)
    {
        _tenantId = tenantId;
    }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Add tenant filtering
        builder.Entity<IdentityUser>().HasQueryFilter(u => u.TenantId == _tenantId);
    }
}
```

## NuGet Package Information

- **Package ID**: `AspNetCore.Identity.FlexDb`
- **Target Framework**: .NET 9.0
- **Repository**: [GitHub](https://github.com/CWALabs/SkyCMS)
- **License**: MIT License
- **Dependencies**:
  - Microsoft.AspNetCore.Identity.EntityFrameworkCore
  - Microsoft.EntityFrameworkCore.Cosmos
  - Microsoft.EntityFrameworkCore.SqlServer
  - MySql.EntityFrameworkCore

## Troubleshooting

### Common Issues

#### Connection String Not Detected

```csharp
// Ensure connection string format matches expected patterns
// Cosmos DB: Must include "AccountEndpoint="
// SQL Server: Must include "User ID"
// MySQL: Must include "uid="
// SQLite: Must include "Data Source="
```

#### Cosmos DB Container Creation

```csharp
// Ensure database and containers exist
await context.Database.EnsureCreatedAsync();
```

#### Migration Issues

```csharp
// For provider switches, consider custom migration logic
public async Task MigrateFromCosmosToSql()
{
    // Export data from Cosmos DB
    // Transform data structure if needed
    // Import to SQL Server
}
```

### Performance Tuning

#### Cosmos DB

- Use appropriate partition keys
- Optimize queries to avoid cross-partition operations
- Monitor RU consumption

#### SQL Server

- Ensure proper indexing on email and username fields
- Use connection pooling
- Consider read replicas for read-heavy workloads

## Contributing

1. Fork the repository
2. Create a feature branch
3. Implement changes with tests
4. Submit a pull request

### Development Guidelines

- Follow .NET coding standards
- Include unit tests for new providers
- Update documentation for new features
- Ensure backward compatibility

## License

This project is licensed under the MIT License. See the license file for details.

## 🔗 Related Projects

- **[SkyCMS](../README.md)**: Content management system using this identity provider
- **[SkyCMS Editor](../Editor/README.md)**: Content editing interface
- **[SkyCMS Publisher](../Publisher/README.md)**: Public website engine

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/CWALabs/SkyCMS/issues)
- **Documentation**: [SkyCMS Documentation](https://github.com/CWALabs/SkyCMS/tree/main/Docs)
- **NuGet**: [Package Page](https://www.nuget.org/packages/AspNetCore.Identity.FlexDb/)

---

**AspNetCore.Identity.FlexDb** - One Identity Provider, Multiple Database Options
