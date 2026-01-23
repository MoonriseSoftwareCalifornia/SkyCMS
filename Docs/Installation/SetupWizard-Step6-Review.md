---
title: Setup Wizard - Step 6 Review
description: Review configuration and complete setup wizard
keywords: setup-wizard, review, complete, verification, configuration
audience: [developers, administrators]
---

# Setup Wizard: Step 6 - Review & Complete

[← CDN Configuration](./SetupWizard-Step5-CDN.md) | **Step 6 of 6** | [Setup Complete →](./SetupWizard-Complete.md)

---

## Review Configuration & Complete Setup

Review all configured settings before finalizing setup.

![Review and finalize setup](../images/screenshots/setup-wizard-review.webp)

---

## Review Summary

The review screen displays all settings configured in previous steps:

### Storage Configuration

- **Provider**: Azure Blob / Amazon S3 / Cloudflare R2 / Google Cloud Storage
- **Connection Status**: ✅ Connected / ❌ Not Connected
- **Container/Bucket**: Name of storage location

### Administrator Account

- **Email**: Administrator's email address
- **Username**: Administrator's username
- **Role**: Administrator

### Publisher Settings

- **Website URL**: Public URL of your website
- **Website Title**: Site title
- **Static Mode**: Enabled / Disabled
- **Authentication Required**: Yes / No
- **Allowed File Types**: List of allowed extensions

### Email Configuration (Optional)

- **Provider**: SendGrid / Azure / SMTP / None
- **Sender Email**: Email address for outgoing messages
- **Status**: ✅ Configured / ⏭️ Skipped

### CDN Configuration (Optional)

- **Provider**: Azure Front Door / Cloudflare / CloudFront / Sucuri / None
- **Status**: ✅ Configured / ⏭️ Skipped

---

## Actions

### "Back" Button

Return to previous steps to modify settings.

**Note**: You can navigate back to any step to change values before finalizing.

### "Finalize Setup" Button

Complete the setup process and apply all configurations.

**What happens when you click "Finalize Setup"**:

1. **Database Initialization**
   - Creates necessary tables/containers
   - Applies schema migrations
   - Seeds initial data

2. **Administrator Account Creation**
   - Creates admin user with provided credentials
   - Assigns Administrator role
   - Sets up initial permissions

3. **Configuration Storage**
   - Saves all settings to database
   - Applies environment-specific overrides
   - Validates configuration integrity

4. **Setup Lock**
   - Disables the setup wizard
   - Sets `CosmosAllowSetup=false` in database
   - Prevents unauthorized re-configuration

5. **Application Restart**
   - Triggers application restart (if needed)
   - Loads new configuration
   - Applies changes

**Duration**: Typically 10-30 seconds depending on database and configuration complexity.

---

## After Completion

Once setup completes, you'll see the **[Completion Screen](./SetupWizard-Complete.md)** with:

- ✅ Setup success confirmation
- Login credentials reminder
- Next steps and links
- How to restart/redeploy your application

---

## Troubleshooting

### "Failed to create administrator account"

**Possible causes**:
- Database connection lost
- Password doesn't meet requirements (should have been validated earlier)
- Username/email already exists (rare in fresh install)

**Solution**: 
1. Check database connectivity
2. Verify admin credentials format
3. Review error message for specifics
4. Click "Back" to re-enter credentials

### "Failed to save configuration"

**Possible causes**:
- Database write permissions
- Storage connection invalid
- Configuration conflict

**Solution**:
1. Verify database is writable
2. Check all connections are valid
3. Review error details
4. Click "Back" to verify settings

### "Setup lock failed to apply"

**Issue**: Setup completed but wizard is still accessible.

**Solution**: 
1. Manually set `CosmosAllowSetup=false` in database or environment variables
2. Restart application
3. If persistent, check logs for errors

### Application Doesn't Restart After Setup

**Cause**: Some environments require manual restart.

**Solution**:

**Docker**:
```powershell
docker-compose restart
```

**Kubernetes**:
```powershell
kubectl rollout restart deployment/skycms-editor
kubectl rollout restart deployment/skycms-publisher
```

**Manual/Development**:
- Stop the application (Ctrl+C)
- Restart using `dotnet run` or your IDE

### Cannot Access Admin Panel After Setup

**Possible causes**:
- Admin credentials incorrect
- Cookie/session issues
- Browser cache

**Solution**:
1. Verify you're using credentials entered in Step 2
2. Clear browser cookies for the site
3. Try incognito/private browsing
4. Check application logs for authentication errors

---

## Configuration Changes After Setup

After setup is complete, configuration changes must be made via:

### Environment Variables

Modify environment variables and restart:

```powershell
$env:CosmosPublisherUrl = "https://new-url.com"
# Restart application
```

### Database Configuration

Some settings are stored in the database and can be modified via:
- Admin panel → Settings
- Direct database updates (advanced)

### Re-Running Setup Wizard

To re-run the setup wizard (⚠️ **Caution**):

1. Set `CosmosAllowSetup=true` in environment or database
2. Restart application
3. Navigate to `/___setup`

**Warning**: Re-running setup on a production system can overwrite existing configuration. Only do this if needed.

---

## What Happens Next

After clicking **Finalize Setup**, you'll proceed to:

**[Setup Complete Screen →](./SetupWizard-Complete.md)**

The application will finalize configuration, create your administrator account, and prepare for first use.

---

## See Also

- **[Setup Wizard Overview](./SetupWizard.md)** - Complete wizard guide
- **[Post-Installation Configuration](./Post-Installation.md)** - What to configure after setup
- **[Minimum Required Settings](./MinimumRequiredSettings.md)** - Required configuration reference
- **[← Previous: CDN Configuration](./SetupWizard-Step5-CDN.md)**
- **[Setup Complete →](./SetupWizard-Complete.md)**
