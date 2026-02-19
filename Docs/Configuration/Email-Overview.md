---
title: Email Provider Integration Overview
description: Email provider configuration for transactional emails including SendGrid, SMTP, and Azure Communication Services
keywords: email, SendGrid, SMTP, Azure-Communication-Services, transactional-emails, configuration
audience: [developers, administrators]
version: 2.0
last_updated: "2026-01-03"
stage: stable
read_time: 5
---

# Email Provider Integration Overview

SkyCMS can send transactional emails (password resets, notifications, etc.) through your preferred email provider. Supported providers:

- **SendGrid** (Twilio) - Cloud email platform with advanced analytics
- **Azure Communication Services** - Azure-native email service
- **SMTP** - Generic SMTP server (Gmail, Office 365, dedicated servers, etc.)
- **None** - No-op provider for development/testing

## When to use this
- You need to enable password resets/notifications and choose an email provider for SkyCMS.
- You want a quick comparison of SendGrid vs ACS vs SMTP with setup pointers.

## Why this matters
- Email is required for account recovery and notifications; misconfiguration blocks user onboarding.
- Picking the right provider impacts deliverability, cost, and compliance.

## Key takeaways
- Environment variables first for production; wizard is fine for first-time setup.
- SendGrid/ACS for managed delivery; SMTP for existing infra; None for dev.
- `AdminEmail` is required as the default sender across providers.

## Prerequisites
- Provider credentials: API key (SendGrid), connection string (ACS), or SMTP host/port/user/pass.
- Verified sender domains/identities where required (SES/SMTP equivalents, ACS, SendGrid).

## Quick path
1. Pick provider and gather credentials.
2. Set env vars (production) or use the wizard (first-time/single-tenant).
3. Send a test email before going live; monitor bounces/complaints where supported.

## How email integration works

- You provide provider-specific credentials in **environment variables** or through the **setup wizard**.
- SkyCMS uses these credentials to send emails via ASP.NET Core Identity (password resets) and custom features.
- Email configuration is stored and used by the application at runtime.
- Each provider has different authentication methods and capabilities.

## Quick prerequisites by provider

| Provider | Values you need | Best for | Setup Complexity |
| --- | --- | --- | --- |
| **SendGrid** | API Key | High-volume, reliable delivery with analytics | Medium |
| **Azure Communication Services** | Connection string | Azure customers, integrated monitoring | Medium |
| **SMTP** | Host, Port, Username, Password | On-premises servers, existing mail infrastructure | Low |
| **None** | N/A | Development/testing only (emails not sent) | None |

## Configuration methods

SkyCMS supports **two ways** to configure email providers:

### Method 1: Environment Variables (recommended for production)

Set configuration **before** the application starts:

```powershell
# SMTP example (Simplified - RECOMMENDED)
$env:SmtpHost = "smtp.gmail.com"
$env:SmtpPort = "587"
$env:SmtpUsername = "your-email@gmail.com"
$env:SmtpPassword = "your-app-password"
$env:SenderEmail = "admin@yourdomain.com"

# SMTP example (ASP.NET Format - Alternative)
$env:SmtpEmailProviderOptions__Host = "smtp.gmail.com"
$env:SmtpEmailProviderOptions__Port = "587"
$env:SmtpEmailProviderOptions__UserName = "your-email@gmail.com"
$env:SmtpEmailProviderOptions__Password = "your-app-password"
$env:AdminEmail = "admin@yourdomain.com"
```

```powershell
# SendGrid example
$env:CosmosSendGridApiKey = "SG.xxxxxxxxxxxxx"
$env:SenderEmail = "admin@yourdomain.com"  # Or AdminEmail
```

```powershell
# Azure Communication Services example (Recommended)
$env:ConnectionStrings__AzureEmailConnectionString = "endpoint=https://xxx.communication.azure.com/;accesskey=xxxxx"
$env:SenderEmail = "admin@yourdomain.com"

# Azure Communication Services example (Alternative)
$env:ConnectionStrings__AzureCommunicationConnection = "endpoint=https://xxx.communication.azure.com/;accesskey=xxxxx"
$env:AdminEmail = "admin@yourdomain.com"
```

### Method 2: Setup Wizard (recommended for first-time setup)

1. Deploy SkyCMS with `CosmosAllowSetup=true`.
2. Open the Editor: `https://yourdomain.com/___setup`
3. Progress through setup steps until **Step 4: Email Configuration**.
4. Choose your provider and enter credentials.
5. Test the configuration.
6. Complete setup (wizard disables itself).

**Note**: Wizard configuration is stored in the database. Environment variables override wizard settings.

## Provider-specific guides

- [SendGrid Email](./Email-SendGrid.md) - Cloud email service
- [Azure Communication Services Email](./Email-AzureCommunicationServices.md) - Azure-native solution
- [SMTP Email](./Email-SMTP.md) - Generic SMTP configuration
- [Email Configuration Reference](./Email-Configuration-Reference.md) - Complete settings reference

## Email configuration priority

SkyCMS tries providers in this order (first available is used):

1. **SMTP** - If `SmtpEmailProviderOptions:Host`, `SmtpEmailProviderOptions:UserName`, and `SmtpEmailProviderOptions:Password` are configured
2. **Azure Communication Services** - If `ConnectionStrings:AzureCommunicationConnection` is set
3. **SendGrid** - If `CosmosSendGridApiKey` is set
4. **None (NoOp)** - Default if no provider is configured (emails logged, not sent)

## Common settings for all providers

All providers require a sender email address:

- **`SenderEmail`** or **`AdminEmail`** - Default sender email address for system emails
  - Environment variables: `SenderEmail` (checked first) or `AdminEmail` (fallback)
  - Used in setup wizard: Yes, displayed as **Sender Email**
  - The code uses: `configuration["SenderEmail"] ?? configuration["AdminEmail"]`
  
**Priority**: If both are set, `SenderEmail` takes precedence over `AdminEmail`.

## Best practices

- **Use environment variables** for production deployments (more secure than storing in config files)
- **Restrict IAM/API permissions** to the minimum needed (least privilege principle)
- **Never commit credentials** to source control; use secrets management tools
- **Test sending** before deploying to production
- **Monitor delivery** and handle bounce/complaint notifications where applicable
- **Use TLS** for SMTP connections (`UsesSsl=false` uses TLS by default, `UsesSsl=true` uses SSL)

## Testing email configuration

Each provider guide includes instructions to:

1. Gather required credentials
2. Create/configure the provider
3. Test delivery from SkyCMS

---

## See Also

- **[Database Configuration](./Database-Overview.md)** - Companion configuration guide
- **[Storage Configuration](./Storage-Overview.md)** - Companion configuration guide
- **[CDN Configuration](./CDN-Overview.md)** - Companion configuration guide
- **[Configuration Overview](./README.md)** - Index of all configuration documentation
- **[Installation Guide](../Installation/MinimumRequiredSettings.md)** - Email in setup wizard
- **[Main Documentation Hub](../index.md)** - Browse all documentation
