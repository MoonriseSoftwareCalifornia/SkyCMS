---
title: Setup Wizard - Step 5 CDN
description: Configure CDN cache purging (CloudFront, Cloudflare, Azure) in setup wizard
keywords: setup-wizard, CDN, CloudFront, Cloudflare, Azure-Front-Door, cache-purging
audience: [developers, administrators]
---

# Setup Wizard: Step 5 - CDN Configuration

[← Email Configuration](./SetupWizard-Step4-Email.md) | **Step 5 of 6** | [Next: Review & Complete →](./SetupWizard-Step6-Review.md)

---

## Configure CDN (Optional)

Configure a Content Delivery Network (CDN) for faster global content delivery and caching.

![CDN provider and purge settings](../images/screenshots/setup-wizard-cdn.webp)

> **Note**: CDN configuration is **optional**. You can skip this step and configure CDN later.

---

## CDN Providers

Select your CDN provider from the dropdown:

| Provider | Best For | Required Information |
|----------|----------|---------------------|
| **None** | No CDN | N/A |
| **Azure Front Door** | Azure-hosted apps | Subscription, Resource Group, Profile |
| **Cloudflare** | Multi-cloud, DDoS protection | API Token, Zone ID |
| **Amazon CloudFront** | AWS-hosted apps | Distribution ID, Credentials |
| **Sucuri** | Security-focused CDN | API Key, Domain |

For detailed setup instructions, see **[CDN Configuration Overview](../Configuration/CDN-Overview.md)**.

---

## Provider-Specific Fields

### None (No CDN)

**No configuration required**

Use when:
- Small traffic websites
- Development/testing
- No global audience
- Configuring CDN later

---

### Azure Front Door

**Required Fields**:
- `AzureSubscriptionId` - Azure subscription ID
- `AzureResourceGroup` - Resource group name
- `AzureFrontDoorProfile` - Front Door profile name

**Can be pre-configured**: Yes (via `CdnProvider`, `AzureSubscriptionId`, `AzureResourceGroup`, `AzureFrontDoorProfile`)

**Example**:
```
Subscription ID: 12345678-1234-1234-1234-123456789abc
Resource Group: my-website-rg
Profile Name: my-frontdoor-profile
```

**Setup Instructions**: **[Azure Front Door Configuration Guide](../Configuration/CDN-AzureFrontDoor.md)**

---

### Cloudflare

**Required Fields**:
- `CloudflareApiToken` - Cloudflare API token
- `CloudflareZoneId` - Zone ID for your domain

**Can be pre-configured**: Yes (via `CdnProvider`, `CloudflareApiToken`, `CloudflareZoneId`)

**Example**:
```
API Token: 1234567890abcdef1234567890abcdef
Zone ID: abcdef1234567890abcdef1234567890
```

**Setup Instructions**: **[Cloudflare Configuration Guide](../Configuration/CDN-Cloudflare.md)**

---

### Amazon CloudFront

**Required Fields**:
- `CloudFrontDistributionId` - CloudFront distribution ID
- `AwsAccessKeyId` - AWS access key
- `AwsSecretAccessKey` - AWS secret key
- `AwsRegion` - AWS region (e.g., `us-east-1`)

**Can be pre-configured**: Yes (via `CdnProvider`, `CloudFrontDistributionId`, `AwsAccessKeyId`, `AwsSecretAccessKey`, `AwsRegion`)

**Example**:
```
Distribution ID: E1234EXAMPLE
Access Key: AKIAIOSFODNN7EXAMPLE
Secret Key: wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
Region: us-east-1
```

**Setup Instructions**: **[CloudFront Configuration Guide](../Configuration/CDN-CloudFront.md)**

---

### Sucuri

**Required Fields**:
- `SucuriApiKey` - Sucuri API key
- `SucuriDomain` - Domain protected by Sucuri

**Can be pre-configured**: Yes (via `CdnProvider`, `SucuriApiKey`, `SucuriDomain`)

**Example**:
```
API Key: 1234567890abcdef1234567890abcdef
Domain: www.mywebsite.com
```

**Setup Instructions**: **[Sucuri Configuration Guide](../Configuration/CDN-Sucuri.md)**

---

## Actions

### "Skip" Button

Skip CDN configuration and proceed to review.

**When to skip**:
- No CDN needed
- Configuring CDN later via environment variables
- Development/testing environment

### "Next" Button

Proceeds to **Step 6: Review & Complete** after validation.

**Validation**:
- Provider-specific fields are complete (if provider selected)
- Credentials are valid format

---

## Pre-Configuration with Environment Variables

Pre-configure CDN via environment variables:

### None Example

```powershell
$env:CdnProvider = "None"
```

### Azure Front Door Example

```powershell
$env:CdnProvider = "AzureFrontDoor"
$env:AzureSubscriptionId = "12345678-1234-1234-1234-123456789abc"
$env:AzureResourceGroup = "my-website-rg"
$env:AzureFrontDoorProfile = "my-frontdoor-profile"
```

### Cloudflare Example

```powershell
$env:CdnProvider = "Cloudflare"
$env:CloudflareApiToken = "your-api-token"
$env:CloudflareZoneId = "your-zone-id"
```

### CloudFront Example

```powershell
$env:CdnProvider = "CloudFront"
$env:CloudFrontDistributionId = "E1234EXAMPLE"
$env:AwsAccessKeyId = "AKIAIOSFODNN7EXAMPLE"
$env:AwsSecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
$env:AwsRegion = "us-east-1"
```

### Sucuri Example

```powershell
$env:CdnProvider = "Sucuri"
$env:SucuriApiKey = "your-api-key"
$env:SucuriDomain = "www.mywebsite.com"
```

**When pre-configured**:
- Provider is auto-selected
- Credentials are read-only/masked
- Cannot be changed in wizard

---

## Troubleshooting

### "Invalid subscription ID format"

**Azure**: Subscription ID must be a valid GUID.

**Solution**: Copy subscription ID from Azure Portal → Subscriptions.

### "API token authentication failed"

**Cloudflare**: Token must have correct permissions.

**Solution**: Verify token has `Zone.Cache Purge` and `Zone.Settings.Read` permissions.

### "Distribution not found"

**CloudFront**: Distribution ID must exist in your AWS account.

**Solution**: Verify distribution ID in CloudFront console.

### "Invalid API key"

**Sucuri**: API key must be valid and active.

**Solution**: Check API key in Sucuri dashboard → Settings → API.

### Cannot Edit Pre-Configured Fields

**Cause**: CDN settings are pre-configured via environment variables.

**Solution**: This is intentional for security. To change, update environment variables and restart.

---

## What Happens Next

After clicking **Next** or **Skip**, you'll proceed to:

**[Step 6: Review & Complete →](./SetupWizard-Step6-Review.md)**

CDN configuration (if provided) is saved and will be applied when setup completes.

---

## See Also

- **[CDN Configuration Overview](../Configuration/CDN-Overview.md)** - Compare providers
- **[Azure Front Door Guide](../Configuration/CDN-AzureFrontDoor.md)** - Azure setup
- **[Cloudflare Guide](../Configuration/CDN-Cloudflare.md)** - Cloudflare setup
- **[CloudFront Guide](../Configuration/CDN-CloudFront.md)** - AWS setup
- **[Sucuri Guide](../Configuration/CDN-Sucuri.md)** - Sucuri setup
- **[← Previous: Email Configuration](./SetupWizard-Step4-Email.md)**
- **[Next: Review & Complete →](./SetupWizard-Step6-Review.md)**
