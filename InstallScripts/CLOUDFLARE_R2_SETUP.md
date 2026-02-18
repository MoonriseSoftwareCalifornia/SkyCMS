<![CDATA[# CloudFlare R2 Documentation Deployment Setup

This guide explains how to deploy your SkyCMS documentation to CloudFlare R2 instead of GitHub Pages.

## Prerequisites

1. **CloudFlare Account** with R2 enabled
2. **Jekyll** installed (`gem install jekyll bundler`)
3. **AWS CLI** installed ([Download](https://aws.amazon.com/cli/))
4. **PowerShell** 7+ or Windows PowerShell

## Step 1: Create CloudFlare R2 Bucket

1. Log in to your CloudFlare dashboard
2. Navigate to **R2 Object Storage**
3. Click **Create bucket**
4. Enter a bucket name (e.g., `skycms-docs`)
5. Click **Create bucket**

## Step 2: Configure Public Access

### Option A: Public Bucket (Simplest)

1. In your R2 bucket settings, go to **Settings**
2. Under **Public Access**, enable **Allow Public Access**
3. CloudFlare will provide a public URL like: `https://pub-xxxxx.r2.dev`

### Option B: Custom Domain (Recommended)

1. In your R2 bucket settings, go to **Settings**
2. Click **Connect Domain**
3. Enter your custom domain (e.g., `docs.yourdomain.com`)
4. Add the required DNS records in CloudFlare DNS:
   - Type: `CNAME`
   - Name: `docs` (or your subdomain)
   - Target: Your R2 bucket URL
5. Enable **Public Access** for the custom domain

## Step 3: Generate R2 API Tokens

1. In CloudFlare dashboard, go to **R2 Object Storage**
2. Click **Manage R2 API Tokens**
3. Click **Create API Token**
4. Configure:
   - **Token Name**: `SkyCMS Docs Deploy`
   - **Permissions**: `Object Read & Write`
   - **Bucket**: Select your bucket or leave as "All buckets"
5. Click **Create API Token**
6. **Save** the Access Key ID and Secret Access Key (you won't see them again!)

## Step 4: Get Your CloudFlare Account ID

1. In CloudFlare dashboard, go to **R2 Object Storage**
2. Your Account ID is displayed at the top right of the R2 overview page
3. Copy and save it

## Step 5: (Optional) Get API Token for Cache Purging

If you want to automatically purge the CloudFlare cache after deployment:

1. Go to **My Profile** > **API Tokens**
2. Click **Create Token**
3. Use the **Edit zone DNS** template or create custom with:
   - **Zone - Cache Purge - Purge**
   - **Zone Resources**: Include your zone
4. Create and save the token

Get your Zone ID:
1. Go to your domain in CloudFlare
2. The Zone ID is on the right side of the **Overview** page

## Step 6: Set Environment Variables

For security, store your credentials as environment variables:

### Windows PowerShell

```powershell
# Set permanently (current user)
[System.Environment]::SetEnvironmentVariable('CLOUDFLARE_R2_ACCESS_KEY_ID', 'your-access-key-id', 'User')
[System.Environment]::SetEnvironmentVariable('CLOUDFLARE_R2_SECRET_ACCESS_KEY', 'your-secret-key', 'User')
[System.Environment]::SetEnvironmentVariable('CLOUDFLARE_ACCOUNT_ID', 'your-account-id', 'User')
[System.Environment]::SetEnvironmentVariable('CLOUDFLARE_ZONE_ID', 'your-zone-id', 'User')
[System.Environment]::SetEnvironmentVariable('CLOUDFLARE_API_TOKEN', 'your-api-token', 'User')

# Restart your terminal for changes to take effect
```

### Linux/macOS

Add to `~/.bashrc` or `~/.zshrc`:

```bash
export CLOUDFLARE_R2_ACCESS_KEY_ID="your-access-key-id"
export CLOUDFLARE_R2_SECRET_ACCESS_KEY="your-secret-key"
export CLOUDFLARE_ACCOUNT_ID="your-account-id"
export CLOUDFLARE_ZONE_ID="your-zone-id"
export CLOUDFLARE_API_TOKEN="your-api-token"
```

Then run: `source ~/.bashrc` or `source ~/.zshrc`

## Step 7: Deploy Your Documentation

### Basic Deployment

```powershell
cd D:\source\SkyCMS\InstallScripts
.\deploy-docs-to-cloudflare.ps1 -BucketName "skycms-docs" -AccountId "your-account-id"
```

### With Cache Purging

```powershell
.\deploy-docs-to-cloudflare.ps1 `
    -BucketName "skycms-docs" `
    -AccountId "your-account-id" `
    -PurgeCache `
    -ZoneId "your-zone-id" `
    -ApiToken "your-api-token"
```

### Using Environment Variables

```powershell
# If all environment variables are set:
.\deploy-docs-to-cloudflare.ps1 `
    -BucketName "skycms-docs" `
    -AccountId $env:CLOUDFLARE_ACCOUNT_ID `
    -PurgeCache
```

### Skip Build (Deploy Only)

If you've already built the site and just want to deploy:

```powershell
.\deploy-docs-to-cloudflare.ps1 `
    -BucketName "skycms-docs" `
    -AccountId "your-account-id" `
    -SkipBuild
```

## Step 8: Configure Bucket for Static Website Hosting

CloudFlare R2 doesn't have built-in static website hosting like S3, but you can use CloudFlare Workers or Pages:

### Option A: CloudFlare Pages (Recommended)

1. Go to **Pages** in CloudFlare dashboard
2. Click **Create a project**
3. Select **Connect to Git** or **Direct Upload**
4. For Direct Upload:
   - Upload the `_site` folder from `Docs/_site`
   - CloudFlare will deploy it automatically

### Option B: CloudFlare Workers

Create a Worker to serve your R2 bucket as a website:

```javascript
export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    let key = url.pathname.slice(1);
    
    // Serve index.html for directory requests
    if (key.endsWith('/')) {
      key += 'index.html';
    } else if (!key.includes('.')) {
      key += '/index.html';
    }
    
    // Get object from R2
    const object = await env.DOCS_BUCKET.get(key);
    
    if (object === null) {
      // Try with .html extension
      const htmlKey = key.replace(/\/?$/, '.html');
      const htmlObject = await env.DOCS_BUCKET.get(htmlKey);
      
      if (htmlObject === null) {
        return new Response('Not Found', { status: 404 });
      }
      
      return new Response(htmlObject.body, {
        headers: {
          'Content-Type': htmlObject.httpMetadata.contentType || 'text/html',
          'Cache-Control': 'public, max-age=3600'
        }
      });
    }
    
    return new Response(object.body, {
      headers: {
        'Content-Type': object.httpMetadata.contentType || 'application/octet-stream',
        'Cache-Control': 'public, max-age=3600'
      }
    });
  }
};
```

Bind your R2 bucket to the worker as `DOCS_BUCKET`.

### Option C: Transform Rules (Modern Approach - Recommended)

**Update (2026)**: CloudFlare now offers Transform Rules which provide a simpler, free-tier friendly alternative to Workers for handling URL routing.

**Advantages over Workers:**
- Available on all CloudFlare plans (including Free)
- No request quotas or rate limits
- Simpler configuration (no code required)
- Better for multiple websites (Workers share quotas)

**Setup Depends on Content Type:**

#### For MkDocs-Generated Sites

Files are stored WITH extensions in directories (e.g., `/installation/index.html`):

**Rule 1**: Match root path
- Filter: `http.request.uri.path eq "/"`
- Rewrite to: `/index.html`

**Rule 2**: Match directory paths with trailing slash
- Filter: `ends_with(http.request.uri.path, "/") and not (http.request.uri.path eq "/")`
- Rewrite to: `concat(http.request.uri.path, "index.html")`

#### For SkyCMS-Generated Sites

Files are stored WITHOUT extensions (e.g., `/about`, `/products/widget`):

**Rule 1 (only rule needed)**: Match root path
- Filter: `http.request.uri.path eq "/"`
- Rewrite to: `/index.html`

Other paths are directly accessible without rewriting since SkyCMS stores them without extensions.

#### Apply Transform Rules

1. Go to CloudFlare Dashboard → Your Domain → **Rules** → **Transform Rules**
2. Click **Create rule** → **URL Rewrite**
3. For **MkDocs**: Create 2 rules (root + directory)
4. For **SkyCMS**: Create 1 rule (root only)
5. Test with [CloudFlare Trace](https://developers.cloudflare.com/rules/trace-request/)

For complete setup details and examples, see:
- [CloudFlare Edge Hosting Guide](../Docs/Installation/CloudflareEdgeHosting.md#step-4--create-custom-rules-to-handle-root-access)
- [Real-World Example: Multiple Sites](../Docs/Installation/CloudflareEdgeHosting.md#real-world-example-multiple-sites-on-cloudflare)
- [GitHub Secrets Setup](../.github/CLOUDFLARE_SECRETS_SETUP.md#post-deployment-url-rewrite-rules-required)

## Automation with GitHub Actions

Create `.github/workflows/deploy-docs-cloudflare.yml`:

```yaml
name: Deploy Docs to CloudFlare R2

on:
  push:
    branches: ["main"]
    paths:
      - 'Docs/**'
  workflow_dispatch:

jobs:
  deploy:
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout
        uses: actions/checkout@v4
      
      - name: Setup Ruby
        uses: ruby/setup-ruby@v1
        with:
          ruby-version: '3.1'
          bundler-cache: true
          working-directory: ./Docs
      
      - name: Build Jekyll site
        run: |
          cd Docs
          bundle install
          JEKYLL_ENV=production bundle exec jekyll build
      
      - name: Configure AWS CLI for CloudFlare R2
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.CLOUDFLARE_R2_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.CLOUDFLARE_R2_SECRET_ACCESS_KEY }}
          aws-region: auto
      
      - name: Deploy to CloudFlare R2
        run: |
          aws s3 sync ./Docs/_site s3://${{ secrets.R2_BUCKET_NAME }} \
            --endpoint-url https://${{ secrets.CLOUDFLARE_ACCOUNT_ID }}.r2.cloudflarestorage.com \
            --delete \
            --cache-control "public, max-age=3600"
      
      - name: Purge CloudFlare Cache
        if: ${{ secrets.CLOUDFLARE_API_TOKEN != '' }}
        run: |
          curl -X POST "https://api.cloudflare.com/client/v4/zones/${{ secrets.CLOUDFLARE_ZONE_ID }}/purge_cache" \
            -H "Authorization: Bearer ${{ secrets.CLOUDFLARE_API_TOKEN }}" \
            -H "Content-Type: application/json" \
            --data '{"purge_everything":true}'
```

### Required GitHub Secrets

Add these secrets to your repository (**Settings** > **Secrets and variables** > **Actions**):

- `CLOUDFLARE_R2_ACCESS_KEY_ID`
- `CLOUDFLARE_R2_SECRET_ACCESS_KEY`
- `CLOUDFLARE_ACCOUNT_ID`
- `R2_BUCKET_NAME`
- `CLOUDFLARE_ZONE_ID` (optional, for cache purging)
- `CLOUDFLARE_API_TOKEN` (optional, for cache purging)

## Troubleshooting

### Jekyll Build Fails

```powershell
# Install dependencies
cd Docs
bundle install

# Try building manually
bundle exec jekyll build --verbose
```

### AWS CLI Authentication Fails

Verify your credentials:

```powershell
aws s3 ls --endpoint-url https://your-account-id.r2.cloudflarestorage.com
```

### Files Not Accessible

Make sure:
1. Bucket has public access enabled, OR
2. Custom domain is configured correctly, OR
3. CloudFlare Worker is properly configured

### Cache Not Updating

Manually purge cache:

```powershell
curl -X POST "https://api.cloudflare.com/client/v4/zones/YOUR-ZONE-ID/purge_cache" `
  -H "Authorization: Bearer YOUR-API-TOKEN" `
  -H "Content-Type: application/json" `
  --data '{"purge_everything":true}'
```

## Cost Comparison

CloudFlare R2 Pricing (as of 2025):
- **Storage**: $0.015 per GB/month
- **Class A Operations** (writes): $4.50 per million
- **Class B Operations** (reads): $0.36 per million
- **Egress**: Free (this is the big win!)

For a typical documentation site:
- Storage: ~100 MB = **$0.0015/month**
- Operations: Negligible for low-traffic docs
- **Total: Essentially free** for most use cases

## Additional Resources

- [CloudFlare R2 Documentation](https://developers.cloudflare.com/r2/)
- [AWS CLI with R2](https://developers.cloudflare.com/r2/api/s3/api/)
- [CloudFlare Workers](https://developers.cloudflare.com/workers/)
- [Jekyll Documentation](https://jekyllrb.com/docs/)
]]>