---
title: Cloudflare Cloud Connector Guide
description: Route traffic to cloud storage using Cloudflare Cloud Connector (beta)
keywords: Cloudflare, Cloud Connector, R2, S3, object storage, static hosting
audience: [developers, devops]
version: 1.0
last_updated: "2026-02-03"
stage: beta
read_time: 5
---

# Cloudflare Cloud Connector: Simplified Static Site Routing

[Cloud Connector](https://developers.cloudflare.com/rules/cloud-connector/) is a **beta** Cloudflare feature that simplifies routing traffic to cloud object storage without manual rule configuration. Instead of creating custom URL rewrite and redirect rules, Cloud Connector automatically configures traffic routing to your storage bucket.

## When to use Cloud Connector vs. Edge Hosting

| Aspect | Cloud Connector | [Edge Hosting](./CloudflareEdgeHosting.md) |
|--------|-----------------|------------------------------------------|
| **Setup complexity** | Simpler—single rule configuration | More manual—multiple rules required |
| **Supported providers** | R2, AWS S3, Google Cloud, Azure | Primarily R2 |
| **Automatic config** | Yes—Host headers, SSL/TLS adjusted | No—manual rule creation |
| **URL rewriting** | Requires separate URL rewrite rules | Uses custom filter expressions |
| **Caching** | Requires separate cache rules | Uses custom filter expressions |
| **Status** | Beta | Stable |

## How Cloud Connector works

1. You configure a **Cloud Connector rule** that specifies:
   - The cloud provider (R2, AWS S3, Google Cloud, Azure)
   - The service/bucket that will accept traffic
   - The traffic pattern that triggers the rule

2. Cloudflare **automatically configures**:
   - `Host` header modification
   - SSL/TLS adjustments (for AWS S3 endpoints)

3. Cloud Connector rules are evaluated **last** in the request workflow, so they override other rules matching the same settings.

## Important limitations

Cloud Connector does **not** automatically:
- **Cache content**: Create a [cache rule](https://developers.cloudflare.com/cache/how-to/cache-rules/) to define cache behavior
- **Rewrite URLs**: Create a [URL rewrite rule](https://developers.cloudflare.com/rules/transform/url-rewrite/) to adjust path structure (e.g., remove `/files` prefix)

You'll still need these additional rules for production use.

## Prerequisites

- Cloudflare account with Cloud Connector enabled (beta)
- Public object storage bucket (R2, S3, etc.)
- Domain proxied through Cloudflare DNS

## Availability and plan limits

Cloud Connector is available in beta to all customers. Maximum rules depend on your plan:

| Plan | Free | Pro | Business | Enterprise |
|------|------|-----|----------|-----------|
| Available | Yes | Yes | Yes | Yes |
| Max rules | 10 | 25 | 50 | 300 |

## Setup steps

### 1. Ensure your bucket is public

Your storage bucket must allow public read access for Cloud Connector to work.

### 2. Create a Cloud Connector rule

Use the Cloudflare dashboard or API to create the rule:

- **Dashboard**: Navigate to Rules > Cloud Connector > Create rule
- **API**: Use the [Cloud Connector API](https://developers.cloudflare.com/rules/cloud-connector/create-api/)
- **Terraform**: Use the [Cloudflare Terraform provider](https://developers.cloudflare.com/rules/cloud-connector/create-terraform/)

Specify:
- The domain/path pattern to match
- The cloud provider (R2, AWS S3, Google Cloud, Azure)
- The bucket and region

### 3. Configure caching (recommended)

Create a [cache rule](https://developers.cloudflare.com/cache/how-to/cache-rules/) to control how long content is cached. Example:

```
If path contains / AND host equals example.com
Then cache everything with Edge TTL = 1 day
```

### 4. Configure URL rewriting (if needed)

If your bucket folder structure differs from public URLs, create a [URL rewrite rule](https://developers.cloudflare.com/rules/transform/url-rewrite/). Example:

```
If path starts with /files
Rewrite path to /archived-files
```

### 5. Deploy with SkyCMS

Configure SkyCMS to publish to your cloud storage (R2, S3, etc.). See your provider's configuration guide:
- [Cloudflare R2](../Configuration/Cloudflare-R2-AccessKeys.md)
- [AWS S3](../Configuration/AWS-S3-AccessKeys.md)

## Comparison: Cloud Connector vs. manual rules

**With Cloud Connector:**
```
1 rule → Automatic Host header + SSL/TLS configuration
+ Separate cache rule
+ Separate URL rewrite rule (if needed)
```

**Without Cloud Connector (manual):**
```
1 HTTPS redirect rule
+ 1 index.html rewrite rule
+ 1 cache rule (if desired)
+ 1+ custom filter expressions
```

Cloud Connector reduces boilerplate rule management, especially for multi-path routing scenarios.

## Learn more

- [Cloud Connector overview](https://developers.cloudflare.com/rules/cloud-connector/)
- [Dashboard rule creation](https://developers.cloudflare.com/rules/cloud-connector/create-dashboard/)
- [API rule creation](https://developers.cloudflare.com/rules/cloud-connector/create-api/)
- [Terraform configuration](https://developers.cloudflare.com/rules/cloud-connector/create-terraform/)
- [Supported cloud providers](https://developers.cloudflare.com/rules/cloud-connector/providers/)

---

**See also**: For an origin-less approach using R2 + manual rules, see [Cloudflare Edge Hosting](./CloudflareEdgeHosting.md).
