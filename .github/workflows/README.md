# Workflows Guide

This document describes every workflow in this folder: when it triggers, why it exists, and what it does.

---

## Test Workflows

These workflows run the .NET test suites. They share a common composite bootstrap action for provider readiness.

### Workflow Matrix

| Workflow | When it runs | Why it exists | Test scope | Provider bootstrap |
|---|---|---|---|---|
| `tests-pr-required-fast.yml` | PR to `main`, manual | Fast PR gate — fail-fast signal before heavier checks | `Common.Tests` | None (intentionally minimal) |
| `tests-pr-required-sky.yml` | PR to `main`, manual | Required PR validation for the main app-level suite | `Sky.Tests` | Cosmos + SQL Server + MySQL + Azurite |
| `tests-pr-required-flexdb.yml` | PR to `main`, manual | Required PR validation for the identity/provider suite | `AspNetCore.Identity.FlexDb.Tests` | Cosmos + SQL Server + MySQL |
| `tests-main-postmerge-full.yml` | Push to `main`, manual | Full safety sweep after every merge to main | Sky + FlexDb + Common + TestSetup | Cosmos + SQL Server + MySQL + Azurite |
| `tests-nightly-full-regression.yml` | Nightly at 04:00 UTC, manual | Full regression coverage on a timer, independent of merges | Sky + FlexDb + Common + TestSetup | Cosmos + SQL Server + MySQL + Azurite |
| `tests-manual-on-demand-full.yml` | Manual | On-demand full or targeted run — useful for investigating failures | Selectable: `all`, `sky`, `flexdb`, `common`, `setup` | Suite-dependent (see below) |
| `tests-identity-flexdb.yml` | Manual | Deep FlexDb test run against live Azure services, with filter and coverage collection | `AspNetCore.Identity.FlexDb.Tests` | None — uses live Azure secrets directly |

### Shared Provider Bootstrap

Workflows that need emulated infrastructure use the shared composite action at:

```
.github/actions/test-provider-bootstrap/action.yml
```

This action handles all readiness hardening before tests execute:

- **Cosmos DB** — certificate trust, emulator readiness polling, idempotent database and container provisioning
- **SQL Server** — readiness polling, idempotent database creation
- **MySQL** — readiness polling, idempotent database creation
- **Azurite** — Azure Blob Storage emulator startup (enabled per-workflow)

### Manual On-Demand Provider Mix

In `tests-manual-on-demand-full.yml`, bootstrap inputs depend on the selected suite:

| Suite input | Provider bootstrap |
|---|---|
| `all`, `sky`, `setup` | Cosmos + SQL Server + MySQL + Azurite |
| `flexdb` | Cosmos + SQL Server + MySQL (no Azurite) |
| `common` | None |

### Storage Environment: Emulator vs Live Azure

Sky.Tests steps in all automated workflows set `CONNECTIONSTRINGS__STORAGECONNECTIONSTRING` to the real Azure Storage secret and `CONNECTIONSTRINGS__AZUREBLOBSTORAGECONNECTIONSTRING` to the hardcoded Azurite endpoint. The test base uses `StorageConnectionString` first, so CI runs against live Azure Storage when the secret is configured.

`tests-identity-flexdb.yml` uses only live Azure secrets for all connections (no emulators) — it is intended for targeted manual investigation, not automated gating.

### Test Coverage

All test projects are covered by at least one automated workflow:

| Project | Covered by |
|---|---|
| `Tests/Sky.Tests.csproj` | `tests-pr-required-sky`, `tests-main-postmerge-full`, `tests-nightly-full-regression`, `tests-manual-on-demand-full` |
| `AspNetCore.Identity.FlexDb.Tests/AspNetCore.Identity.FlexDb.Tests.csproj` | `tests-pr-required-flexdb`, `tests-main-postmerge-full`, `tests-nightly-full-regression`, `tests-manual-on-demand-full`, `tests-identity-flexdb` |
| `Common.Tests/Cosmos.Common.Tests.csproj` | `tests-pr-required-fast`, `tests-main-postmerge-full`, `tests-nightly-full-regression`, `tests-manual-on-demand-full` |
| `Sky.TestSetup/Sky.TestSetup.csproj` | `tests-main-postmerge-full`, `tests-nightly-full-regression`, `tests-manual-on-demand-full` |

---

## Build & Deploy Workflows

### `docker-image.yml`

**Triggers:** Manual (`workflow_dispatch`)

Builds Docker images for `Sky.Editor` and `Sky.Publisher`, runs a smoke test against each image (verifying the container stays running for 15 seconds), then pushes both images to Docker Hub under the configured account.

### `NuGetPush.yml`

**Triggers:** Manual (`workflow_dispatch`)

Restores, builds the solution in Release, and pushes NuGet packages for `Cosmos.Common`, `Cosmos.BlobService`, and `Cosmos.ConnectionStrings` to nuget.org. Uses `--skip-duplicate` so it is safe to re-run after a partial push.

### `deploy-spa.yml`

**Triggers:** Manual (`workflow_dispatch`)

Builds a React SPA (Node 20, `npm ci` + `npm run build`) and deploys the build output as a ZIP to a target SkyCMS instance via `POST /api/spa/deploy`. Fails the workflow if the HTTP response is not 200. The target URL, article ID, and deploy key come from repository secrets.

---

## Quality & Security Workflows

### `azure-install-validation.yml`

**Triggers:** PRs touching `InstallScripts/Azure/**`, `Editor/Program.cs`, `Editor/Dockerfile`, or this workflow file itself; also manual.

Runs in two jobs:

1. **`static-validation`** — Validates Bicep templates (`validate-bicep.ps1`) and template-source contracts (`validate-template-contracts.ps1`) on every trigger. No Azure credentials required.
2. **`optional-whatif`** — Runs an ARM what-if deployment against a real resource group. Only runs on manual dispatch when both `AZURE_CREDENTIALS` and `AZURE_VALIDATION_RESOURCE_GROUP` are configured; silently skips otherwise.

### `codeql.yml`

**Triggers:** Weekly on Sunday at 07:40 UTC; also manual.

Runs GitHub CodeQL security analysis across two language matrices: `csharp` (build-mode: none) and `javascript-typescript` (build-mode: none). Results are published to the repository's Security tab.
