# Sky.Editor — Developer README

Purpose
- The `Editor` project contains the web-based editor UI and related backend components used during content editing.

Quick start (local)
```powershell
dotnet build SkyCMS.sln
dotnet run --project Editor\Sky.Editor.csproj
```

Where to look
- Static assets and third-party frontend libraries: `Editor/wwwroot/lib`.
- Frontend package manifest: `Editor/package.json`.
- Key controllers and boot code: `Editor/Program.cs`, `Editor/Boot` and `Editor/Controllers`.

Developer tips
- Respect `Editor/stylecop.json` and the repository `Directory.Packages.props` versions.
- Prefer using existing frontend libraries present under `Editor/wwwroot/lib` rather than adding new frameworks.
- When making server-side changes, add unit tests in the matching `Tests` project and run `dotnet test`.
- To refresh the checked-in CKEditor 5 assets from the sibling `ckeditor5` repository, use the workflow in `Editor/CKEDITOR_SYNC.md` and the script `Scripts/sync-ckeditor5-to-editor.ps1`.
# SkyCMS Editor

The SkyCMS Editor is the authoring & administration application of the SkyCMS platform. It provides multiple authoring modes (visual, rich‑text, and code), asset & permission management, versioning, and publishing workflows targeting the SkyCMS Publisher (dynamic, static, headless, or decoupled modes).

![Editor UI](./wwwroot/images/skycms/SkyCMSLogoNoWiTextDarkTransparent30h.png)

## At a Glance

| Capability | Description |
|------------|-------------|
| Authoring Modes | CKEditor 5 (WYSIWYG), GrapesJS (visual layout / drag & drop), Monaco (code / HTML / diff) |
| Content Lifecycle | Draft → Versioned → Published (with revert & history) |
| Media & Assets | Integrated File Manager, banner image picker, per‑page asset folders (`pub/articles/{id}`) |
| Permissions | Role & user level access; publishing requires permissions when authentication enforced |
| Autosave & Recovery | Local autosave with recovery modal on failure |
| Page Metadata | Title / URL path change dialog with validation |
| Realtime Helpers | SignalR (live status / potential for collaborative extensions) |
| Storage Abstraction | Multi‑cloud blob (Azure Blob, S3, Cloudflare R2) via `Cosmos.BlobService` |
| Database Providers | Cosmos DB, SQL Server, MySQL (.NET 9 / EF Core) |
| Security | ASP.NET Core Identity / external providers (shared with Publisher) |
| Extensibility | Domain events, pluggable UI actions, utility & logic layer separation |

## Technology Stack

- Runtime: **.NET 9**, ASP.NET Core **Razor Pages / MVC hybrid**
- UI: Bootstrap 5, CKEditor 5, GrapesJS, Monaco, FilePond, Filerobot Image Editor, Font Awesome
- Realtime: SignalR
- Data: EF Core multi-provider (Cosmos DB, SQL Server, MySQL)
- Storage: Abstraction over Azure Blob / S3 / R2
- Auth: ASP.NET Core Identity (roles: *Authors*, *Reviewers*, *Administrators*, etc.)

## Project Structure (High-Level)

```text
Editor/
├── Boot/                      # Application startup configurations
│   ├── MultiTenant.cs        # Multi-tenant mode configuration
│   └── SingleTenant.cs       # Single-tenant mode configuration
├── Controllers/              # MVC Controllers
│   ├── EditorController.cs   # Main content editing controller
│   ├── FileManagerController.cs  # File management operations
│   ├── TemplatesController.cs    # Template management
│   └── ...
├── Data/                     # Data layer and business logic
│   ├── Logic/               # Business logic services
│   └── ApplicationDbContext.cs
├── Models/                   # View models and data models
├── Services/                 # Application services
├── Views/                    # Razor views and templates
└── wwwroot/                  # Static assets and libraries
```

### Core Components

#### EditorController

The main controller handling content editing operations:

- **Article Management**: Create, edit, delete, and version articles
- **Content Editing**: Support for HTML, code, and visual editing modes
- **Publishing Workflow**: Draft management and content publishing
- **Collaboration Tools**: Real-time editing and commenting features

#### Multi-Tenancy Support

- **Single Tenant Mode**: Traditional CMS setup for individual websites
- **Multi-Tenant Mode**: Shared editor serving multiple client websites
- **Configuration Management**: Dynamic settings per tenant

#### Content Editing Tools Integration

##### CKEditor 5

- Rich text WYSIWYG editing
- Custom plugins for SkyCMS functionality
- Auto-save capabilities
- Media embedding and link management

##### GrapesJS

- Visual web page builder
- Drag-and-drop interface
- Component-based design
- CSS editing and styling tools

##### Monaco Editor (VS Code)

- Advanced code editing with syntax highlighting
- IntelliSense and auto-completion
- Diff tools for version comparison
- Emmet support for faster HTML/CSS editing

##### Filerobot Image Editor

- Integrated image editing capabilities
- Crop, resize, and filter operations
- Direct integration with file management

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- Storage: Azure Blob or Amazon S3 (see StorageConfig)
- Database: Azure SQL / SQL Server / Cosmos DB / MySQL
- Node.js (for frontend dependencies)

### Installation

1. **Clone and Navigate**

   ```bash
   git clone https://github.com/CWALabs/SkyCMS.git
   cd SkyCMS/Editor
   ```

2. **Install Dependencies**

   ```bash
   # .NET packages
   dotnet restore
   
   # Frontend dependencies
   npm install
   ```

3. **Configure Settings**

    Update `appsettings.json` (local quick start shown):

    ```json
    {
       "ConnectionStrings": {
          "ApplicationDbContextConnection": "Data Source=skycms.db", 
          "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=yourkey;EndpointSuffix=core.windows.net",
          "BackupStorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=backupacct;AccountKey=...;EndpointSuffix=core.windows.net"
       },
       "CosmosPublisherUrl": "https://your-publisher-url.com",
       "MultiTenantEditor": false
    }
    ```

    Notes:
    - For S3, use: `Bucket={bucket};Region={region};KeyId={access-key-id};Key={secret};`
    - For Azure managed identity, use: `AccountKey=AccessToken` and assign Blob Data roles to the app identity.

4. **Initialize Database**

   For relational providers (SQL Server/MySQL), run EF migrations:

   ```bash
   dotnet ef database update
   ```

5. **Run the Application**

   ```bash
   dotnet run
   ```

   The Editor will be available at `https://localhost:5001`

### Single-tenant setup wizard (new)

Use the built-in setup wizard for single-tenant deployments only (`MultiTenantEditor=false`).

- Prereqs: set `CosmosAllowSetup=true` in `appsettings.json` or environment variables and ensure `ConnectionStrings:ApplicationDbContextConnection` points to a reachable database (the wizard validates this on start). Keep this flag **false** for multi-tenant installs and use DynamicConfig instead.
- Start the wizard at `/Setup` (the app redirects there if `CosmosAllowSetup` is enabled and setup is incomplete).
- Steps: Welcome → Storage (Azure Blob / S3 / R2 + CDN/public URL) → Admin account (email + strong password) → Publisher (site URL, title, layout, auth toggle, allowed file types) → Email (SendGrid / Azure Communication Services / SMTP, optional test send) → CDN (Azure CDN/Front Door, Cloudflare, or Sucuri; optional) → Review & Complete.
- Completion: wizard writes settings to the database, disables itself, and requires an app restart; credentials entered here become the first Administrator account.
- Single-tenant only: multi-tenant editors must leave `CosmosAllowSetup=false` and configure tenants through `Cosmos.ConnectionStrings`.

## Configuration

### Single Tenant vs Multi-Tenant

#### Single Tenant Mode

- Configuration via `appsettings.json`
- Direct database connection
- Simpler setup for individual websites

#### Multi-Tenant Mode

- Dynamic configuration per tenant
- Shared application with tenant isolation
- Requires DynamicConfig (Cosmos.ConnectionStrings)

Register in Program.cs:

```csharp
using Cosmos.DynamicConfig;

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IDynamicConfigurationProvider, DynamicConfigurationProvider>();

var app = builder.Build();
app.UseMiddleware<DomainMiddleware>();
```

And add the config database connection:

```json
{
   "ConnectionStrings": {
      "ConfigDbConnectionString": "AccountEndpoint=...;AccountKey=...;Database=SkyCmsConfig" 
   }
}
```

### Key Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `MultiTenantEditor` | Enable multi-tenant mode | false |
| `CosmosPublisherUrl` | Publisher application URL | Required |
| `StorageConnectionString` | Storage provider connection (Azure/S3) | Required |
| `BackupStorageConnectionString` | Optional backup storage for periodic snapshots | Optional |
| `AzureBlobStorageEndPoint` | Static file storage URL (legacy) | "/" |
| `CosmosRequiresAuthentication` | Publisher authentication | false |
| `CosmosStaticWebPages` | Static site generation | true |
| `CosmosAllowSetup` | Enable single-tenant setup wizard | false |

### Storage Provider Selection

Provider inferred by connection string pattern. Public URL must match actual CDN/edge origin for correct asset resolution in editor previews.

## CDN Integration

SkyCMS Editor supports Content Delivery Network (CDN) integration to automatically purge cached content after publishing changes, ensuring visitors see updated content immediately.

### Supported CDN Providers

- **Azure CDN** - Microsoft's global CDN service
- **Cloudflare** - Global CDN with edge computing capabilities
- **Sucuri** - Security-focused CDN with WAF protection

### Configuration

CDN settings are stored in the application database and configured through the Editor settings interface:

1. Navigate to **Settings → CDN Configuration**
2. Select your CDN provider
3. Enter provider-specific credentials:

**Sucuri Configuration:**
```json
{
  "CdnProvider": "Sucuri",
  "Value": "{\"ApiKey\":\"your-api-key\",\"ApiSecret\":\"your-api-secret\"}"
}
```

**Cloudflare Configuration:**
```json
{
  "CdnProvider": "Cloudflare",
  "Value": "{\"ZoneId\":\"your-zone-id\",\"ApiToken\":\"your-api-token\"}"
}
```

**Azure CDN Configuration:**
```json
{
  "CdnProvider": "AzureCDN",
  "Value": "{\"ProfileName\":\"your-profile\",\"EndpointName\":\"your-endpoint\",\"ResourceGroup\":\"your-rg\",\"SubscriptionId\":\"your-sub-id\"}"
}
```

### Automatic Purge Workflows

CDN purging is automatically triggered when:

- **Publishing pages** - Purges the specific page URL
- **Updating redirects** - Purges affected URL paths
- **Changing templates** - Purges all pages using the template
- **Modifying layouts** - Purges all pages using the layout

### URL Count Limits and Behaviors

**Sucuri CDN:**
- **1-20 URLs**: Purges each URL individually
- **0 URLs, >20 URLs, or root path `/`**: Triggers full cache purge
- **Rate Limits**: Subject to Sucuri API rate limits (failures are logged)

**Cloudflare:**
- **Up to 30 files per request**: Batches URLs efficiently
- **Wildcards supported**: Can purge patterns like `/blog/*`
- **Enterprise plans**: Support for tag-based and hostname purging

**Azure CDN:**
- Purges specific paths or entire endpoint
- Propagation time: 2-10 minutes depending on CDN tier

### Error Handling

CDN purge failures are logged but do not block publishing operations:

- **Network failures**: Retried once, then logged as warning
- **Authentication errors**: Logged with provider details for troubleshooting
- **Rate limit exceeded**: Logged with retry guidance
- **Invalid configuration**: Logged with configuration validation details

Check application logs for CDN operation results:

```powershell
# View CDN logs in Azure App Service
az webapp log tail --name your-app-name --resource-group your-rg --filter "CDN"
```

### Health Checks

Editor includes health check endpoints for monitoring:

**Endpoint:** `GET /health`

**Response Format:**
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "storage": "Healthy",
    "cdn": "Healthy"
  },
  "duration": "00:00:00.1234567"
}
```

**Register in Program.cs:**
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddCheck("storage", () => /* storage connectivity check */);

app.MapHealthChecks("/health");
```

## Troubleshooting

### Common Issues

**Database Connection Failures**
- Verify `ApplicationDbContextConnection` is set correctly
- For Cosmos DB: Check account endpoint and key
- For SQL Server: Verify server name and credentials
- For MySQL: Ensure port 3306 is accessible

**Storage Configuration Problems**
- Confirm `StorageConnectionString` format matches your provider
- Azure: Verify storage account name and key
- S3: Check bucket name, region, and access keys
- R2: Validate Account ID and API token

**Multi-Tenant Setup Issues**
- Ensure `ConfigDbConnectionString` is configured
- Verify `IDynamicConfigurationProvider` is registered
- Check that `DomainMiddleware` is added to pipeline
- Confirm tenant domains are configured in config database

**File Upload Errors**
- Check storage container/bucket exists
- Verify storage permissions (read/write/delete)
- For Azure managed identity: Assign "Storage Blob Data Contributor" role
- For S3: Ensure IAM policy allows required actions

**Authentication Problems**
- Verify external OAuth provider credentials (Google/Microsoft)
- Check redirect URIs match your application URL
- Ensure Identity database tables are created (run migrations)

### Getting Help

- Review [Storage Configuration](https://cwalabs.github.io/SkyCMS/StorageConfig.html) for detailed setup
- Check [Database Configuration](https://cwalabs.github.io/SkyCMS/DatabaseConfig.html) for provider-specific guidance
- Search [GitHub Issues](https://github.com/CWALabs/SkyCMS/issues)

---

## See Also

**Configuration Guides:**
- [Storage Configuration](https://cwalabs.github.io/SkyCMS/StorageConfig.html) - Azure Blob, S3, R2 setup
- [Database Configuration](https://cwalabs.github.io/SkyCMS/DatabaseConfig.html) - Cosmos DB, SQL Server, MySQL setup
- [Dynamic Configuration](../Cosmos.ConnectionStrings/README.md) - Multi-tenant configuration

**Related Components:**
- [Publisher Application](../Publisher/README.md) - Public-facing website component
- [Cosmos.Common](../Common/README.md) - Shared core library
- [Cosmos.BlobService](../Cosmos.BlobService/README.md) - Storage abstraction layer
- [AspNetCore.Identity.FlexDb](../AspNetCore.Identity.FlexDb/README.md) - Multi-database identity

**Documentation:**
- [Main Documentation Hub](https://cwalabs.github.io/SkyCMS/README.html) - Browse all documentation
- [Editor Services Documentation](./Services/README.md) - Service layer reference
- [Page Scheduling Guide](https://cwalabs.github.io/SkyCMS/Editors/PageScheduling.html) - Automated publishing


