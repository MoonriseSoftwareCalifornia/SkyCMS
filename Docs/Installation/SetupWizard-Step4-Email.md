---
title: Setup Wizard - Step 4 Email
description: Configure email provider (SendGrid, SMTP, Azure) in setup wizard
keywords: setup-wizard, email, SendGrid, SMTP, Azure-Communication-Services, configuration
audience: [developers, administrators]
---

# Setup Wizard: Step 4 - Email Configuration

[← Publisher Settings](./SetupWizard-Step3-Publisher.md) | **Step 4 of 6** | [Next: CDN Configuration →](./SetupWizard-Step5-CDN.md)

---

## Configure Email Provider (Optional)

Configure email delivery for user registration, password resets, and notifications.

![Email provider selection and test send](../images/screenshots/setup-wizard-email.webp)

> **Note**: Email configuration is **optional**. You can skip this step and configure email later.

---

## Email Providers

Select your email provider from the dropdown:

| Provider | Best For | Required Information |
|----------|----------|---------------------|
| **SendGrid** | Production websites | API Key |
| **Azure Communication Services** | Azure-hosted apps | Connection String |
| **SMTP** | Custom mail servers | Host, Port, Credentials |
| **None** | Development/Testing | No credentials needed |

For detailed setup instructions, see **[Email Configuration Overview](../Configuration/Email-Overview.md)**.

---

## Provider-Specific Fields

### SendGrid

**Field Name**: `SendGridApiKey`  
**Required**: ✅ Yes (if SendGrid selected)  
**Format**: API key starting with `SG.`  
**Can be pre-configured**: Yes (via `SendGridApiKey`)

**Example**: `SG.ABcDefGHijKLmnoPQrsTUvwXYz`

**Setup Instructions**: **[SendGrid Configuration Guide](../Configuration/Email-SendGrid.md)**

---

### Azure Communication Services

**Field Name**: `AzureEmailConnectionString`  
**Required**: ✅ Yes (if Azure selected)  
**Format**: Azure connection string  
**Can be pre-configured**: Yes (via `AzureEmailConnectionString`)

**Example**: `endpoint=https://myservice.communication.azure.com/;accesskey=...`

**Setup Instructions**: **[Azure Communication Services Guide](../Configuration/Email-AzureCommunicationServices.md)**

---

### SMTP

**Field Names**:
- `SmtpHost` - SMTP server hostname
- `SmtpPort` - Port number (587, 465, or 25)
- `SmtpUsername` - SMTP username/email
- `SmtpPassword` - SMTP password

**Required**: ✅ All fields required (if SMTP selected)  
**Can be pre-configured**: Yes (via `SmtpHost`, `SmtpPort`, `SmtpUsername`, `SmtpPassword`)

**Common Configurations**:

| Provider | Host | Port | Auth |
|----------|------|------|------|
| Gmail | smtp.gmail.com | 587 | App Password |
| Office 365 | smtp.office365.com | 587 | Email/Password |
| Outlook.com | smtp-mail.outlook.com | 587 | Email/Password |

**Setup Instructions**: **[SMTP Configuration Guide](../Configuration/Email-SMTP.md)**

---

### None (Development/Testing)

**No configuration required**  
**Can be pre-configured**: Yes (via `EmailProvider=None`)

Emails are logged but not sent. Useful for:
- Local development
- Testing ASP.NET Identity features
- Environments without email services

**Setup Instructions**: **[No-Op Email Provider Guide](../Configuration/Email-None.md)**

---

## Sender Email Address

**Field Name**: `SenderEmail`  
**Required**: ✅ Yes  
**Format**: Valid email address  
**Can be pre-configured**: Yes (via `SenderEmail`)

**Example**: `noreply@mywebsite.com`, `admin@company.com`

**Requirements**:
- Must be a valid email format
- For SendGrid: Must be a verified sender identity
- For Azure: Must be from a verified domain
- For SMTP: Must be authorized by your mail server

---

## Test Email Feature

After entering configuration, click **"Send Test Email"** to:
- Verify credentials are correct
- Check connectivity to email provider
- Confirm sender email is authorized
- Test email delivery

**Recommendation**: ✅ **Always test** before proceeding.

---

## Actions

### "Skip" Button

Skip email configuration and proceed to CDN configuration.

**When to skip**:
- Configuring email later via environment variables
- Development/testing environment
- Email not needed yet

### "Next" Button

Proceeds to **Step 5: CDN Configuration** after validation.

**Validation**:
- Provider-specific fields are complete
- Sender email is valid format
- (Optional) Test email succeeds

---

## Pre-Configuration with Environment Variables

Pre-configure email via environment variables:

### SendGrid Example

```powershell
$env:EmailProvider = "SendGrid"
$env:SendGridApiKey = "SG.your-api-key-here"
$env:SenderEmail = "noreply@mywebsite.com"
```

### Azure Example

```powershell
$env:EmailProvider = "AzureCommunicationService"
$env:AzureEmailConnectionString = "endpoint=https://...;accesskey=..."
$env:SenderEmail = "noreply@mywebsite.com"
```

### SMTP Example

```powershell
$env:EmailProvider = "SMTP"
$env:SmtpHost = "smtp.gmail.com"
$env:SmtpPort = "587"
$env:SmtpUsername = "myapp@gmail.com"
$env:SmtpPassword = "your-app-password"
$env:SenderEmail = "myapp@gmail.com"
```

### None (Development) Example

```powershell
$env:EmailProvider = "None"
$env:SenderEmail = "dev@localhost"
```

**When pre-configured**:
- Provider is auto-selected
- Credentials are read-only/masked
- Test email still works

**Complete Reference**: **[Email Configuration Reference](../Configuration/Email-Configuration-Reference.md)**

---

## Troubleshooting

### "Invalid API key format"

**SendGrid**: API keys start with `SG.` and contain random characters.

**Solution**: Copy the full API key from SendGrid dashboard.

### "Connection string format invalid"

**Azure**: Must include `endpoint=` and `accesskey=`.

**Solution**: Copy connection string from Azure Portal → Communication Service → Keys.

### "SMTP authentication failed"

**Common issues**:
- Wrong username/password
- Gmail: Need app-specific password (not account password)
- Office 365: May need app password or OAuth

**Solution**: Verify credentials and check provider's authentication requirements.

### "Test email failed to send"

**Possible causes**:
- Invalid credentials
- Sender email not verified (SendGrid/Azure)
- SMTP server blocks your IP
- Network connectivity issues

**Solution**: Check error message, verify credentials, ensure sender is authorized.

### "Sender email must be verified"

**SendGrid/Azure**: Sender identity must be verified before sending.

**Solution**: Complete sender verification in SendGrid or Azure domain verification.

### Cannot Edit Pre-Configured Fields

**Cause**: Email settings are pre-configured via environment variables.

**Solution**: This is intentional for security. To change, update environment variables and restart.

---

## What Happens Next

After clicking **Next** or **Skip**, you'll proceed to:

**[Step 5: CDN Configuration →](./SetupWizard-Step5-CDN.md)** (Optional)

Email configuration (if provided) is saved and will be applied when setup completes.

---

## See Also

- **[Email Configuration Overview](../Configuration/Email-Overview.md)** - Compare providers
- **[SendGrid Setup Guide](../Configuration/Email-SendGrid.md)** - SendGrid configuration
- **[Azure Communication Services Guide](../Configuration/Email-AzureCommunicationServices.md)** - Azure setup
- **[SMTP Setup Guide](../Configuration/Email-SMTP.md)** - SMTP configuration
- **[No-Op Provider Guide](../Configuration/Email-None.md)** - Development mode
- **[Email Configuration Reference](../Configuration/Email-Configuration-Reference.md)** - Complete reference
- **[← Previous: Publisher Settings](./SetupWizard-Step3-Publisher.md)**
- **[Next: CDN Configuration →](./SetupWizard-Step5-CDN.md)**
