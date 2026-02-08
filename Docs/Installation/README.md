---
audience: [developers, administrators]
title: Installation Guide
description: Complete installation guide for SkyCMS including wizard setup and manual configuration options
keywords: installation, setup, Docker, AWS, Azure, configuration, wizard
version: 2.0
updated: 2025-12-20
canonical: /Installation/README.html
aliases: []
scope:
   platforms: [azure, aws, cloudflare, local]
   tenancy: [single, multi]
status: stable
chunk_hint: 340
---

# SkyCMS Installation Guide

<script type="application/ld+json">
{
   "@context": "https://schema.org",
   "@type": "HowTo",
   "name": "Install SkyCMS",
   "description": "Choose the wizard or environment-variable path, gather credentials, configure settings, and verify post-install checks.",
   "totalTime": "PT30M",
   "audience": ["developers", "administrators"],
   "proficiencyLevel": "Beginner",
   "inLanguage": "en",
   "supply": [
      {"@type": "HowToSupply", "name": "Database and storage credentials"}
   ],
   "tool": [
      {"@type": "HowToTool", "name": "Provider CLIs (az, aws, cloudflare, mysql/sqlcmd)"}
   ],
   "step": [
      {
         "@type": "HowToStep",
         "name": "Choose configuration approach",
         "url": "https://docs-sky-cms.com/Installation/README.html#choose-approach",
         "text": "Decide between the interactive wizard for single-tenant quick starts or environment variables for production and multi-tenant setups."
      },
      {
         "@type": "HowToStep",
         "name": "Gather prerequisites",
         "url": "https://docs-sky-cms.com/Installation/README.html#prerequisites",
         "text": "Ensure provider roles and CLIs are installed, and collect database and storage credentials for your chosen platform."
      },
      {
         "@type": "HowToStep",
         "name": "Configure settings",
         "url": "https://docs-sky-cms.com/Installation/README.html#quick-path",
         "text": "Follow the Minimum Required Settings, then set environment variables or run the Setup Wizard as appropriate."
      },
      {
         "@type": "HowToStep",
         "name": "Verify post-install",
         "url": "https://docs-sky-cms.com/Installation/README.html#after-installation",
         "text": "Publish a test page, validate email/CDN, and finalize admin setup with the post-install guide."
      }
   ],
   "url": "https://docs-sky-cms.com/Installation/README.html"
}
</script>

## When to use this
- You need to install SkyCMS for the first time (single-tenant wizard) or configure production-ready env-var deployments.
- You are selecting a platform path (Azure, AWS, Cloudflare, local) and need prerequisites in one place.

## Why this matters
- Reduces misconfigurations by front-loading required env vars, roles, and platform assumptions.
- Clarifies when to use the wizard vs. DynamicConfig for multi-tenant production setups.

## Key takeaways
- Two paths: wizard (single-tenant) vs. env-vars (production/automation); multi-tenant skips the wizard.
- Validate provider credentials/roles before deployment to avoid runtime failures.
- After install, follow the post-install checklist to verify publish, email, CDN, and admin setup.

## Prerequisites
- Database and storage credentials reachable from your target platform.
- Cloud access/roles for Azure/AWS/Cloudflare if deploying there; provider CLIs installed for validation.
- Decision on tenancy: single (wizard ok) vs. multi (DynamicConfig, `CosmosAllowSetup=false`).

## Quick path
1. Review Minimum Required Settings.
2. Pick wizard (single-tenant) or env-vars (production/CI, multi-tenant with DynamicConfig).
3. Run the Setup Wizard if applicable; otherwise set env vars and start the app.
4. Complete post-install verification (publish test page, email, CDN, admin accounts).

## Docs Publisher (separate repo)
If you want to publish Markdown docs without cloning the full SkyCMS repository, use the SkyCMS.DocsPublisher template repo.
See [Docs Publisher](./DocsPublisher.md) for details and the link to the template.

## Key facts {#key-facts}

- Two config paths: single-tenant wizard (CosmosAllowSetup=true) vs env-var-driven (production); multi-tenant uses DynamicConfig, not the wizard.
- Minimum Required Settings are the source of truth for env vars and connection strings—do that before platform-specific guides.
- Azure/AWS/Cloudflare guides assume provider roles are set; use provider CLIs to validate connectivity before running SkyCMS.
- Post-install checklist: run Setup Wizard (single-tenant), verify publish, email, CDN, and create first admin.

## Choose Your Configuration Approach {#choose-approach}

SkyCMS offers two ways to configure your installation:

![Setup wizard welcome with configuration options](../images/screenshots/setup-wizard-welcome.webp)

### 🧙 **Interactive Wizard (Recommended for New Users)** {#wizard}
- Minimal pre-configuration (just database + enable wizard)
- Step-by-step guided setup through web UI
- Configure storage, admin account, publisher, email, CDN interactively
- **Best for**: First-time installations, development, testing
- **Start here**: [Setup Wizard Guide](./SetupWizard.md)

### ⚙️ **Environment Variables (Recommended for Production)** {#env-vars}
- Pre-configure all settings via environment variables
- Optional: use wizard to configure remaining settings interactively
- Settings are read-only/hidden in wizard when pre-configured
- **Best for**: Docker/Kubernetes, CI/CD pipelines, production deployments
- **Start here**: [Minimum Required Settings](./MinimumRequiredSettings.md)

> **You can mix both approaches**: Pre-configure sensitive settings (database, storage credentials) via environment variables, then use the wizard for remaining settings (admin account, publisher URL, etc.).

---

## Quick Start: Minimum Required Settings {#minimum-settings}

Before deploying to any platform, review the [Minimum Required Settings](./MinimumRequiredSettings.md) guide. This covers:
- Single-tenant vs. multi-tenant configuration
- Required connection strings
- Environment variables and secrets management
- Configuration examples

**Start here** if you're setting up SkyCMS for the first time or need to understand the configuration options.

### After Installation Completes {#after-installation}

Once setup is finished, follow the **[Post-Installation Configuration Guide](./Post-Installation.md)** to:
- Verify your installation is fully operational
- Create and publish your first page
- Configure security and access control
- Test email and CDN integration
- Set up user accounts for your team

![Final review and completion of setup wizard](../images/screenshots/setup-wizard-review.webp)

---

## Platform-Specific Guides {#platform-guides}

### Azure {#azure}

[Azure Installation Guide](./AzureInstall.md)

Deploy SkyCMS to Microsoft Azure using the automated deployment template. This guide covers:
- Deploy-to-Azure button quick start
- Azure resource configuration
- Post-deployment setup
- Connecting your domain

**Best for**: Organizations already using Azure, need managed services, prefer Microsoft ecosystem.

---

### AWS {#aws}

[AWS Installation Guide](./AWSInstall.md)

Deploy SkyCMS on AWS with flexible hosting options. This guide covers:
- S3 bucket creation and configuration
- EC2, ECS, or Lightsail deployment options
- S3 static hosting for publishers
- CloudFront CDN integration
- IAM access key setup and permissions

**Best for**: Organizations already using AWS, need flexible infrastructure, prefer AWS services.

---

### Cloudflare Edge Hosting {#cloudflare}

[Cloudflare Edge Hosting Guide](./CloudflareEdgeHosting.md)

Deploy a static website to Cloudflare using an origin-less (edge) architecture with R2 storage. This guide covers:
- Setting up Cloudflare R2 bucket
- Connecting SkyCMS to R2 storage
- Configuring custom domains and rules
- Edge-based request handling (no origin server)

**Best for**: Maximum performance, origin-less architecture, global edge distribution, pay-per-use pricing.

---

## Additional Resources {#additional-resources}

- [Configuration Overview](../Configuration/) - Detailed configuration options for databases, storage, and CDN
- [Troubleshooting](./MinimumRequiredSettings.md#troubleshooting) - Common installation issues and solutions
- [Developer Experience](../DeveloperExperience.md) - Development environment setup

---

## Next Steps After Installation {#next-steps}

1. Access your SkyCMS Editor instance
2. Run the **[Setup Wizard](./SetupWizard.md)** (single-tenant) to configure:
   - Database connection
   - Storage provider (Azure Blob, S3, R2, or Google Cloud Storage)
   - Administrator account
   - Publisher settings
   - Email configuration (optional)
   - CDN configuration (optional)
3. Create your first page and publish content
4. Configure your custom domain
5. Set up CDN for optimal performance (optional)

See the **[Setup Wizard Guide](./SetupWizard.md)** for step-by-step instructions with dedicated pages for each wizard step.

For more details, see [About SkyCMS](../About.md) and the [Complete Documentation Index](../).

## FAQ {#faq}

- **Should I use the wizard in production?** Only for single-tenant quick starts. For production, preconfigure via environment variables; for multi-tenant, disable the wizard and use DynamicConfig.
- **Which database should I start with locally?** SQLite for local demos; Azure SQL or MySQL for shared and production setups.
- **How do I verify connectivity before running SkyCMS?** Use provider CLIs (az, aws, cloudflare, mysql/sqlcmd) to test connection strings and permissions first.

<script type="application/ld+json">
{
   "@context": "https://schema.org",
   "@type": "FAQPage",
   "mainEntity": [
      {"@type": "Question", "name": "Should I use the wizard in production?", "acceptedAnswer": {"@type": "Answer", "text": "Use the wizard only for single-tenant quick starts. For production, preconfigure via environment variables; for multi-tenant, disable the wizard and use DynamicConfig."}},
      {"@type": "Question", "name": "Which database should I start with locally?", "acceptedAnswer": {"@type": "Answer", "text": "Use SQLite for local demos; use Azure SQL or MySQL for shared and production setups."}},
      {"@type": "Question", "name": "How do I verify connectivity before running SkyCMS?", "acceptedAnswer": {"@type": "Answer", "text": "Use provider CLIs such as az, aws, cloudflare, mysql, or sqlcmd to test connection strings and permissions before running SkyCMS."}}
   ]
}
</script>
