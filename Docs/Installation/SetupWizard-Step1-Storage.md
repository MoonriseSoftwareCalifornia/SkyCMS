---
title: Setup Wizard - Step 1 Storage
description: Configure cloud storage provider (S3, Azure Blob, Cloudflare R2) in setup wizard
keywords: setup-wizard, storage, S3, Azure-Blob, Cloudflare-R2, configuration
audience: [developers, administrators]
---

# Setup Wizard: Step 1 - Storage Configuration

[← Welcome](./SetupWizard-Welcome.md) | **Step 1 of 6** | [Next: Admin Account →](./SetupWizard-Step2-Admin.md)

---

## Configure File Storage

Configure where SkyCMS stores uploaded files, images, and media.

![Storage configuration step](../images/screenshots/setup-wizard-storage.webp)

---

## Supported Storage Providers

- **Azure Blob Storage** - Microsoft Azure cloud storage
- **Amazon S3** - AWS object storage
- **Cloudflare R2** - Zero-egress S3-compatible storage
- **Google Cloud Storage** - Google Cloud object storage

---

## Fields

### Storage Connection String (Required)

The connection string for your storage provider.

**Field Name**: `StorageConnectionString`  
**Required**: ✅ Yes  
**Can be pre-configured**: Yes (via `ConnectionStrings__StorageConnectionString`)

#### Azure Blob Storage Format

```
DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=xxxxxxxxxxxxx;EndpointSuffix=core.windows.net;
```

- Get from Azure Portal → Storage Account → Access Keys

#### Amazon S3 Format

```
Bucket=mybucket;Region=us-east-1;KeyId=AKIAIOSFODNN7EXAMPLE;Key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY;
```

- `Bucket`: S3 bucket name
- `Region`: AWS region (e.g., `us-east-1`)
- `KeyId`: IAM access key ID
- `Key`: IAM secret access key

#### Cloudflare R2 Format

```
AccountId=abc123;Bucket=mybucket;AccessKeyId=xxxxx;SecretAccessKey=yyyyy;
```

- Get from Cloudflare Dashboard → R2 → Manage R2 API Tokens

### Blob Public URL (Required)

The public URL where files are accessible.

**Field Name**: `BlobPublicUrl`  
**Required**: ✅ Yes  
**Default**: `/`  
**Can be pre-configured**: Yes (via `AzureBlobStorageEndPoint`)

**Examples**:

- **Azure Blob with CDN**: `https://mycdn.azureedge.net/`
- **Azure Blob without CDN**: `https://mystorageaccount.blob.core.windows.net/`
- **Amazon S3**: `https://mybucket.s3.amazonaws.com/`
- **Cloudflare R2**: `https://pub-xxxxx.r2.dev/` or custom domain
- **Root-relative** (if files are served from site root): `/`

---

## Actions

### "Test Connection" Button

Click **Test Connection** to verify the storage credentials.

**What it does**:
- Attempts to connect to the storage provider
- Verifies credentials are valid
- Checks bucket/container exists and is accessible

**Success**:
- ✅ Green checkmark appears
- "Storage connection successful!" message

**Failure**:
- ❌ Red error message appears
- Details about what went wrong (invalid credentials, bucket not found, etc.)

### "Next" Button

Proceeds to **Step 2: Administrator Account** after successful validation.

**Validation**:
- Storage connection string is required
- Blob public URL is required
- Storage connection test must pass (unless skipped)

---

## Pre-Configuration with Environment Variables

If storage is pre-configured via environment variables, the fields appear **read-only** (masked as `**********************`).

### Pre-Configure Storage

```powershell
# Windows PowerShell
$env:ConnectionStrings__StorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=mystorage;AccountKey=xxxxx;EndpointSuffix=core.windows.net;"
$env:AzureBlobStorageEndPoint = "/"
```

```bash
# Linux/macOS
export ConnectionStrings__StorageConnectionString="Bucket=mybucket;Region=us-east-1;KeyId=xxx;Key=yyy;"
export AzureBlobStorageEndPoint="https://mybucket.s3.amazonaws.com/"
```

**When pre-configured**:
- Connection string shows as `**********************`
- Fields cannot be edited in the wizard
- Click **Next** to proceed (test connection skipped)

---

## Storage Provider Setup Guides

Detailed setup instructions for each provider:

- **[Azure Blob Storage](../Configuration/Storage-AzureBlob.md)** - Create storage account, get connection string
- **[Amazon S3](../Configuration/Storage-S3.md)** - Create bucket, configure IAM credentials
- **[Cloudflare R2](../Configuration/Storage-Cloudflare.md)** - Create bucket, generate API tokens
- **[Google Cloud Storage](../Configuration/Storage-GoogleCloud.md)** - Create bucket, service account setup

---

## Common Connection Strings

### Azure Blob Storage

```
DefaultEndpointsProtocol=https;AccountName=skycmsstorage;AccountKey=abc123xyz789==;EndpointSuffix=core.windows.net;
```

**Public URL Examples**:
- With custom domain/CDN: `https://cdn.mywebsite.com/`
- Direct blob URL: `https://skycmsstorage.blob.core.windows.net/`
- Root-relative: `/`

### Amazon S3

```
Bucket=skycms-files;Region=us-east-1;KeyId=AKIAIOSFODNN7EXAMPLE;Key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY;
```

**Public URL Examples**:
- With CloudFront CDN: `https://d111111abcdef8.cloudfront.net/`
- Direct S3 URL: `https://skycms-files.s3.us-east-1.amazonaws.com/`
- S3 static hosting: `http://skycms-files.s3-website-us-east-1.amazonaws.com/`

### Cloudflare R2

```
AccountId=abc123def456;Bucket=skycms-media;AccessKeyId=xxxxxxx;SecretAccessKey=yyyyyyy;
```

**Public URL Examples**:
- Custom domain: `https://media.mywebsite.com/`
- R2.dev subdomain: `https://pub-xxxxxxx.r2.dev/`

---

## Troubleshooting

### "Storage connection test failed"

**Common Causes**:

1. **Invalid credentials**
   - Azure: Wrong account name or key
   - S3: Invalid access key ID or secret
   - R2: Invalid access key or account ID

2. **Bucket/Container doesn't exist**
   - Create the bucket/container before configuring
   - Verify spelling matches exactly

3. **Insufficient permissions**
   - Azure: Storage account must allow blob access
   - S3: IAM user needs `s3:GetObject`, `s3:PutObject`, `s3:DeleteObject`, `s3:ListBucket`
   - R2: API token needs read/write permissions

4. **Network/firewall issues**
   - Verify outbound HTTPS (443) is allowed
   - Check storage provider's status page

### "Blob public URL is invalid"

**Solutions**:

- Must be a valid URL starting with `http://` or `https://`, OR
- Can be `/` for root-relative paths
- Do not include trailing paths (e.g., avoid `/files/images/`)

### Cannot Edit Connection String (Shows `****`)

**Cause**: Storage is pre-configured via environment variables.

**This is intentional**:
- Environment variables take precedence over wizard settings
- To change, update environment variables and restart

### "Required field" Errors

**Solution**:
- Both connection string and public URL are required
- Fill in all fields before clicking **Next**

---

## What Happens Next

After clicking **Next**, you'll proceed to:

**[Step 2: Administrator Account →](./SetupWizard-Step2-Admin.md)**

The storage configuration is saved to the setup session. You can return to this step later if needed.

---

## See Also

- **[Setup Wizard Overview](./SetupWizard.md)** - Complete wizard guide
- **[Storage Configuration Overview](../Configuration/Storage-Overview.md)** - Provider comparison
- **[Storage Configuration Reference](../Configuration/Storage-Configuration-Reference.md)** - Connection string formats
- **[← Previous: Welcome](./SetupWizard-Welcome.md)**
- **[Next: Admin Account →](./SetupWizard-Step2-Admin.md)**
