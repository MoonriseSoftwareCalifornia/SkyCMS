---
title: "SkyCMS: Edge-Native CMS"
description: "Fast, edge-native CMS with instant publishing, multi-cloud deployment, and no-build pipelines."
audience: ["developers", "administrators", "content-creators", "decision-makers"]
tags: ["edge-native", "cms", "static", "cloud", "azure", "aws", "cloudflare"]
keywords: ["skycms", "edge", "static", "jamstack", "cms", "azure", "aws", "cloudflare"]
version: "9.2x"
last_updated: "2026-01-03"
stage: "ga"
platforms: ["aws", "azure", "cloudflare", "any-docker"]
tenancy: ["single", "multi"]
read_time: 7
canonical: "https://github.com/CWALabs/SkyCMS"
robots: "index,follow"
---

# SkyCMS: The Edge-Native CMS

![SkyCMS Logo](./Editor/wwwroot/images/skycms/SkyCMSLogoNoWiTextDarkTransparent30h.png)

[![CodeQL](https://github.com/CWALabs/SkyCMS/actions/workflows/codeql.yml/badge.svg)](https://github.com/CWALabs/SkyCMS/actions/workflows/codeql.yml)
[![Publish Docker Images CI](https://github.com/CWALabs/SkyCMS/actions/workflows/docker-image.yml/badge.svg)](https://github.com/CWALabs/SkyCMS/actions/workflows/docker-image.yml)
[![Unit tests](https://raw.githubusercontent.com/MoonriseSoftwareCalifornia/SkyCMS/main/test-badge.svg)](https://github.com/MoonriseSoftwareCalifornia/SkyCMS/actions)
[![License: GPL v2+](https://img.shields.io/badge/License-GPL%20v2%2B-blue.svg)](https://www.gnu.org/licenses/gpl-2.0)
[![Documentation](https://img.shields.io/badge/docs-complete-brightgreen.svg)](https://docs.sky-cms.com)

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FCWALabs%2FSkyCMS%2Fmain%2FInstallScripts%2FAzure%2Fbicep%2Fmain.json)
[![Deploy to AWS](./Docs/assets/images/deploy-to-aws.svg)](./InstallScripts/AWS/QUICK_START.md)

**Documentation home:** https://docs.sky-cms.com

> SkyCMS is a lightweight, edge-native CMS that ships instant publishing, static generation, and multi-cloud deployment without external build pipelines.

## When to use SkyCMS
- You need sub-second global delivery with static output but want a full CMS editing experience.
- You want multi-cloud optionality (Azure, AWS, Cloudflare, any S3-compatible storage) without lock-in.
- You prefer instant publishing over Git/CI-driven rebuilds and long queues.

## Who this is for
- Content teams needing an easy editor and fast preview/publish loops.
- Developers who want a CMS without maintaining bespoke build systems.
- Ops/DevOps who need predictable, low-cost, globally distributed sites.

## Prerequisites
- Docker-capable environment and one supported database (MySQL, MS SQL, SQLite, or Cosmos DB) plus S3-compatible or Azure storage.
- For cloud deploys: access to Azure/AWS/Cloudflare accounts and their CLIs where applicable.

## Deploy to a cloud

[![Deploy to Azure](https://img.shields.io/badge/Deploy_to-Azure-0078D4?style=plastic&logo=microsoftazure&logoColor=white)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FCWALabs%2FSkyCMS%2Fmain%2FInstallScripts%2FAzure%2Fbicep%2Fmain.json) [![Deploy to AWS](https://img.shields.io/badge/Deploy_to-AWS-FF9900?style=plastic&logo=amazonaws&logoColor=white)](./InstallScripts/AWS/QUICK_START.md)

> **Quick Start:** 
> - **Azure:** Click the Azure button above for one-click deployment to Container Apps + MySQL
> - **AWS (Recommended):** Download the SkyCMS AWS Deployment Package from Releases and run `cdk-deploy.ps1` — https://github.com/CWALabs/SkyCMS/releases/latest
> - **AWS (Advanced):** Run from source using the interactive CDK deployment — [AWS CDK Guide](./InstallScripts/AWS/AWS_CDK_INTERACTIVE_DEPLOYMENT.md)
>
> Both deployments include detailed post-deployment setup wizard and configuration guides.

**A powerful, modern alternative to Netlify CMS, CloudCannon, TinaCMS, Stackbit, and Publii** — SkyCMS delivers all the benefits of JAMstack architecture without the complexity, cost, or technical barriers that plague traditional static site generators.

**A light-weight, high-performance, multi-cloud content management system built for edge delivery and static site generation.**

Deploy anywhere: AWS, Azure, Cloudflare, Google — or any cloud that supports Docker containers, one of our supported databases (MySQL, MS SQL, SQLite, or Cosmos DB), and either S3‑compatible object storage or Azure Storage.

Content tools are intuitive and rich, making them developer-friendly and non-technical user-friendly, perfect for web professionals working together with content creators.

Choose your hosting model: [Static](https://cwalabs.github.io/SkyCMS/StorageConfig.html#static-website-hosting-azure) | [Edge (origin-less)](https://cwalabs.github.io/SkyCMS/CloudflareEdgeHosting.html) | [Dynamic](./Publisher/README.md) | [Decoupled](./Publisher/README.md)

Read the [About SkyCMS](https://cwalabs.github.io/SkyCMS/About.html) page for a concise overview.

**[View the Master Table of Contents](https://cwalabs.github.io/SkyCMS/)** for complete documentation index and navigation.

## SkyCMS & Modern Approach (Overview)

SkyCMS is designed around modern web practices: componentized layouts/templates, content-first workflows (draft→review→publish), clear separation of concerns, and built-in quality guardrails (accessibility, performance, SEO, link validation). For a deeper look at how these principles are enabled in SkyCMS, see [SkyCMS & Modern Approach (Deep Dive)](Docs/Developer-Guides/SkyCMS-Modern-Approach.md).

## How SkyCMS Compares

Evaluating SkyCMS against alternatives?
- **[Comprehensive Comparison Matrix](Docs/Comparisons.md)** — SkyCMS vs. WordPress, Jekyll, Hugo, Gatsby, Contentful, Sanity (features, cost, workflow)
- **[Developer Experience Comparison](Docs/Developer-Experience-Comparison.md)** — Learning curves, daily workflows, deployment complexity

---

## What is an Edge-Native CMS?

**SkyCMS is an Edge-Native CMS** — a new category of content management system that combines the **editing experience of traditional CMSs** with the **performance and simplicity of static site generators**, all while being optimized for **edge delivery and global CDN distribution**.

### The Problem We Solve

Modern web teams face a difficult choice:

**Traditional CMSs** (WordPress, Drupal, etc.)

- Easy for editors to use
- Real-time content updates
- Slow performance under load
- Security vulnerabilities
- High hosting costs
- Complex scaling requirements

**Static Site Generators** (Jekyll, Hugo, Gatsby, Next.js)

- Blazing fast performance
- Low hosting costs
- Great security
- Complex Git-based workflows
- Requires technical knowledge
- Long build times
- Multiple tools to configure and maintain

**Headless CMSs** (Contentful, Strapi, Sanity)

- Modern editing experience
- API-driven content delivery
- Expensive API usage costs
- Requires custom frontend development
- Complex architecture with multiple systems
- Ongoing maintenance burden

### The SkyCMS Solution

**SkyCMS eliminates this false choice** by being purpose-built for edge deployment while maintaining a complete CMS editing experience:

#### **For Content Editors**

- Familiar WYSIWYG editing (CKEditor 5)
- Visual page building (GrapesJS)
- No Git knowledge required
- Instant content previews
- Built-in version control
- One-click publishing

#### **For Developers**

- No external build pipelines to configure
- No CI/CD complexity
- Direct deployment to edge locations
- Code editor with Monaco (VS Code)
- Multiple deployment modes
- Docker-based infrastructure

#### **For Performance**

- Static file generation at the edge
- Global CDN distribution
- Origin-less hosting via Cloudflare R2 + Rules (no Worker required)
- Sub-second page loads
- Handles massive traffic spikes
- Minimal infrastructure costs

### How SkyCMS Fills Its Niche

SkyCMS sits at the **intersection of three architectures**, taking the best from each:

```text
Traditional CMS          SkyCMS (Edge-Native)      Static Site Generator
(WordPress)              (Best of Both)            (Jekyll/Hugo)
     │                          │                         │
     └──────────────────────────┴─────────────────────────┘
           Easy Editing    +    Edge Performance    =    Modern Web
```

**What Makes SkyCMS Different:**

1. **Integrated Publishing Pipeline**: Built-in Publisher component handles rendering and deployment — no external build tools, no Git workflows, no CI/CD pipelines to configure

2. **Hybrid Architecture**: Render content as static files for edge delivery while maintaining dynamic capabilities when needed

3. **Multi-Cloud Native**: Deploy to Azure, AWS, Cloudflare, or any S3-compatible storage without vendor lock-in

4. **Origin-Less Edge Hosting**: Deploy directly to Cloudflare's edge network using Cloudflare R2 + Rules (no Worker required) — no origin servers required

5. **Instant Publishing**: Changes go live in seconds, not minutes — no waiting for build pipelines

6. **Complete CMS Experience**: Full-featured content management with version control, templates, media management, and user roles — not just a "content API"

### Real-World Impact

| Scenario | Traditional CMS | Static Site Generator | SkyCMS |
|----------|----------------|----------------------|---------|
| **Content update time** | Instant (but slow delivery) | 2-15 minutes (build + deploy) | < 5 seconds |
| **Technical skill required** | Low | High (Git, CLI, build tools) | Low |
| **Performance under load** | Poor (requires scaling) | Excellent | Excellent |
| **Hosting cost (100k pageviews)** | $50-500/month | $0-10/month | $0-10/month |
| **Setup complexity** | Moderate | High (multiple tools) | Low (single platform) |
| **Maintenance burden** | High (security, updates) | High (build pipeline) | Low (containerized) |

## Overview

[SkyCMS](https://Sky-cms.com/) is an open-source, cloud-native Edge-Native CMS that **renders complete web pages** optimized for edge delivery and global distribution. Built with modern web technologies, SkyCMS runs in multiple modes to meet different deployment needs:

- **Static Mode** (Primary): Content rendered as static HTML files and hosted on cloud storage (Azure Blob, S3, Cloudflare R2) — highest performance, stability, and operational simplicity
  - Can be deployed as an **origin-less, edge-hosted** site via Cloudflare R2 + Rules (no Worker required)
  - Automatic static site generation without external build tools
  - Integrated publishing pipeline eliminates CI/CD complexity
- **Dynamic Mode**: Publisher application dynamically renders pages on-demand — full CMS functionality with server-side rendering
- **Decoupled Mode**: Separate editor and publisher applications — near-static performance with backend functionality
- **API Mode** (Optional): RESTful API available for headless scenarios — content delivered as JSON for multi-channel distribution

## Design Objectives

SkyCMS was built with the following core objectives:

- **Performance**: Outperform traditional CMSs through static site generation and optimized dynamic rendering
- **User-Friendly**: Easy to use by both web developers and non-technical content editors
- **Low Maintenance**: Easy to administer with low operational costs
- **Flexible Deployment**: Support for static, dynamic, decoupled, and optional API modes
- **Cloud-Native**: Built for modern cloud infrastructure with global scalability
- **Complete Page Rendering**: Primary focus on delivering complete HTML pages rather than API-first architecture
- **Integrated Publishing**: Built-in version control, automatic triggers, and direct cloud deployment—eliminating the complexity of traditional Git-based static site workflows

## Key Features

### Advantages Over Traditional Git-Based Static Site Deployment

SkyCMS represents a **next-generation approach** to static site publishing that eliminates the complexity and friction of traditional Git-based CI/CD workflows:

| **Traditional Approach** | **SkyCMS Approach** |
|--------------------------|---------------------|
| External Git repository required | Built-in version control integrated into CMS |
| Separate CI/CD pipeline (GitHub Actions, Netlify, etc.) | Automatic triggers built into the system |
| Static site generator needed (Jekyll, Hugo, Gatsby, Next.js) | Direct rendering without external build tools |
| Multiple tools to learn and configure | Single integrated platform |
| Build time delays (minutes per deployment) | Instant publishing with Publisher component |
| Complex pipeline debugging | Streamlined troubleshooting |
| Content creators need Git knowledge | User-friendly content management interface |
| Static OR dynamic content | **Hybrid: simultaneous static AND dynamic content** |
| Manual scheduling or cron-based rebuilds | **Built-in page scheduling with calendar widget** |

**Key Technical Advantages:**

- **No Build Pipeline Required**: Content is rendered directly by the Publisher component, eliminating wait times and pipeline configuration
- **Integrated Version Control**: Full versioning system built into the CMS—no external Git workflow needed
- **Automatic Deployment**: Direct deployment to Azure Blob Storage, AWS S3, or Cloudflare R2 without intermediary services
- **Built-in Page Scheduling**: Schedule pages for future publication with a simple calendar widget—no GitHub Actions, cron jobs, or CI/CD scheduling needed
- **Faster Publishing**: Changes go live immediately without waiting for CI/CD builds
- **Hybrid Architecture**: Serve static files for performance while maintaining dynamic capabilities when needed
- **Simplified Operations**: Fewer moving parts mean less infrastructure to maintain and fewer points of failure
- **Multi-Cloud Native**: Deploy to any cloud platform that supports Docker containers and object storage

### Performance Benchmarks

Real-world comparison of publishing workflows:

| Scenario | Git-Based (Netlify) | SkyCMS |
|----------|-------------------|---------|
| Single page update | 2-5 minutes | < 5 seconds |
| Bulk content update (50 pages) | 5-15 minutes | < 30 seconds |
| Image optimization | Build-time penalty | On-upload processing |
| Preview environment | Separate branch + build | Instant preview mode |
| Rollback time | Redeploy previous build (2-5 min) | Instant version restore |
| **Scheduled publishing** | **Cron job + full rebuild** | **Calendar widget + instant activation** |

This **CMS-native approach** achieves the same benefits as JAMstack (speed, scalability, global distribution) but with dramatically reduced complexity and operational overhead.

### Content Management

- **Multiple Content Types**: Standard pages, blog posts, and custom article types
- **Rich Editing Tools**: CKEditor 5, GrapesJS, Monaco Editor, and Filerobot image editor
- **Version Control**: Full versioning system with restore capabilities
- **Template System**: Reusable page templates with editable regions
- **Page Scheduling**: Schedule pages for automatic publication at future dates and times using Hangfire
- **Multi-Mode Publishing**: Static file generation, dynamic rendering, or optional API delivery

### Blogging Platform

SkyCMS includes robust blogging capabilities built on its flexible article system:

- **Blog Post Management**: Create and manage blog posts with categories and introductions
- **Article Versioning**: Full version history for all blog posts
- **Publishing Workflow**: Draft, schedule, and publish blog content
- **Catalog System**: Organized listing of blog posts with metadata
- **Future Enhancements**: RSS feeds, category archives, tagging system, and more

[Learn more about Blog Features](https://cwalabs.github.io/SkyCMS/blog/BlogPostLifecycle.html) | [Planned Blog Enhancements](https://cwalabs.github.io/SkyCMS/blog/BlogFutureEnhancements.html)

### Performance & Scalability

- **Edge Hosting**: Origin-less deployment via Cloudflare R2 + Rules (no Worker required)

### Security & Access Control

- **Role-Based Access**: Administrator and Editor roles
- **Identity Integration**: ASP.NET Core Identity
- **External Providers**: Google and Microsoft authentication
- **Permission System**: Article-level access control
- **Secure File Storage**: Encrypted and authenticated storage access

## Use Cases

SkyCMS excels in demanding scenarios:

- **High-Capacity Websites**: Government sites during emergencies, news portals
- **Content-Heavy Platforms**: Media sites like New York Times, National Geographic, streaming platforms
- **Performance-Critical Applications**: Sites requiring minimal latency and efficient content delivery
- **Global Distribution**: Multi-regional redundancy with minimal administration overhead
- **Non-Technical Teams**: User-friendly interface requiring minimal training

## Content Editing Tools

SkyCMS integrates the best web content creation tools to provide a comprehensive editing experience:

### CKEditor 5

![CKEditor](https://cwalabs.github.io/SkyCMS/ckeditor.webp)

Industry-standard WYSIWYG editor with rich text formatting, extensive plugin support, and intuitive interface. Perfect for non-technical users who want Word-like editing capabilities.

[Learn more about the Live Editor](https://cwalabs.github.io/SkyCMS/Editors/LiveEditor/README.html)

### GrapesJS

![GrapesJS](https://cwalabs.github.io/SkyCMS/grapesjs.png)

Visual web builder with drag-and-drop interface for creating complex layouts without coding. Ideal for designing landing pages, newsletters, and custom templates.

[Watch our GrapesJS demo video](https://www.youtube.com/watch?v=mVGPlbnbC5c) | [Designer Documentation](https://cwalabs.github.io/SkyCMS/Editors/Designer/README.html)

### Monaco Editor (Visual Studio Code)

![Monaco Editor](https://cwalabs.github.io/SkyCMS/CodeEditor.png)

Powerful code editor for developers, featuring syntax highlighting, IntelliSense, and advanced editing capabilities. Includes diff tools and Emmet notation support.

[Code Editor Documentation](https://cwalabs.github.io/SkyCMS/Editors/CodeEditor/README.html)

### Filerobot Image Editor

![Filerobot](https://cwalabs.github.io/SkyCMS/Filerobot.png)

Integrated image editing with resizing, cropping, filtering, and annotation capabilities. Edit images directly within the CMS without external tools.

[Image Editing Documentation](https://cwalabs.github.io/SkyCMS/Editors/ImageEditing/README.html)

### FilePond File Uploader

Modern file upload interface with drag-and-drop, image previews, and file validation. Supports multiple file types with progress tracking.

## Architecture & Technology Stack

### Core Applications

- **Editor Application** (`Editor/`): Content creation and management interface
- **Publisher Application** (`Publisher/`): Public-facing website renderer
- **Common Library** (`Common/`): Shared functionality and utilities
- **Blob Service** (`Cosmos.BlobService/`): File storage management
- **Dynamic Configuration** (`Cosmos.ConnectionStrings/`): Runtime configuration
- **Identity Framework** (`AspNetCore.Identity.FlexDb/`): User authentication and authorization

### Technology Stack

- **Backend**: ASP.NET Core 9.0+ (C#)
- **Frontend**: JavaScript (70% of codebase), HTML5, CSS3, SCSS
- **Database**: Azure Cosmos DB (NoSQL), MS SQL, MySQL, SQLite
- **Storage**: Azure Blob Storage, Amazon S3, Cloudflare R2 (S3-compatible)
- **Hosting**: Linux Docker containers
- **Authentication**: ASP.NET Core Identity, Google and Microsoft

### Infrastructure Components

- **Database Options**
  - Azure Cosmos DB: Multi-user, globally distributed NoSQL database
  - MS SQL, MySQL: Relational databases
  - SQLite: File-based database for development and small deployments
- **Cloud Storage Options**
  - Azure Storage: File share and BLOB storage
  - Amazon S3 (and compatible): BLOB storage
  - Cloudflare R2 for BLOB storage
  - Any SMB or NFS persistent file share storage
- **Edge Hosting Options**
  - Cloudflare R2 + Rules: Origin-less static hosting at the edge (no Worker required)

## Project Structure

SkyCMS/
├── ArmTemplates/ # ARM templates coming soon
├── Common/                 # Shared libraries and utilities
├── Cosmos.BlobService/     # File storage service layer
├── Cosmos.ConnectionStrings/ # Dynamic configuration management
├── AspNetCore.Identity.FlexDb/ # Flexible identity framework
├── Editor/                 # Content management application
├── Publisher/              # Public website application
├── docker-compose.yml      # Local development orchestration
└── SkyCMS.sln             # Visual Studio solution file


## Component Documentation

Each component has detailed documentation explaining its purpose, configuration, and usage:

### Infrastructure & Deployment

- **ARM Templates** _(coming soon)_ - Azure deployment templates and infrastructure setup
  - Complete Azure Resource Manager templates
  - One-click deployment configuration
  - Email service integration (Azure Communication Services, SendGrid, SMTP)
  - Storage and database setup

### Applications

- **[Editor Application](./Editor/README.md)** - Content management interface
  - Article creation and editing with CKEditor 5, GrapesJS, and Monaco Editor
  - Media management with Filerobot image editor
  - User management and role-based access control
  - Real-time collaboration features

- **[Publisher Application](./Publisher/README.md)** - Public-facing website
  - High-performance content delivery
  - SEO optimization and sitemap generation
  - Multi-tenant support for hosting multiple websites
  - Static and dynamic content rendering

### Shared Libraries

- **[Common Library](./Common/README.md)** - Core shared functionality
  - Multi-database support (Cosmos DB, SQL Server, MySQL, SQLite)
  - Base controllers and data models
  - Authentication utilities and services
  - Article management and content processing

- **[Blob Service](./Cosmos.BlobService/README.md)** - Multi-cloud file storage
  - Azure Blob Storage and AWS S3 support
  - File management and media handling
  - CDN integration and performance optimization
  - Secure file access and permissions

- **[Dynamic Configuration](./Cosmos.ConnectionStrings/README.md)** - Runtime configuration management
  - Multi-tenant configuration support
  - Dynamic connection string management
  - Environment-specific settings
  - Configuration caching and performance

- **[Identity Framework](./AspNetCore.Identity.FlexDb/README.md)** - Flexible authentication
  - Multi-database identity provider support
  - ASP.NET Core Identity integration
  - Azure B2C and external provider support
  - Role-based security and permissions

## Docker Containers

SkyCMS applications are distributed as Docker containers for consistent deployment:

- **Editor**: [`toiyabe/sky-editor:latest`](https://hub.docker.com/r/toiyabe/sky-editor)
- **Publisher**: [`toiyabe/sky-publisher:latest`](https://hub.docker.com/r/toiyabe/sky-publisher)
- **API**: [`toiyabe/sky-api:latest`](https://hub.docker.com/r/toiyabe/sky-api)

Alternative NodeJS Publisher: [Sky.Publisher.NodeJs](https://github.com/CWALabs/Sky.Publisher.NodeJs)

## Quick Start

### Azure Deployment (Recommended)

1. Click the "Deploy to Azure" button above
2. Fill in required parameters (email configuration, storage options)
3. Deploy and access your SkyCMS instance


### System Requirements

- **.NET 9.0+** for local development
- **Docker** for containerized deployment
- **Azure/AWS/Google Cloud, etc...** for cloud deployment
- **Visual Studio 2022** or **VS Code** (recommended for development)

## Documentation

> **[Complete Documentation Index →](https://cwalabs.github.io/SkyCMS/README.html)**  
> **Browse 40+ guides organized by topic** - installation, configuration, content management, development, and more
> **[Master Table of Contents →](https://cwalabs.github.io/SkyCMS/)**  
> Exhaustive hierarchical index of all documentation files

### Quick Links

#### Getting Started

- **[Quick Start Guide](https://cwalabs.github.io/SkyCMS/QuickStart.html)** - Get up and running quickly
- **[Azure Installation](./Docs/Installation/AzureInstall.md)** - Deploy to Microsoft Azure
- **[AWS Deployment](https://cwalabs.github.io/SkyCMS/S3StaticWebsite.html)** - Deploy using S3 static hosting (CloudFormation/ECS guide coming soon)

#### Configuration

- **[Storage Configuration](https://cwalabs.github.io/SkyCMS/StorageConfig.html)** - Azure Blob, AWS S3, Cloudflare R2 setup
- **[Database Configuration](https://cwalabs.github.io/SkyCMS/DatabaseConfig.html)** - Cosmos DB, SQL Server, MySQL, SQLite setup
- **[Cloudflare Edge Hosting](https://cwalabs.github.io/SkyCMS/CloudflareEdgeHosting.html)** - Origin-less hosting with R2 + Rules

#### Content Management Guides

- **[Layouts Guide](https://cwalabs.github.io/SkyCMS/Layouts/Readme.html)** - Site-wide layouts and structure
- **[Page Templates](https://cwalabs.github.io/SkyCMS/Templates/Readme.html)** - Reusable page templates
- **[File Management](https://cwalabs.github.io/SkyCMS/FileManagement/README.html)** - Managing media and assets
- **[Page Scheduling](https://cwalabs.github.io/SkyCMS/Editors/PageScheduling.html)** - Schedule automatic publication

#### Editing Tools

- **[Live Editor (CKEditor)](https://cwalabs.github.io/SkyCMS/Editors/LiveEditor/README.html)** - WYSIWYG editing
- **[Designer (GrapesJS)](https://cwalabs.github.io/SkyCMS/Editors/Designer/README.html)** - Visual page builder
- **[Code Editor (Monaco)](https://cwalabs.github.io/SkyCMS/Editors/CodeEditor/README.html)** - HTML/CSS/JavaScript editing
- **[Image Editing](https://cwalabs.github.io/SkyCMS/Editors/ImageEditing/README.html)** - Filerobot image editor

#### Blogging

- **[Blog Post Lifecycle](https://cwalabs.github.io/SkyCMS/blog/BlogPostLifecycle.html)** - Creating and managing blog posts
- **[Future Blog Features](https://cwalabs.github.io/SkyCMS/blog/BlogFutureEnhancements.html)** - Upcoming functionality

#### For Developers

- **[Developer Documentation](https://cwalabs.github.io/SkyCMS/Developers/README.html)** - Technical documentation
- **[Widgets Overview](https://cwalabs.github.io/SkyCMS/Widgets/README.html)** - Reusable UI components
- **[Component READMEs](https://cwalabs.github.io/SkyCMS/README.html#architecture--components)** - Architecture documentation

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](./Docs/CONTRIBUTING.md) and:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

**Community Resources**:
- [Code of Conduct](./CODE_OF_CONDUCT.md) *(if available)*
- [Security Policy](./AzureMP/SECURITY.md) - Report vulnerabilities
- [Support Channels](./AzureMP/SUPPORT.md) - Get help

For documentation updates, follow the pattern in [Docs/CONTRIBUTING.md](./Docs/CONTRIBUTING.md): keep overviews focused on what/why, put detailed settings and defaults in configuration references, and start from the templates in [Docs/_templates](./Docs/_templates).

## Email Configuration

SkyCMS supports multiple email providers:

- **Azure Communication Services**
- **SendGrid**
- **Any SMTP**

See the [Azure installation guide](./Docs/Installation/AzureInstall.md) for configuration details.

---

## SkyCMS Licensing

SkyCMS is **dual-licensed** to provide maximum flexibility:

## GPL 2.0-or-later License (Default for Open Source Use)

When using SkyCMS with the included open-source **CKEditor 5**, the entire
application is licensed under **GNU General Public License Version 2.0 or later (GPL-2.0-or-later)**.

This matches CKEditor's licensing and allows you to choose GPL 2.0, GPL 3.0,
or any later version of the GPL.

**License Files:**

- [LICENSE-GPL](LICENSE-GPL) - Full GPL 2.0 license text
- [LICENSE-CKEDITOR-GPL](LICENSE-CKEDITOR-GPL) - CKEditor-specific licensing information

**This means:**

- Free to use for open-source projects
- Must distribute source code of modifications
- Derivative works must also be GPL-licensed

---

## MIT License (For Commercial Use)

### Option 1: Source Code MIT License

All **original SkyCMS source code** (excluding CKEditor and other third-party components)
is available under the **MIT License**.

See [LICENSE-MIT](LICENSE-MIT) for full MIT license terms.

### Option 2: Complete Application with Commercial CKEditor

If you purchase a **commercial license for CKEditor** from CKSource,
you may use the **entire SkyCMS application** under the **MIT License**.

**This means:**

- Use in proprietary/commercial applications
- No requirement to distribute source code
- Minimal restrictions

---

## How to Choose Your License

| Your Use Case | License to Use | Action Required |
|---------------|----------------|-----------------|
| **Open Source Project** | GPL 2.0-or-later | Use freely (no cost) |
| **Commercial Product** | MIT + Commercial CKEditor | [Purchase CKEditor license](https://ckeditor.com/pricing/) |
| **Using SkyCMS Source Code Only** (without CKEditor) | MIT | Use freely (no cost) |

---

## Third-Party Components

SkyCMS integrates several third-party libraries with their own licenses.

**Full attribution and licensing details**: [NOTICE.md](NOTICE.md)

### CKEditor 5 License

- **Copyright**: © 2003-2024 CKSource Holding sp. z o.o.
- **License**: GPL 2.0-or-later (open source) or Commercial
- **Website**: <https://ckeditor.com/>
- **Source**: <https://github.com/ckeditor/ckeditor5>
- **Commercial License**: <https://ckeditor.com/pricing/>

CKEditor 5 is a powerful rich text editor. SkyCMS uses it under the GPL 2.0-or-later license.

For questions about CKEditor licensing, contact CKSource directly.

### Other Components

- **GrapesJS**: BSD 3-Clause License (© 2024 Artur Arseniev)
- **Monaco Editor**: MIT License (© Microsoft Corporation)
- **Filerobot Image Editor**: MIT License (© Scaleflex)
- **FilePond**: MIT License (© 2024 PQINA | Rik Schennink)

See [NOTICE.md](NOTICE.md) for complete third-party license information and full legal text.

---

## License Summary

This project is dual-licensed under GPL 2.0-or-later (with open-source CKEditor) or MIT License (with commercial CKEditor license). See [LICENSE-GPL](LICENSE-GPL), [LICENSE-MIT](LICENSE-MIT), and [NOTICE.md](NOTICE.md) for full license terms and third-party attributions.

---

**Copyright (c) 2025 Moonrise Software, LLC. All rights reserved.**

---

## Important Links

- **[Documentation](https://docs-sky-cms.com)** - Complete guides and tutorials
- **[GitHub Repository](https://github.com/CWALabs/SkyCMS)** - Source code and issues
- **[Support Options](./AzureMP/SUPPORT.md)** - Get help from community or commercial support
- **[Security Policy](./AzureMP/SECURITY.md)** - Report vulnerabilities
- **[Privacy Policy](./AzureMP/PRIVACY.md)** - Data handling and privacy
- **[Terms of Service](./AzureMP/TERMS_OF_SERVICE.md)** - License and usage terms
- **[Azure Marketplace](https://aka.ms/skycms-azure)** - One-click Azure deployment *(coming soon)*
