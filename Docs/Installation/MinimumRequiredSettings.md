---
title: Minimum Required Settings
description: Environment variable configuration guide for Docker, Kubernetes, and production deployments
keywords: configuration, environment-variables, Docker, Kubernetes, production, settings
audience: [developers, devops, administrators]
version: 2.0
last_updated: "2026-01-03"
stage: stable
read_time: 8
---

# SkyCMS Minimum Required Settings

This guide covers **environment variable configuration** for SkyCMS. Use this when:
- Pre-configuring settings for Docker/Kubernetes deployments
- Setting up production environments
- Configuring multi-tenant installations
- Bypassing the Setup Wizard entirely

## When to use this
- You’re deploying to Docker/Kubernetes/production and want full env-var control.
- You need multi-tenant setup (no wizard) with DynamicConfig.
- You want to preconfigure everything and skip the wizard entirely.

## Why this matters
- Prevents runtime failures by declaring all required settings up front.
- Enables automation/CI and secure secret handling without UI steps.

## Key takeaways
- Wizard is single-tenant only; multi-tenant uses DynamicConfig with `CosmosAllowSetup=false`.
- `ApplicationDbContextConnection` (single) vs `ConfigDbConnectionString` + `DataProtectionStorage` (multi).
- Environment variables override appsettings and are preferred for production.

## Prerequisites
- Chosen tenancy model (single vs multi) and a reachable database/storage for that model.
- Access to set env vars/secrets in your runtime (Docker/K8s/cloud service).

## Quick path
1. Pick single vs multi-tenant and set the corresponding required keys.
2. Apply connection strings via env vars; start the app.
3. (Single) run `/___setup`; (Multi) configure tenants via DynamicConfig.

> **New to SkyCMS?** Consider using the **[Setup Wizard](./SetupWizard.md)** instead for an interactive configuration experience.

---

## Table of Contents

- [Quick Start](#quick-start)
  - [Single-Tenant (Easiest)](#single-tenant-easiest)
  - [Multi-Tenant](#multi-tenant)
- [Complete Reference](#complete-reference)
  - [Configuration File Locations](#configuration-file-locations)
  - [Single-Tenant Details](#single-tenant-details)
  - [Multi-Tenant Details](#multi-tenant-details)
  - [Connection String Formats](#connection-string-formats)
  - [Configuration Examples](#configuration-examples)
  - [Troubleshooting](#troubleshooting)

---

## Quick Start

### Single-Tenant (Easiest)

**For a single website with built-in setup wizard:**

```json
{
  "CosmosAllowSetup": true,
  "MultiTenantEditor": false,
  "ConnectionStrings": {
    "ApplicationDbContextConnection": "Data Source=D:\\data\\cosmoscms.db"
  }
}
```

Then navigate to `https://yourdomain.com/___setup` to configure database, storage, admin account, and other settings through the web interface.

**That's it!** After setup completes, the wizard disables itself and all settings move to your database.

### Multi-Tenant

**For multiple websites sharing one application:**

```json
{
  "MultiTenantEditor": true,
  "MultiTenantRedirectUrl": "https://your-landing-page.com",
  "ConnectionStrings": {
    "ConfigDbConnectionString": "AccountEndpoint=https://xxx.documents.azure.com:443/;AccountKey=xxx;Database=configs;",
    "DataProtectionStorage": "DefaultEndpointsProtocol=https;AccountName=xxx;AccountKey=xxx;EndpointSuffix=core.windows.net;"
  }
}
```

![Multi-tenant architecture showing central config database and per-tenant storage](../images/screenshots/multi-tenant-architecture.webp)

**Key difference**: No setup wizard. Instead:
- **ConfigDbConnectionString** = Central database that stores all tenant configurations (required)
- **DataProtectionStorage** = Azure Blob Storage for encryption keys (required, shared by all tenants)
- Each tenant's own database and storage settings are stored in the ConfigDb (you configure these separately)

---

## Complete Reference

### Configuration File Locations

### User Secrets (Development)

```plaintext
Windows:
C:\Users\{USERNAME}\AppData\Roaming\Microsoft\UserSecrets\aspnet-CDT.Cosmos.Cms-{GUID}\secrets.json

Linux/macOS:
~/.microsoft/usersecrets/aspnet-CDT.Cosmos.Cms-{GUID}/secrets.json
```

### AppSettings (Production)

Use `appsettings.json` or environment variables. Environment variables override `appsettings.json` values.

Format for environment variables: `ConnectionStrings__ApplicationDbContextConnection`

---

### Single-Tenant Details

| Setting | Required | Default | Description |
|---------|----------|---------|-------------|
| `CosmosAllowSetup` | **Yes** | `false` | Enable single-tenant setup wizard |
| `MultiTenantEditor` | No | `false` | Must be `false` or omitted |
| `ConnectionStrings:ApplicationDbContextConnection` | **Yes** | None | Your database connection |

**Setup wizard steps** (if enabled):
1. Database connection
2. Storage (Azure Blob, S3, or Cloudflare R2)
3. Administrator account
4. Publisher settings (URL, authentication, file types)
5. Email settings (optional)
6. CDN settings (optional)

After setup completes, settings move to database and wizard disables itself. Requires app restart.

---

### Multi-Tenant Details

**Architecture Overview:**

```
┌─────────────────────────────────────────┐
│  SkyCMS Editor (Single Instance)        │
├─────────────────────────────────────────┤
│ Points to:                              │
│  • ConfigDbConnectionString (Cosmos DB) │ ← Central config database
│  • DataProtectionStorage (Blob Storage) │ ← Shared encryption keys
└─────────────────────────────────────────┘
         │
         ├─→ Tenant 1 data (stored in ConfigDb)
         │    └─→ Database: mydb1
         │    └─→ Storage: mybucket1
         │
         ├─→ Tenant 2 data (stored in ConfigDb)
         │    └─→ Database: mydb2
         │    └─→ Storage: mybucket2
         │
         └─→ Tenant N...
```

| Setting | Required | Description |
|---------|----------|-------------|
| `MultiTenantEditor` | **Yes** | Set to `true` |
| `MultiTenantRedirectUrl` | **Yes** | Where users land if they hit root domain (not a tenant domain) |
| `ConnectionStrings:ConfigDbConnectionString` | **Yes** | **Central Cosmos DB** that stores all tenant configurations |
| `ConnectionStrings:DataProtectionStorage` | **Yes** | **Azure Blob Storage** for encryption keys (shared by all tenants) |

**Important**: No setup wizard in multi-tenant mode. Configure tenants through the admin interface.

---

### Connection String Formats

### Databases

**SQLite** (good for development):
```
Data Source=D:\data\cosmoscms.db
Data Source=D:\data\cosmoscms.db;Password=YourPassword;
```

**Azure Cosmos DB**:
```
AccountEndpoint=https://<account>.documents.azure.com:443/;AccountKey=<key>;Database=<database>;
```

**MySQL / Azure Database for MySQL**:
```
Server=<server>;Database=<database>;User=<user>;Password=<password>;
```

**SQL Server / Azure SQL Database**:
```
Server=<server>;Database=<database>;User=<user>;Password=<password>;
```

### Storage

**Azure Blob Storage**:
```
DefaultEndpointsProtocol=https;AccountName=<account>;AccountKey=<key>;EndpointSuffix=core.windows.net;
```

**AWS S3**:
```
Bucket=<bucket>;Region=<region>;KeyId=<access-key>;Key=<secret-key>;
```

**Cloudflare R2**:
```
AccountId=<account-id>;Bucket=<bucket>;KeyId=<access-key>;key=<secret-key>;
```

---

### Configuration Examples

**Example 1: Single-Tenant with SQLite + Setup Wizard**

Minimal config - everything else configured via the wizard:

```json
{
  "CosmosAllowSetup": true,
  "MultiTenantEditor": false,
  "ConnectionStrings": {
    "ApplicationDbContextConnection": "Data Source=D:\\data\\cosmoscms.db"
  }
}
```

Navigate to `https://yourdomain.com/___setup` after starting the app.

---

**Example 2: Single-Tenant with Pre-configured Settings**

Skip the wizard and configure everything up front:

```json
{
  "CosmosAllowSetup": false,
  "CosmosPublisherUrl": "https://www.mywebsite.com",
  "AzureBlobStorageEndPoint": "/",
  "CosmosStaticWebPages": true,
  "AdminEmail": "admin@mywebsite.com",
  "ConnectionStrings": {
    "ApplicationDbContextConnection": "AccountEndpoint=https://myaccount.documents.azure.com:443/;AccountKey=<key>;Database=cosmoscms;",
    "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=filesaccount;AccountKey=<key>;EndpointSuffix=core.windows.net;"
  }
}
```

After configuring, restart the app. The database and storage must exist and be accessible.

---

**Example 3: Multi-Tenant**

One SkyCMS instance serving multiple websites:

```json
{
  "MultiTenantEditor": true,
  "MultiTenantRedirectUrl": "https://www.my-saas-landing-page.com",
  "ConnectionStrings": {
    "ConfigDbConnectionString": "AccountEndpoint=https://multi-tenant-db.documents.azure.com:443/;AccountKey=<key>;Database=skycms-config;",
    "DataProtectionStorage": "DefaultEndpointsProtocol=https;AccountName=editorkeys;AccountKey=<key>;EndpointSuffix=core.windows.net;"
  }
}
```

Manage tenants through the admin interface. Each tenant's database and storage settings are stored in the ConfigDb.

---

## Email Configuration (Optional)

Email is **optional** in SkyCMS. Use it to send transactional emails like password resets and notifications.

### Setup Wizard Method (Recommended)

If `CosmosAllowSetup=true`, the setup wizard includes **Step 4: Email Configuration**:

1. Choose a provider: **SendGrid**, **Azure Communication Services**, **SMTP**, or **None** (skip email)
2. Enter provider-specific credentials
3. Test the configuration
4. Complete setup

### Pre-Configuration Method (Manual)

Configure email **before** starting the application via environment variables or `appsettings.json`.

#### SMTP Example (Gmail, Office 365, etc.)

**Environment Variables** (Windows PowerShell):

```powershell
$env:SmtpEmailProviderOptions__Host = "smtp.gmail.com"
$env:SmtpEmailProviderOptions__Port = "587"
$env:SmtpEmailProviderOptions__UserName = "your-email@gmail.com"
$env:SmtpEmailProviderOptions__Password = "your-app-password"
$env:AdminEmail = "your-email@gmail.com"
```

**appsettings.json**:

```json
{
  "SmtpEmailProviderOptions": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UserName": "your-email@gmail.com",
    "Password": "your-app-password",
    "UsesSsl": false
  },
  "AdminEmail": "your-email@gmail.com"
}
```

#### SendGrid Example

**Environment Variables**:

```powershell
$env:CosmosSendGridApiKey = "SG.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
$env:AdminEmail = "noreply@yourdomain.com"
```

**appsettings.json**:

```json
{
  "CosmosSendGridApiKey": "SG.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "AdminEmail": "noreply@yourdomain.com"
}
```

#### Azure Communication Services Example

**Environment Variables**:

```powershell
$env:ConnectionStrings__AzureCommunicationConnection = "endpoint=https://yourresource.communication.azure.com/;accesskey=xxxxx=="
$env:AdminEmail = "DoNotReply@yourdomain.azurecomm.net"
```

**appsettings.json**:

```json
{
  "ConnectionStrings": {
    "AzureCommunicationConnection": "endpoint=https://yourresource.communication.azure.com/;accesskey=xxxxx=="
  },
  "AdminEmail": "DoNotReply@yourdomain.azurecomm.net"
}
```

### Provider Priority

If multiple providers are configured, SkyCMS uses this order:

1. **SMTP** (if all fields present)
2. **Azure Communication Services** (if connection string set)
3. **SendGrid** (if API key set)
4. **No-Op** (development only, emails logged but not sent)

### Configuration Reference

Complete email configuration documentation:

- **[Email Overview](../Configuration/Email-Overview.md)** - Provider comparison and setup methods
- **[Email Configuration Reference](../Configuration/Email-Configuration-Reference.md)** - All settings and formats
- **[Email SendGrid Setup](../Configuration/Email-SendGrid.md)** - SendGrid-specific guide
- **[Email Azure Communication Setup](../Configuration/Email-AzureCommunicationServices.md)** - Azure setup
- **[Email SMTP Setup](../Configuration/Email-SMTP.md)** - SMTP provider guide (Gmail, Office 365, etc.)
- **[Email No-Op Setup](../Configuration/Email-None.md)** - Development provider

---

### Setup Wizard Not Appearing

**Symptoms:**
- Navigating to the site doesn't redirect to `/___setup`
- Application loads normally but you can't access setup

**Check:**
1. `CosmosAllowSetup` is set to `true`
2. `MultiTenantEditor` is `false` or not set
3. Database does not have `Settings` table with `Group='SYSTEM'` and `Name='AllowSetup'` set to `'false'`
4. Navigate directly to `https://yourdomain.com/___setup`

**Solution:**

````plaintext
{ "CosmosAllowSetup": true, "ConnectionStrings": { "ApplicationDbContextConnection": "Data Source=D:\data\cosmoscms.db" } }
````

### Multi-Tenant Mode Not Working

**Symptoms:**
- Application crashes on startup
- Cannot access tenant configurations
- Data protection errors

**Check:**
1. `MultiTenantEditor` is set to `true`
2. `ConfigDbConnectionString` is valid and points to a Cosmos DB database
3. `DataProtectionStorage` is valid and points to Azure Blob Storage
4. Tenant configurations exist in the config database

**Solution:**

