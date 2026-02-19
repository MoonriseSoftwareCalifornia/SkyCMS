---
title: Setup Wizard Installation Guide
description: Interactive web-based setup wizard for first-time SkyCMS configuration
keywords: setup-wizard, installation, configuration, interactive, guided-setup
audience: [developers, administrators]
version: 2.0
last_updated: "2026-01-03"
stage: stable
read_time: 6
---

# Setup Wizard Installation Guide

The **Setup Wizard** is the easiest way to configure SkyCMS for first-time deployment. It walks you through each configuration step in an interactive web interface.

![Setup wizard welcome screen](../images/screenshots/setup-wizard-welcome.webp)

## When to use this
- First-time or single-tenant setups that want guided, interactive configuration.
- Teams preferring UI-driven setup instead of pre-seeding all env vars.

## Why this matters
- Reduces setup errors and time-to-first-publish by validating connections in-UI.
- Clarifies what must be preconfigured vs. safely entered in the wizard.

## Key takeaways
- Wizard is for single-tenant; multi-tenant uses DynamicConfig with `CosmosAllowSetup=false`.
- Preconfig via env vars makes fields read-only to protect critical settings.
- Typical completion time: ~10–15 minutes.

## Prerequisites
- `CosmosAllowSetup=true` set before start; reachable database connection string.
- Optional: storage/email/CDN settings if you want them pre-seeded/read-only.

## Quick path
1. Deploy SkyCMS with `CosmosAllowSetup=true` and DB connection string.
2. Navigate to `/___setup` and complete steps (Storage → Admin → Publisher → optional Email/CDN → Review).
3. Finish, let the app restart, then sign in with the admin you created.

---

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Starting the Wizard](#starting-the-wizard)
- [Wizard Steps](#wizard-steps)
- [Pre-Configuration with Environment Variables](#pre-configuration-with-environment-variables)
- [After Setup Completes](#after-setup-completes)
- [Troubleshooting](#troubleshooting)

---

## Overview

The **Setup Wizard** provides an interactive web interface to configure SkyCMS. You can use it in two ways:

### 🧙 **Approach 1: Full Wizard Configuration (Easiest)**
- Configure only database connection + enable wizard
- Enter ALL settings through the wizard UI
- Interactive and guided
- **Best for**: New users, learning SkyCMS, development

### ⚙️ **Approach 2: Pre-Configure + Wizard (Production)**
- Pre-configure sensitive settings via environment variables (database, storage, credentials)
- Use wizard for remaining settings (admin account, publisher URL, etc.)
- Pre-configured fields appear as read-only in the wizard
- **Best for**: Docker/Kubernetes, security-conscious deployments, CI/CD

---

## Wizard Steps

The wizard consists of 7 steps, each with a dedicated documentation page:

1. **[Welcome Screen](./SetupWizard-Welcome.md)** - Prerequisites check and database configuration
2. **[Storage Configuration](./SetupWizard-Step1-Storage.md)** - Connect to Azure Blob, S3, R2, or Google Cloud Storage
3. **[Admin Account](./SetupWizard-Step2-Admin.md)** - Create your administrator account
4. **[Publisher Settings](./SetupWizard-Step3-Publisher.md)** - Configure your website URL and options
5. **[Email Configuration](./SetupWizard-Step4-Email.md)** (Optional) - Set up transactional email
6. **[CDN Configuration](./SetupWizard-Step5-CDN.md)** (Optional) - Configure cache purging
7. **[Review & Complete](./SetupWizard-Step6-Review.md)** - Finalize and apply settings
8. **[Setup Complete](./SetupWizard-Complete.md)** - Post-setup instructions and next steps

![Storage configuration step](../images/screenshots/setup-wizard-storage.webp)

**Time to Complete**: 10-15 minutes

> **Tip**: Click any step above to view detailed instructions, field references, troubleshooting, and pre-configuration examples for that specific step.

**When to Use the Wizard**:
- ✅ First-time SkyCMS installation
- ✅ Single-tenant deployments
- ✅ Interactive configuration preferred
- ✅ Learning/testing environments

**When NOT to Use**:
- ❌ Multi-tenant deployments (no wizard support - use [multi-tenant configuration](./MinimumRequiredSettings.md#multi-tenant))
- ❌ Fully automated deployments (pre-configure everything via environment variables instead)

![Review and complete step](../images/screenshots/setup-wizard-review.webp)

---

## Prerequisites

### Required for Both Approaches

1. **Database Connection** - Required
2. **Enable Setup Wizard** - Set `CosmosAllowSetup=true`

### Choose Your Approach

#### 🧙 **Approach 1: Minimal Configuration (Full Wizard)**

Provide only the bare minimum, configure everything else through the wizard:

**appsettings.json** or **environment variables**:

```json
{
  "CosmosAllowSetup": true,
  "ConnectionStrings": {
    "ApplicationDbContextConnection": "Data Source=D:\\data\\cosmoscms.db"
  }
}
```

**What you'll configure in the wizard**:
- Storage connection (Azure Blob, S3, R2, or GCS)
- Administrator account (email, username, password)
- Publisher URL and settings
- Email provider (optional)
- CDN provider (optional)

---

#### ⚙️ **Approach 2: Pre-Configured (Wizard for Remaining Settings)**

Pre-configure sensitive/infrastructure settings, use wizard for user-facing settings:

**Environment variables example** (Docker/Kubernetes):

```bash
# Required
CosmosAllowSetup=true
ConnectionStrings__ApplicationDbContextConnection="Data Source=/data/cosmoscms.db"

# Pre-configured storage (will be read-only in wizard)
ConnectionStrings__StorageConnectionString="Bucket=my-bucket;Region=us-east-1;KeyId=AKIA...;Key=..."
AzureBlobStorageEndPoint="/"

# Pre-configured publisher URL (read-only in wizard)
CosmosPublisherUrl="https://www.mywebsite.com"
```

**What you'll still configure in the wizard**:
- Administrator account (email, username, password)
- Publisher settings not pre-configured (title, file types, etc.)
- Email provider (if not pre-configured)
- CDN provider (if not pre-configured)

**Fields pre-configured via environment variables will**:
- Show as read-only/masked (`****`) in the wizard
- Cannot be edited through the UI
- Prevent accidental changes to infrastructure settings

See the [Pre-Configuration section](#pre-configuration-with-environment-variables) below for complete details.

---

## Starting the Wizard

### Step 1: Deploy SkyCMS

Deploy SkyCMS with the minimal configuration above. The application will start and redirect you to the setup wizard.

### Step 2: Access the Wizard

Open your browser and navigate to:

```
https://yourdomain.com/___setup
```

Or if running locally:

```
http://localhost:5000/___setup
```

**Note**: If already configured, you'll be redirected to the home page. To re-run setup:
1. Delete the database (or `AllowSetup` setting)
2. Set `CosmosAllowSetup=true` again
3. Restart the application

---

## Wizard Steps

The wizard consists of 7 screens. Click each step below for detailed instructions:

1. **[Welcome Screen](./SetupWizard-Welcome.md)** - Start setup and verify prerequisites
2. **[Step 1: Storage Configuration](./SetupWizard-Step1-Storage.md)** - Configure file storage
3. **[Step 2: Administrator Account](./SetupWizard-Step2-Admin.md)** - Create admin user
4. **[Step 3: Publisher Settings](./SetupWizard-Step3-Publisher.md)** - Configure website settings
5. **[Step 4: Email Configuration](./SetupWizard-Step4-Email.md)** (Optional) - Set up email provider
6. **[Step 5: CDN Configuration](./SetupWizard-Step5-CDN.md)** (Optional) - Configure CDN
7. **[Step 6: Review & Complete](./SetupWizard-Step6-Review.md)** - Review and finalize

---

## Pre-Configuration with Environment Variables

You can **pre-configure** certain settings via environment variables **before** starting the wizard. Pre-configured settings appear as **read-only** in the wizard (cannot be edited).

### Why Pre-Configure?

- **Security**: Keep sensitive credentials out of the wizard UI
- **Automation**: Pre-set values for Docker/Kubernetes deployments
- **Consistency**: Ensure certain values match your infrastructure

### What Can Be Pre-Configured?

| Setting | Environment Variable | Wizard Behavior |
| --- | --- | --- |
| **Database** | `ConnectionStrings__ApplicationDbContextConnection` | Required (not in wizard) |
| **Storage Connection** | `ConnectionStrings__StorageConnectionString` | Read-only if set |
| **Storage Public URL** | `AzureBlobStorageEndPoint` | Read-only if set |
| **Email Provider** | `CosmosSendGridApiKey`, `ConnectionStrings__AzureCommunicationConnection`, or `SmtpEmailProviderOptions__*` | Fields hidden/disabled if set |
| **Publisher URL** | `CosmosPublisherUrl` | Read-only if set |

**Environment variables ALWAYS override wizard settings.**

### Example: Pre-Configured Storage

```powershell
# Pre-configure storage (wizard will show as read-only)
$env:ConnectionStrings__StorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=mystorage;AccountKey=xxxxx;EndpointSuffix=core.windows.net;"
$env:AzureBlobStorageEndPoint = "/"

# Still need these
$env:CosmosAllowSetup = "true"
$env:ConnectionStrings__ApplicationDbContextConnection = "Data Source=D:\data\cosmoscms.db"

# Start SkyCMS
dotnet run
```

When you reach **Step 1: Storage**, the connection string will be masked (`****`) and cannot be edited.

### Example: Fully Automated Setup (No Wizard UI)

Skip the wizard entirely by pre-configuring everything:

```powershell
# Required
$env:CosmosAllowSetup = "false"  # Disable wizard
$env:ConnectionStrings__ApplicationDbContextConnection = "Data Source=D:\data\cosmoscms.db"
$env:ConnectionStrings__StorageConnectionString = "Bucket=mybucket;Region=us-east-1;KeyId=xxx;Key=xxx;"
$env:CosmosPublisherUrl = "https://www.mywebsite.com"
$env:AzureBlobStorageEndPoint = "/"
$env:AdminEmail = "admin@mywebsite.com"

# Optional
$env:CosmosSendGridApiKey = "SG.xxxxx"
$env:CosmosStaticWebPages = "true"
```

See [Minimum Required Settings](./MinimumRequiredSettings.md) for complete reference.

---

## After Setup Completes

### 1. Setup Disables Itself

Once setup completes successfully:
- `CosmosAllowSetup` is automatically set to `false` in the database
- The `/___setup` URL will redirect to the home page
- Settings are stored in the database and used on subsequent starts

### 2. Restart Required

**Docker/Container Deployments**:
- The setup completion page displays a **"Restart Application"** button
- Clicking it triggers a graceful shutdown
- Docker automatically restarts the container with new settings

**Manual Deployments** (IIS, systemd, PM2):
- Manually restart the application after setup completes
- Settings take effect on next startup

### 3. Login with Admin Account

After restart:
1. Navigate to your website URL (e.g., `https://www.mywebsite.com`)
2. Click **Login** or navigate to `/Identity/Account/Login`
3. Enter the admin email and password you created in Step 2
4. You're now logged in as an administrator

### 4. Next Steps

- **[Create Your First Page](../QuickStart.md)** - Get started with content
- **[Configure Layouts](../Layouts/)** - Customize your site structure
- **[Configure Email](../Configuration/Email-Overview.md)** - If you skipped Step 4
- **[Configure CDN](../Configuration/CDN-Overview.md)** - If you skipped Step 5

---

## Troubleshooting

### "Setup not allowed" or Redirect to Home Page

**Cause**: `CosmosAllowSetup` is not set to `true` or setup already completed.

**Solution**:
1. Check `CosmosAllowSetup=true` in appsettings.json or environment variables
2. If already completed, delete the `AllowSetup` setting from database
3. Or use a fresh database
4. Restart the application

### "Database connection string not found"

**Cause**: `ConnectionStrings:ApplicationDbContextConnection` is missing.

**Solution**:
```json
{
  "ConnectionStrings": {
    "ApplicationDbContextConnection": "Data Source=D:\\data\\cosmoscms.db"
  }
}
```

### "Storage connection test failed"

**Cause**: Invalid storage connection string or credentials.

**Solutions**:
- **Azure Blob**: Verify connection string from Azure Portal (includes `AccountName` and `AccountKey`)
- **Amazon S3**: Verify `Bucket`, `Region`, `KeyId`, and `Key` are correct
- **Cloudflare R2**: Verify `AccountId`, `Bucket`, and `AccessKey` are correct

### Cannot Edit Pre-Configured Fields

**Cause**: Fields set via environment variables cannot be edited in the wizard.

**Solution**:
- This is intentional for security
- To change values, update environment variables and restart
- Or remove environment variables to allow wizard editing

### "Email configuration incomplete"

**Cause**: Email provider selected but missing required fields.

**Solution**:
- Either complete all required fields for chosen provider
- Or select **None** to skip email configuration

### "Setup wizard is stuck on a step"

**Cause**: Required validation failed or missing data.

**Solution**:
1. Check browser console for JavaScript errors (F12)
2. Ensure all required fields are filled
3. Click **Test** buttons to verify credentials
4. Review error messages at top of page

### Application Won't Restart After Setup

**Docker**:
- Verify restart policy: `docker run --restart unless-stopped ...`
- Check container logs: `docker logs <container-id>`

**Manual Deployments**:
- Restart manually via IIS, systemd, or your hosting platform
- Check application logs for errors

---

## See Also

- **[Minimum Required Settings](./MinimumRequiredSettings.md)** - Manual configuration reference
- **[Storage Configuration](../Configuration/Storage-Overview.md)** - Storage provider details
- **[Email Configuration](../Configuration/Email-Overview.md)** - Email provider details
- **[CDN Configuration](../Configuration/CDN-Overview.md)** - CDN provider details
- **[Quick Start Guide](../QuickStart.md)** - After setup completes
- **[Installation Overview](./README.md)** - All installation guides
