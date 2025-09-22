# Azure Resource Manager Template for SkyCMS Deployment

This ARM template (`azuredeploy.json`) provides a complete Azure infrastructure deployment for the SkyCMS (Sky Content Management System) application.

## Overview

The template deploys a full-featured CMS solution with two main components:

- **Editor**: Content creation and management interface
- **Publisher**: Public-facing website that serves content

## What This Template Deploys

### Infrastructure Components

1. **Azure Storage Account**
   - Stores static files and website content
   - Configured with either locally-redundant (LRS) or geo-redundant (GRS) storage
   - Includes blob storage with `$web` container for static website hosting
   - File share for SQLite database storage

2. **App Service Plan**
   - Linux-based hosting plan
   - Configurable tiers: Basic (B3) or Premium (P0v3)
   - Supports container deployment

3. **Two Web Apps**
   - **Editor App**: Content management interface (`editor-{uniqueId}`)
   - **Publisher App**: Public website (`publisher-{uniqueId}`)
   - Both deployed as Docker containers from Docker Hub

### Application Features

- **Content Management**: Full CMS capabilities through the editor interface
- **Static Website Generation**: Publisher can generate static websites
- **Email Integration**: Multiple email service options
- **Shared Database**: SQLite database shared between editor and publisher via Azure File Share

## Parameters

### Required Parameters

| Parameter | Description |
|-----------|-------------|
| `noReplyEmailAddress` | Email address for system notifications |

### Email Service Configuration (Choose One)

#### Azure Communications

- `azureCommunicationsConnectionString`: Connection string for Azure Communication Services

#### SendGrid

- `sendGridApiKey`: API key for SendGrid email service

#### SMTP Relay

- `smtpHostName`: SMTP server hostname
- `smtpPort`: SMTP server port (typically 587)
- `smtpEnableSsl`: Enable TLS/SSL (true/false)
- `smtpUserName`: SMTP authentication username
- `smtpPassword`: SMTP authentication password

### Infrastructure Options

| Parameter | Default | Options | Description |
|-----------|---------|---------|-------------|
| `webAppPlanChoice` | Premium Tier | Basic Tier, Premium Tier | App Service plan tier |
| `storageAccountType` | Locally-redundant storage | Locally-redundant storage, Geo-redundant storage | Storage redundancy level |

## Deployment Process

### Prerequisites

- Azure subscription with appropriate permissions
- Resource group created for deployment

### Using Azure Portal

1. Click "Deploy to Azure" button (if available)
2. Fill in required parameters
3. Choose your email service configuration
4. Select infrastructure options
5. Review and deploy

### Using Azure CLI

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file azuredeploy.json \
  --parameters noReplyEmailAddress="noreply@yourdomain.com" \
  --parameters sendGridApiKey="<your-sendgrid-key>"
```

### Using PowerShell

```powershell
New-AzResourceGroupDeployment `
  -ResourceGroupName "<your-resource-group>" `
  -TemplateFile "azuredeploy.json" `
  -noReplyEmailAddress "noreply@yourdomain.com" `
  -sendGridApiKey "<your-sendgrid-key>"
```

## Post-Deployment Configuration

After successful deployment:

1. **Access the Editor**: Navigate to `https://editor-{uniqueId}.azurewebsites.net`
2. **Initial Setup**: Complete the CMS setup process (enabled by `CosmosAllowSetup: true`)
3. **Configure Content**: Create pages, articles, and configure your website
4. **Access Publisher**: Your public site will be available at `https://publisher-{uniqueId}.azurewebsites.net`

## Architecture

```text
┌─────────────────┐    ┌─────────────────┐
│   Editor App    │    │  Publisher App  │
│ (Content Mgmt)  │    │ (Public Site)   │
└─────────────────┘    └─────────────────┘
         │                       │
         └───────────┬───────────┘
                     │
         ┌─────────────────┐
         │ App Service Plan │
         │    (Linux)       │
         └─────────────────┘
                     │
         ┌─────────────────┐
         │ Storage Account │
         │  - Blob Storage │
         │  - File Share   │
         │  - SQLite DB    │
         └─────────────────┘
```

## Key Features

- **Container-Based Deployment**: Uses Docker images for consistent deployment
- **Shared Storage**: Both apps share the same storage account and database
- **SSL/HTTPS**: HTTPS enforced on all web apps
- **Scalable**: App Service Plan can be scaled up/down as needed
- **Email Ready**: Multiple email service integrations available
- **Static Website Support**: Publisher can generate static websites to blob storage

## Security Considerations

- HTTPS is enforced on all web applications
- FTP/FTPS is disabled for security
- System-assigned managed identities are enabled
- Secure parameters are used for sensitive configuration (API keys, passwords)

## Troubleshooting

### Common Issues

1. **Container startup issues**: Check Docker image availability and app service logs
2. **Database connection**: Verify file share mount and SQLite file permissions
3. **Email not working**: Confirm email service configuration and credentials

### Monitoring

- Use Application Insights (if configured) for performance monitoring
- Check App Service logs for application-specific issues
- Monitor storage account for capacity and performance metrics

## Documentation Links

- [Cosmos CMS Installation Guide](https://cosmos.moonrise.net/install)
- [Email Configuration](https://cosmos.moonrise.net/install#Com)
- [App Service Plan Options](https://cosmos.moonrise.net/install#Plan)
- [Storage Configuration](https://cosmos.moonrise.net/install#Storage)

## Version

Template Version: 10.13.0.0

## Support

For issues and support, refer to the Cosmos CMS documentation or repository.
