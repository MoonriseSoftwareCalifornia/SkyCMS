---
title: Storage Configuration Reference
description: Complete reference for cloud storage configuration with S3, Azure Blob, Cloudflare R2, and Google Cloud
keywords: storage, configuration-reference, S3, Azure-Blob, Cloudflare-R2, Google-Cloud
audience: [developers, administrators]
version: 2.0
last_updated: "2026-01-03"
stage: stable
read_time: 8
---

# Storage Configuration

SkyCMS stores static web assets (images, CSS/JS, downloads) in cloud object storage. It automatically selects a storage driver based on the connection string configured under:

`ConnectionStrings:StorageConnectionString`

If that key is not present, the system also checks:

`ConnectionStrings:AzureBlobStorageConnectionString`

Use the following structure in `appsettings.json` (or environment variables/secrets):

```json
{
   "ConnectionStrings": {
      "StorageConnectionString": "<your-storage-connection-string>"
   }
}
```

Jump to:

- [Storage Configuration](#storage-configuration)
  - [Azure Blob Storage](#azure-blob-storage)
  - [Amazon S3](#amazon-s3)
  - [Cloudflare R2 (S3-compatible)](#cloudflare-r2-s3-compatible)
  - [Which storage should I use?](#which-storage-should-i-use)
  - [Static website hosting (Azure)](#static-website-hosting-azure)
  - [Security and secrets](#security-and-secrets)
  - [Troubleshooting](#troubleshooting)

---

## When to use this
- You need precise connection string formats and options for storage providers (Azure Blob, S3, R2, GCS).
- You’re wiring production/CI and need authoritative reference values.

## Why this matters
- Correct strings and endpoints are required for publishing/uploads to succeed.
- Least-privilege keys and secret handling reduce risk.

## Key takeaways
- `StorageConnectionString` drives provider selection; S3-compatible endpoints work if formatted correctly.
- For Azure static website enablement, use a key-based string (managed identity can’t enable it programmatically).
- Keep credentials in env vars/secret stores; test uploads after configuring.

## Prerequisites
- Chosen storage provider, bucket/container, and credentials with write/list/delete.
- Public endpoint/CDN URL if serving static assets.

## Quick path
1. Pick provider; copy the matching format below and fill values.
2. Set `ConnectionStrings__StorageConnectionString` (env) or appsettings.
3. Restart and test upload/publish; confirm files appear in storage.

## Azure Blob Storage

Azure storage connection string:

```json
{
   "ConnectionStrings": {
      "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName={account};AccountKey={key};EndpointSuffix=core.windows.net"
   }
}
```

Find values in the Azure Portal:

1. Open the [Azure Portal](https://portal.azure.com) → Storage accounts → select your account
2. Security + networking → Access keys → copy a connection string (Key1 or Key2)

Managed identity (no secret in config):

```json
{
   "ConnectionStrings": {
      "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName={account};AccountKey=AccessToken;EndpointSuffix=core.windows.net"
   }
}
```

> Note: “AccountKey=AccessToken” enables Azure Default Credential in code. Ensure your app’s identity has Blob Data access roles on the storage account.

---

## Amazon S3

Quick setup guide: see [AWS S3 access keys for SkyCMS](./AWS-S3-AccessKeys.md) for a step‑by‑step, least‑privilege walkthrough (create IAM user, bucket‑scoped policy, and access keys).

```json
{
   "ConnectionStrings": {
      "StorageConnectionString": "Bucket={bucket};Region={aws-region};KeyId={access-key-id};Key={secret-access-key};"
   }
}
```

Where to find values in AWS Console:

1. S3 → choose your bucket → note the bucket name and region (e.g., `us-west-2`)
2. IAM → Users → your user → Security credentials → Create access key → copy Access key ID and Secret access key

Best practice: Scope IAM permissions to the specific bucket and required actions (GetObject, PutObject, ListBucket, DeleteObject).

---

## Cloudflare R2 (S3-compatible)

Cloudflare R2 is S3-compatible. With SkyCMS you’ll provide your Account ID, bucket name, and S3-style credentials (Key ID/Secret).

Quick setup guide: see [Cloudflare R2 access keys for SkyCMS](./Cloudflare-R2-AccessKeys.md) to find your Account ID and bucket, and to generate an S3 API token (read/write/delete). For edge/origin-less hosting with Cloudflare, see [Cloudflare Edge Hosting](../Installation/CloudflareEdgeHosting.md) for instructions on binding your R2 bucket and configuring Cloudflare Rules (no Worker required).

Format the connection string for R2 storage in the following manner. Note it requires
an Account ID, Bucket name, Key ID and Key Secret:

```json
{
   "ConnectionStrings": {
      "StorageConnectionString": "AccountId={Account ID};Bucket={bucket name};KeyId={access-key-id};Key={secret-access-key};"
   }
}
```

---

## Which storage should I use?

Use what your team already knows when possible. Quick guidance:

| Provider           | Best for                                      | Pros                                                | Considerations |
|--------------------|-----------------------------------------------|-----------------------------------------------------|----------------|
| Azure Blob Storage | Azure-native deployments, static website CDN  | First-class Azure integration, managed identity     | Requires Azure account/roles |
| Amazon S3          | AWS-native or multi-cloud compatibility       | Ubiquitous, scalable, rich tooling                  | Access keys management, region selection |
| Cloudflare R2      | S3-compatible, egress-friendly pricing        | Cost model benefits                                 | Custom endpoint may be required for R2; see Cloudflare docs and [CloudflareEdgeHosting.md](../Installation/CloudflareEdgeHosting.md) |

---

## Static website hosting (Azure)

SkyCMS can programmatically enable Azure Storage static website hosting. This requires a standard key-based connection string.

> Important: When using managed identity (`AccountKey=AccessToken`), the code cannot enable static website due to SDK restrictions. Use a key-based connection temporarily to enable it, or enable it manually in the portal.

---

## Google Cloud Storage

### Overview

Google Cloud Storage (GCS) is supported through the S3-compatible API. SkyCMS automatically detects GCS configuration and uses the appropriate driver.

### Configuration

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "StorageConnectionString": "Bucket=your-bucket-name;Region=us-central1;AccessKey=your-access-key;SecretKey=your-secret-key;ServiceUrl=https://storage.googleapis.com"
  },
  "CosmosStorageConfig": {
    "CdnConfig": {
      "AzureCdnConfig": {
        "PublicUrl": "https://storage.googleapis.com/your-bucket-name"
      }
    }
  }
}
```

### Setup Steps

1. **Create a GCS bucket:**
   ```bash
   gsutil mb -l us-central1 gs://your-bucket-name
   ```

2. **Enable S3 Interoperability:**
   - Go to Google Cloud Console → Cloud Storage → Settings → Interoperability
   - Click "Create a key for a service account"
   - Note the Access Key and Secret

3. **Set bucket permissions:**
   ```bash
   gsutil iam ch serviceAccount:your-service-account@project.iam.gserviceaccount.com:objectAdmin gs://your-bucket-name
   ```

4. **Enable public access (if needed):**
   ```bash
   gsutil iam ch allUsers:objectViewer gs://your-bucket-name
   ```

### Supported Features

- Upload, download, delete operations
- Chunked multipart uploads
- Metadata management
- S3-compatible API
- Static website hosting (requires additional CDN setup)
- Native GCS API features (use S3 compatibility mode)

### Important Notes

- **Service URL**: Must include `ServiceUrl=https://storage.googleapis.com` for proper routing
- **Region**: Specify the bucket region in the connection string
- **Public Access**: Configure Cloud CDN or load balancer for public website serving
- **Costs**: Review GCS pricing for storage class, operations, and egress

---

## Static Website Hosting

### Overview

SkyCMS supports static website hosting configurations for serving published content directly from cloud storage, enabling cost-effective, scalable hosting without application servers.

### Azure Blob Storage Static Websites

**Automatic Enablement:**

SkyCMS can programmatically enable Azure static website hosting:

```csharp
// Automatically enables static website feature
storageContext.EnableStaticWebsite();
```

**Manual Configuration:**

1. **Azure Portal:**
   - Navigate to Storage Account → Static website
   - Enable: **Enabled**
   - Index document: `index.html`
   - Error document: `404.html`

2. **Azure CLI:**
   ```bash
   az storage blob service-properties update \
     --account-name yourstorageaccount \
     --static-website \
     --404-document 404.html \
     --index-document index.html
   ```

**CORS Configuration:**

SkyCMS automatically configures CORS when enabling static websites:

```csharp
CorsRules = new CorsRule[]
{
    new CorsRule
    {
        AllowedOrigins = { "*" },
        AllowedMethods = { "GET", "HEAD", "OPTIONS" },
        AllowedHeaders = { "*" },
        MaxAgeInSeconds = 3600
    }
};
```

**Primary Web Endpoint:**

Azure provides a dedicated endpoint for static websites:
```
https://yourstorageaccount.z13.web.core.windows.net/
```

Use this endpoint as the `PublicUrl` in your configuration.

### Cloudflare R2 Static Websites

**R2 Custom Domains:**

Cloudflare R2 supports static website hosting through custom domains:

1. **Enable public access:**
   ```bash
   wrangler r2 bucket update your-bucket --public-access true
   ```

2. **Configure custom domain:**
   - Navigate to R2 → your bucket → Settings → Custom Domain
   - Add your domain: `www.example.com`
   - Configure DNS: CNAME to R2 endpoint

3. **Set default index:**
   - Use Cloudflare Workers or Pages for index document routing
   - Configure 404 handling via Workers

**Workers Integration:**

```javascript
export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    let path = url.pathname;
    
    // Default to index.html for directory paths
    if (path.endsWith('/')) path += 'index.html';
    
    const object = await env.YOUR_BUCKET.get(path);
    
    if (!object) {
      // Return 404.html for missing files
      const notFound = await env.YOUR_BUCKET.get('404.html');
      return new Response(notFound?.body, { status: 404 });
    }
    
    return new Response(object.body, {
      headers: { 'Content-Type': object.httpMetadata.contentType }
    });
  }
};
```

### Amazon S3 Static Websites

**Bucket Configuration:**

1. **Enable static website hosting:**
   ```bash
   aws s3 website s3://your-bucket-name/ \
     --index-document index.html \
     --error-document 404.html
   ```

2. **Set bucket policy for public access:**
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [{
       "Sid": "PublicReadGetObject",
       "Effect": "Allow",
       "Principal": "*",
       "Action": "s3:GetObject",
       "Resource": "arn:aws:s3:::your-bucket-name/*"
     }]
   }
   ```

3. **Website endpoint:**
   ```
   http://your-bucket-name.s3-website-us-east-1.amazonaws.com
   ```

**CloudFront Integration:**

For HTTPS and global CDN:

```bash
aws cloudfront create-distribution \
  --origin-domain-name your-bucket-name.s3.amazonaws.com \
  --default-root-object index.html
```

### Limitations and Considerations

**Azure Managed Identity Restriction:**
> ⚠️ When using managed identity (`AccountKey=AccessToken`), the SDK cannot programmatically enable static website hosting due to API limitations. Use a key-based connection temporarily to enable it, or enable manually via Azure Portal.

**Performance:**
- **Static hosting** eliminates application server overhead (faster, cheaper)
- **Origin pull** (Publisher + CDN) provides dynamic content flexibility
- **Hybrid approach**: Static assets from storage, dynamic content from Publisher

**SEO Considerations:**
- Ensure proper `robots.txt` and `sitemap.xml` in storage root
- Configure custom 404 pages with appropriate HTTP status codes
- Use CDN for SSL termination and custom domains

**Cost Optimization:**
- **Storage class**: Use hot/cool tiers based on access patterns
- **Egress**: CDN reduces storage egress costs significantly
- **Operations**: Static hosting has minimal GET operation costs

---

## Security and secrets

- Do not commit secrets to source control.
- Prefer environment variables, ASP.NET Core User Secrets (for local dev), or Azure Key Vault.
- Enforce least-privilege on credentials; rotate regularly.

---

## Troubleshooting

- Provider detection is based on the connection string:
   - Starts with `DefaultEndpointsProtocol=` → Azure Blob Storage
   - Contains `Bucket=` → Amazon S3
- Ensure the connection key is `ConnectionStrings:StorageConnectionString` (or `AzureBlobStorageConnectionString` as fallback).
- For Azure managed identity, grant the app identity "Storage Blob Data Contributor" (or finer-grained roles) on the target account.
- For S3, verify region matches the bucket's region and keys are valid.

---

## See Also

- **[Storage Overview](./Storage-Overview.md)** - Compare storage options and when to use each
- **[Database Configuration](./Database-Overview.md)** - Configure your database provider
- **[Configuration Overview](./README.md)** - Index of all configuration documentation
- **[LEARNING_PATHS: DevOps](../LEARNING_PATHS.md#devops--system-administrator)** - Storage setup for DevOps professionals
- **[AWS S3 Access Keys](./AWS-S3-AccessKeys.md)** - Step-by-step S3 setup guide
- **[Cloudflare R2 Access Keys](./Cloudflare-R2-AccessKeys.md)** - Step-by-step R2 setup guide
- **[Cloudflare Edge Hosting](../Installation/CloudflareEdgeHosting.md)** - Origin-less hosting with R2
- **[Troubleshooting Guide](../Troubleshooting.md)** - Common storage issues and solutions
- **[Main Documentation Hub](../index.md)** - Browse all documentation
