---
title: Cloudflare Edge Hosting Guide
description: Origin-less static website architecture using Cloudflare R2 and edge rules
keywords: Cloudflare, R2, edge-hosting, origin-less, CDN, static-website
audience: [developers, devops]
version: 2.0
last_updated: "2026-01-03"
stage: stable
read_time: 7
---

# Cloudflare Edge Hosting: Origin-less Static Website Architecture

This guide shows how to host a static site on Cloudflare using an origin-less (edge) pattern with R2 object storage and Cloudflare Rules for request handling at the edge. It also explains how to configure SkyCMS to deploy your build output to R2.

Key idea: Unlike traditional static hosting that serves from a single origin, edge/origin-less sites are distributed and executed at Cloudflare's global edge—improving latency, resilience, and cost profiles.

## When to use this
- You want origin-less static hosting with Cloudflare R2 + Rules (no Workers required).
- You need a low-cost, globally distributed static site backed by S3-compatible storage.

**Note**: Cloudflare also offers [Cloud Connector (beta)](./CloudflareCloudConnector.md), which simplifies routing to cloud storage by automatically configuring headers and SSL/TLS. See [Cloud Connector vs. Edge Hosting](./CloudflareCloudConnector.md#comparison-cloud-connector-vs-manual-rules) for when to use each approach.

## Why this matters
- Removes origin servers, reducing ops overhead and latency.
- Clarifies the minimal setup: R2 bucket + custom domain + rules for HTTPS and index rewrites.

## Key takeaways
- R2 is S3-compatible; SkyCMS publishes directly using Account ID + Key ID/Secret + bucket.
- Two key rules: HTTP→HTTPS redirect and root→index.html rewrite.
- Custom domain binding is required for clean URLs and TLS.

## Prerequisites
- Cloudflare account with R2 enabled; ability to create API token/keys.
- Wrangler CLI authenticated; domain in Cloudflare DNS if using custom domains.

## Quick path
1. Create R2 bucket; grab Account ID and keys (S3 API token).
2. Set `StorageConnectionString` for R2 in SkyCMS; publish.
3. Bind custom domain to R2 and add Rules (HTTPS redirect, index.html rewrite).

## What "origin-less" means (vs. traditional static hosting)

- Traditional: User → CDN → Origin Server (S3/Netlify/VPS) → Response
- Origin-less/Edge: User → Cloudflare Edge (R2 + Rules) → R2 (object) → Response

Benefits of origin-less:

- No centralized origin server to manage or scale
- Content served near users, reducing latency
- Built-in DDoS protection and global availability
- Pay for usage (storage and requests), not for idle servers

## Prerequisites

- Cloudflare account with R2 enabled and Cloudflare Rules configured (no Worker required)
- Wrangler CLI installed and authenticated
- A domain in Cloudflare DNS (optional but recommended)
- Your site's static build output (for SkyCMS, see "Deploying from SkyCMS" below)

## Step 1 — Create an R2 bucket

You can create R2 storage from the Cloudflare dashboard, or
you can use 'wrangler' as shown below.

```bash
npm install -g wrangler
wrangler login
wrangler r2 bucket create your-website-bucket

```
## Step 2 - Connect SkyCMS to R2

Cloudflare R2 is S3-compatible. With SkyCMS you'll provide your Account ID, bucket name, and S3-style credentials (Key ID/Secret).

Note: Cloudflare R2 uses a custom S3 endpoint (eg. `https://{account-id}.r2.cloudflarestorage.com`). For guidance on credentials and endpoint information, see [Cloudflare R2 access keys](../Configuration/Cloudflare-R2-AccessKeys.md).

Quick setup guide: see [Cloudflare R2 access keys](../Configuration/Cloudflare-R2-AccessKeys.md) to find your Account ID and bucket, and to generate an S3 API token (read/write/delete).

Format the connection string for R2 storage in the following manner. Note it requires
an Account ID, Bucket name, Key ID and Key Secret:

```json
{
   "ConnectionStrings": {
      "StorageConnectionString": "AccountId={Account ID};Bucket={bucket name};KeyId={access-key-id};Key={secret-access-key};"
   }
}
```

## Step 3 — Bind the R2 container to your domain

Open your R2 storage on the Cloudflare dashboard, choose "Settings" then "Custom Domains."

Near the top of the dialog, click '+ Add' button to the right of the title "Custom Domains."

Follow the dialog from that point forward.

## Step 4 — Create custom rules to handle root access

Open your domain on the Cloudflare dashboard, then find and expand "Rules" on the left edge of the dialog.

First, create a basic HTTP to HTTPS redirect using the built-in template. Then, create URL Rewrite rules based on what type of content you're hosting.

### Rule A: HTTP to HTTPS Redirect (All Sites)

Use the built-in template provided by Cloudflare. This applies to all site types.

### Rule B: The Essential Rule for SkyCMS Sites

**Applies to**: Static content published by SkyCMS (`PublishingService`)

**File structure**: SkyCMS generates files WITHOUT extensions:
```
/about
/products/widget
/docs/getting-started
/index.html (root only)
```

**Create one URL Rewrite Rule:**

**Name:** "Serve root index"

**Filter Expression:**
```
http.request.uri.path eq "/"
```

**Rewrite to (Static):**
```
/index.html
```

**Result**: `/` → `/index.html` ✓

**Why only one rule?** SkyCMS stores files without extensions (e.g., `/about`, `/products/widget`), so they're directly accessible by their paths. R2 serves them with the `text/html` content-type based on file metadata. Only the root path needs to map to `index.html`.

**Result Examples:**
- `/about` → `/about` (direct, no rewrite) ✓
- `/products/widget` → `/products/widget` (direct, no rewrite) ✓
- `/docs/getting-started` → `/docs/getting-started` (direct, no rewrite) ✓
- `/style.css` → `/style.css` (unchanged) ✓

---

### Rule C (Optional): If You Also Have MkDocs Documentation

**Suppose you have one website created with MkDocs and the rest created by SkyCMS?** Simply add one more rule BEFORE the SkyCMS root rule above.

**Applies to**: Documentation built with MkDocs (with `use_directory_urls: true`)

**File structure**: MkDocs generates files WITH extensions at directory level:
```
/installation/index.html
/guides/tutorial/index.html
/index.html
```

**Add this URL Rewrite Rule** (before the SkyCMS root rule):

**Name:** "MkDocs directory index"

**Filter Expression:**
```
(http.host eq "docs.sky-cms.com") and ends_with(http.request.uri.path, "/") and not (http.request.uri.path eq "/")
```

**Rewrite to (Dynamic):**
```
concat(http.request.uri.path, "index.html")
```

**Result Examples:**
- `https://docs.sky-cms.com/installation/` → `/installation/index.html` ✓
- `https://docs.sky-cms.com/guides/tutorial/` → `/guides/tutorial/index.html` ✓
- `https://sky-cms.com/about` → `/about` (unaffected, different host) ✓

**Why this filter?**
- `(http.host eq "docs.sky-cms.com")` - Only apply to your MkDocs domain
- `ends_with(http.request.uri.path, "/")` - Only match directory paths
- `and not (http.request.uri.path eq "/")` - Exclude root (handled by the SkyCMS root rule)

**Rule Execution Order:**
1. MkDocs rule (if present) - matches docs.sky-cms.com subdirectories
2. SkyCMS root rule - matches `/` on all domains
3. All other content - direct access via R2

---

## Why Transform Rules Instead of Workers?

Transform Rules are available on **all Cloudflare plans** (including Free) and don't count against Workers usage limits. For multiple websites, Transform Rules are more cost-effective than Cloudflare Workers, which share a daily request quota across all sites on the free tier.

## Applying Rules to Specific Domains

When you have multiple sites on Cloudflare, it's important to understand how rules are scoped:

### Separate Cloudflare Zones (Recommended)

If your domains are **separate Cloudflare zones** (most common setup):
- `docs.sky-cms.com` → **Zone A** (Cloudflare nameservers for this domain)
- `sky-cms.com` → **Zone B** (Cloudflare nameservers for this domain)

**Rules are automatically zone-specific:**
- Rules created in Zone A only apply to `docs.sky-cms.com`
- Rules created in Zone B only apply to `sky-cms.com`
- No cross-zone interference

**How to apply:**
1. Log into Cloudflare and select **Zone A** (`docs.sky-cms.com`)
2. Go to **Rules** → **Transform Rules** → **URL Rewrite**
3. Create MkDocs rules (2 rules for directory index)
4. Log out and select **Zone B** (`sky-cms.com`)
5. Go to **Rules** → **Transform Rules** → **URL Rewrite**
6. Create SkyCMS rules (1 rule for root index)

Each zone's rules apply independently to its domain.

### Subdomains Under One Zone (Advanced)

If your sites are **subdomains under one Cloudflare zone** (e.g., `docs.example.com` and `www.example.com` both under `example.com`):

You can target specific subdomains using the `http.host` field in filter expressions:

**For a specific subdomain (root path):**
```
(http.host eq "docs.example.com") and (http.request.uri.path eq "/")
```

**Note**: Both conditions are required:
- `http.host eq "docs.example.com"` - ensures the rule only applies to this subdomain
- `http.request.uri.path eq "/"` - ensures the rewrite only applies to the root path (otherwise all paths on that subdomain would get rewritten)

**For multiple subdomains with root path:**
```
(http.host matches "^(docs|api)\\.example\\.com$") and (http.request.uri.path eq "/")
```

**For all subdomains except one (root path):**
```
(http.host ne "admin.example.com") and (http.request.uri.path eq "/")
```

However, this approach is more complex and requires carefully crafted expressions for each subdomain. **It's generally simpler to use separate Cloudflare zones for different sites**, where host conditions aren't needed (rules are automatically zone-specific).

---

## Important Notes

- **Start simple**: All Cloudflare zones using SkyCMS only need Rule B (one rule for the root path)
- **Add as needed**: If you add MkDocs documentation to one domain, simply add Rule C before the SkyCMS root rule
- **Rule order matters**: If using both rules, ensure the MkDocs rule (Rule C) comes BEFORE the SkyCMS root rule (Rule B)
- **Test thoroughly**: Use [Cloudflare Trace](https://developers.cloudflare.com/rules/trace-request/) to validate rule matches before deploying
- **Query strings preserved**: The rewrite only affects the path, not query parameters
- **Asset serving**: CSS, JS, and image files have extensions, so they're unaffected by these rules

## Quick Reference Table

| Use Case | Rules Needed | Configuration |
|----------|---|---|
| **SkyCMS only** (all sites generated by SkyCMS) | 1 rule | Rule B: Root index rule |
| **SkyCMS + MkDocs mixed** (some sites SkyCMS, one MkDocs) | 2 rules | Rule C (MkDocs) + Rule B (SkyCMS root) |
| **MkDocs only** (pure documentation site) | 1 rule | Just Rule C, but remove the `http.host` filter |

---

## Real-World Example: Mixed SkyCMS + MkDocs Setup

### Your Setup

| Domain | Generator | Purpose |
|--------|-----------|---------|
| `docs.sky-cms.com` | MkDocs | Documentation site |
| `sky-cms.com` | SkyCMS | Main website |
| `www.sky-cms.com` | SkyCMS | Website alias |
| Other domains | SkyCMS | Additional sites |

### Rules Required: 2 Total

**Rule 1: "MkDocs directory index"** (added before the SkyCMS rule)
```
Filter: (http.host eq "docs.sky-cms.com") and ends_with(http.request.uri.path, "/") and not (http.request.uri.path eq "/")
Rewrite to: concat(http.request.uri.path, "index.html")
```

**Test URLs:**
- `https://docs.sky-cms.com/installation/` → `/installation/index.html` ✓
- `https://docs.sky-cms.com/configuration/` → `/configuration/index.html` ✓

---

**Rule 2: "Serve root index"** (applies to all domains)
```
Filter: http.request.uri.path eq "/"
Rewrite to: /index.html
```

**Test URLs:**
- `https://docs.sky-cms.com/` → `/index.html` ✓
- `https://sky-cms.com/` → `/index.html` ✓
- `https://www.sky-cms.com/` → `/index.html` ✓

---

### How the Rules Work Together

**For `https://docs.sky-cms.com/installation/`:**
1. Rule 1: Filter matches (right host, path ends with `/`, not root) → Rewrites to `/installation/index.html` ✓
2. (Rule 2 not checked, Rule 1 already matched)

**For `https://docs.sky-cms.com/`:**
1. Rule 1: No match (path is `/`, excluded by `not (http.request.uri.path eq "/")`)
2. Rule 2: Filter matches (path is `/`) → Rewrites to `/index.html` ✓

**For `https://sky-cms.com/about`:**
1. Rule 1: No match (different host, no trailing slash)
2. Rule 2: No match (path is not `/`)
3. R2 serves `/about` directly ✓

**For `https://sky-cms.com/`:**
1. Rule 1: No match (different host)
2. Rule 2: Filter matches (path is `/`) → Rewrites to `/index.html` ✓

---

---

---

With this edge/origin-less approach, your site is globally distributed, highly performant, and free from the operational overhead of maintaining an origin server. R2 stores your files, Cloudflare Rules handle root and rewrite behaviors at the edge, and SkyCMS slots into your pipeline to publish updates reliably.
