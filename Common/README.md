# Cosmos.Common - SkyCMS Core Library

## Overview

Cosmos.Common is the foundational library for the SkyCMS content management system, providing core functionality, data models, services, and base controllers that are shared across both the Editor and Publisher applications. This package contains essential components for content management, authentication, data access, and utility functions.

## Features

### Core Infrastructure

- **Multi-Database Support**: Entity Framework integration with Cosmos DB, SQL Server, MySQL, and SQLite
- **Base Controllers**: Common controller functionality for Editor and Publisher applications
- **Data Models**: Comprehensive set of entities for content management
- **Utility Functions**: Essential helper methods and extensions
- **Authentication Integration**: ASP.NET Core Identity with flexible provider support

### Content Management

- **Article Management**: Complete article lifecycle management with versioning
- **Page Publishing**: Published page management and routing
- **Layout System**: Template and layout management for content presentation
- **File Management**: Integration with blob storage for media handling
- **Search Functionality**: Content search and indexing capabilities

### Multi-Tenant Architecture

- **Contact Management**: Customer contact and communication handling
- **Metrics Collection**: Usage tracking and analytics
- **Security**: Role-based access control and article permissions
- **Configuration Management**: Dynamic settings and configuration

### Developer Tools

- **Health Checks**: Database connectivity and system status monitoring
- **Logging**: Comprehensive activity logging and audit trails
- **Cache Management**: Memory caching with Cosmos DB integration
- **Validation**: Model validation and data integrity

## Architecture

### Core Components

#### ApplicationDbContext

The main Entity Framework DbContext that provides access to all CMS entities with support for multiple database providers including Cosmos DB, SQL Server, MySQL, and SQLite.

#### Base Controllers

- **HomeControllerBase**: Common functionality for home controllers in Editor and Publisher
- **PubControllerBase**: Secure file access and authentication for Publisher applications

#### Data Models

Comprehensive set of entities including Articles, Pages, Layouts, Templates, Users, and system configuration.

#### Utility Classes

- **CosmosUtilities**: Static utility methods for authentication and file management
- **CosmosLinqExtensions**: LINQ extensions for Cosmos DB operations

## Installation

This package is part of the SkyCMS solution and can be obtained by cloning the [SkyCMS GitHub repository](https://github.com/MoonriseSoftwareCalifornia/CosmosCMS).

### NuGet Package

The package is also available on NuGet:

```bash
Install-Package Cosmos.Common
```

Or via .NET CLI:

```bash
dotnet add package Cosmos.Common
```

## Configuration

### Database Configuration

The ApplicationDbContext automatically detects the database provider based on the connection string:

#### Cosmos DB

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "AccountEndpoint=https://your-cosmos-account.documents.azure.com:443/;AccountKey=your-key;Database=your-database"
  }
}
```

#### SQL Server

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-server;Database=your-database;Trusted_Connection=true;"
  }
}
```

#### MySQL

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=your-server;database=your-database;user=your-user;password=your-password"
  }
}
```

#### SQLite

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=your-database.db"
  }
}
```

## Usage

### Service Registration

In your `Program.cs` or `Startup.cs`:

```csharp
using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;

// Register ApplicationDbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseCosmos(connectionString, databaseName)); // or UseSqlServer, UseMySQL, UseSqlite

// Register other common services
builder.Services.AddScoped<ArticleLogic>();
builder.Services.AddScoped<ContactManagementService>();
```

### Using Base Controllers

#### HomeController Example

```csharp
using Cosmos.Common;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;

public class HomeController : HomeControllerBase
{
    public HomeController(
        ArticleLogic articleLogic,
        ApplicationDbContext dbContext,
        StorageContext storageContext,
        ILogger<HomeController> logger,
        IEmailSender emailSender)
        : base(articleLogic, dbContext, storageContext, logger, emailSender)
    {
    }

    public async Task<IActionResult> Index()
    {
        // Use inherited functionality from HomeControllerBase
        var toc = await GetTOC("/", false, 0, 10);
        return View(toc);
    }
}
```

#### Publisher Controller Example

```csharp
using Cosmos.Publisher.Controllers;
using Cosmos.Common.Data;

public class FileController : PubControllerBase
{
    public FileController(
        ApplicationDbContext dbContext,
        StorageContext storageContext,
        bool requiresAuthentication = false)
        : base(dbContext, storageContext, requiresAuthentication)
    {
    }

    // Inherits secure file serving functionality
}
```

### Database Operations

#### Article Management

```csharp
public class ArticleService
{
    private readonly ApplicationDbContext _context;

    public ArticleService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Article> CreateArticleAsync(string title, string content)
    {
        var article = new Article
        {
            Title = title,
            Content = content,
            Published = DateTimeOffset.UtcNow,
            Updated = DateTimeOffset.UtcNow
        };

        _context.Articles.Add(article);
        await _context.SaveChangesAsync();
        return article;
    }

    public async Task<List<PublishedPage>> GetPublishedPagesAsync()
    {
        return await _context.Pages
            .Where(p => p.Published.HasValue)
            .OrderByDescending(p => p.Published)
            .ToListAsync();
    }
}
```

#### User Authentication

```csharp
public class AuthService
{
    private readonly ApplicationDbContext _context;

    public AuthService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanUserAccessArticle(ClaimsPrincipal user, int articleNumber)
    {
        return await CosmosUtilities.AuthUser(_context, user, articleNumber);
    }

    public async Task<List<TableOfContentsItem>> GetUserArticles(ClaimsPrincipal user)
    {
        return await CosmosUtilities.GetArticlesForUser(_context, user);
    }
}
```

### Contact Management

```csharp
public class ContactController : Controller
{
    private readonly ContactManagementService _contactService;

    public ContactController(ContactManagementService contactService)
    {
        _contactService = contactService;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitContact(ContactViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _contactService.AddContactAsync(model);
            return Json(result);
        }
        return BadRequest(ModelState);
    }
}
```

### File Management

```csharp
public class FileService
{
    private readonly StorageContext _storageContext;

    public FileService(StorageContext storageContext)
    {
        _storageContext = storageContext;
    }

    public async Task<List<FileManagerEntry>> GetArticleFiles(int articleNumber, string path = "")
    {
        return await CosmosUtilities.GetArticleFolderContents(_storageContext, articleNumber, path);
    }
}
```

## API Reference

### Entity Models

#### Core Entities

| Entity | Description |
|--------|-------------|
| `Article` | Main content articles with versioning |
| `PublishedPage` | Published pages accessible via URLs |
| `Layout` | Page layouts and templates |
| `Template` | Reusable content templates |
| `CatalogEntry` | Article catalog with permissions |
| `Contact` | Customer contact information |
| `Setting` | System configuration settings |

#### User Management

| Entity | Description |
|--------|-------------|
| `IdentityUser` | System users (from ASP.NET Core Identity) |
| `IdentityRole` | User roles and permissions |
| `AuthorInfo` | Public author information |
| `TotpToken` | Two-factor authentication tokens |

#### System Entities

| Entity | Description |
|--------|-------------|
| `ArticleLog` | Activity logging and audit trails |
| `ArticleLock` | Article editing locks |
| `ArticleNumber` | Article numbering system |
| `Metric` | System usage metrics |

### Base Controller Methods

#### HomeControllerBase

| Method | Description | Parameters |
|--------|-------------|------------|
| `GetTOC` | Get table of contents | `page`, `orderByPub`, `pageNo`, `pageSize` |
| `CCMS_POSTCONTACT_INFO` | Handle contact form submissions | `ContactViewModel` |
| `CCMS___SEARCH` | Search published content | `searchTxt`, `includeText` |
| `CCMS_UTILITIES_NET_PING_HEALTH_CHECK` | System health check | None |

#### PubControllerBase

| Method | Description | Parameters |
|--------|-------------|------------|
| `Index` | Serve files with authentication | None (uses request path) |

### Utility Methods

#### CosmosUtilities

| Method | Description | Parameters |
|--------|-------------|------------|
| `AuthUser` | Authenticate user for article access | `dbContext`, `user`, `articleNumber` |
| `GetArticleFolderContents` | Get article file contents | `storageContext`, `articleNumber`, `path` |
| `GetArticlesForUser` | Get articles accessible to user | `dbContext`, `user` |

## Dependencies

### Core Dependencies

- **.NET 9.0**: Modern .NET framework
- **Microsoft.EntityFrameworkCore**: Entity Framework Core ORM
- **Microsoft.EntityFrameworkCore.Cosmos**: Cosmos DB provider
- **Microsoft.EntityFrameworkCore.SqlServer**: SQL Server provider
- **Microsoft.AspNetCore.Identity**: ASP.NET Core Identity system
- **Microsoft.AspNetCore.DataProtection**: Data protection services

### Azure Integration

- **Azure.Extensions.AspNetCore.Configuration.Secrets**: Azure Key Vault integration
- **Azure.Monitor.Query**: Azure Monitor integration
- **Microsoft.PowerBI.Api**: Power BI integration

### Additional Services

- **MailChimp.Net.V3**: Email marketing integration
- **X.Web.Sitemap**: Sitemap generation
- **Otp.NET**: One-time password support

### Project References

- **AspNetCore.Identity.FlexDb**: Flexible identity provider
- **Cosmos.BlobService**: Multi-cloud blob storage
- **Cosmos.DynamicConfig**: Dynamic configuration management

## Multi-Database Support

The ApplicationDbContext automatically detects and configures the appropriate database provider:

### Database Provider Detection

```csharp
// Cosmos DB detection
if (connectionString.Contains("AccountEndpoint"))
{
    // Configure for Cosmos DB
}
// SQL Server detection
else if (connectionString.Contains("Server=") || connectionString.Contains("Data Source="))
{
    // Configure for SQL Server or SQLite
}
// MySQL detection
else if (connectionString.Contains("server="))
{
    // Configure for MySQL
}
```

### Container Configuration (Cosmos DB)

Each entity is mapped to appropriate Cosmos DB containers:

- Articles → "Articles" container
- Pages → "Pages" container
- Identity data → "Identity" container
- Settings → "Settings" container

## Performance Considerations

- **Connection Pooling**: Efficient database connection management
- **Async Operations**: All database operations are asynchronous
- **Query Optimization**: Optimized LINQ queries for each database provider
- **Caching**: Memory caching for frequently accessed data
- **Pagination**: Built-in pagination support for large datasets

## Security Features

- **Role-Based Access Control**: Granular permissions system
- **Article Permissions**: Per-article access control
- **Authentication Integration**: ASP.NET Core Identity integration
- **Data Protection**: ASP.NET Core data protection services
- **Input Validation**: Comprehensive model validation

## Health Monitoring

Built-in health check endpoints:

- Database connectivity verification
- System status monitoring
- Performance metrics collection

## License

Licensed under the MIT License. See the LICENSE file for details.

## Contributing

This project is part of the SkyCMS ecosystem. For contribution guidelines and more information, visit the [SkyCMS GitHub repository](https://github.com/MoonriseSoftwareCalifornia/CosmosCMS).

