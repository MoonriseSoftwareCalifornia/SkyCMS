# ADR 0015: Startup Migration Orchestration for Schema and Data

## Status

Accepted

## Context

SkyCMS requires both schema migrations and data migrations (for example layout versioning)
while supporting single-tenant and multi-tenant deployments across multiple database
providers.

Manual migration execution or partial migration handling at startup creates risk of
inconsistent runtime state and difficult operational recovery.

A unified startup orchestration strategy was needed to run migration phases deliberately,
provider-aware, and mode-aware.

## Design Goals

This decision aims to:

1. Run schema and data migrations through a coordinated startup path
2. Support both single-tenant and multi-tenant migration execution
3. Use provider-aware configuration for each target database
4. Continue multi-tenant processing even if one tenant migration fails
5. Halt single-tenant startup when migration failure makes runtime unsafe

## Non-Goals

This decision does not attempt to:

- Guarantee zero-downtime migration semantics in all environments
- Replace external migration governance/runbooks
- Define every individual migration implementation detail
- Remove need for migration observability and follow-up validation

## Decision

SkyCMS uses StartupMigrationService as the startup migration orchestrator (when setup is
allowed), with the following behavior:

- Executes schema and data migration phases
- Branches by deployment mode (single-tenant vs multi-tenant)
- Uses provider detection and provider-specific EF configuration per connection
- Multi-tenant: processes tenants independently and continues after tenant-level failures
- Single-tenant: startup is halted on migration failure

## Detailed Rationale

### Coordinated Migration Phases

Combining schema and data migration execution in one orchestration flow reduces operational
gaps between structural and semantic upgrades.

### Mode-Aware Safety Model

Single-tenant and multi-tenant deployments have different failure-tolerance requirements;
startup behavior should reflect this explicitly.

### Provider and Tenant Specificity

Per-connection provider detection and per-tenant processing preserve compatibility and
reduce cross-tenant migration coupling.

## Alternatives Considered

### Schema-Only Startup Migration

Rejected because data migration requirements are first-class for some platform upgrades.

### Strict Fail-Fast for Entire Multi-Tenant Startup

Rejected because one tenant failure should not block all tenants when partial continuity is
acceptable.

### Fully Manual Migration-Only Process

Rejected because operational consistency and safety benefit from startup orchestration.

## Consequences

### Positive Outcomes

- More reliable startup migration behavior across modes/providers
- Reduced drift between schema and required data state
- Better resilience in multi-tenant migration scenarios

### Constraints Introduced

- Startup logic now includes migration orchestration complexity
- Migration failure handling semantics must remain intentional by mode
- Changes to migration flow require careful regression and operational review

## Evidence

- Startup migration orchestration and mode-specific handling:
  - Editor/Program.cs
- Migration service mode branching, tenant discovery, and per-tenant processing:
  - Editor/Services/Migrations/StartupMigrationService.cs
