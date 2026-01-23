---
title: Setup Wizard - Step 3 Publisher
description: Configure publisher URL and settings in setup wizard
keywords: setup-wizard, publisher, URL, configuration, deployment
audience: [developers, administrators]
---

# Setup Wizard: Step 3 - Publisher Settings

[← Admin Account](./SetupWizard-Step2-Admin.md) | **Step 3 of 6** | [Next: Email Configuration →](./SetupWizard-Step4-Email.md)

---

## Configure Website Publisher Settings

Configure how your website operates and publishes content.

![Publisher settings screen](../images/screenshots/setup-wizard-publisher.webp)

---

## Fields

### Website URL (Required)

The public URL where your published website will be accessible.

**Field Name**: `PublisherUrl`  
**Required**: ✅ Yes  
**Format**: Valid URL (http:// or https://)  
**Can be pre-configured**: Yes (via `CosmosPublisherUrl`)

**Examples**:
- `https://www.mywebsite.com`
- `https://mysite.azurewebsites.net`
- `http://localhost:8080` (development only)

**Notes**:
- Must include protocol (`https://` or `http://`)
- Should match your domain/CDN configuration
- No trailing slash

### Website Title (Required)

The name of your website displayed in the editor and page titles.

**Field Name**: `WebsiteTitle`  
**Required**: ✅ Yes  
**Example**: `My Company Website`, `Tech Blog`

### Static Website Mode (Recommended)

Enable static website mode for better performance and CDN caching.

**Field Name**: `StaticWebPages`  
**Type**: Checkbox  
**Default**: ✅ Enabled (recommended)  
**Can be pre-configured**: Yes (via `CosmosStaticWebPages`)

**When Enabled**:
- Pages are pre-rendered as HTML
- Faster page loads
- Better CDN caching
- Reduced server load

**When Disabled**:
- Pages rendered dynamically on each request
- Useful for database-driven content
- Higher server load

**Recommendation**: ✅ **Enable** for most websites.

### Require Authentication

Require users to log in before accessing the website.

**Field Name**: `CosmosRequiresAuthentication`  
**Type**: Checkbox  
**Default**: ❌ Disabled  
**Can be pre-configured**: Yes (via `CosmosRequiresAuthentication`)

**When Enabled**:
- All pages require login
- Visitors must create accounts or authenticate
- Useful for intranets, member sites, private content

**When Disabled**:
- Public access to all published content
- Standard for public websites

**Recommendation**: ❌ **Disable** for public websites, ✅ **Enable** for private/intranet sites.

### Allowed File Types

File extensions that can be uploaded through the file manager.

**Field Name**: `AllowedFileTypes`  
**Required**: ✅ Yes  
**Format**: Comma-separated list with leading dots  
**Default**: `.js,.css,.htm,.html,.mov,.webm,.avi,.mp4,.mpeg,.ts,.svg,.json`

**Examples**:
```
.js,.css,.htm,.html,.svg,.json,.pdf
.jpg,.jpeg,.png,.gif,.svg,.webp,.pdf,.doc,.docx
```

**Common File Types**:
- **Web**: `.js`, `.css`, `.htm`, `.html`, `.svg`, `.json`
- **Images**: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.svg`, `.ico`
- **Video**: `.mp4`, `.webm`, `.mov`, `.avi`, `.mpeg`
- **Documents**: `.pdf`, `.doc`, `.docx`, `.txt`

**Security Note**: Only allow file types you trust and need. Avoid allowing executable files (`.exe`, `.dll`, `.sh`, `.bat`).

### Site Design Template (Optional)

Choose a pre-built design template to get started quickly.

**Field Name**: `SiteDesignId`  
**Type**: Dropdown/Selection  
**Default**: None (blank site)

**Available Templates** (may vary by version):
- **Blank** - Empty site, full customization
- **Business** - Professional business template
- **Blog** - Blog-focused layout
- **Portfolio** - Portfolio/showcase template

**Note**: Templates include layouts, sample pages, and styling. You can customize after setup.

---

## Actions

### "Next" Button

Proceeds to **Step 4: Email Configuration** after validation.

**Validation**:
- Website URL is required and valid format
- Website title is required
- Allowed file types is required

---

## Pre-Configuration with Environment Variables

Pre-configure publisher settings via environment variables:

```powershell
$env:CosmosPublisherUrl = "https://www.mywebsite.com"
$env:CosmosStaticWebPages = "true"
$env:CosmosRequiresAuthentication = "false"
$env:AllowedFileTypes = ".js,.css,.htm,.html,.svg,.json,.jpg,.png,.pdf"
```

**When pre-configured**:
- Fields are read-only in the wizard
- Values cannot be changed (intentional)

---

## Troubleshooting

### "Website URL is required"

**Solution**: Enter your website's public URL with protocol (`https://www.mywebsite.com`)

### "Invalid URL format"

**Common issues**:
- Missing protocol (`mywebsite.com` should be `https://mywebsite.com`)
- Extra spaces or trailing slashes
- Invalid characters

**Solution**: Format as `https://yourdomain.com` (no trailing slash)

### "Allowed file types must start with a dot"

**Incorrect**: `jpg,png,pdf`  
**Correct**: `.jpg,.png,.pdf`

### Cannot Edit Pre-Configured Fields

**Cause**: Fields are pre-configured via environment variables.

**Solution**: This is intentional for security. To change values, update environment variables and restart.

---

## What Happens Next

After clicking **Next**, you'll proceed to:

**[Step 4: Email Configuration →](./SetupWizard-Step4-Email.md)** (Optional)

Publisher settings are saved and will be applied when setup completes.

---

## See Also

- **[Setup Wizard Overview](./SetupWizard.md)** - Complete wizard guide
- **[Publishing Overview](../Publishing-Overview.md)** - How publishing works
- **[← Previous: Admin Account](./SetupWizard-Step2-Admin.md)**
- **[Next: Email Configuration →](./SetupWizard-Step4-Email.md)**
