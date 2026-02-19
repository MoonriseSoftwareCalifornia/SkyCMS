title: Troubleshooting Guide
description: Common issues and solutions for SkyCMS setup, configuration, and operation
keywords: troubleshooting, errors, debugging, solutions, configuration, database, storage, cdn
audience: [developers, administrators]
version: 2.0
updated: 2025-12-20
canonical: /Troubleshooting.html
aliases: []
scope:
  platforms: [azure, aws, cloudflare, local]
  tenancy: [single, multi]
status: stable
chunk_hint: 360
---

# Troubleshooting Guide

Common issues and solutions for SkyCMS setup, configuration, and operation.

## Key facts {#key-facts}

- Diagnose by layer: database → storage → CDN → publishing → auth → platform.
- Keep provider CLIs handy (az, aws, gcloud, cloudflare) to validate credentials and connectivity.
- The setup wizard is for single-tenant; DynamicConfig for multi-tenant—misuse leads to wizard failures.
- Most "cannot connect" issues are credentials, firewall, or missing roles; start there.

## Database Configuration Issues {#database-issues}

### "Connection string validation failed" or "Cannot connect to database"

**Causes:**
- Incorrect connection string format
- Database server is unreachable
- Firewall rules block access
- Invalid credentials

**Solutions:**
- Verify the connection string format matches your provider:
  - [Azure Cosmos DB](./Configuration/Database-CosmosDB.md)
  - [MS SQL Server / Azure SQL](./Configuration/Database-SQLServer.md)
  - [MySQL](./Configuration/Database-MySQL.md)
  - [SQLite](./Configuration/Database-SQLite.md)
- Check that the database server is running and accessible from your SkyCMS instance
- For Azure SQL, ensure your firewall rules allow your IP or enable "Allow Azure services and resources to access this server"
- Test the connection string with the provider's CLI tool (e.g., `sqlcmd`, `mysql` client)
- Verify username, password, and database name are correct

### "Database does not exist" or "Table not found"

**Causes:**
- Database hasn't been created yet
- Entity Framework migrations haven't run

**Solutions:**
- For first-time setup, use the SkyCMS Setup Wizard to create the database automatically
- If using manual configuration, run Entity Framework migrations:
  ```bash
  dotnet ef database update
  ```
- Ensure the connection string includes the correct database name

### "Access denied" or "Login failed"

**Causes:**
- User credentials lack required permissions
- Authentication method mismatch (SQL auth vs. Azure AD)

**Solutions:**
- For SQL Server: verify user login and password; ensure user has db_owner or equivalent permissions
- For Azure SQL: if using Azure AD, ensure managed identity or service principal is assigned the correct role (Contributor or higher on the database)
- For MySQL: ensure user has appropriate privileges: `GRANT ALL PRIVILEGES ON database.* TO 'user'@'host';`

---

## Storage Configuration Issues {#storage-issues}

### "Storage validation failed" or "Cannot write to storage"

**Causes:**
- Incorrect connection string format
- Storage account/bucket doesn't exist
- Credentials lack write permissions
- Network/firewall blocks access

**Solutions:**
- Verify the connection string format:
  - [Azure Blob Storage](./Configuration/Storage-AzureBlob.md)
  - [Amazon S3](./Configuration/Storage-S3.md)
  - [Cloudflare R2](./Configuration/Storage-Cloudflare.md)
  - [Google Cloud Storage](./Configuration/Storage-GoogleCloud.md)
- Ensure the storage account/bucket exists
- Verify credentials have read/write/delete permissions
- Test credentials with provider CLI (e.g., `az storage`, `aws s3`, `gsutil`)
- Check security groups, firewall rules, or IP allowlists

### "Access denied" when writing to storage

**Causes:**
- IAM policy or role is missing required permissions
- Credentials are read-only

**Solutions:**
- For Azure: ensure managed identity or connection string account has **Storage Blob Data Contributor** role
- For S3: verify IAM user policy includes `s3:PutObject`, `s3:DeleteObject`, `s3:GetObject`, `s3:ListBucket`
- For Cloudflare R2: ensure API token has **Object Read & Write** and **Bucket List** permissions
- For GCS: verify service account has **Storage Object Admin** role

### "Container/Bucket does not exist"

**Causes:**
- Storage container/bucket wasn't created beforehand

**Solutions:**
- Create the container/bucket before starting SkyCMS
- For Azure: SkyCMS can auto-create containers if the account has permissions
- For S3/R2/GCS: manually create the bucket in the provider's console

---

## CDN Configuration Issues {#cdn-issues}

### "CDN validation failed" or "Cannot purge cache"

**Causes:**
- Invalid credentials or distribution ID
- Permissions are insufficient
- Provider is unreachable

**Solutions:**
- Verify credentials and IDs match the provider:
  - [Azure Front Door](./Configuration/CDN-AzureFrontDoor.md)
  - [Cloudflare CDN](./Configuration/CDN-Cloudflare.md)
  - [Amazon CloudFront](./Configuration/CDN-CloudFront.md)
  - [Sucuri](./Configuration/CDN-Sucuri.md)
- Test credentials with provider CLI or API
- Ensure role/policy allows cache invalidation/purge

### CloudFront: "AccessDenied" on invalidation

**Cause:** IAM user lacks CloudFront permissions

**Solution:** Ensure IAM policy includes:
```json
{
  "Effect": "Allow",
  "Action": [
    "cloudfront:CreateInvalidation",
    "cloudfront:GetInvalidation"
  ],
  "Resource": "arn:aws:cloudfront::*:distribution/YOUR_DISTRIBUTION_ID"
}
```

### Azure Front Door: "Role assignment not found"

**Cause:** Managed identity doesn't have the required role

**Solution:** In Azure Portal, assign **CDN Endpoint Contributor** role to the identity on the Front Door profile

### Cloudflare: "Invalid Zone ID"

**Cause:** Zone ID doesn't match the domain

**Solution:** Verify Zone ID in Cloudflare Dashboard → select domain → copy Zone ID from Overview

---

## Publishing & Content Issues {#publishing-issues}

### "Publish failed" or "Content not updating"

**Causes:**
- Storage is unreachable
- CDN purge failed (but content still published)
- Permissions issue on storage

**Solutions:**
- Check storage connectivity (see Storage Configuration Issues above)
- Verify the publish log in the Editor for specific errors
- CDN purge failures don't block publishing; content is still updated in storage
- Clear your browser cache or wait for CDN TTL to expire
- Manually purge CDN cache if automated purge fails

### "File upload fails"

**Cause:** Storage permissions or size limits

**Solutions:**
- Verify storage credentials have write permissions
- Check file size against provider limits (most providers support multi-part uploads for large files)
- Check disk space on storage account/bucket
- Verify file format is allowed

### "Scheduled page didn't publish"

**Causes:**
- Publisher process wasn't running at scheduled time
- Publisher crashed or was restarted

**Solutions:**
- Ensure Publisher application is running continuously
- Check Publisher logs for errors around the scheduled time
- Manually publish the page from the Editor
- Configure a monitoring/alerting system to detect Publisher downtime

### "Cannot reach database during wizard" {#wizard-db}

**Causes:**
- Connection string incorrect or missing
- Database firewall blocks the Editor app
- Required provider client libraries not present

**Solutions:**
- Verify the connection string in environment variables before launching the wizard
- For Azure SQL, allow the App Service outbound IPs or enable "Allow Azure services" temporarily
- For local SQLite demos, ensure the volume path exists and is writable

### "Wizard fails at the Review step" {#wizard-review}

**Causes:**
- Required fields missing (storage, admin, publisher URL)
- Storage/CDN credentials invalid

**Solutions:**
- Re-enter storage/CDN credentials and verify permissions
- Confirm all wizard steps show green check marks before Review
- Restart the app after fixing inputs and rerun `/Setup`

---

## Authentication & User Issues {#auth-issues}

### "Login fails with correct credentials"

**Causes:**
- User account doesn't exist
- Password is incorrect or reset needed
- User role lacks access

**Solutions:**
- Verify user account exists in database
- For first-time setup, the first user is created during the wizard
- Reset password through database (consult [Identity Framework docs](./Components/AspNetCore.Identity.FlexDb.md))
- Verify user has appropriate roles (Editor, Admin, etc.)

### "Cannot create new users"

**Causes:**
- Admin user doesn't have permission
- Identity database isn't configured

**Solutions:**
- Ensure admin user has sufficient privileges
- Check Identity Framework is properly configured with the database
- Verify identity tables were created during wizard/migration

---

## Performance Issues {#performance-issues}

### "Slow page load" or "high CDN latency"

**Causes:**
- CDN not configured or purging inefficiently
- Static assets aren't being cached
- Origin database is slow

**Solutions:**
- Verify CDN is configured (see [CDN Integration Overview](./Configuration/CDN-Overview.md))
- Check CDN cache hit ratio in provider dashboard
- Enable cache headers on static assets
- Review database query performance; optimize slow queries
- Check network latency to origin

### "Slow publishing"

**Causes:**
- Storage is distant or slow
- Large number of files being published
- CDN purge is slow

**Solutions:**
- Choose a storage region close to your users
- Consider using a faster storage tier (if available)
- Optimize the number of files being published
- CDN purge can take time; large invalidations may take minutes

---

## Container & Deployment Issues {#container-issues}

### "Container fails to start"

**Causes:**
- Missing environment variables
- Port is already in use
- Volume mount issues

**Solutions:**
- Verify all required connection strings are set as environment variables
- Check port (default 5000) isn't in use by another process
- For Docker, ensure volumes are properly mounted and have correct permissions
- Check container logs: `docker logs <container-id>`

### "Multiple instances lose data"

**Cause:** SQLite database being used (file-based, doesn't support concurrent writes)

**Solution:** Migrate to a centralized database (Azure SQL, Cosmos DB, MySQL)

---

## FAQ {#faq}

- **What’s the most common cause of setup failures?** Misconfigured connection strings or firewall blocks for database/storage.
- **Why does publishing succeed but CDN shows stale content?** CDN purge failed or is delayed; content is already in storage—retry purge or wait for TTL.
- **Why does the wizard stop working after first run?** It disables itself; set `CosmosAllowSetup=true` temporarily only when you need to rerun it.

<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "FAQPage",
  "mainEntity": [
    {"@type": "Question", "name": "What’s the most common cause of setup failures?", "acceptedAnswer": {"@type": "Answer", "text": "Misconfigured connection strings or firewall blocks for database or storage."}},
    {"@type": "Question", "name": "Why does publishing succeed but CDN shows stale content?", "acceptedAnswer": {"@type": "Answer", "text": "CDN purge failed or is delayed; the content is already in storage. Retry purge or wait for TTL."}},
    {"@type": "Question", "name": "Why does the wizard stop working after first run?", "acceptedAnswer": {"@type": "Answer", "text": "The wizard disables itself after completion. Temporarily set CosmosAllowSetup=true, rerun /Setup, then set it back to false."}}
  ]
}
</script>

## Getting More Help {#getting-help}

- **GitHub Issues**: [Report bugs or ask questions](https://github.com/CWALabs/SkyCMS/issues)
- **Community Discussions**: [Engage with the community](https://github.com/CWALabs/SkyCMS/discussions)
- **Provider-specific Help**:
  - Azure: [Azure Documentation](https://learn.microsoft.com/en-us/azure/)
  - AWS: [AWS Documentation](https://docs.aws.amazon.com/)
  - Cloudflare: [Cloudflare Documentation](https://developers.cloudflare.com/)
