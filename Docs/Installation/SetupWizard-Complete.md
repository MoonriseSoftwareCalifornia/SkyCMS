---
title: Setup Wizard - Complete
description: Setup wizard completion and next steps for SkyCMS configuration
keywords: setup-wizard, complete, success, next-steps
audience: [developers, administrators]
---

# Setup Wizard: Setup Complete

[← Review & Complete](./SetupWizard-Step6-Review.md) | **Setup Complete** ✅

---

## 🎉 Setup Complete!

Your SkyCMS installation has been successfully configured.

![Setup Complete Screen](../assets/setup-complete.png) *(Screenshot placeholder)*

---

## What Was Configured

Your setup included:

### ✅ Database
- Database connection established
- Schema initialized
- Migrations applied

### ✅ Storage Provider
- Cloud storage connected
- File upload/download configured
- Storage containers/buckets ready

### ✅ Administrator Account
- Admin user created
- Login credentials set
- Full system access granted

### ✅ Publisher Settings
- Website URL configured
- Publishing mode set
- File types configured

### ✅ Email Configuration (Optional)
- Email provider configured (if selected)
- Sender identity set
- Ready for notifications

### ✅ CDN Configuration (Optional)
- CDN provider configured (if selected)
- Caching enabled
- Global delivery ready

---

## Next Steps

### 1. Restart Your Application

The setup wizard has disabled itself. You need to restart the application to apply all changes.

#### Docker Deployment

```powershell
docker-compose restart
```

#### Kubernetes Deployment

```powershell
kubectl rollout restart deployment/skycms-editor
kubectl rollout restart deployment/skycms-publisher
```

#### Manual/Development Deployment

- Stop the application (Ctrl+C in terminal or stop in IDE)
- Start again:
  ```powershell
  dotnet run --project Editor/Editor.csproj
  ```

---

### 2. Log In to Admin Panel

Navigate to your website's login page:

```
https://your-website-url/Identity/Account/Login
```

**Use the credentials you created in Step 2:**
- **Email**: [Your administrator email]
- **Password**: [Your administrator password]

**Forgot your credentials?**
- Reset password via email (if email configured)
- Or manually reset via database (advanced)

---

### 3. Explore the Editor

After logging in, access the CMS editor:

```
https://your-website-url/editor
```

**What you can do**:
- ✏️ Create and edit pages
- 📁 Upload files via file manager
- 🎨 Customize layouts and templates
- 👥 Manage users and roles
- ⚙️ Configure advanced settings

---

### 4. Follow Post-Installation Guide

Now that setup is complete, continue with the **[Post-Installation Configuration Guide](./Post-Installation.md)** to:
- Verify your installation is fully operational
- Create your first published page
- Test email and CDN integration
- Configure security settings
- Set up user accounts for your team

---

### 4. Publish Your First Page

1. **Create a page** in the editor
2. **Add content** using the visual editor
3. **Publish** the page
4. **View** on your public website

Your published site is available at:
```
https://your-publisher-url/
```

---

## Configuration Reference

### Verify Configuration

Check that all settings were applied correctly:

**Via Admin Panel**:
1. Log in as administrator
2. Navigate to **Settings** → **Configuration**
3. Review all configured values

**Via Environment Variables**:
```powershell
# View current environment variables
Get-ChildItem Env: | Where-Object { $_.Name -like "Cosmos*" -or $_.Name -like "*Connection*" }
```

**Via Database**:
- Configuration stored in `Settings` or `Configuration` table/container
- Query directly if needed (advanced)

---

## Troubleshooting

### Setup Wizard Still Accessible After Restart

**Issue**: Navigating to `/___setup` still shows the wizard.

**Cause**: Setup lock not applied or environment variable override.

**Solution**:
1. Check database for `CosmosAllowSetup` setting (should be `false`)
2. Verify no environment variable sets `CosmosAllowSetup=true`
3. Restart application again
4. Clear browser cache

---

### Cannot Log In with Admin Credentials

**Issue**: Login fails with error or "Invalid credentials".

**Possible causes**:
- Incorrect username/password
- Password case-sensitive
- Cookie/session issues

**Solution**:
1. Verify username and password (check for typos)
2. Try password reset via email (if email configured)
3. Clear browser cookies
4. Check application logs for errors
5. If all else fails, reset password via database (advanced)

---

### Published Pages Not Appearing

**Issue**: Pages edited in CMS don't appear on public site.

**Possible causes**:
- Page not published (saved as draft)
- Publisher service not running
- CDN caching old content

**Solution**:
1. In editor, verify page status is "Published" (not "Draft")
2. Check that Publisher service is running
3. If CDN configured, purge cache
4. Wait 1-2 minutes for static pages to regenerate

---

### File Uploads Not Working

**Issue**: Cannot upload files via file manager.

**Possible causes**:
- Storage connection issue
- File type not allowed
- File size exceeds limit

**Solution**:
1. Verify storage connection in Settings
2. Check file extension is in **Allowed File Types** list (from Step 3)
3. Check file size is under limit (default: 10 MB)
4. Review application logs for errors

---

### Email Not Sending

**Issue**: Password reset or notification emails not delivered.

**Possible causes**:
- Email provider not configured
- Invalid credentials
- Sender email not verified
- Email in spam folder

**Solution**:
1. Verify email provider configured correctly in Settings
2. Re-test email connection via Settings → Email → Test
3. Check spam/junk folder
4. Verify sender email is verified (SendGrid/Azure)
5. Review application logs for email errors

---

## Re-Running Setup Wizard

If you need to re-configure via the setup wizard:

### ⚠️ **Warning**

Re-running setup can **overwrite existing configuration** and **reset settings**. Only do this if absolutely necessary.

### Steps to Re-Enable

1. **Set environment variable**:
   ```powershell
   $env:CosmosAllowSetup = "true"
   ```

2. **Or update database**:
   - Locate `CosmosAllowSetup` setting in database
   - Set value to `true`

3. **Restart application**

4. **Navigate to setup wizard**:
   ```
   https://your-website-url/___setup
   ```

5. **After completing setup**:
   - Remove `CosmosAllowSetup=true` from environment
   - Restart application

---

## Additional Resources

### Documentation

- **[Installation Overview](./README.md)** - All installation methods
- **[Minimum Required Settings](./MinimumRequiredSettings.md)** - Configuration reference
- **[Post-Installation Configuration](./Post-Installation.md)** - Advanced setup
- **[Configuration Overview](../Configuration/README.md)** - All configuration options

### Configuration Guides

- **[Storage Configuration](../Configuration/Storage-Overview.md)** - Storage providers
- **[Email Configuration](../Configuration/Email-Overview.md)** - Email providers
- **[CDN Configuration](../Configuration/CDN-Overview.md)** - CDN providers

### Troubleshooting

- **[Common Issues](../Troubleshooting.md)** - FAQ and solutions
- **[Logs and Diagnostics](../Troubleshooting.md)** - Debugging guide

### Community & Support

- **GitHub Issues**: [github.com/CWALabs/SkyCMS/issues](https://github.com/CWALabs/SkyCMS/issues)
- **Documentation**: [github.com/CWALabs/SkyCMS/Docs](https://github.com/CWALabs/SkyCMS/tree/main/Docs)

---

## Summary

✅ **Setup Complete**  
✅ **Configuration Applied**  
✅ **Administrator Account Created**  
✅ **Ready to Use**

**Next Action**: Restart your application and log in to start building your website.

**Happy publishing!** 🚀

---

## Additional Resources

### Documentation

- **[Getting Started Guide](../QuickStart.md)** - First steps after installation
- **[Installation Overview](./README.md)** - Complete installation reference
- **[Post-Installation Configuration](./Post-Installation.md)** - ⭐ **Start here** after wizard completes
- **[Minimum Required Settings](./MinimumRequiredSettings.md)** - Configuration reference
- **[Configuration Overview](../Configuration/README.md)** - All configuration options

### Next Steps

1. **Immediately**: [Post-Installation Configuration](./Post-Installation.md) - Verify setup and configure features
2. **Creating Content**: [Editor Guides](../Editors/LiveEditor/README.md) - Learn to create and edit pages
3. **Customization**: [Layouts Guide](../Layouts/README.md) - Customize your site appearance
