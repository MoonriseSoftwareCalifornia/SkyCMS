---
title: Cloudflare R2 Storage Configuration
description: Configure Cloudflare R2 S3-compatible object storage with no egress fees
keywords: Cloudflare-R2, storage, S3-compatible, object-storage, configuration
audience: [developers, administrators]
version: 2.0
last_updated: "2026-01-03"
stage: stable
read_time: 6
---

# Cloudflare R2 with SkyCMS

Cloudflare R2 is an S3-compatible object storage service with no egress fees, making it cost-effective for static asset delivery. SkyCMS integrates seamlessly with R2.

## When to use this
- You want low-cost S3-compatible storage with no egress fees and Cloudflare edge delivery.
- You plan to run origin-less hosting with R2 + Cloudflare Rules (no Worker needed).

## Why this matters
- Correct Account ID/keys/public URL are required for publishing and edge rewrites.
- Scoped tokens reduce risk; R2 endpoints differ from AWS S3 defaults.

## Key takeaways
- Connection string: AccountId + Bucket + KeyId + Key; provide the R2 public URL or custom domain.
- Pair with Cloudflare Rules for HTTPS redirect and index rewrite for origin-less sites.
- Scope S3 API tokens to the bucket; rotate regularly.

## Prerequisites
- R2 bucket, Account ID, and S3 API token (KeyId/Key) with read/write/delete.
- Public URL (account-based or custom domain) for assets.

## Quick path
1. Create R2 bucket and S3 API token; note Account ID.
2. Set `StorageConnectionString` via wizard or env/appsettings; add public URL.
3. Publish and verify objects; add Cloudflare Rules for edge hosting if desired.

## Values you need

- **Account ID**: Your Cloudflare account ID
- **Bucket Name**: R2 bucket name
- **Access Key ID**: S3-style access key ID
- **Secret Access Key**: S3-style secret access key

## Create a Cloudflare R2 Bucket

1. **Cloudflare Dashboard** → **R2** → **Create bucket**.
2. Fill in:
   - **Bucket name**: Choose a name (does not need to be globally unique)
   - **Region**: Choose the closest region (optional; defaults to WNAM)
3. Click **Create bucket**.

## Create R2 API Token (S3 credentials)

1. **Cloudflare Dashboard** → **R2** → **Settings** (or API Tokens in top nav).
2. Click **Create API token** → **S3 API Tokens** → **Create S3 API Token**.
3. Fill in:
   - **Token name**: E.g., "SkyCMS"
   - **Permissions**: Select **Object Read & Write** and **Bucket List**
   - **Bucket Access**: Choose **All buckets** or the specific bucket for SkyCMS
4. Click **Create API Token**.
5. Copy the **Access Key ID** and **Secret Access Key** (visible only once).

## Get your Account ID

1. **Cloudflare Dashboard** → **R2** → **Overview**.
2. Copy your **Account ID** (shown at the top right or in API tokens section).

## Configure in SkyCMS

### Using the Setup Wizard (recommended)

1. Deploy SkyCMS with `CosmosAllowSetup=true`.
2. Open the Editor setup wizard.
3. When prompted for **Storage**, paste the connection string:
   ```
   AccountId=1234567890abcdef;Bucket=my-bucket;KeyId=AKIAIOSFODNN7EXAMPLE;Key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY;
   ```
4. Enter the **Public URL** for your bucket. For R2, this is typically:
   ```
   https://your-bucket-name.1234567890abcdef.r2.cloudflarestorage.com/
   ```
   Or if you've set up a custom domain in R2 settings:
   ```
   https://your-custom-domain.com/
   ```
5. Click **Validate** and proceed.

### Manual Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "StorageConnectionString": "AccountId=1234567890abcdef;Bucket=my-bucket;KeyId=AKIAIOSFODNN7EXAMPLE;Key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY;"
  }
}
```

### Environment Variables

```powershell
$env:ConnectionStrings__StorageConnectionString = "AccountId=1234567890abcdef;Bucket=my-bucket;KeyId=AKIAIOSFODNN7EXAMPLE;Key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY;"
```

## Connection String Format {#connection-string-format}

The Cloudflare R2 connection string follows this format:

```
AccountId=<account-id>;Bucket=<bucket-name>;KeyId=<r2-access-key-id>;Key=<r2-secret-access-key>;
```

**Components:**
- `AccountId`: Your Cloudflare Account ID (32-character hex string)
- `Bucket`: Your R2 bucket name
- `KeyId`: R2 API Token Access Key ID
- `Key`: R2 API Token Secret Access Key

**Example:**
```
AccountId=abc123def456789;Bucket=my-cms-bucket;KeyId=a1b2c3d4e5f6;Key=secret123abc456xyz789;
```

## Origin-less (Edge) Hosting with Cloudflare

For maximum performance, use Cloudflare R2 with origin-less hosting (no origin server required):

1. Bind your R2 bucket to your domain in Cloudflare.
2. Set up Cloudflare Rules for routing (index document, 404 handling, rewrites).
3. See [Cloudflare Edge Hosting](../Installation/CloudflareEdgeHosting.md) for detailed setup.

## Best practices

- **Scope API tokens** to specific buckets when possible.
- **Never commit access keys** to source control; use environment variables or Cloudflare Secrets.
- **Rotate API tokens** periodically; create new tokens before deleting old ones.
- **Use custom domains** for better branding and easier CDN setup.
- **Enable Cloudflare cache rules** for optimal performance on R2 buckets.
- **Monitor storage** and egress in the Cloudflare dashboard.

## Tips and troubleshooting

- Connection string format: `AccountId=...;Bucket=...;KeyId=...;Key=...;`
- Account ID is a hexadecimal string (found in R2 overview or API tokens section).
- Bucket names in R2 do not need to be globally unique.
- Public URL format: `https://<bucket>.<account-id>.r2.cloudflarestorage.com/`
- If validation fails, verify the Account ID, bucket name, and API token permissions.
- R2 has **no egress fees**, making it cost-effective compared to S3.
- For truly global edge delivery, use Cloudflare Rules to serve directly from R2 without an origin server.

## Further reading

- [Cloudflare R2 Access Keys Guide](./Cloudflare-R2-AccessKeys.md) — Detailed step-by-step for obtaining credentials
- [Cloudflare Edge Hosting](../Installation/CloudflareEdgeHosting.md) — Origin-less static website setup with R2 and Cloudflare Rules
