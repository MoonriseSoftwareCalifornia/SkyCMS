
# SkyCMS Documentation

## 1. Getting Started

- [Quick Start](./QuickStart.md)
- [Developer Experience Overview](./Project-Documentation/DeveloperExperience.md)
- [Cost Comparison](./CostComparison.md)
- [SkyCMS vs Headless CMS](./CosmosVsHeadless.md)
- [Migrating from JAMstack](./MigratingFromJAMstack.md)
- [Competitors Analysis](./SkyCMS-Competitors.md)

## 2. Installation & Deployment

### 2.1 Cloud Platforms

- [Azure Installation](./AzureInstall.md)
- [Azure ARM Templates](../ArmTemplates/README.md)
- [AWS S3 Static Website Hosting](./S3StaticWebsite.md)
- [Cloudflare Edge Hosting](./CloudflareEdgeHosting.md)

## 3. Configuration

- [Database Configuration Overview](./Database-Overview.md)
  - [Azure Cosmos DB](./Database-CosmosDB.md)
  - [MS SQL Server / Azure SQL](./Database-SQLServer.md)
  - [MySQL](./Database-MySQL.md)
  - [SQLite](./Database-SQLite.md)
- [Storage Configuration Overview](./Storage-Overview.md)
  - [Azure Blob Storage](./Storage-AzureBlob.md)
  - [Amazon S3](./Storage-S3.md)
  - [Cloudflare R2](./Storage-Cloudflare.md)
  - [Google Cloud Storage](./Storage-GoogleCloud.md)
- [CDN Integration Overview](./CDN-Overview.md)
  - [Azure Front Door CDN](./CDN-AzureFrontDoor.md)
  - [Cloudflare CDN](./CDN-Cloudflare.md)
  - [Amazon CloudFront CDN](./CDN-CloudFront.md)
  - [Sucuri CDN/WAF](./CDN-Sucuri.md)

## 4. Content Management

### 4.1 Pages & Layouts

- [Layouts Guide](./Layouts/Readme.md)
- [Templates Guide](./Templates/Readme.md)
- [Page Scheduling](./Editors/PageScheduling.md)
- [Migration: Save Article Pipeline](./MIGRATION-SAVE-ARTICLE.md)

### 4.2 File & Media Management

- [File Management Overview](./FileManagement/README.md)
  - [Quick Start](./FileManagement/Quick-Start.md)
  - [Code Editing Files](./FileManagement/Code-Editing.md)
  - [Image Editing](./FileManagement/Image-Editing.md)
  - [Index](./FileManagement/index.md)
  - [Summary](./FileManagement/SUMMARY.md)

## 5. Editing Tools

### 5.1 Live Editor (CKEditor 5)

- [Overview](./Editors/LiveEditor/README.md)
  - [Quick Start](./Editors/LiveEditor/QuickStart.md)
  - [Visual Guide](./Editors/LiveEditor/VisualGuide.md)
  - [Technical Reference](./Editors/LiveEditor/TechnicalReference.md)
  - [Index](./Editors/LiveEditor/index.md)

### 5.2 Designer (GrapesJS)

- [Overview](./Editors/Designer/README.md)
  - [Quick Start](./Editors/Designer/QuickStart.md)

### 5.3 Code Editor (Monaco)

- [Overview](./Editors/CodeEditor/README.md)

### 5.4 Image Editing (Filerobot)

- [Overview](./Editors/ImageEditing/README.md)

## 6. Blogging

- [Blog Post Lifecycle](./blog/BlogPostLifecycle.md)
- [Future Blog Enhancements](./blog/BlogFutureEnhancements.md)

## 7. Developers

### 7.1 Developer Hub

- [Developer README](./Developers/README.md)

### 7.2 Controllers & Base Classes

- [Controllers Overview](./Developers/Controllers/README.md)
  - [HomeControllerBase](./Developers/Controllers/HomeControllerBase.md)
  - [PubControllerBase](./Developers/Controllers/PubControllerBase.md)

### 7.3 Widgets

- [Widgets Overview](./Widgets/README.md)
  - [Image Widget](./Widgets/Image-Widget.md)
  - [Crypto Widget](./Widgets/Crypto-Widget.md)
  - [Crumbs Widget](./Widgets/Crumbs-Widget.md)
  - [Forms Widget](./Widgets/Forms-Widget.md)
  - [Nav Builder Widget](./Widgets/Nav-Builder-Widget.md)
  - [Search Widget](./Widgets/Search-Widget.md)
  - [ToC Widget](./Widgets/ToC-Widget.md)

### 7.4 Deep Dives

- [Image Widget Development](./Developers/ImageWidget.md)

## 8. Architecture & Components

### 8.1 Core Applications

- [Editor Application](../Editor/README.md)
- [Publisher Application](../Publisher/README.md)

### 8.2 Component Libraries

- [Common Library](./Components/Cosmos.Common.md) — Core CMS library ([full docs](../Common/README.md))
- [Blob Service](./Components/Cosmos.BlobService.md) — Multi-cloud storage ([full docs](../Cosmos.BlobService/README.md))
- [Identity Framework](./Components/AspNetCore.Identity.FlexDb.md) — Flexible database identity ([full docs](../AspNetCore.Identity.FlexDb/README.md))
- [Dynamic Configuration](../Cosmos.ConnectionStrings/README.md)

## 9. Development & Testing

### 9.1 Testing Guide

- [Testing Overview](./Development/Testing/README.md)

## 10. Troubleshooting

- [Troubleshooting Guide](./Troubleshooting.md)

## 11. Release & Changelog

- [Changelog](./CHANGELOG.md)

## 12. Marketing & Content Assets

- [Homepage Content (HTML)](./SkyCMS-Homepage-Content.md)
- [Homepage Content (Markdown)](./SkyCMS-Homepage-Content.md)
- [Azure Marketplace Description](./AzureMarketplaceDescription.md)

## 13. Licensing & Legal

- [License Summary](./License.md)
- [Third-Party Notices](../NOTICE.md)
- [GPL License](../LICENSE-GPL)
- [MIT License](../LICENSE-MIT)

## 13. Additional Reference

- [Master Docs Index (Human-Friendly)](./README.md)
- [Uploading Secrets to GitHub Repository](../Developer-Guides/UploadSecretsToGithubRepo.md)

---

## 14. How to Use This TOC

- Use this file for exhaustive discovery.
- Prefer the curated `README.md` for onboarding.
- Contribute new docs by adding them under the appropriate section here and in the curated index if user-facing.

---

Last updated: December 2025

