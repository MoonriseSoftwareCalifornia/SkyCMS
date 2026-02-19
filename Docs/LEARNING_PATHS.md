---
title: SkyCMS Learning Paths
description: Role-based documentation journeys for content editors, developers, DevOps, and decision makers
keywords: learning-paths, guides, tutorials, getting-started, role-based
audience: [all]
---

# SkyCMS Learning Paths

**Not sure where to start?** Choose your role below and follow a curated documentation journey designed for your needs.

---

## 📋 Choose Your Path

- [Content Editor (Non-Technical)](#content-editor-non-technical)
- [Developer](#developer)
- [DevOps / System Administrator](#devops--system-administrator)
- [Decision Maker / Manager](#decision-maker--manager)

---

## Content Editor (Non-Technical)

**Goal:** Learn to create and manage content without technical knowledge

**Time to Complete:** 30-45 minutes

### Step 1: Understand What SkyCMS Is (5 min)
📖 **[About SkyCMS](./About.md)**
- What is SkyCMS?
- Who is it for?
- Key features overview

### Step 2: Get Started Quickly (10 min)
🚀 **[Quick Start Guide](./QuickStart.md)**
- Access your SkyCMS installation
- First login and navigation
- Basic concepts

### Step 3: Learn the Live Editor (10 min)
✏️ **[Live Editor Quick Start](./Editors/LiveEditor/QuickStart.md)**
- Creating and editing pages
- Text formatting basics
- Adding images and links
- Saving and publishing

### Step 4: Manage Your Files (10 min)
📁 **[File Management Quick Start](./FileManagement/Quick-Start.md)**
- Uploading images and files
- Organizing your media
- Using files in pages

### Step 5: Advanced Content Features (Optional)
📚 **Deep Dive Resources:**
- **[Live Editor Complete Guide](./Editors/LiveEditor/README.md)** - Full feature documentation
- **[Page Scheduling](./Editors/PageScheduling.md)** - Schedule posts for future publication
- **[Visual Page Builder](./Editors/Designer/README.md)** - Drag-and-drop page design
- **[Blog Post Lifecycle](./blog/BlogPostLifecycle.md)** - Creating and managing blog posts

### 💡 Quick Tips
- Use the visual editor for most content
- Save frequently (Ctrl+S / Cmd+S)
- Preview before publishing
- Keep the [Troubleshooting Guide](./Troubleshooting.md) bookmarked

---

## Developer

**Goal:** Understand architecture, customize SkyCMS, and extend functionality

**Time to Complete:** 2-3 hours

### Step 1: Understand the Platform (10 min)
📖 **[About SkyCMS](./About.md)**
- Architecture overview
- Edge-native approach
- Deployment models

📘 **[Developer Experience Comparison](./Developer-Experience-Comparison.md)**
- Development workflow comparison
- Technology stack and tooling
- Why developers choose SkyCMS

### Step 2: Quick Local Setup (15 min)
🚀 **[Quick Start Guide](./QuickStart.md)**
- Docker-based local development
- Configuration basics
- First run experience

### Step 3: Configure Database & Storage (30 min)
🔧 **Configuration Essentials:**
- **[Database Configuration Overview](./Configuration/Database-Overview.md)** - Choose and configure your database
  - Start with SQLite for local development
  - Review [connection string reference](./Configuration/Database-Configuration-Reference.md) for production
- **[Storage Configuration Overview](./Configuration/Storage-Overview.md)** - Configure file storage
  - Local file storage for development
  - S3/Azure Blob for production

### Step 4: Understand Core Architecture (45 min)
🏗️ **Architecture Deep Dive:**
- **[Editor Application](https://github.com/CWALabs/SkyCMS/tree/main/Editor)** - Content management interface architecture
- **[Publisher Application](https://github.com/CWALabs/SkyCMS/tree/main/Publisher)** - Public site rendering and delivery
- **[Common Library](./Components/Cosmos.Common.md)** - Shared functionality and data models
- **[Blob Service](./Components/Cosmos.BlobService.md)** - Multi-cloud storage abstraction

### Step 5: Customization & Extension (30 min)
🔨 **Developer Resources:**
- **[Base Controllers](./Developers/Controllers/README.md)** - Extending core controllers
  - [HomeControllerBase](./Developers/Controllers/HomeControllerBase.md)
  - [PubControllerBase](./Developers/Controllers/PubControllerBase.md)
- **[Widgets Overview](./Widgets/README.md)** - Building custom widgets
- **[Creating Editable Areas](./Developer-Guides/CreatingEditableAreas.md)** - Defining regions, headings, and image widgets
- **[Testing Guide](./Development/Testing/README.md)** - Writing and running tests

### Step 6: Advanced Topics (Optional)
📚 **Advanced Resources:**
- **[Identity Framework](./Components/AspNetCore.Identity.FlexDb.md)** - Authentication and authorization
- **[Image Widget Development](./Developers/ImageWidget.md)** - Deep dive into widget architecture
- **[Dynamic Configuration](https://github.com/CWALabs/SkyCMS/tree/main/Cosmos.ConnectionStrings)** - Multi-tenant configuration

### 💡 Developer Quick Tips
- Use Docker Compose for local development
- SQLite is fastest for development, Cosmos DB or SQL Server for production
- Review component README files for detailed API documentation
- Check [Troubleshooting Guide](./Troubleshooting.md) for common development issues

### 🔗 Key Links for Developers
- [GitHub Repository](https://github.com/CWALabs/SkyCMS)
- [Docker Hub - Editor](https://hub.docker.com/r/toiyabe/sky-editor)
- [Docker Hub - Publisher](https://hub.docker.com/r/toiyabe/sky-publisher)

---

## DevOps / System Administrator {#devops--system-administrator}

**Goal:** Deploy, configure, and maintain SkyCMS in production

**Time to Complete:** 3-4 hours

### Step 1: Understand Deployment Options (15 min)
📖 **[About SkyCMS](./About.md)**
- Deployment modes (Static, Edge, Dynamic, Decoupled)
- Infrastructure requirements
- Cloud provider support

🆚 **[Cost Comparison](./_Marketing/Cost-Comparison.md)**
- Infrastructure costs by platform
- Scaling considerations

### Step 2: Choose Your Platform (15 min)
📦 **Pick Your Deployment:**
- **Azure** - [Azure Installation Guide](./Installation/AzureInstall.md)
- **AWS** - [S3 Static Website Hosting](./S3StaticWebsite.md)
- **Cloudflare** - [Edge Hosting Guide](./Installation/CloudflareEdgeHosting.md)
- **Docker** - Use Quick Start with production configuration

### Step 3: Database Configuration (45 min)
🔧 **Database Setup:**
- **[Database Configuration Overview](./Configuration/Database-Overview.md)** - Choose your database
- **Provider-Specific Guides:**
  - [Azure Cosmos DB](./Configuration/Database-CosmosDB.md)
  - [MS SQL Server / Azure SQL](./Configuration/Database-SQLServer.md)
  - [MySQL](./Configuration/Database-MySQL.md)
  - [SQLite](./Configuration/Database-SQLite.md) (dev/testing only)
- **[Database Config Reference](./Configuration/Database-Configuration-Reference.md)** - Connection strings and security

### Step 4: Storage Configuration (45 min)
🗄️ **Storage Setup:**
- **[Storage Configuration Overview](./Configuration/Storage-Overview.md)** - Choose your storage provider
- **Provider-Specific Guides:**
  - [Azure Blob Storage](./Configuration/Storage-AzureBlob.md)
  - [Amazon S3](./Configuration/Storage-S3.md) - See also [S3 Access Keys Setup](./Configuration/AWS-S3-AccessKeys.md)
  - [Cloudflare R2](./Configuration/Storage-Cloudflare.md) - See also [R2 Access Keys Setup](./Configuration/Cloudflare-R2-AccessKeys.md)
  - [Google Cloud Storage](./Configuration/Storage-GoogleCloud.md)
- **[Storage Config Reference](./Configuration/Storage-Configuration-Reference.md)** - Advanced configuration

### Step 5: CDN Integration (30 min)
🌐 **CDN Setup:**
- **[CDN Integration Overview](./Configuration/CDN-Overview.md)** - Choose your CDN provider
- **Provider-Specific Guides:**
  - [Azure Front Door](./Configuration/CDN-AzureFrontDoor.md)
  - [Cloudflare CDN](./Configuration/CDN-Cloudflare.md)
  - [Amazon CloudFront](./Configuration/CDN-CloudFront.md)
  - [Sucuri CDN/WAF](./Configuration/CDN-Sucuri.md)

### Step 6: Configuration & Setup (45 min)
⚙️ **Initial Configuration:**
- **[Minimum Required Settings](./Installation/MinimumRequiredSettings.md)** - Essential environment variables
- **[Setup Wizard Guide](./Installation/SetupWizard.md)** - Interactive configuration walkthrough
  - Choose between full wizard or pre-configured approach
  - Step-by-step documentation for each wizard screen
- **[Email Configuration](./Configuration/Email-Overview.md)** (Optional) - Transactional email setup

### Step 7: Deploy & Verify (30 min)
🚀 **Deployment:**
- Follow platform-specific deployment guide
- Run setup wizard or configure via environment variables
- Verify database connectivity
- Verify storage access
- Test CDN integration
- Create admin account

### Step 8: Keep Handy (Bookmark These)
🔖 **Essential Resources:**
- **[Troubleshooting Guide](./Troubleshooting.md)** - First stop for issues
- **[Changelog](./CHANGELOG.md)** - Track updates and breaking changes
- Platform-specific docs (Azure/AWS/Cloudflare)

### 💡 DevOps Quick Tips
- Always use environment variables for secrets (never commit to git)
- Test configuration with `CosmosAllowSetup=true` setup wizard first
- Use managed identities where possible (Azure, AWS IAM roles)
- Monitor Application Insights / CloudWatch for performance
- Set up automated backups for your database
- Test CDN purge integration before going live

### 🔗 Key Deployment Resources
- [Docker Images on Docker Hub](https://hub.docker.com/u/toiyabe)
- [GitHub Repository - Issues & Support](https://github.com/CWALabs/SkyCMS/issues)

---

## Decision Maker / Manager {#decision-maker--manager}

**Goal:** Understand SkyCMS capabilities, costs, and competitive position

**Time to Complete:** 30-45 minutes

### Step 1: Understand SkyCMS (10 min)
📖 **[About SkyCMS](./About.md)**
- What is an edge-native CMS?
- Key capabilities and benefits
- Who is it for?

### Step 2: Compare Alternatives (15 min)
🆚 **Competitive Analysis:**
- **[SkyCMS vs Headless CMS](./CosmosVsHeadless.md)** - Why traditional rendering matters
- **[Competitors Analysis](./_Marketing/Competitor-Analysis.md)** - How SkyCMS compares to Netlify CMS, CloudCannon, TinaCMS, etc.
- **[Migrating from JAMstack](./MigratingFromJAMstack.md)** - Benefits over Git-based workflows

### Step 3: Understand Costs (10 min)
💰 **[Cost Comparison](./_Marketing/Cost-Comparison.md)**
- Infrastructure costs by platform
- Comparison to competitor pricing
- Total cost of ownership

### Step 4: Review Technical Approach (Optional)
🏗️ **Architecture Overview:**
- **[Developer Experience](./Developer-Experience-Comparison.md)** - Technical approach and benefits
- **[Publisher Application](https://github.com/CWALabs/SkyCMS/tree/main/Publisher)** - How content is delivered

### Step 5: Explore Capabilities (Optional)
✨ **Feature Deep Dives:**
- **[Live Editor Overview](./Editors/LiveEditor/README.md)** - WYSIWYG content editing
- **[Designer Overview](./Editors/Designer/README.md)** - Visual page builder
- **[Blog Features](./blog/BlogPostLifecycle.md)** - Blogging capabilities

### 💡 Key Decision Factors

#### ✅ Why Choose SkyCMS
- **Lower costs** - $0-50/month vs $300-1000/month for competitors
- **Faster publishing** - Seconds vs minutes (no CI/CD pipeline)
- **User-friendly** - No Git knowledge required for content teams
- **Performance** - Edge delivery with CDN integration
- **Flexibility** - Deploy anywhere Docker runs
- **Multi-cloud** - Not locked to single provider

#### ⚠️ When to Consider Alternatives
- **Established ecosystem preference** - If team already invested in WordPress/Drupal
- **Enterprise features required** - Advanced workflow, compliance, multi-language
- **Managed service preference** - If you want zero infrastructure management
- **Specific integrations** - If you need pre-built connections to enterprise systems

### 🔗 Next Steps for Decision Makers
1. **Evaluate** - Review cost comparison and competitor analysis
2. **Pilot** - Run Quick Start on local machine or test server
3. **Compare** - Side-by-side with current CMS or competitors
4. **Decide** - Assess fit for your requirements and budget

---

## 📊 Learning Path Comparison

| Role | Time Investment | Focus Areas | Key Documents |
|------|----------------|-------------|---------------|
| **Content Editor** | 30-45 min | Using the CMS | Quick Start, Live Editor, File Management |
| **Developer** | 2-3 hours | Customization | Architecture, Components, Widgets |
| **DevOps** | 3-4 hours | Deployment | Installation, Database, Storage, CDN |
| **Decision Maker** | 30-45 min | Strategy | About, Competitors, Cost Comparison |

---

## 🆘 Need Help?

- **Can't find what you need?** Check the [Complete Documentation Index](./index.md#complete-documentation-index)
- **Stuck on a problem?** See the [Troubleshooting Guide](./Troubleshooting.md)
- **Have questions?** [GitHub Issues](https://github.com/CWALabs/SkyCMS/issues)
- **Want to contribute?** [GitHub Repository](https://github.com/CWALabs/SkyCMS)

---

## 🔄 After Completing Your Path

### Next Steps by Role

**Content Editors:**
- Explore advanced editing features
- Learn about page templates and layouts
- Master keyboard shortcuts for efficiency

**Developers:**
- Build a custom widget
- Extend base controllers
- Contribute to the project on GitHub

**DevOps:**
- Set up monitoring and alerts
- Configure automated backups
- Optimize CDN and caching settings
- Document your deployment for team

**Decision Makers:**
- Schedule pilot project
- Identify team members for roles
- Plan migration timeline if moving from another system

---

**Last Updated:** December 17, 2025  
**Feedback Welcome:** Help us improve these learning paths by [opening an issue](https://github.com/CWALabs/SkyCMS/issues)
