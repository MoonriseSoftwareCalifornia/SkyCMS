# SkyCMS

SkyCMS is a .NET-based content management platform for teams that want integrated authoring and reliable website delivery without assembling a separate CMS, frontend, and publishing chain for every project.

## Start Here

- Self-host quick start: [docs/self-host-quick-start.md](docs/self-host-quick-start.md)
- Technical comparison (SkyCMS vs headless/static workflows): [docs/skycms-vs-headless-and-ssg.md](docs/skycms-vs-headless-and-ssg.md)
- Azure deployment path: [InstallScripts/Azure/README.md](InstallScripts/Azure/README.md)
- AWS deployment path: [InstallScripts/AWS/README.md](InstallScripts/AWS/README.md)

## What SkyCMS Is

SkyCMS is built around two cooperating applications:

- Sky.Editor: content authoring, workflow, administration, templates, layouts, and publishing controls.
- Sky.Publisher: public website delivery, supporting both dynamic rendering and static website proxy scenarios.

This architecture supports page-first website delivery while still allowing API and integration scenarios where needed.

## Why Teams Choose SkyCMS

SkyCMS focuses on practical technical strengths:

- One platform, two delivery modes.
  - Dynamic mode for interactive or runtime-rendered sites.
  - Static publishing/proxy mode for high-cache, CDN-friendly delivery.
- Multi-tenant architecture in core configuration model, with domain-based tenant resolution.
- Multi-database support across Cosmos DB, SQL Server/Azure SQL, MySQL, and SQLite (dev-focused).
- Multi-cloud storage support through Azure Blob, S3-compatible storage, and Cloudflare R2 patterns.
- Built-in role-based workflows for administrators, editors, authors, and reviewers.
- Integrated CDN purge workflows and operational setup patterns.

## Supported Database Providers (Native)

SkyCMS supports these database providers natively in core runtime and data access flows. This support is built in and does not require plugins or add-ons.

- Azure Cosmos DB
- SQL Server / Azure SQL
- MySQL
- SQLite (primarily for development and local scenarios)

## Supported BLOB Storage Providers (Native)

SkyCMS supports these BLOB/object storage providers natively through its built-in storage abstractions. This support is built in and does not require plugins or add-ons.

- Azure Blob Storage
- Amazon S3
- Cloudflare R2 (S3-compatible)

## Supported Clouds

- Microsoft Azure (first-class deployment scripts and guidance)
- Amazon Web Services (first-class deployment scripts and guidance)
- Other clouds via self-hosted/container-based deployment patterns

## Supported CDNs (Native Purge Integration)

SkyCMS supports native CDN cache purge/invalidation integrations in publishing workflows. This support is built in and does not require plugins or add-ons.

- Azure Front Door / Azure CDN
- Amazon CloudFront
- Cloudflare
- Sucuri

## Who SkyCMS Is For

### Developers

- Build and run with a .NET-first workflow.
- Work with layout/template/page hierarchy and typed backend services.
- Extend controllers, services, and publishing behavior in one solution.

### Web Designers and Site Builders

- Build reusable site structure with layouts and templates.
- Use visual page composition tools where appropriate.
- Keep design consistency while allowing editor-safe content regions.

### Web Content Creators

- Author and update content through structured editorial workflows.
- Use multiple editing modes:
  - Visual page builder
  - Rich text editor
  - Code editor
- Publish now or schedule publication windows.

### Website Administrators

- Configure platform behavior, roles, and settings.
- Use setup and deployment flows for environment onboarding.
- Manage integrations such as storage, email, and CDN behavior.

## AI-Assisted Authoring in the Code Editor

SkyCMS includes integrated AI chat functionality in the code editor, including support for OpenAI and GitHub Copilot-assisted workflows. This is intended to help technical users iterate faster on HTML, CSS, JavaScript, and page-level code edits during authoring.

## Architecture at a Glance

- Core shared libraries:
  - Cosmos.Common
  - Cosmos.DynamicConfig (in Cosmos.ConnectionStrings)
  - Cosmos.BlobService
- Main runtime applications:
  - Sky.Editor
  - Sky.Publisher
- API and shared contract projects:
  - Sky.Api
  - Sky.Cms.Api.Shared
- Test projects:
  - Sky.Tests
  - Cosmos.Common.Tests
  - AspNetCore.Identity.FlexDb.Tests

## Repository Quick Map

- Editor application: [Editor/README.md](Editor/README.md)
- Publisher application: [Publisher/README.md](Publisher/README.md)
- Dynamic configuration and tenant routing: [Cosmos.ConnectionStrings/README.md](Cosmos.ConnectionStrings/README.md)
- Common shared library: [Common/README.md](Common/README.md)
- Test suite: [Tests/README.md](Tests/README.md)

## Local Development

Build solution:

```powershell
dotnet build SkyCMS.sln
```

Run tests:

```powershell
dotnet test SkyCMS.sln
```

Run Sky.Editor:

```powershell
dotnet run --project Editor/Sky.Editor.csproj
```

Run Sky.Publisher:

```powershell
dotnet run --project Publisher/Sky.Publisher.csproj
```

## Deployment Options

- Azure infrastructure and scripts: [InstallScripts/Azure/README.md](InstallScripts/Azure/README.md)
- AWS infrastructure and scripts: [InstallScripts/AWS/README.md](InstallScripts/AWS/README.md)

For a quick deployment path in either direction, start with:

- [InstallScripts/Azure/QUICK_START.md](InstallScripts/Azure/QUICK_START.md)
- [InstallScripts/AWS/QUICK_START.md](InstallScripts/AWS/QUICK_START.md)

## License

SkyCMS includes MIT and GPL-licensed components. See:

- [LICENSE-MIT](LICENSE-MIT)
- [LICENSE-GPL](LICENSE-GPL)
- [LICENSES-DISTRIBUTION.md](LICENSES-DISTRIBUTION.md)
- [NOTICE.md](NOTICE.md)
