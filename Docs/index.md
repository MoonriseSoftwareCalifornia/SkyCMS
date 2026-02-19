---
title: SkyCMS Documentation
description: Fast, edge-native CMS for developers and content teams
keywords: documentation, cms, getting started
audience: [all]
---

# SkyCMS Documentation

<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "TechArticle",
  "headline": "SkyCMS Documentation Home",
  "description": "Fast, edge-native CMS for developers and content teams.",
  "author": { "@type": "Organization", "name": "CWALabs" },
  "dateModified": "2026-01-03",
  "version": "Docs 2.0 / SkyCMS v9.2x",
  "keywords": ["skycms", "documentation", "cms", "edge", "jamstack", "azure", "aws", "cloudflare"],
  "audience": ["all"],
  "inLanguage": "en",
  "url": "https://docs-sky-cms.com"
}
</script>

**Edge-native CMS.** Publish in seconds, no build pipelines. Deploy to Azure, AWS, or other clouds, and integrate with your edge providers—Azure Front Door, Cloudflare, CloudFront, Fastly, or Sucuri—all with one configuration model.

**Version:** 2.0 (Dec 2025) · **Compatible:** v9.2x

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FCWALabs%2FSkyCMS%2Fmain%2FInstallScripts%2FAzure%2Fbicep%2Fmain.json)
[![Deploy to AWS](./assets/images/deploy-to-aws.svg)](./InstallScripts/AWS/QUICK_START.html)

## Choose Your Path

### Creating Content

**Start here:** [Content Creator Quick Start](./ContentCreation-QuickStart.md) — Learn the workflow: Layouts → Templates → Pages → Editing
- **Design:** [Layouts Guide](./Layouts/) · [Page Templates Guide](./Templates/)
- **Edit:** [Live Editor](./Editors/LiveEditor/) · [Designer](./Editors/Designer/) · [Code Editor](./Editors/CodeEditor/) · [Image Editing](./Editors/ImageEditing/)
- **Manage:** [File Management](./FileManagement/) · [Publishing & Scheduling](./Editors/PageScheduling.md) · [Blog Management](./blog/BlogPostLifecycle.md)

---

### Developer Information
*Build, customize, and ship*

**Start here:** [Developer Quick Start](./QuickStart.md) · [Website Launch Workflow](./Developer-Guides/Website-Launch-Workflow.md)
- [Installation Overview](./Installation/)
- [Layouts & Templates](./Layouts/)
- [Widgets & Components](./Widgets/)
- [Architecture](./Architecture/Startup-Lifecycle.md)
- [Testing Guide](./Development/Testing/)

---

## Quick links
- [When to Use SkyCMS (includes Edge-Native Architecture)](./When-to-Use-SkyCMS.md)
- [Content Creator Quick Start](./ContentCreation-QuickStart.md) — *For editors and content teams*
- [Installation Quick Start](./QuickStart.md) — *For setup and deployment*
- [Installation Overview](./Installation/)
- [Minimum Required Settings](./Installation/MinimumRequiredSettings.md)
- [Setup Wizard](./Installation/SetupWizard.md)
- [Website Launch Workflow](./Developer-Guides/Website-Launch-Workflow.md)
- [FAQ](./FAQ.md)



## When to use SkyCMS
- You need sub-second global delivery with static output but want a full CMS editing experience.
- You want multi-cloud optionality (Azure, AWS, Cloudflare, any S3-compatible storage) without lock-in.
- You prefer instant publishing over Git/CI-driven rebuilds and long queues.

[Read more about when to use Sky.](./When-to-Use-SkyCMS.md) ⭐

## Who this is for
- Content teams needing an easy editor and fast preview/publish loops.
- Developers who want a CMS without maintaining bespoke build systems.
- Ops/DevOps who need predictable, low-cost, globally distributed sites.

### Decision Makers
*Evaluate fit and cost*

**Start here:** [When to Use SkyCMS](./When-to-Use-SkyCMS.md) ⭐ · [About SkyCMS](./About.md)
- [SkyCMS vs Alternatives](./Comparisons.md)
- [Developer Experience Comparison](./Developer-Experience-Comparison.md)
- [Total Cost of Ownership](./_Marketing/Cost-Comparison.md)
- [FAQ](./FAQ.md)

---

### DevOps / System Administrators
*Deploy, configure, operate*

**Start here:** [Installation Overview](./Installation/) · [Minimum Required Settings](./Installation/MinimumRequiredSettings.md)
- Deploy: [Azure](./Installation/AzureInstall.md) · [AWS S3](./S3StaticWebsite.md) · [Cloudflare Edge](./Installation/CloudflareEdgeHosting.md)
- Configure: [Setup Wizard](./Installation/SetupWizard.md) · [Database](./Configuration/Database-Overview.md) · [Storage](./Configuration/Storage-Overview.md) · [CDN](./Configuration/CDN-Overview.md) · [Multi-Tenant](./Configuration/Multi-Tenant-Configuration.md)
- Operate: [Post-Installation](./Installation/Post-Installation.md) ⭐ · [Monthly Maintenance](./Checklists/Monthly-Maintenance.md) · [Troubleshooting](./Troubleshooting.md)

---

## Prerequisites
- Docker-capable environment and one supported database (MySQL, MS SQL, SQLite, or Cosmos DB) plus S3-compatible or Azure storage.
- For cloud deploys: access to Azure/AWS/Cloudflare accounts and their CLIs where applicable.

---

## Core Topics

### Authentication & Security
- [Authentication Overview](./Authentication-Overview.md)
- [Role-Based Access Control](./Administration/Roles-and-Permissions.md) ⭐
- [Identity Framework](./Components/AspNetCore.Identity.FlexDb.md)

### Content Management
- [Page Templates](./Templates/)
- [Layouts](./Layouts/)
- [File Management](./FileManagement/)
- [Blog System](./blog/BlogPostLifecycle.md)

### Publishing
- [Publishing Modes](./Publishing-Overview.md)
- [Scheduled Publishing](./Editors/PageScheduling.md)
- [Static Generation](./Publishing-Overview.md)

### Integration
- [Email Configuration](./Configuration/Email-Overview.md)
- [CDN Integration](./Configuration/CDN-Overview.md)
- [Storage Providers](./Configuration/Storage-Overview.md)

---

## Popular Guides

| Getting Started | Migration | Reference |
|----------------|-----------|-----------|
| [Quick Start Guide](./QuickStart.md) | [From JAMstack](./MigratingFromJAMstack.md) | [Widgets Directory](./Widgets/) |
| [Learning Paths](./LEARNING_PATHS.md) | [From WordPress](./FAQ.md#wordpress-migration) | [Configuration Reference](./Configuration/Database-Configuration-Reference.md) |
| [First Website Tutorial](./Developer-Guides/Website-Launch-Workflow.md) | [Setup Wizard](./Installation/SetupWizard.md) | [API Documentation](./Developers/) |

---

## Need Help?

- **Can't find what you're looking for?** Try the [complete index](#complete-documentation-index) below
- **Report issues:** [GitHub Issues](https://github.com/CWALabs/SkyCMS/issues)
- **Contribute:** [Documentation Guidelines](./CONTRIBUTING.md)

---

## Complete Documentation Index

<details>
<summary><strong>Click to expand all documentation</strong></summary>

### Installation & Deployment
- [Installation Overview](./Installation/)
- [Minimum Required Settings](./Installation/MinimumRequiredSettings.md)
- [Setup Wizard](./Installation/SetupWizard.md)
  - [Welcome](./Installation/SetupWizard-Welcome.md)
  - [Storage](./Installation/SetupWizard-Step1-Storage.md)
  - [Admin Account](./Installation/SetupWizard-Step2-Admin.md)
  - [Publisher](./Installation/SetupWizard-Step3-Publisher.md)
  - [Email](./Installation/SetupWizard-Step4-Email.md)
  - [CDN](./Installation/SetupWizard-Step5-CDN.md)
  - [Review](./Installation/SetupWizard-Step6-Review.md)
- [Post-Installation](./Installation/Post-Installation.md)
- Platform Guides:
  - [Azure](./Installation/AzureInstall.md)
  - [AWS S3](./S3StaticWebsite.md)
  - [Cloudflare Edge](./Installation/CloudflareEdgeHosting.md)

### Configuration
- [Database](./Configuration/Database-Overview.md): [Cosmos DB](./Configuration/Database-CosmosDB.md) | [SQL Server](./Configuration/Database-SQLServer.md) | [MySQL](./Configuration/Database-MySQL.md) | [SQLite](./Configuration/Database-SQLite.md)
- [Storage](./Configuration/Storage-Overview.md): [Azure Blob](./Configuration/Storage-AzureBlob.md) | [S3](./Configuration/Storage-S3.md) | [R2](./Configuration/Storage-Cloudflare.md) | [GCS](./Configuration/Storage-GoogleCloud.md)
- [CDN](./Configuration/CDN-Overview.md): [Azure Front Door](./Configuration/CDN-AzureFrontDoor.md) | [Cloudflare](./Configuration/CDN-Cloudflare.md) | [CloudFront](./Configuration/CDN-CloudFront.md) | [Sucuri](./Configuration/CDN-Sucuri.md)
- [Email](./Configuration/Email-Overview.md): [SendGrid](./Configuration/Email-SendGrid.md) | [Azure Communication](./Configuration/Email-AzureCommunicationServices.md) | [SMTP](./Configuration/Email-SMTP.md)
- [Multi-Tenant](./Configuration/Multi-Tenant-Configuration.md)

### Content Editing
- **Live Editor:** [Guide](./Editors/LiveEditor/) | [Quick Start](./Editors/LiveEditor/QuickStart.md) | [Visual Guide](./Editors/LiveEditor/VisualGuide.md)
- **Designer:** [Guide](./Editors/Designer/) | [Quick Start](./Editors/Designer/QuickStart.md)
- **Code Editor:** [Guide](./Editors/CodeEditor/)
- **Image Editing:** [Guide](./Editors/ImageEditing/)
- **File Management:** [Guide](./FileManagement/) | [Quick Start](./FileManagement/Quick-Start.md)

### Developer Resources
- [Developer Hub](./Developers/)
- [Website Launch Workflow](./Developer-Guides/Website-Launch-Workflow.md)
- Controllers: [HomeControllerBase](./Developers/Controllers/HomeControllerBase.md) | [PubControllerBase](./Developers/Controllers/PubControllerBase.md)
- Widgets: [Overview](./Widgets/) | [Image](./Widgets/Image-Widget.md) | [Forms](./Widgets/Forms-Widget.md) | [Search](./Widgets/Search-Widget.md) | [Navigation](./Widgets/Nav-Builder-Widget.md)
- Architecture: [Startup](./Architecture/Startup-Lifecycle.md) | [Middleware](./Architecture/Middleware-Pipeline.md)
- [Testing Guide](./Development/Testing/)

### Comparisons & Evaluation
- [Comparisons Matrix](./Comparisons.md)
- [Developer Experience Comparison](./Developer-Experience-Comparison.md)
- [SkyCMS vs Headless CMS](./CosmosVsHeadless.md)
- [Migrating from JAMstack](./MigratingFromJAMstack.md)
- [FAQ](./FAQ.md)

### Support & Maintenance
- [Troubleshooting](./Troubleshooting.md)
- [Documentation Gaps Analysis](https://github.com/CWALabs/SkyCMS/blob/main/Docs/_archive/Project-Documentation/DOCUMENTATION_GAPS_ANALYSIS.md)
- [Changelog](./CHANGELOG.md)

</details>

---

## Additional Resources

- **[Sitemap](./sitemap.xml)** - XML sitemap for search engine crawlers
- **[JSON Feed](./docs-index.json)** - Machine-readable documentation index
- **[Analytics Setup](./Analytics-Setup.md)** - Documentation engagement tracking
- **[AI & SEO Content Standards](./AI-SEO-Content-Standards.md)** - Front matter conventions
- **[License Information](./License.md)** - Dual licensing (GPL 2.0-or-later / MIT)

---

**Last Updated:** December 17, 2025  
**Maintained by:** Moonrise Software, LLC  
**License:** See [LICENSE-GPL](../LICENSE-GPL) and [LICENSE-MIT](../LICENSE-MIT)

For project overview and "what is SkyCMS," see the [main README](https://github.com/CWALabs/SkyCMS#readme).

