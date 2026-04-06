# Self-Host Quick Start

This quick start gets SkyCMS running for technical users who want to self-host and evaluate quickly.

## What You Get

SkyCMS runs as two cooperating applications:

- Sky.Editor: authoring, workflow, and administration.
- Sky.Publisher: website delivery in dynamic or static proxy mode.

## Prerequisites

- .NET SDK 9.0+
- Access to one supported database provider:
  - Azure Cosmos DB
  - SQL Server or Azure SQL
  - MySQL
  - SQLite (best for local development)
- Access to one supported object storage provider:
  - Azure Blob Storage
  - S3-compatible storage
  - Cloudflare R2

## Fastest Local Start (Single-Tenant)

1. Build the solution.
2. Run Sky.Editor.
3. Complete setup wizard.
4. Run Sky.Publisher.

### 1) Build

```powershell
dotnet build SkyCMS.sln
```

### 2) Run Sky.Editor

```powershell
dotnet run --project Editor/Sky.Editor.csproj
```

### 3) Complete Setup Wizard

Open the Editor URL shown in console and complete setup with:

- Storage connection details
- Administrator account
- Publisher URL
- Optional email/CDN settings

### 4) Run Sky.Publisher

```powershell
dotnet run --project Publisher/Sky.Publisher.csproj
```

## Verify Basic Flow

- Create a page in Sky.Editor.
- Publish the page.
- Open the published URL in Sky.Publisher.

## Production-Oriented Deployment Paths

Use the platform scripts in this repository for cloud deployment:

- Azure path: [InstallScripts/Azure/README.md](../InstallScripts/Azure/README.md)
- AWS path: [InstallScripts/AWS/README.md](../InstallScripts/AWS/README.md)

Quick deployment references:

- Azure quick start: [InstallScripts/Azure/QUICK_START.md](../InstallScripts/Azure/QUICK_START.md)
- AWS quick start: [InstallScripts/AWS/QUICK_START.md](../InstallScripts/AWS/QUICK_START.md)

## Common Evaluation Commands

Build:

```powershell
dotnet build SkyCMS.sln
```

Run tests:

```powershell
dotnet test SkyCMS.sln
```

Run Editor:

```powershell
dotnet run --project Editor/Sky.Editor.csproj
```

Run Publisher:

```powershell
dotnet run --project Publisher/Sky.Publisher.csproj
```

## Next Documents

- Feature and architecture comparison: [docs/skycms-vs-headless-and-ssg.md](./skycms-vs-headless-and-ssg.md)
- Editor deep dive: [Editor/README.md](../Editor/README.md)
- Publisher deep dive: [Publisher/README.md](../Publisher/README.md)
- Dynamic configuration and multi-tenancy: [Cosmos.ConnectionStrings/README.md](../Cosmos.ConnectionStrings/README.md)
