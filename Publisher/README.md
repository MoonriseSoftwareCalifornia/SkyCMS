# Sky.Publisher — Developer README

Purpose
- Publisher project responsible for serving published content and static proxy endpoints used in production delivery.

Quick start (local)
```powershell
dotnet build SkyCMS.sln
dotnet run --project Publisher
```

Where to look
- Entry point: `Publisher/Program.cs`.
- Static proxy and routing controllers: `Publisher/Controllers`.
- Refer to `Editor` for how content is produced and `Cosmos.*` projects for storage patterns.

Tests
- Run tests with `dotnet test SkyCMS.sln` or target specific test projects.

Notes & conventions
- Avoid changing public-facing routes without tests and coordination.
- Follow DI and scoped service conventions used across the solution.
- Ask before editing `.github/workflows/*` or release pipelines.
# SkyCMS Publisher

The SkyCMS Publisher is the public-facing web application component of the SkyCMS system that **renders and serves complete web pages** to end users. It operates in multiple modes to provide flexible deployment options for different performance and architectural requirements.

## Overview

**The Publisher application's primary function is to render complete HTML pages**, either dynamically through server-side processing or by serving pre-generated static HTML files from cloud storage (Azure Blob Storage, AWS S3, or Cloudflare R2). This is fundamentally different from headless CMS architectures that rely on API endpoints to deliver content fragments.

The Publisher is designed to be highly performant, scalable, and configurable to meet various deployment scenarios.

### Advantages Over Traditional Git-Based Static Site Deployment

The Publisher component represents a **revolutionary improvement** over traditional static site deployment workflows (GitHub Pages, Netlify, Vercel, etc.):

**Traditional Git-Based Workflow Pain Points:**
- Content editors must understand Git workflows
- Separate CI/CD pipeline configuration required
- External static site generators (Jekyll, Hugo, Gatsby) needed
- Build time delays (minutes per deployment)
- Multiple tools and services to integrate
- Complex troubleshooting across pipeline stages
- Choose either static OR dynamic—not both

**SkyCMS Publisher Solution:**
- **No Git Knowledge Required**: Content editors use intuitive CMS interface
- **No CI/CD Pipeline**: Built-in automatic triggers and rendering
- **No External Generators**: Direct rendering without Jekyll, Hugo, or Gatsby
- **Instant Publishing**: Changes go live immediately without build delays
- **Single Integrated Platform**: Everything in one system
- **Simplified Operations**: Fewer moving parts and failure points
- **Hybrid Architecture**: Serve static files AND dynamic content simultaneously

**Technical Superiority:**
- The Publisher acts as both the build system and deployment mechanism
- Tight integration eliminates the complexity of JAMstack toolchains
- Achieves the same benefits (speed, scalability, CDN distribution) without the overhead
- Direct deployment to cloud storage without intermediary services
- Version control built into the CMS—no external repository needed

## Architecture Modes

The Publisher operates in two primary modes, determined by the `CosmosStaticWebPages` configuration setting:

### 1. Dynamic Publisher Mode (Server-Side Rendering)

**Traditional CMS functionality with full page rendering:**

- **Server-side HTML generation** - Complete pages rendered on each request
- **Database-driven content** from Azure Cosmos DB, SQL Server, MySQL
- **User authentication and authorization** support
- **Real-time content** with dynamic data integration
- **Interactive features** like comments, forms, and user sessions
- **Personalized content** based on user context

**When to use:** Dynamic content requirements, user-specific pages, real-time data integration

### 2. Static Website Proxy Mode (Static Site Generation)

**High-performance pre-rendered pages:**

- **Pre-generated HTML files** stored in cloud storage (Azure Blob, S3, Cloudflare R2)
- **Blazing-fast delivery** - Pages served directly from CDN/edge locations
- **Minimal server overhead** - Publisher acts as a smart proxy to storage
- **Maximum scalability** - Handle massive traffic with minimal infrastructure
- **Automatic static site generation** triggered by content publishing in Editor
- **Edge hosting support** - Deploy origin-less sites with Cloudflare R2 + Rules (no Worker required)

**When to use:** Public-facing websites, high-traffic sites, edge hosting, maximum performance requirements

## Content Delivery Philosophy

### Page Rendering First, API Optional

SkyCMS is built on the principle that **most websites are best served as complete HTML pages** rather than through API-driven frontend frameworks. This approach provides:

**Better Performance**: Static HTML from CDN beats API calls
**Simpler Architecture**: No separate frontend application needed
**SEO Benefits**: Server-rendered HTML works perfectly with search engines
**Lower Costs**: Minimal compute requirements, especially in static mode
**Faster Development**: Standard web development skills (HTML/CSS/JS)
**Better Reliability**: Fewer dependencies and points of failure

### Optional API Endpoints

While the Publisher does expose API endpoints (e.g., `/pub/` routes for file access), these are:

- **Supporting infrastructure** for asset delivery, not primary content delivery
- **Optional capabilities** for specific use cases (mobile apps, integrations)
- **Not the primary architecture** - pages are the primary output

This contrasts with headless CMS systems where APIs are the primary (or only) way to deliver content.

## Technology Stack

### Runtime
- **.NET 9.0** - Latest ASP.NET Core framework
- **Docker containerized** - Linux-based deployment
- **Azure-native** - Optimized for Azure App Services

### Dependencies
- **Azure Cosmos DB** - Primary database for content storage
- **Azure Blob Storage** - Static asset and file storage
- **Azure Application Insights** - Telemetry and monitoring
- **ASP.NET Core Identity** - User authentication framework

### Key Libraries
- **Cosmos.EmailServices** - Email delivery services
- **Cosmos.MicrosoftGraph** - Microsoft Graph integration
- **Microsoft.Extensions.Caching.Cosmos** - Distributed caching
- **Newtonsoft.Json** - JSON serialization

## Project Structure

```text
Publisher/
├── Boot/                          # Application bootstrapping
│   ├── DynamicPublisherWebsite.cs # Dynamic mode configuration
│   └── StaticWebsiteProxy.cs      # Static mode configuration
├── Controllers/                   # MVC Controllers
│   ├── HomeController.cs          # Main content controller
│   ├── PubController.cs           # Publication controller
│   └── StaticProxyController.cs   # Static proxy handling
├── Models/                        # Data models
│   ├── ApiResult.cs               # API response models
│   ├── ErrorViewModel.cs          # Error handling models
│   ├── FileCacheObject.cs         # File caching models
│   └── InputVarDefinition.cs      # Input variable definitions
├── Views/                         # Razor view templates
├── wwwroot/                       # Static web assets
├── Areas/Identity/                # Authentication views
├── appsettings.json              # Configuration
├── Dockerfile                    # Container definition
└── Sky.Publisher.csproj          # Project file
```

## Key Features

### Content Delivery
- **SEO-optimized** HTML output with meta tags and structured data
- **Responsive design** support for mobile and desktop
- **Fast loading times** through optimized caching strategies
- **Content versioning** with publish/unpublish workflows

### Performance Optimization
- **Memory caching** for frequently accessed content
- **Distributed caching** using Azure Cosmos DB
- **Static file optimization** through Azure Blob Storage
- **Rate limiting** to prevent abuse and ensure stability

### Security & Authentication
- **OAuth integration** with Google and Microsoft accounts
- **Azure B2C** support for enterprise authentication
- **CORS configuration** for cross-origin requests
- **Anti-forgery tokens** for CSRF protection
- **HTTPS enforcement** with security headers

### Monitoring & Diagnostics
- **Application Insights** integration for telemetry
- **Structured logging** for debugging and monitoring
- **Health checks** for system status monitoring
- **Error handling** with graceful degradation

## Configuration

### Required Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `CosmosStaticWebPages` | Enable static mode | `true` or `false` |
| `ConnectionStrings__ApplicationDbContextConnection` | Database connection | `AccountEndpoint=...;AccountKey=...;Database=...` |
| `ConnectionStrings__StorageConnectionString` | Azure Storage connection | `DefaultEndpointsProtocol=https;...` |

### Optional Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `CosmosPublisherUrl` | Publisher URL | Auto-detected |
| `CorsAllowedOrigins` | CORS origins | All origins |
| `CosmosIdentityDbName` | Database name | `cosmoscms` |
| `GoogleOAuth__ClientId` | Google OAuth client | Not configured |
| `MicrosoftOAuth__ClientId` | Microsoft OAuth client | Not configured |

## Deployment

### Docker Deployment (Recommended)

```bash
# Pull the latest image
docker pull toiyabe/sky-publisher:latest

# Run with basic configuration
docker run -d \
  -p 8080:8080 \
  -e CosmosStaticWebPages=false \
  -e ConnectionStrings__ApplicationDbContextConnection="AccountEndpoint=...;AccountKey=...;Database=..." \
  toiyabe/sky-publisher:latest
```

### Azure App Service Deployment

1. **Create App Service** with Linux container support
2. **Configure container** to use `toiyabe/sky-publisher:latest`
3. **Set environment variables** in Application Settings
4. **Enable Application Insights** for monitoring

### Local Development

```bash
# Clone the repository
git clone https://github.com/CWALabs/SkyCMS.git
cd SkyCMS/Publisher

# Restore dependencies
dotnet restore

# Run the application
dotnet run
```

## Content Management Integration

### Editor Integration
- **Seamless content sync** from SkyCMS Editor
- **Real-time updates** when content is published
- **Version management** with rollback capabilities
- **Preview functionality** before publishing

### Static Site Generation
- **Automatic generation** of static HTML files
- **Asset optimization** and compression
- **CDN integration** for global distribution
- **Incremental updates** for changed content only

## API Endpoints

### Public Endpoints
- `GET /` - Home page with latest content
- `GET /{article-path}` - Individual article pages
- `GET /pub/{path}` - Public API for content access
- `GET /ccms__antiforgery/token` - CSRF token for forms

### Administrative Endpoints
- `GET /Identity/Account/Login` - User authentication
- `GET /Identity/Account/Register` - User registration
- `POST /Identity/Account/Logout` - User logout

## Performance Characteristics

### Response Times
- **Static mode**: ~50ms average response time
- **Dynamic mode**: ~200ms average response time
- **Cache hit ratio**: >95% for frequently accessed content

### Scalability
- **Horizontal scaling** through multiple container instances
- **Auto-scaling** based on CPU and memory usage
- **Load balancing** across multiple regions
- **Database scaling** through Azure Cosmos DB

## Monitoring and Troubleshooting

### Health Monitoring
```bash
# Check application health
curl https://your-publisher.azurewebsites.net/Identity/Account/Login

# Monitor application insights
# Navigate to Azure portal > Application Insights > your-app
```

### Common Issues

1. **Database Connection Issues**
   - Verify connection strings in app settings
   - Check Azure Cosmos DB firewall rules
   - Validate database permissions

2. **Static File Serving Problems**
   - Confirm Azure Storage account configuration
   - Verify blob container permissions
   - Check CORS settings on storage account

3. **Authentication Failures**
   - Validate OAuth provider configurations
   - Check redirect URIs in Azure/Google console
   - Verify SSL certificate configuration

### Logging
Application logs are available through:
- **Azure App Service Logs** (Log Stream)
- **Application Insights** (Traces and Exceptions)
- **Docker container logs** (`docker logs <container-id>`)

## Security Considerations

### Data Protection
- **Connection string encryption** using Azure Key Vault
- **Data protection keys** stored in Azure Blob Storage
- **Secure cookie** configuration with HTTPS only
- **SQL injection protection** through Entity Framework

### Access Control
- **Role-based authentication** with ASP.NET Core Identity
- **OAuth integration** for secure third-party authentication
- **Rate limiting** to prevent denial-of-service attacks
- **CORS policy** configuration for API access

## Development

### Prerequisites
- **.NET 9.0 SDK** or later
- **Docker Desktop** (optional, for containerization)
- **Azure Storage Emulator** or Azure Storage account
- **Visual Studio 2022** or **VS Code** (recommended)

### Building from Source
```bash
# Build the project
dotnet build Sky.Publisher.csproj

# Run tests (if available)
dotnet test

# Publish for deployment
dotnet publish -c Release -o ./publish
```

### Debugging
- Use **Visual Studio debugger** with container support
- Enable **detailed error pages** in development
- Configure **logging levels** for troubleshooting
- Use **Azure Application Insights** for production debugging

## CDN Integration

The Publisher application works seamlessly with CDN providers to deliver cached content globally while supporting automatic cache invalidation triggered by the Editor.

### How It Works

1. **Editor publishes content** → Triggers CDN purge via configured provider API
2. **CDN receives purge request** → Invalidates cached copies at edge locations
3. **Next visitor request** → CDN fetches fresh content from Publisher origin
4. **Subsequent requests** → Served from CDN edge cache (fast delivery)

### Supported CDN Providers

- **Azure CDN** - Integrated with Azure Front Door and CDN profiles
- **Cloudflare** - Full integration with zones and purge APIs
- **Sucuri** - WAF-integrated CDN with security features
- **Generic CDN** - Works with any CDN supporting origin pull

### Configuration Requirements

Publisher must be accessible as CDN origin:

**Azure CDN:**
```json
{
  "CdnConfig": {
    "OriginUrl": "https://your-publisher.azurewebsites.net",
    "EnableCaching": true,
    "CacheDuration": "1.00:00:00"
  }
}
```

**Cloudflare:**
- Point DNS to Cloudflare nameservers
- Configure SSL/TLS mode (Full or Full Strict recommended)
- Set cache level: Standard or Aggressive
- Configure page rules for dynamic content exclusions

**Publisher Response Headers:**
```csharp
// In Program.cs - Configure caching headers
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "public, max-age=3600";
        context.Response.Headers.Vary = "Accept-Encoding";
    }
    await next();
});
```

### Cache Control Strategy

**Static Assets** (CSS, JS, images):
- Cache-Control: `public, max-age=31536000, immutable`
- Versioning: Use query strings or path versioning (`/css/style.v123.css`)

**HTML Pages:**
- Cache-Control: `public, max-age=3600` (1 hour)
- Purged automatically on publish via Editor

**Dynamic Content** (search, forms):
- Cache-Control: `no-cache` or `private`
- Excluded from CDN caching

### Health Checks

**Endpoint:** `GET /health`

**Response:**
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "storage": "Healthy"
  },
  "totalDuration": "00:00:00.0523"
}
```

Use for CDN origin health monitoring and load balancer probes.

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork the repository** and create a feature branch
2. **Follow coding standards** using the included StyleCop rules
3. **Add unit tests** for new functionality
4. **Update documentation** as needed
5. **Submit a pull request** with a clear description

## License

This project is licensed under the GNU General Public License v2.0-or-later (GPL-2.0-or-later). See the [LICENSE-GPL](../LICENSE-GPL) and [LICENSE-MIT](../LICENSE-MIT) files for details. For complete third-party attribution, see [NOTICE.md](../NOTICE.md).

## Support

- **Documentation**: [sky.moonrise.net/docs](https://sky.moonrise.net/docs)
- **Community Support**: [SkyCMS Slack](https://sky-cms.slack.com/)
- **Issues**: [GitHub Issues](https://github.com/CWALabs/SkyCMS/issues)
- **Professional Support**: Available through [Moonrise Software](https://moonrise.net)

---

## See Also

**Configuration Guides:**
- [Storage Configuration](https://cwalabs.github.io/SkyCMS/StorageConfig.html) - Configure cloud storage providers
- [Database Configuration](https://cwalabs.github.io/SkyCMS/DatabaseConfig.html) - Set up database connections
- [Cloudflare Edge Hosting](https://cwalabs.github.io/SkyCMS/CloudflareEdgeHosting.html) - Origin-less static hosting

**Related Components:**
- [Editor Application](../Editor/README.md) - Content management interface
- [Cosmos.Common](../Common/README.md) - Shared core library
- [Cosmos.BlobService](../Cosmos.BlobService/README.md) - Storage abstraction
- [Dynamic Configuration](../Cosmos.ConnectionStrings/README.md) - Multi-tenant support

**Documentation:**
- [Main Documentation Hub](https://cwalabs.github.io/SkyCMS/README.html) - Complete documentation index
- [Azure Installation](https://cwalabs.github.io/SkyCMS/AzureInstall.html) - Deploy to Azure
- [Quick Start Guide](https://cwalabs.github.io/SkyCMS/QuickStart.html) - Get started quickly

---

**Copyright (c) 2025 Moonrise Software, LLC. All rights reserved.**
