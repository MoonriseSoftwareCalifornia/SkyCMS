# Common — Developer README

Purpose
- Utility and shared helper code used by many projects (data helpers, shared services, and cross-cutting concerns).

Quick start (local)
```powershell
dotnet build SkyCMS.sln
```

Where to look
- Data access and helpers: `Common/Data`.
- Shared features and utilities: `Common/Features`.

Tests
- Run solution tests or target specific test projects that depend on `Common`:
```powershell
dotnet test SkyCMS.sln
```

Notes & conventions
- Keep `Common` low-dependency and focused on reusable logic.
- When changing data-layer helpers, ensure affected projects have tests updated and run full solution tests.
- Prefer adding small, well-documented helpers rather than large utility classes.
# Cosmos.Common - SkyCMS Core Library

> **Package Info:** This is the `Cosmos.Common` package with namespace `Cosmos.Common.*`. It provides shared functionality for both Sky.Editor and Cosmos.Publisher applications.

## Overview

Cosmos.Common is the foundational library for the SkyCMS content management system, providing core functionality, data models, services, and base controllers that are shared across both the Editor and Publisher applications. This package contains essential components for content management, authentication, data access, and utility functions.

## What's New

- Updated docs aligned with .NET 9 and the latest storage/database guides
- Cross-links to Storage and Database configuration, AWS S3, and Cloudflare R2 setup

## Features

### Core Infrastructure

- **Multi-Database Support**: Entity Framework integration with Cosmos DB, SQL Server, and MySQL
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

The main Entity Framework DbContext that provides access to all CMS entities with support for multiple database providers including Cosmos DB, SQL Server, and MySQL.

#### Base Controllers

- **HomeControllerBase**: Common functionality for home controllers in Editor and Publisher
- **PubControllerBase**: Secure file access and authentication for Publisher applications

#### Data Models

Comprehensive set of entities including Articles, Pages, Layouts, Templates, Users, and system configuration.

#### Utility Classes

- **CosmosUtilities**: Static utility methods for authentication and file management
- **CosmosLinqExtensions**: LINQ extensions for Cosmos DB operations

## Installation

This package is part of the SkyCMS solution and can be obtained by cloning the [SkyCMS GitHub repository](https://github.com/CWALabs/SkyCMS).

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

SkyCMS registers `ApplicationDbContext` in the host app. In the provided templates, the connection string key is `ConnectionStrings:ApplicationDbContextConnection` (with `DefaultConnection` as a common fallback). Choose the EF Core provider accordingly:

Minimal hosting example (Program.cs):

```csharp
using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("ApplicationDbContextConnection")
         ?? builder.Configuration.GetConnectionString("DefaultConnection")!;

// Choose one based on your connection string/provider
if (cs.Contains("AccountEndpoint="))
{
    // Cosmos DB
    builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseCosmos(cs, databaseName: "SkyCms"));
}
else if (cs.Contains("Server=") || cs.Contains("Data Source="))
{
    // SQL Server
    builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(cs));
}
else if (cs.Contains("server=") || cs.Contains("Server="))
{
    // MySQL
    builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseMySQL(cs));
}
```

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

## Usage

### Service Registration

In your `Program.cs` or `Startup.cs`:

```csharp
using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;

// Register ApplicationDbContext with your chosen provider (see example above)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)); // Or UseCosmos / UseMySQL

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
using Cosmos.Common;
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

## Related Documentation

- Storage: [Docs/StorageConfig.md](https://cwalabs.github.io/SkyCMS/StorageConfig.html)
    - AWS S3 keys: [Docs/AWS-S3-AccessKeys.md](https://cwalabs.github.io/SkyCMS/AWS-S3-AccessKeys.html)
    - Cloudflare R2 keys: [Docs/Cloudflare-R2-AccessKeys.md](https://cwalabs.github.io/SkyCMS/Cloudflare-R2-AccessKeys.html)
- Database: [Docs/DatabaseConfig.md](https://cwalabs.github.io/SkyCMS/DatabaseConfig.html)

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
    // Configure for SQL Server
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

## Email Providers

SkyCMS Common supports multiple email service providers for transactional emails, newsletters, and notifications.

### SendGrid Configuration

**appsettings.json:**
```json
{
  "SendGridConfig": {
    "ApiKey": "SG.your-api-key",
    "SenderEmail": "noreply@example.com",
    "SenderName": "SkyCMS"
  }
}
```

**Usage:**
```csharp
using Cosmos.Cms.Common.Services.Configurations;
using SendGrid;
using SendGrid.Helpers.Mail;

public class EmailService
{
    private readonly SendGridConfig _config;
    
    public EmailService(IOptions<SendGridConfig> config)
    {
        _config = config.Value;
    }
    
    public async Task SendEmailAsync(string toEmail, string subject, string htmlContent)
    {
        var client = new SendGridClient(_config.ApiKey);
        var from = new EmailAddress(_config.SenderEmail, _config.SenderName);
        var to = new EmailAddress(toEmail);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
        
        var response = await client.SendEmailAsync(msg);
    }
}
```

### MailChimp Configuration

**appsettings.json:**
```json
{
  "MailChimpConfig": {
    "ApiKey": "your-mailchimp-api-key",
    "DataCenter": "us1",
    "ListId": "your-list-id"
  }
}
```

**Usage:**
```csharp
using Cosmos.Common.Services.Configurations;
using MailChimp.Net;
using MailChimp.Net.Interfaces;

public class NewsletterService
{
    private readonly MailChimpConfig _config;
    private readonly IMailChimpManager _mailChimpManager;
    
    public NewsletterService(IOptions<MailChimpConfig> config)
    {
        _config = config.Value;
        _mailChimpManager = new MailChimpManager(_config.ApiKey);
    }
    
    public async Task SubscribeToNewsletterAsync(string email, string firstName, string lastName)
    {
        var member = new Member
        {
            EmailAddress = email,
            StatusIfNew = Status.Subscribed,
            MergeFields = new Dictionary<string, object>
            {
                { "FNAME", firstName },
                { "LNAME", lastName }
            }
        };
        
        await _mailChimpManager.Members.AddOrUpdateAsync(
            _config.ListId,
            member);
    }
}
```

**Environment Variables (Recommended for Production):**
```bash
SENDGRID__APIKEY=your-sendgrid-key
MAILCHIMP__APIKEY=your-mailchimp-key
MAILCHIMP__LISTID=your-list-id
```

## Proxy and Routing Configuration

### ProxyConfig

Configures reverse proxy behavior for routing requests to backend services.

**appsettings.json:**
```json
{
  "ProxyConfig": {
    "EnableProxy": true,
    "ProxyUrl": "https://backend-service.example.com",
    "ProxyTimeout": "00:00:30",
    "BypassPaths": ["/health", "/metrics"]
  }
}
```

**Usage:**
```csharp
using Cosmos.Cms.Common.Services.Configurations;

public class ProxyMiddleware
{
    private readonly ProxyConfig _config;
    private readonly HttpClient _httpClient;
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (!_config.EnableProxy || _config.BypassPaths.Contains(context.Request.Path))
        {
            await _next(context);
            return;
        }
        
        var targetUri = new Uri(_config.ProxyUrl + context.Request.Path + context.Request.QueryString);
        var proxyRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);
        
        var response = await _httpClient.SendAsync(proxyRequest);
        context.Response.StatusCode = (int)response.StatusCode;
        await response.Content.CopyToAsync(context.Response.Body);
    }
}
```

### SimpleProxyConfigs

Lightweight proxy configurations for content delivery and API gateway scenarios.

**appsettings.json:**
```json
{
  "SimpleProxyConfigs": {
    "Routes": [
      {
        "Path": "/api/*",
        "Target": "https://api.example.com",
        "StripPrefix": false
      },
      {
        "Path": "/media/*",
        "Target": "https://cdn.example.com",
        "StripPrefix": true
      }
    ]
  }
}
```

### EditorUrl Configuration

Configures the Editor application URL for cross-application communication.

**Usage:**
```csharp
public class PublisherService
{
    private readonly string _editorUrl;
    
    public PublisherService(IConfiguration configuration)
    {
        _editorUrl = configuration["EditorUrl"];
    }
    
    public async Task<string> FetchContentFromEditorAsync(string contentId)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync($"{_editorUrl}/api/content/{contentId}");
        return await response.Content.ReadAsStringAsync();
    }
}
```

## Security Utilities

### OneTimeTokenProvider

Generates time-limited, single-use tokens for secure operations (password reset, email verification).

**Usage:**
```csharp
using Cosmos.Common.Services;
using Microsoft.AspNetCore.Identity;

public class AccountService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly OneTimeTokenProvider<IdentityUser> _tokenProvider;
    
    public async Task<string> GeneratePasswordResetTokenAsync(IdentityUser user)
    {
        // Token valid for 1 hour by default
        var token = await _tokenProvider.GenerateAsync(
            "PasswordReset",
            _userManager,
            user);
        
        return token;
    }
    
    public async Task<bool> ValidatePasswordResetTokenAsync(
        IdentityUser user,
        string token)
    {
        var isValid = await _tokenProvider.ValidateAsync(
            "PasswordReset",
            token,
            _userManager,
            user);
        
        return isValid;
    }
}
```

### CryptoJsDecryption

Decrypts data encrypted with CryptoJS (JavaScript library), enabling secure client-server communication.

**Usage:**
```csharp
using Cosmos.Common.Services;

public class SecureDataService
{
    public string DecryptClientData(string encryptedData, string passphrase)
    {
        // Decrypt data sent from JavaScript CryptoJS.AES.encrypt()
        var decrypted = CryptoJsDecryption.Decrypt(encryptedData, passphrase);
        return decrypted;
    }
}
```

**JavaScript (Client Side):**
```javascript
import CryptoJS from 'crypto-js';

const passphrase = 'your-secret-key';
const data = { userId: 123, email: 'user@example.com' };
const encrypted = CryptoJS.AES.encrypt(
    JSON.stringify(data),
    passphrase
).toString();

// Send encrypted to server
await fetch('/api/secure-data', {
    method: 'POST',
    body: JSON.stringify({ data: encrypted })
});
```

### Validation Attributes

**DateTimeUtcKindAttribute:**

Ensures DateTime values are in UTC.

```csharp
using Cosmos.Common.Models;

public class EventModel
{
    [DateTimeUtcKind(ErrorMessage = "Event time must be in UTC")]
    public DateTime EventTime { get; set; }
}
```

**RedirectUrlAttribute:**

Validates redirect URLs to prevent open redirect vulnerabilities.

```csharp
using Cosmos.Common.Models;

public class LoginViewModel
{
    [RedirectUrl(ErrorMessage = "Invalid redirect URL")]
    public string ReturnUrl { get; set; }
}
```

## Caching

### CosmosMemoryCache

Enhanced memory caching with automatic expiration and statistics.

**Configuration:**
```csharp
using Cosmos.Cms.Common.Services;

// In Program.cs
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CosmosMemoryCache>();
```

**Usage:**
```csharp
public class ContentService
{
    private readonly CosmosMemoryCache _cache;
    
    public async Task<Article> GetArticleAsync(int id)
    {
        var cacheKey = $"article_{id}";
        
        if (_cache.TryGetValue(cacheKey, out Article cached))
        {
            return cached;
        }
        
        var article = await _dbContext.Articles.FindAsync(id);
        
        // Cache for 10 minutes
        _cache.Set(cacheKey, article, TimeSpan.FromMinutes(10));
        
        return article;
    }
}
```

### Caching Strategies

**Templates Caching:**
- Duration: 1 hour
- Invalidation: On template update/delete
- Key pattern: `template_{id}`

**Configuration Values:**
- Duration: 10 seconds (DynamicConfigurationProvider)
- Invalidation: Time-based expiration
- Key pattern: `config_{domain}_{key}`

**Article Listings:**
- Duration: 5 minutes
- Invalidation: On article publish/unpublish
- Key pattern: `articles_list_{page}_{pageSize}`

**Navigation Menus:**
- Duration: 30 minutes
- Invalidation: On menu structure change
- Key pattern: `menu_{location}`

**Cache Invalidation Example:**
```csharp
public async Task UpdateArticleAsync(Article article)
{
    await _dbContext.SaveChangesAsync();
    
    // Invalidate related caches
    _cache.Remove($"article_{article.Id}");
    _cache.Remove("articles_list");
    _cache.Remove("menu_main");
}
```

**Memory Pressure Management:**
```csharp
// Configure cache size limits
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024; // 1024 items max
    options.CompactionPercentage = 0.25; // Remove 25% when limit reached
});
```

## Power BI Integration

SkyCMS supports embedding Power BI reports and dashboards for analytics and reporting.

### PowerBiTokenRequest

**Configuration:**
```json
{
  "PowerBi": {
    "ClientId": "your-azure-ad-app-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id",
    "WorkspaceId": "your-workspace-id"
  }
}
```

**Usage:**
```csharp
using Cosmos.Common.Models;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;

public class PowerBiService
{
    public async Task<string> GetEmbedTokenAsync(string reportId)
    {
        var tokenRequest = new PowerBiTokenRequest
        {
            ReportId = reportId,
            AccessLevel = "View",
            AllowEdit = false
        };
        
        // Authenticate with Azure AD
        var authToken = await GetAzureAdTokenAsync();
        
        // Get Power BI embed token
        using var client = new PowerBIClient(new Uri("https://api.powerbi.com"), 
            new TokenCredentials(authToken));
        
        var embedToken = await client.Reports.GenerateTokenAsync(
            new Guid(tokenRequest.ReportId),
            new GenerateTokenRequest(accessLevel: "View"));
        
        return embedToken.Token;
    }
}
```

**Embed in Razor View:**
```html
<div id="powerbi-report" style="height:600px;"></div>

<script src="https://cdn.jsdelivr.net/npm/powerbi-client@2.x/dist/powerbi.min.js"></script>
<script>
    const embedConfig = {
        type: 'report',
        tokenType: models.TokenType.Embed,
        accessToken: '@Model.EmbedToken',
        embedUrl: 'https://app.powerbi.com/reportEmbed',
        id: '@Model.ReportId',
        settings: {
            panes: {
                filters: { expanded: false, visible: false }
            }
        }
    };
    
    const reportContainer = document.getElementById('powerbi-report');
    const report = powerbi.embed(reportContainer, embedConfig);
</script>
```

## License

Licensed under the MIT License. See the LICENSE file for details.

## Contributing

This project is part of the SkyCMS ecosystem. For contribution guidelines and more information, visit the [SkyCMS GitHub repository](https://github.com/CWALabs/SkyCMS).

