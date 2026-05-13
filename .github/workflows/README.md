# Test Workflows Guide

This document maps each test workflow to **when/why** it should run and which providers it bootstraps.

## Workflow Matrix

| Workflow | When it runs | Why it exists | Test scope | Provider bootstrap |
|---|---|---|---|---|
| `tests-pr-required-fast.yml` | PR to `main`, manual | Fast PR gate for quick signal | `Common.Tests` | None (intentionally fast/minimal) |
| `tests-pr-required-sky.yml` | PR to `main`, manual | Required PR validation for app-level suite | `Sky.Tests` | Cosmos + SQL Server + MySQL + Azurite |
| `tests-pr-required-flexdb.yml` | PR to `main`, manual | Required PR validation for identity/provider suite | `AspNetCore.Identity.FlexDb.Tests` | Cosmos + SQL Server + MySQL |
| `tests-main-postmerge-full.yml` | Push to `main`, manual | Full post-merge safety sweep | Sky + FlexDb + Common + TestSetup | Cosmos + SQL Server + MySQL + Azurite |
| `tests-nightly-full-regression.yml` | Nightly schedule, manual | Full regression coverage on a timer | Sky + FlexDb + Common + TestSetup | Cosmos + SQL Server + MySQL + Azurite |
| `tests-manual-on-demand-full.yml` | Manual | On-demand full or targeted execution | Selectable: `all`, `sky`, `flexdb`, `common`, `setup` | Suite-dependent mix (see below) |
| `sky-tests.yml` | Manual only | Deprecated monolithic workflow notice | None | None |

## Shared Provider Bootstrap

Workflows that require providers use the shared composite action:

- `.github/actions/test-provider-bootstrap/action.yml`

This preserves readiness hardening before tests execute:

- Cosmos emulator readiness checks
- Cosmos certificate trust
- Cosmos database/container idempotent provisioning
- SQL Server readiness + idempotent database creation
- MySQL readiness + idempotent database creation
- Azurite startup (when enabled)

## Manual Workflow Provider Mix

In `tests-manual-on-demand-full.yml`, provider setup is selected by suite:

- `all`, `sky`, `setup` -> Cosmos + SQL Server + MySQL + Azurite
- `flexdb` -> Cosmos + SQL Server + MySQL (no Azurite)
- `common` -> no provider bootstrap

## Coverage Check

All test projects are covered by at least one purpose-based workflow:

- `Tests/Sky.Tests.csproj`
- `AspNetCore.Identity.FlexDb.Tests/AspNetCore.Identity.FlexDb.Tests.csproj`
- `Common.Tests/Cosmos.Common.Tests.csproj`
- `Sky.TestSetup/Sky.TestSetup.csproj`
