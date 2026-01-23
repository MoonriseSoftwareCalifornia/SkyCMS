---
title: Post-Installation Configuration
description: Verify installation and configure essential features for production use after setup wizard
keywords: post-installation, verification, production, configuration, security
audience: [developers, administrators]
version: 2.0
last_updated: "2026-01-03"
stage: stable
read_time: 7
---

# Post-Installation Configuration

After completing the [Setup Wizard](./SetupWizard.md), follow this guide to verify your installation and configure essential features for production use.

> **Before starting**: Ensure the application has been restarted after setup completion.

## When to use this
- Immediately after finishing the wizard or a production deploy to validate the installation.
- Before granting wider access to editors or going live.

## Why this matters
- Catches misconfigurations early (DB/storage/email/publisher) before users hit errors.
- Ensures publishing, email, and CDN are working end-to-end.

## Key takeaways
- Verify editor login, DB, storage uploads, email, and publisher reachability.
- Run through a first publish to confirm the full path works.
- Harden defaults (auth, file types, roles) before opening to users.

## Prerequisites
- Completed setup wizard (or equivalent config) and app restart.
- Admin credentials and access to storage/CDN/email providers if verification fails.

## Quick path
1. Run the verification checklist (login, DB, storage upload, email test, publisher reachability).
2. Publish a first page and verify on the public site.
3. Review defaults (static mode, auth, file types) and apply hardening steps.

---

## Table of Contents

- [Verification Checklist](#verification-checklist)
- [First Steps](#first-steps)
- [Core Configuration](#core-configuration)
- [Publisher Verification](#publisher-verification)
- [Email Verification](#email-verification)
- [Optional Features](#optional-features)
- [Security Hardening](#security-hardening)
- [Next Steps](#next-steps)

---

## Verification Checklist

Before proceeding, verify your installation is fully operational:

- [ ] Application restarted successfully
- [ ] Admin login works with credentials from Step 2
- [ ] Database connection active (no errors in logs)
- [ ] Storage provider accessible (test file upload)
- [ ] Email configured and tested (if needed)
- [ ] Publisher application running and accessible

**If any item fails**, see [Troubleshooting](#troubleshooting) at the bottom.

---

## First Steps

### 1. Access the Editor

After restarting and logging in, you should see the editor dashboard:

```
https://your-website-url/editor
```

![Editor dashboard after first login](../images/screenshots/live-editor-dashboard.webp)

**What you see**:
- Left sidebar with navigation
- Main content area (likely empty for new install)
- Top menu with admin options

### 2. Verify Storage Connection

Test that file uploads work:

1. Navigate to **File Manager** (left sidebar or top menu)
2. Click **Upload** 
3. Select a small test image or file
4. Upload should complete without errors

![File Manager upload test](../images/screenshots/storage-upload-test.webp)

**If upload fails**:
- Check storage connection string in settings
- Verify provider credentials are correct
- Check storage provider's access logs
- See [Troubleshooting: Storage Issues](#storage-connection-failed)

### 3. Check Administrator Account

Verify your admin account has full permissions:

1. Navigate to **Admin Panel** → **Users** or **Settings**
2. Locate your admin email
3. Confirm role is **Administrator**
4. Verify "Full Access" or similar permission indicator

**If role is wrong**:
- Contact site administrator or database owner to correct

---

## Core Configuration

### Configure Your Publisher URL

If you haven't already configured your publisher URL, do this now:

1. Go to **Settings** → **Publisher Configuration** (or similar)
2. Enter your public website URL:
   ```
   https://www.your-domain.com
   ```
3. Verify **Static Mode** is enabled (recommended)
4. Save changes

**Why this matters**:
- Publisher uses this URL for all published content links
- Incorrect URL breaks publishing and CDN integration
- Must match your actual public domain

### Review Default Settings

Navigate to **Settings** and review defaults:

| Setting | Default | Recommendation | Notes |
|---------|---------|-----------------|-------|
| **Static Mode** | Enabled | Keep enabled | Best performance; faster publishing |
| **Authentication Required** | Disabled | Keep disabled | Only enable for private/intranet sites |
| **Allowed File Types** | Pre-configured | Review | Restrict to needed types for security |

---

## Publisher Verification

### Access Your Published Website

Navigate to your publisher URL:

```
https://your-publisher-url/
```

**What you should see**:
- Either a blank/default page (if nothing published yet)
- Or your configured layout with any pre-existing content

**If you see an error or 404**:
- Check publisher application is running
- Verify Publisher URL is accessible from your network
- Check firewall/CDN rules
- See [Troubleshooting: Publisher Not Accessible](#publisher-not-accessible)

### Create Your First Published Page

1. In editor, navigate to **Articles** or **Pages**
2. Click **Create New Page**
3. Add a title: `Welcome to SkyCMS`
4. Add some content in the editor
5. Click **Publish** (not Save as Draft)
6. Wait 5-30 seconds for publishing to complete

**Then check the publisher**:

```
https://your-publisher-url/welcome-to-skycms
```

or wherever your URL structure places it.

**If page doesn't appear**:
- Check publisher application logs
- Verify publishing service is running
- See [Troubleshooting: Pages Not Publishing](#pages-not-publishing)

---

## Email Verification

### Test Email Configuration

If you configured email in the setup wizard:

1. Go to **Settings** → **Email** (or similar)
2. Look for **"Send Test Email"** button
3. Enter a test email address (yours or a test account)
4. Click **Send**

![Email settings with test send](../images/screenshots/settings-email-test.webp)

**Check your inbox**:
- Email should arrive within 1-2 minutes
- Check spam folder if not in inbox
- Look for delivery errors in settings

**If test email fails**:
- Verify provider credentials are correct
- Check provider-specific settings (sender verification, API quotas, etc.)
- See email provider-specific guide:
  - [SendGrid Troubleshooting](../Configuration/Email-SendGrid.md#troubleshooting)
  - [Azure Communication Services Troubleshooting](../Configuration/Email-AzureCommunicationServices.md#troubleshooting)
  - [SMTP Troubleshooting](../Configuration/Email-SMTP.md#troubleshooting)

### Configure Email Templates (Optional)

Email can be customized for:
- Password reset confirmations
- Account verification
- Admin notifications

This is optional for initial setup; revisit after basic operations are working.

---

## Optional Features

### Set Up CDN Cache Purging

If you configured a CDN:

1. Go to **Settings** → **CDN** (or similar)
2. Verify CDN provider is showing as **Connected**
3. Look for **"Test Connection"** or similar button
4. Click to verify CDN connectivity

![CDN settings with test/purge controls](../images/screenshots/settings-cdn-test.webp)

**If CDN test fails**:
- Verify API credentials
- Check provider permissions
- Ensure CDN distribution is active
- See CDN-specific guide

**CDN purging behavior**:
- When you publish a page, the CDN cache is automatically purged for that page
- Full purge on major settings changes
- This happens automatically; no additional configuration needed

### Enable Error Logging & Monitoring (Recommended)

For production, enable Application Insights or logging:

**Azure users**:
- Configure Application Insights connection string
- Monitor in Azure Portal

**All users**:
- Enable detailed logging in application settings
- Monitor application logs for errors
- Set up alerts for critical errors

See [Monitoring & Logging](../Operations/Monitoring-and-Logging.md) *(forthcoming)* for details.

---

## Security Hardening

### 1. Change Default Admin Password

Create a strong password if you haven't already:

1. Go to **User Profile** (usually top-right menu)
2. Click **Change Password**
3. Enter current password, then new strong password
4. Password should be:
   - At least 8 characters
   - Mix of upper, lower, numbers, special characters
   - Unique and hard to guess
5. Save

### 2. Create Additional Users (For Teams)

If multiple people will manage content:

1. Go to **Settings** → **Users** or **User Management**
2. Click **Add New User**
3. Create account with appropriate role:
   - **Administrator** - Full access (be careful!)
   - **Editor** - Can create/edit/publish content
   - **Viewer** - Read-only access
4. Send credentials securely (not via email)

**Best practices**:
- One admin account per person (not shared)
- Use strong passwords for all accounts
- Assign least privilege (don't make everyone admin)
- Regularly review who has access

### 3. Enable HTTPS Everywhere

Ensure your publisher URL uses HTTPS:

1. Verify publisher URL starts with `https://`
2. If not, configure SSL/TLS certificate:
   - **Azure**: Use Application Gateway or App Service SSL
   - **AWS**: Use CloudFront or ALB with ACM certificate
   - **Cloudflare**: Auto-enabled
   - **Self-hosted**: Use Let's Encrypt or your certificate
3. Test with browser dev tools (green lock icon)

### 4. Backup Your Database & Storage

Set up automated backups:

**Database backups**:
- Azure Cosmos DB: [Enable automatic backups](https://learn.microsoft.com/en-us/azure/cosmos-db/periodic-backup-restore)
- Azure SQL: Built-in automatic backups
- AWS RDS: Built-in automatic backups
- MySQL/Self-hosted: Configure backup schedule

**Storage backups**:
- Azure Blob: Enable versioning or snapshots
- S3: Enable versioning on bucket
- Cloudflare R2: No built-in; use sync to secondary storage

See [Backup & Recovery](../Operations/Backup-and-Recovery.md) *(forthcoming)* for details.

### 5. Review Security Settings

1. Go to **Settings** → **Security** (if available)
2. Review and enable:
   - CORS restrictions (if public API)
   - API rate limiting
   - Session timeout settings
   - IP whitelisting (if applicable)

---

## Domain & DNS Configuration

### Point Your Domain to SkyCMS

After verifying everything works locally:

1. **Get your publisher URL/IP**:
   - If using Azure App Service: `yourdomain.azurewebsites.net`
   - If using CDN: Get CDN endpoint (e.g., `example.azureedge.net`)
   - If self-hosted: Your server IP or hostname

2. **Configure DNS**:
   - Go to your domain registrar's DNS settings
   - Create **CNAME** or **A** record:
     ```
     www.your-domain.com  CNAME  your-publisher-url
     ```
   - Or create **A** record pointing to IP
   - Wait 5 minutes to 24 hours for DNS propagation

3. **Update SkyCMS Publisher URL**:
   - Go to **Settings** → **Publisher Configuration**
   - Change to your actual domain: `https://www.your-domain.com`
   - Save

4. **Test from browser**:
   - Navigate to `https://www.your-domain.com`
   - Verify you see your published content
   - Check for SSL certificate errors (should see green lock)

---

## Troubleshooting

### Storage Connection Failed

**Symptom**: File uploads fail or "Cannot connect to storage"

**Solutions**:
1. Go to **Settings** → **Storage**
2. Verify connection string format is correct:
   - [Azure Blob connection strings](../Configuration/Storage-AzureBlob.md#connection-string-format)
   - [S3 connection strings](../Configuration/Storage-S3.md#connection-string-format)
   - [Cloudflare R2 connection strings](../Configuration/Storage-Cloudflare.md#connection-string-format)
3. Test credentials in provider's CLI:
   ```bash
   az storage blob list --connection-string "YOUR_CONNECTION_STRING"
   ```
4. Verify storage container/bucket exists and is accessible
5. Check provider's access logs for permission errors

### Publisher Not Accessible

**Symptom**: Cannot reach publisher at configured URL; 404 or connection refused

**Solutions**:
1. Verify publisher application is running:
   - Check Docker container status: `docker ps | grep publisher`
   - Check Kubernetes pods: `kubectl get pods | grep publisher`
   - Check logs for startup errors
2. Verify URL is correct:
   - Match exactly what's in **Settings** → **Publisher Configuration**
   - Include protocol (https://), no trailing slash
3. Check network connectivity:
   - Can you ping the publisher server?
   - Are firewall rules allowing traffic?
   - Is CDN/ALB/load balancer configured correctly?
4. Check publisher application logs for errors

### Pages Not Publishing

**Symptom**: Create page in editor, publish, but it doesn't appear on publisher

**Solutions**:
1. Wait 10-30 seconds (static pages are generated in background)
2. Hard refresh browser (Ctrl+Shift+R or Cmd+Shift+R)
3. Check if page is actually published:
   - In editor, verify status shows "Published" (not "Draft")
   - Check publication date is not in future
4. Check publisher service/container is running
5. Check application logs for publishing errors
6. If using CDN, purge CDN cache for the page
7. Check file permissions on storage

### Email Not Sending

**Symptom**: Test email fails; real emails not being sent

**Solutions**:
1. Verify email provider is configured:
   - Go to **Settings** → **Email**
   - Verify provider dropdown shows correct provider
   - Verify credentials are filled in
2. Test connection:
   - Click **Test Email** button
   - Check for error message
3. For specific providers:
   - **SendGrid**: API key must start with `SG.`; check API quotas
   - **Azure**: Connection string must include valid keys; check managed domain verification
   - **SMTP**: Verify host, port, username, password; try with telnet first
4. Check application logs for email service errors
5. Verify sender email is authorized/verified in email provider

### Admin Login Fails

**Symptom**: Cannot log in with admin credentials from setup wizard

**Solutions**:
1. Verify application has restarted after setup
2. Verify setup wizard actually completed successfully
3. Check for password errors:
   - Password is case-sensitive
   - Verify no extra spaces before/after
   - Try password reset if available
4. Check database has schema migrations:
   - Verify database connectivity
   - Check Entity Framework migrations applied
5. Check application logs for authentication errors

---

## Performance Tuning

### Enable Static Mode (If Not Already)

Static mode generates pre-rendered HTML for best performance:

1. Go to **Settings** → **Publisher**
2. Verify **Static Mode** is **Enabled**
3. This is set during setup but double-check
4. Static pages load much faster than dynamic rendering

### Configure Caching

If available in your deployment:

1. **Application caching**: Usually auto-configured
2. **CDN caching**: Configure cache duration:
   - HTML pages: 5-15 minutes (cache headers)
   - Assets (CSS/JS): 30 days (long cache)
   - Images: 30-90 days (very long cache)
3. **Browser caching**: Auto-handled by CDN/web server

---

## What's Next

After completing this guide:

1. **Create Content**: Build your first pages and publish
2. **Customize Design**: Use Designer or Layouts to customize appearance
3. **Invite Team**: Create additional users for your team
4. **Monitor Performance**: Check logs and set up alerts
5. **Configure Backups**: Set up automated backup procedures
6. **Plan Scaling**: As traffic grows, optimize database and storage

---

## Additional Resources

- **[Getting Started Guide](../QuickStart.md)** - General getting started
- **[Setup Wizard Guide](./SetupWizard.md)** - If you need to re-run setup
- **[Configuration Reference](../Configuration/README.md)** - All configuration options
- **[Troubleshooting Guide](../Troubleshooting.md)** - More troubleshooting scenarios
- **[Editor Documentation](../Editors/LiveEditor/README.md)** - Creating content

---

## See Also

- **[Installation Overview](./README.md)** - Installation methods and platforms
- **[Minimum Required Settings](./MinimumRequiredSettings.md)** - Configuration reference
- **[LEARNING_PATHS](../LEARNING_PATHS.md)** - Role-based learning journeys
