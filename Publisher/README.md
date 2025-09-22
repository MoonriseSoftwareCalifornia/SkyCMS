# SkyCMS Publisher

The SkyCMS Publisher is the public-facing web application component of the SkyCMS system that serves content to end users. It operates in multiple modes to provide flexible deployment options for different performance and architectural requirements.

## Overview

The Publisher application is responsible for rendering and delivering content created in the SkyCMS Editor to website visitors. It's designed to be highly performant, scalable, and configurable to meet various deployment scenarios.

## Architecture Modes

The Publisher operates in two primary modes, determined by the `CosmosStaticWebPages` configuration setting:

### 1. Dynamic Publisher Mode (Default)

- **Full CMS functionality** with server-side rendering
- **Database-driven content** from Azure Cosmos DB or SQLite
- **User authentication and authorization** support
- **Dynamic content generation** with real-time updates
- **Interactive features** like comments, forms, and user sessions

### 2. Static Website Proxy Mode

- **High-performance static content** delivery
- **Blob storage integration** for static file serving
- **CDN-optimized** for global content distribution
- **Minimal server overhead** for maximum scalability
- **Automatic static site generation** from CMS content

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
| `ConnectionStrings__ApplicationDbContextConnection` | Database connection | `Data Source=/data/sqlite/skycms.db` |
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
  -e ConnectionStrings__ApplicationDbContextConnection="Data Source=/data/sqlite/skycms.db" \
  toiyabe/sky-publisher:latest
```

### Azure App Service Deployment

1. **Create App Service** with Linux container support
2. **Configure container** to use `toiyabe/sky-publisher:latest`
3. **Set environment variables** in Application Settings
4. **Configure storage mount** for SQLite database (if using)
5. **Enable Application Insights** for monitoring

### Local Development

```bash
# Clone the repository
git clone https://github.com/MoonriseSoftwareCalifornia/SkyCMS.git
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

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork the repository** and create a feature branch
2. **Follow coding standards** using the included StyleCop rules
3. **Add unit tests** for new functionality
4. **Update documentation** as needed
5. **Submit a pull request** with a clear description

## License

This project is licensed under the GNU General Public License v3.0. See the [LICENSE.md](../LICENSE.md) file for details.

## Support

- **Documentation**: [sky.moonrise.net/docs](https://sky.moonrise.net/docs)
- **Community Support**: [SkyCMS Slack](https://sky-cms.slack.com/)
- **Issues**: [GitHub Issues](https://github.com/MoonriseSoftwareCalifornia/SkyCMS/issues)
- **Professional Support**: Available through [Moonrise Software](https://moonrise.net)

---

**Copyright (c) 2024 Moonrise Software, LLC. All rights reserved.**
