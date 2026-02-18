# GitHub Secrets Setup for CloudFlare R2 Deployment

This guide explains how to configure GitHub repository secrets for automated documentation deployment to CloudFlare R2.

## Required Secrets

Navigate to your repository on GitHub:
1. Go to **Settings** → **Secrets and variables** → **Actions**
2. Click **New repository secret** for each of the following:

### 1. CLOUDFLARE_R2_ACCESS_KEY_ID
- **Value**: Your CloudFlare R2 Access Key ID
- **How to get it**:
  1. Log in to CloudFlare dashboard
  2. Go to **R2 Object Storage**
  3. Click **Manage R2 API Tokens**
  4. Click **Create API Token**
  5. Set permissions to **Object Read & Write**
  6. Copy the **Access Key ID**

### 2. CLOUDFLARE_R2_SECRET_ACCESS_KEY
- **Value**: Your CloudFlare R2 Secret Access Key
- **How to get it**:
  1. Same process as above
  2. Copy the **Secret Access Key** (shown only once!)
  3. ⚠️ **Important**: Save this immediately - you won't see it again!

### 3. CLOUDFLARE_ACCOUNT_ID
- **Value**: Your CloudFlare Account ID
- **How to get it**:
  1. In CloudFlare dashboard, go to **R2 Object Storage**
  2. Your Account ID is displayed at the top right
  3. Format: `1234567890abcdef1234567890abcdef`

### 4. R2_BUCKET_NAME
- **Value**: The name of your R2 bucket for documentation
- **Example**: `skycms-docs`
- **How to get it**:
  1. In CloudFlare R2 dashboard, note your bucket name
  2. If you haven't created one yet, create a new bucket

## Optional Secrets (for Cache Purging)

### 5. CLOUDFLARE_ZONE_ID (Optional but Recommended)
- **Value**: Your CloudFlare Zone ID
- **How to get it**:
  1. Go to CloudFlare dashboard
  2. Select your domain/website
  3. Scroll down on the **Overview** page
  4. Find **Zone ID** on the right side
  5. Format: `1234567890abcdef1234567890abcdef`

### 6. CLOUDFLARE_API_TOKEN (Optional but Recommended)
- **Value**: CloudFlare API Token with cache purge permissions
- **How to get it**:
  1. Go to **My Profile** → **API Tokens**
  2. Click **Create Token**
  3. Use **Edit zone DNS** template or create custom with:
     - Permission: **Zone** → **Cache Purge** → **Purge**
     - Zone Resources: **Include** → **Specific zone** → Select your domain
  4. Click **Continue to summary**
  5. Click **Create Token**
  6. Copy the token (shown only once!)

## Verification Steps

After adding all secrets, verify they're set correctly:

1. Go to **Settings** → **Secrets and variables** → **Actions**
2. You should see these secrets listed:
   - ✅ `CLOUDFLARE_R2_ACCESS_KEY_ID`
   - ✅ `CLOUDFLARE_R2_SECRET_ACCESS_KEY`
   - ✅ `CLOUDFLARE_ACCOUNT_ID`
   - ✅ `R2_BUCKET_NAME`
   - ⚪ `CLOUDFLARE_ZONE_ID` (optional)
   - ⚪ `CLOUDFLARE_API_TOKEN` (optional)

## Security Best Practices

- ✅ **Never commit secrets** to your repository
- ✅ **Use GitHub secrets** for all sensitive data
- ✅ **Rotate tokens** periodically (every 90 days recommended)
- ✅ **Use minimum required permissions** for API tokens
- ✅ **Monitor token usage** in CloudFlare dashboard
- ❌ **Don't share tokens** via email, chat, or screenshots

## Testing the Workflow

After setting up secrets:

1. Make a small change to any file in the `Docs/` folder
2. Commit and push to the `main` branch:
   ```bash
   git add Docs/
   git commit -m "Test CloudFlare R2 deployment"
   git push origin main
   ```
3. Go to **Actions** tab in your GitHub repository
4. Watch the **Deploy Docs to CloudFlare R2** workflow run
5. Check the deployment summary for success confirmation

## Manual Workflow Trigger

You can also trigger the workflow manually:

1. Go to **Actions** tab
2. Select **Deploy Docs to CloudFlare R2**
3. Click **Run workflow**
4. Select branch (usually `main`)
5. Click **Run workflow**

## Troubleshooting

### Workflow fails with "Access Denied"
- ✅ Verify `CLOUDFLARE_R2_ACCESS_KEY_ID` and `CLOUDFLARE_R2_SECRET_ACCESS_KEY` are correct
- ✅ Ensure API token has **Object Read & Write** permissions
- ✅ Check that the token is for the correct CloudFlare account

### Workflow fails with "Bucket not found"
- ✅ Verify `R2_BUCKET_NAME` matches your actual bucket name exactly
- ✅ Ensure bucket exists in CloudFlare R2
- ✅ Check `CLOUDFLARE_ACCOUNT_ID` is correct

### Cache purge step fails
- ⚪ This is optional - workflow will still succeed
- ✅ Verify `CLOUDFLARE_ZONE_ID` and `CLOUDFLARE_API_TOKEN` are set
- ✅ Ensure API token has **Cache Purge** permission

### Files deployed but site not accessible
- ✅ Configure bucket for public access in CloudFlare R2 settings
- ✅ Or set up custom domain mapping (recommended)
- ✅ Configure Transform Rules for directory index handling (see below)

### Directory URLs return 404 errors (e.g., /docs/ not found)
- ✅ CloudFlare R2 does not automatically serve `index.html` for directory paths
- ✅ You must configure a **Transform Rule** to handle directory indexes
- ✅ See the "Directory Index Handling" section below for setup instructions

## Post-Deployment: URL Rewrite Rules (Required!)

**CRITICAL**: CloudFlare R2 does not automatically serve HTML files. You **must configure Transform Rules** to handle URL routing properly. The rules depend on **how your documentation was generated**.

### If Using MkDocs for Documentation

**File structure**: `/installation/index.html`, `/guides/tutorial/index.html`

**Create two URL Rewrite rules:**

1. **"Serve root index (MkDocs)"**
   - Filter: `http.request.uri.path eq "/"`
   - Rewrite to: `/index.html`

2. **"Serve directory index (MkDocs)"**
   - Filter: `ends_with(http.request.uri.path, "/") and not (http.request.uri.path eq "/")`
   - Rewrite to: `concat(http.request.uri.path, "index.html")`

### If Using SkyCMS for Website Content

**File structure**: `/about`, `/products/widget` (no extensions)

**Create one URL Rewrite rule:**

1. **"Serve root index (SkyCMS)"**
   - Filter: `http.request.uri.path eq "/"`
   - Rewrite to: `/index.html`

**Why only one rule?** SkyCMS stores pages without file extensions, so they're directly accessible. R2 serves them with the correct `text/html` content-type based on file metadata. Only the root path (`/`) needs special handling to map to `/index.html`.

### Applied to Multiple Domains

If you have multiple Cloudflare zones (separate domains), you must configure rules **in each zone independently**:

**Example**: Two zones under your account
- **Zone A** (`docs.sky-cms.com`): Create 2 MkDocs rules (root + directory index)
- **Zone B** (`sky-cms.com`): Create 1 SkyCMS rule (root index only)

Each zone's rules only apply to that domain. Rules do not cross zone boundaries.

For detailed real-world examples with your actual domains, see:
[Cloudflare Edge Hosting - Real-World Example](../Docs/Installation/CloudflareEdgeHosting.md#real-world-example-multiple-sites-on-cloudflare)

### Deploy Rules

1. Navigate to **Rules** → **Transform Rules** → **URL Rewrite**
2. Create rules in the specified order
3. Test with [Cloudflare Trace](https://developers.cloudflare.com/rules/trace-request/)

For complete setup instructions, see: [Cloudflare Edge Hosting Guide](../Docs/Installation/CloudflareEdgeHosting.md#step-4--create-custom-rules-to-handle-root-access)

## Quick Reference Table

| Secret Name | Required | Format | Example |
|------------|----------|--------|---------|
| `CLOUDFLARE_R2_ACCESS_KEY_ID` | ✅ Yes | 32-char alphanumeric | `abc123def456...` |
| `CLOUDFLARE_R2_SECRET_ACCESS_KEY` | ✅ Yes | 64-char base64 | `aBcDeF123...` |
| `CLOUDFLARE_ACCOUNT_ID` | ✅ Yes | 32-char hex | `1a2b3c4d5e6f...` |
| `R2_BUCKET_NAME` | ✅ Yes | Bucket name | `skycms-docs` |
| `CLOUDFLARE_ZONE_ID` | ⚪ Optional | 32-char hex | `9z8y7x6w5v...` |
| `CLOUDFLARE_API_TOKEN` | ⚪ Optional | Variable length | `token_xyz...` |

## Next Steps

After successful deployment:

1. ✅ **Configure Transform Rules** for directory index handling (see above - required for working documentation links)
2. ✅ Verify your documentation is accessible at your R2 public URL or custom domain
3. ✅ Set up custom domain if not already done (see [CloudflareEdgeHosting.md](../Docs/Installation/CloudflareEdgeHosting.md))
4. ✅ Test all documentation navigation and directory paths
5. ✅ Monitor deployment logs for any issues
6. ✅ Consider disabling the old GitHub Pages workflow if migrating

## Support

For more detailed CloudFlare R2 setup instructions, see:
- [../InstallScripts/CLOUDFLARE_R2_SETUP.md](../InstallScripts/CLOUDFLARE_R2_SETUP.md)
- [CloudFlare R2 Documentation](https://developers.cloudflare.com/r2/)
