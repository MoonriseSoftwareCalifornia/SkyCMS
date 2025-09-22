# AspNetCore.Identity.FlexDb - Flexible Database Provider for ASP.NET Core Identity

A flexible, multi-database implementation of ASP.NET Core Identity that automatically selects the appropriate database provider based on your connection string. Supports Azure Cosmos DB, SQL Server, MySQL, and SQLite with seamless switching between providers.

## 🎯 Overview

AspNetCore.Identity.FlexDb eliminates the need to choose a specific database provider at compile time. Simply provide a connection string, and the library automatically configures the correct Entity Framework provider, making it perfect for applications that need to support multiple deployment scenarios or migrate between database systems.

### Key Features

- **Automatic Provider Detection**: Intelligently selects database provider from connection string
- **Multi-Database Support**: Cosmos DB, SQL Server, MySQL, and SQLite
- **Azure Integration**: Native support for Azure Cosmos DB and Azure SQL Database
- **Backward Compatibility**: Supports legacy Cosmos DB configurations
- **Personal Data Protection**: Built-in encryption for sensitive user data
- **NuGet Package**: Easy installation as `AspNetCore.Identity.CosmosDb`

## 🗄️ Supported Database Providers

### Azure Cosmos DB

- **Primary Target**: Optimized for cloud-native applications
- **Global Distribution**: Multi-region replication and scaling
- **NoSQL Flexibility**: Schema-less document storage
- **Connection String**: `AccountEndpoint=https://account.documents.azure.com:443/;AccountKey=key;Database=dbname;`

### SQL Server / Azure SQL Database

- **Enterprise Ready**: Full ACID compliance and relational features
- **Connection String**: `Server=server;Initial Catalog=database;User ID=user;Password=password;`

### MySQL

- **Open Source**: Cost-effective relational database option
- **Connection String**: `Server=server;Port=3306;uid=user;pwd=password;database=dbname;`

### SQLite

- **Development**: Lightweight option for development and testing
- **Connection String**: `Data Source=database.db`

## 🚀 Quick Start

### Installation

Install the NuGet package:

```bash
dotnet add package AspNetCore.Identity.CosmosDb
```

### Basic Configuration

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
    "SQLite": "Data Source=myapp.db"
  }
}
```

## 🏗️ Architecture

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
- **SQLite**: Detects `Data Source=` with `.db` extension

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

## 🔧 Configuration Options

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

## 🔐 Security Features

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

## 🚀 Database Migration

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

## 📊 Performance Considerations

### Cosmos DB Optimizations

- **Partition Key Strategy**: Optimized partitioning for user data
- **Query Efficiency**: Minimized RU consumption
- **Bulk Operations**: Efficient batch processing
- **Connection Pooling**: Optimized client connections

### Relational Database Optimizations

- **Indexing Strategy**: Appropriate indexes for Identity queries
- **Connection Pooling**: Efficient connection management
- **Query Optimization**: Optimized LINQ to SQL translations

## 🛠️ Advanced Usage

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

## 📦 NuGet Package Information

- **Package ID**: `AspNetCore.Identity.CosmosDb`
- **Target Framework**: .NET 9.0
- **Repository**: [GitHub](https://github.com/CosmosSoftware/AspNetCore.Identity.FlexDb)
- **License**: MIT License
- **Dependencies**:
  - Microsoft.AspNetCore.Identity.EntityFrameworkCore
  - Microsoft.EntityFrameworkCore.Cosmos
  - Microsoft.EntityFrameworkCore.SqlServer
  - MySql.EntityFrameworkCore

## 🐛 Troubleshooting

### Common Issues

#### Connection String Not Detected

```csharp
// Ensure connection string format matches expected patterns
// Cosmos DB: Must include "AccountEndpoint="
// SQL Server: Must include "User ID"
// MySQL: Must include "uid="
// SQLite: Must include "Data Source=" with ".db"
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

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Implement changes with tests
4. Submit a pull request

### Development Guidelines

- Follow .NET coding standards
- Include unit tests for new providers
- Update documentation for new features
- Ensure backward compatibility

## 📄 License

This project is licensed under the MIT License. See the license file for details.

## 🔗 Related Projects

- **[SkyCMS](../README.md)**: Content management system using this identity provider
- **[SkyCMS Editor](../Editor/README.md)**: Content editing interface
- **[SkyCMS Publisher](../Publisher/README.md)**: Public website engine

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/CosmosSoftware/AspNetCore.Identity.FlexDb/issues)
- **Documentation**: [Project Wiki](https://github.com/CosmosSoftware/AspNetCore.Identity.FlexDb/wiki)
- **NuGet**: [Package Page](https://www.nuget.org/packages/AspNetCore.Identity.CosmosDb/)

---

**AspNetCore.Identity.FlexDb** - One Identity Provider, Multiple Database Options
