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

## Azure Blob Storage

TL;DR connection string:

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

TL;DR connection string:

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

Cloudflare R2 is S3-compatible. However, the current implementation uses the AWS SDK with regional endpoints and does not expose a custom S3 endpoint in configuration. R2 typically requires a custom endpoint (e.g., `https://<account>.r2.cloudflarestorage.com`).

- If you need R2 support, you will likely require a code change to provide a custom ServiceURL in the S3 client configuration.
- In the meantime, prefer Azure Blob Storage or Amazon S3 for a turnkey setup.

R2 credentials are created from the Cloudflare dashboard (R2 → API Tokens). Use a User API Token with appropriate bucket scope.

---

## Which storage should I use?

Use what your team already knows when possible. Quick guidance:

| Provider           | Best for                                      | Pros                                                | Considerations |
|--------------------|-----------------------------------------------|-----------------------------------------------------|----------------|
| Azure Blob Storage | Azure-native deployments, static website CDN  | First-class Azure integration, managed identity     | Requires Azure account/roles |
| Amazon S3          | AWS-native or multi-cloud compatibility       | Ubiquitous, scalable, rich tooling                  | Access keys management, region selection |
| Cloudflare R2      | S3-compatible, egress-friendly pricing        | Cost model benefits                                 | Custom endpoint not currently configurable |

---

## Static website hosting (Azure)

SkyCMS can programmatically enable Azure Storage static website hosting. This requires a standard key-based connection string.

> Important: When using managed identity (`AccountKey=AccessToken`), the code cannot enable static website due to SDK restrictions. Use a key-based connection temporarily to enable it, or enable it manually in the portal.

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
- For Azure managed identity, grant the app identity “Storage Blob Data Contributor” (or finer-grained roles) on the target account.
- For S3, verify region matches the bucket’s region and keys are valid.
