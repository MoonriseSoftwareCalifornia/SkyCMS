---
title: SMTP Email Configuration
description: Configure SMTP email server for transactional emails (Gmail, Outlook, custom servers)
keywords: SMTP, email, Gmail, Outlook, configuration, transactional-emails
audience: [developers, administrators]
version: 2.0
last_updated: "2026-01-03"
stage: stable
read_time: 7
---

# SMTP Email with SkyCMS

SMTP (Simple Mail Transfer Protocol) is the standard protocol for sending emails. SkyCMS supports any SMTP server, making it compatible with:

- Gmail / Google Workspace
- Office 365 / Microsoft Exchange
- Self-hosted mail servers
- Managed email services (Zoho, FastMail, ProtonMail, etc.)

## When to use this
- You want to reuse existing SMTP infra (Gmail/Workspace/Office 365/SES/custom).
- You prefer SMTP credentials over API-based providers.

## Why this matters
- Email is required for password resets and notifications; provider settings must be correct.
- TLS/SSL choices and auth details vary per provider.

## Key takeaways
- Configure `SmtpEmailProviderOptions` + `AdminEmail`; use app passwords where required.
- TLS (UsesSsl=false) on 587 is typical; SSL rarely needed.
- Store creds in env vars/secret stores; avoid committing them.

## Prerequisites
- SMTP host/port/user/password and sender email; provider allows SMTP auth.

## Quick path
1. Gather SMTP host/port/user/pass; generate app password if needed (Gmail/Workspace).
2. Set env vars or wizard fields; choose TLS (UsesSsl=false) unless provider requires SSL.
3. Send test email; adjust firewall/provider settings if blocked.

## Values you need

- **SMTP Host**: The mail server address (e.g., `smtp.gmail.com`)
- **SMTP Port**: Port number (usually `587` for TLS or `25`/`465` for SSL)
- **Username**: Email address or account name
- **Password**: Email password or app-specific password
- **Use TLS/SSL**: Whether to encrypt the connection
- **Admin Email**: Default sender email address

## Common Email Providers

### Gmail

- **Host**: `smtp.gmail.com`
- **Port**: `587` (TLS)
- **Username**: Your full Gmail address (e.g., `your-email@gmail.com`)
- **Password**: [Generate App Password](#gmail-app-password)
- **TLS/SSL**: TLS (UsesSsl = false)

### Office 365 / Outlook

- **Host**: `smtp.office365.com`
- **Port**: `587` (TLS)
- **Username**: Your full email address (e.g., `your-email@company.com`)
- **Password**: Your Office 365 password
- **TLS/SSL**: TLS (UsesSsl = false)

### Google Workspace

- **Host**: `smtp.google.com`
- **Port**: `587` (TLS)
- **Username**: Your workspace email (e.g., `admin@company.com`)
- **Password**: [Generate App Password](#google-workspace-app-password)
- **TLS/SSL**: TLS (UsesSsl = false)

### SendGrid (SMTP Alternative)

- **Host**: `smtp.sendgrid.net`
- **Port**: `587` (TLS)
- **Username**: `apikey`
- **Password**: Your SendGrid API Key
- **TLS/SSL**: TLS (UsesSsl = false)

### Amazon SES (Simple Email Service)

- **Host**: `email-smtp.region.amazonaws.com` (e.g., `email-smtp.us-east-1.amazonaws.com`)
- **Port**: `587` (TLS)
- **Username**: Your SES SMTP username
- **Password**: Your SES SMTP password
- **TLS/SSL**: TLS (UsesSsl = false)

## Setup Instructions by Provider

### Gmail Setup

#### Generate App Password {#gmail-app-password}

1. Go to [Google Account Security](https://myaccount.google.com/security)
2. Enable **2-Step Verification** (if not already enabled)
3. Go back to Security settings
4. Find **App passwords** (appears only with 2FA enabled)
5. Select **App**: Mail, **Device**: Windows/Mac/Linux
6. Click **Generate**
7. Copy the 16-character password (without spaces)
8. Use this as your SMTP password

#### SkyCMS Configuration

```powershell
$env:SmtpEmailProviderOptions__Host = "smtp.gmail.com"
$env:SmtpEmailProviderOptions__Port = "587"
$env:SmtpEmailProviderOptions__UserName = "your-email@gmail.com"
$env:SmtpEmailProviderOptions__Password = "your-app-password-without-spaces"
$env:AdminEmail = "your-email@gmail.com"
```

### Office 365 Setup

#### Prerequisites

1. Office 365 account with email enabled
2. Ensure SMTP authentication is enabled (usually default)

#### SkyCMS Configuration

```powershell
$env:SmtpEmailProviderOptions__Host = "smtp.office365.com"
$env:SmtpEmailProviderOptions__Port = "587"
$env:SmtpEmailProviderOptions__UserName = "your-email@company.com"
$env:SmtpEmailProviderOptions__Password = "your-office365-password"
$env:AdminEmail = "your-email@company.com"
```

### Google Workspace Setup

#### Generate App Password {#google-workspace-app-password}

1. Go to [Google Admin Console](https://admin.google.com)
2. Navigate to **Security** → **Authentication** → **App Passwords**
3. Select **Mail** and **Windows/Mac/Linux**
4. Click **Generate**
5. Copy the password (16 characters)

#### SkyCMS Configuration

```powershell
$env:SmtpEmailProviderOptions__Host = "smtp.google.com"
$env:SmtpEmailProviderOptions__Port = "587"
$env:SmtpEmailProviderOptions__UserName = "your-workspace-email@company.com"
$env:SmtpEmailProviderOptions__Password = "your-app-password"
$env:AdminEmail = "your-workspace-email@company.com"
```

## Configure in SkyCMS

### Method 1: Environment Variables (recommended)

```powershell
# Generic SMTP example
$env:SmtpEmailProviderOptions__Host = "smtp.yourmailserver.com"
$env:SmtpEmailProviderOptions__Port = "587"
$env:SmtpEmailProviderOptions__UserName = "user@example.com"
$env:SmtpEmailProviderOptions__Password = "yourpassword"
$env:AdminEmail = "user@example.com"
```

**Note**: The `__` (double underscore) in environment variable names converts to `:` (colon) in JSON configuration.

### Method 2: appsettings.json (not recommended for production)

```json
{
  "SmtpEmailProviderOptions": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UserName": "your-email@gmail.com",
    "Password": "your-app-password",
    "UsesSsl": false
  },
  "AdminEmail": "your-email@gmail.com"
}
```

**Setting**: `UsesSsl`
- `false` (default): Uses TLS (recommended, most common)
- `true`: Uses SSL (older, rarely needed)

### Method 3: Setup Wizard

1. Deploy SkyCMS with `CosmosAllowSetup=true`
2. Navigate to `https://yourdomain.com/___setup`
3. Complete setup through **Step 4: Email Configuration**
4. Select **SMTP**
5. Fill in:
   - **Host**: `smtp.gmail.com` (or your provider)
   - **Port**: `587`
   - **Username**: Your email address
   - **Password**: App password or email password
   - **Sender Email**: Email that will appear as "From"
6. Click **Test Email** to verify
7. Complete the wizard

## Test email sending

### From the Setup Wizard

During setup, after entering credentials:

1. Scroll to **Test Recipient Email**
2. Enter a test email address (any email you can check)
3. Click **Test Email Settings**
4. Check the test inbox (may take 1-2 minutes)
5. If successful, you'll see the test email; if failed, error details appear

### Manual Testing with Telnet (Advanced)

```powershell
# Test SMTP connectivity (telnet)
telnet smtp.gmail.com 587

# Expected response: "220 smtp.gmail.com ESMTP..."
# If connected, type: quit
```

## SMTP features and considerations

### Advantages

- **Universal compatibility**: Works with any SMTP server
- **No fees**: Use existing corporate email infrastructure
- **Simple setup**: Just need host, port, username, password
- **Industry standard**: Well-documented and widely supported
- **Flexible**: Gmail, Office 365, self-hosted, or managed services

### Limitations

- **Manual port/auth management**: More configuration than cloud services
- **TLS/SSL required**: Modern SMTP requires encryption
- **IP whitelisting**: Some servers restrict sending IPs
- **Rate limits**: Some providers rate-limit email volume
- **Less analytics**: No built-in delivery tracking like SendGrid

### Connection Security

- **Port 587** (STARTTLS): Most common, upgrades connection to TLS
- **Port 465** (SMTPS): SSL from start, older standard, less common
- **Port 25** (Plain): Rarely supported in modern environments due to spam

Use **Port 587 with TLS** (`UsesSsl = false`) for most providers.

## Troubleshooting

### "Authentication failed" or "Invalid credentials"

- Double-check username and password
- Gmail users: Use [app password](#gmail-app-password), not regular password
- Office 365 users: Verify SMTP authentication is enabled in admin center
- Try logging into the email service directly to confirm credentials work

### "Connection refused" or "Unable to connect"

- Verify the SMTP host is correct for your provider
- Verify the port is correct (usually 587 for TLS)
- Check firewall/network isn't blocking SMTP outbound
- Some networks block port 587; try port 25 or 465 if allowed

### "TLS required" or "STARTTLS required" error

- Ensure `UsesSsl = false` (uses TLS, the standard)
- Set port to `587`

### "Relay not permitted" error

- The SMTP server doesn't recognize your authentication
- Verify the account has SMTP send permissions
- For corporate servers, check with your IT department

### Email sent but not received

- Check recipient email is correct
- Verify sender email is recognized by your mail provider
- Some providers block emails from non-matching sender addresses
- Check spam/junk folder in recipient's mailbox

### "Timeout" or "Connection dropped"

- The SMTP server is slow or unresponsive
- Try connecting manually with telnet to verify server responsiveness
- Check if your ISP or network is throttling SMTP

## Best practices

- **Use app passwords** for Gmail and Workspace instead of account password
- **Store credentials in environment variables**, not in source code
- **Use TLS (port 587)** rather than SSL for modern compatibility
- **Restrict email account permissions** to SMTP send only if possible
- **Monitor sending limits** imposed by your email provider
- **Test thoroughly** with a test account before production deployment
- **Set up mail server logs** to diagnose delivery issues
- **Rotate passwords** periodically for security

## See Also

- [SMTP Protocol Specification (RFC 5321)](https://tools.ietf.org/html/rfc5321)
- [Gmail App Passwords Documentation](https://support.google.com/accounts/answer/185833)
- [Office 365 SMTP Settings](https://learn.microsoft.com/en-us/exchange/clients-and-mobile-in-exchange-online/authenticated-smtp)
- [Email Provider Overview](./Email-Overview.md)
- [Email Configuration Reference](./Email-Configuration-Reference.md)
- [Main Configuration Guide](./README.md)
