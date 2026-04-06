# AspNetCore.Identity.FlexDb

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/AspNetCore.Identity.FlexDb.svg)](https://www.nuget.org/packages/AspNetCore.Identity.FlexDb/)

A flexible, multi-database implementation of **ASP.NET Core Identity** that automatically selects the appropriate database provider based on your connection string. Part of the [SkyCMS](https://github.com/CWALabs/SkyCMS) project.

## 🚀 Quick Start

### Installation

```bash
dotnet add package AspNetCore.Identity.FlexDb
```

### Basic Setup (.NET 10)

```csharp
using AspNetCore.Identity.FlexDb;
using AspNetCore.Identity.FlexDb.Extensions;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Connection string determines the provider automatically
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Configure DbContext with automatic provider detection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    CosmosDbOptionsBuilder.ConfigureDbOptions(options, connectionString));

// Add Identity with FlexDb
builder.Services.AddCosmosIdentity<ApplicationDbContext, IdentityUser, IdentityRole, string>(
    options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    });

var app = builder.Build();
app.Run();

// Your DbContext
public class ApplicationDbContext : CosmosIdentityDbContext<IdentityUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions options) : base(options) { }
}
```

## ✨ Key Features

- **Zero Configuration Switching** - Change databases by updating connection string only
- **Multi-Database Support** - Azure Cosmos DB, SQL Server, MySQL, SQLite
- **Strategy Pattern** - Clean, extensible architecture
- **Auto Provider Detection** - Intelligent connection string analysis
- **Thread-Safe** - All operations are concurrent-friendly
- **Well Documented** - Comprehensive XML documentation

## 🗄️ Supported Databases

| Database | Connection String Pattern | Best For |
|----------|--------------------------|----------|
| **Azure Cosmos DB** | `AccountEndpoint=...` | Global-scale, cloud-native apps |
| **SQL Server** | `Server=...;User ID=...` | Enterprise applications |
| **MySQL** | `Server=...;uid=...` | Open-source, cost-effective |
| **SQLite** | `Data Source=...` | Testing, development |

### Example Connection Strings

```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://account.documents.azure.com:443/;AccountKey=key;Database=MyDb;",
    "SqlServer": "Server=tcp:server.database.windows.net,1433;Initial Catalog=MyDb;User ID=user;Password=pwd;",
    "MySQL": "Server=server;Port=3306;uid=user;pwd=password;database=MyDb;",
    "SQLite": "Data Source=app.db;"
  }
}
```

## 🔧 Configuration Options

### Identity Options

```csharp
builder.Services.AddCosmosIdentity<ApplicationDbContext, IdentityUser, IdentityRole, string>(
    options =>
    {
        // Password requirements
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        
        // User settings
        options.User.RequireUniqueEmail = true;
        
        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        
        // Sign-in settings
        options.SignIn.RequireConfirmedEmail = true;
    },
    cookieExpireTimeSpan: TimeSpan.FromDays(30),
    slidingExpiration: true
);
```

### Custom User/Role Types

```csharp
public class ApplicationUser : IdentityUser<string>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class ApplicationRole : IdentityRole<string>
{
    public string Description { get; set; }
}

// Use in configuration
builder.Services.AddCosmosIdentity<ApplicationDbContext, ApplicationUser, ApplicationRole, string>(...);
```

## 🔐 Security Features

### Personal Data Protection

```csharp
services.Configure<IdentityOptions>(options =>
{
    options.Stores.ProtectPersonalData = true;
});

public class ApplicationUser : IdentityUser
{
    [PersonalData]
    [ProtectedPersonalData]
    public string? SocialSecurityNumber { get; set; }
}
```

### Authentication

- Cookie-based authentication with configurable expiration
- Built-in two-factor authentication (2FA)
- External OAuth provider integration support

## 📊 Provider Selection

FlexDb automatically detects the database provider using a **Strategy Pattern** implementation. The selection is based on connection string patterns:

1. **Cosmos DB** (Priority 10) - Detects `AccountEndpoint=`
2. **SQL Server** (Priority 20) - Detects `Server=` or `User ID=`
3. **MySQL** (Priority 30) - Detects `uid=` with `server=`
4. **SQLite** (Priority 40) - Detects `Data Source=` with `.db`

### Manual Provider Override

```csharp
// Force specific provider (bypass auto-detection)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
```

## 🎯 When to Use FlexDb

### ✅ Great For

- Multi-environment deployments (dev → staging → production)
- Applications requiring database flexibility
- Cloud-native applications using Cosmos DB
- Rapid prototyping and development
- Projects transitioning between database providers

### ⚠️ Consider Alternatives When

- You need provider-specific advanced features
- Your application is tightly coupled to one database
- You require maximum optimization for a single provider

## 🌐 Real-World Usage

AspNetCore.Identity.FlexDb is used in production by **[SkyCMS](https://github.com/CWALabs/SkyCMS)**, a multi-tenant content management system supporting:

- Azure Cosmos DB for global-scale deployments
- SQL Server for enterprise installations
- MySQL for open-source hosting
- SQLite for local development and testing

## 📦 Dependencies

- Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.5)
- Microsoft.EntityFrameworkCore.Cosmos (10.0.5)
- Microsoft.EntityFrameworkCore.SqlServer (10.0.5)
- Microting.EntityFrameworkCore.MySql (10.0.5)
- AspNetCore.Identity.CosmosDb (10.0.5.1)

## 🔗 Part of SkyCMS

This package is part of the **SkyCMS** project, an open-source, multi-tenant content management system built on ASP.NET Core.

- **Repository**: [https://github.com/CWALabs/SkyCMS](https://github.com/CWALabs/SkyCMS)
- **Documentation**: [SkyCMS Docs](https://github.com/CWALabs/SkyCMS/tree/main/Docs)
- **License**: [MIT License](https://opensource.org/licenses/MIT)

## 📖 Full Documentation

For comprehensive documentation, advanced usage, troubleshooting, and contribution guidelines, visit:

- **GitHub**: [AspNetCore.Identity.FlexDb](https://github.com/CWALabs/SkyCMS/tree/main/AspNetCore.Identity.FlexDb)
- **README**: [Complete Documentation](https://github.com/CWALabs/SkyCMS/blob/main/AspNetCore.Identity.FlexDb/README.md)

## 🐛 Issues & Support

- **Report Issues**: [GitHub Issues](https://github.com/CWALabs/SkyCMS/issues)
- **Discussions**: [GitHub Discussions](https://github.com/CWALabs/SkyCMS/discussions)

## 📄 License

This project is licensed under the **MIT License**.

```
MIT License

Copyright (c) Moonrise Software, LLC

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

**AspNetCore.Identity.FlexDb** - One Identity Provider, Multiple Database Options 🚀
