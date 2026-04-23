# ADR 0003: Editor Deployment Mode Split (Single-Tenant vs Multi-Tenant)

## Status
Accepted

## Context

SkyCMS Editor is deployed in two materially different operating modes:
- single-tenant deployments with direct application database configuration,
- multi-tenant deployments with tenant configuration resolved dynamically.

These modes are not minor runtime toggles. They change startup validation requirements,
service registration, setup workflows, and runtime assumptions about where configuration
comes from.

A clear architectural decision was needed so contributors understand this as a deliberate
mode split, rather than a temporary implementation convenience.

## Design Goals

This decision aims to:

1. Support both deployment models from one codebase
2. Fail fast when required configuration for the selected mode is missing
3. Keep mode-specific wiring explicit at startup
4. Preserve a shared core service layer while allowing mode-specific configuration paths
5. Make setup and diagnostics behavior predictable per mode

## Non-Goals

This decision does not attempt to:

- Eliminate one deployment mode in favor of the other
- Define tenant onboarding UX details
- Standardize infrastructure provisioning for all environments
- Merge all mode-specific setup screens into one universal flow

## Decision

SkyCMS Editor startup is mode-driven by configuration key MultiTenantEditor:

- If true, Editor runs in multi-tenant mode and requires ConfigDbConnectionString.
- If false, Editor runs in single-tenant mode and requires ApplicationDbContextConnection.

Startup validates the required connection string early and throws if missing. Service
registration then branches intentionally:

- multi-tenant mode uses DynamicConfigurationProvider and multi-tenant setup services,
- single-tenant mode uses SingleTenantConfigurationProvider and single-tenant wiring.

## Detailed Rationale

### Explicit Mode Boundaries

Treating single-tenant and multi-tenant as first-class deployment modes keeps the startup
model understandable and avoids hidden conditional behavior spread across many modules.

### Early Configuration Validation

Failing fast during startup prevents partial initialization and hard-to-diagnose runtime
errors. Operators get immediate, actionable feedback when mode prerequisites are not met.

### Shared Core, Targeted Divergence

Most infrastructure remains shared, while mode-specific services are wired only where
necessary. This balances maintainability with deployment flexibility.

## Alternatives Considered

### Single Universal Runtime Mode with Optional Tenant Features

Rejected because it blurs boundaries and increases hidden branching across the codebase,
making operational behavior harder to reason about.

### Separate Editor Applications per Mode

Rejected because it would duplicate significant logic and increase maintenance burden.

### Lazy Mode Detection from Available Connection Strings

Rejected because implicit behavior is fragile and error-prone; explicit mode selection is
clearer and safer operationally.

## Consequences

### Positive Outcomes

- Predictable and auditable startup behavior
- Clear operational requirements per deployment model
- Reduced ambiguity for contributors and operators
- Ability to evolve each mode without cloning the codebase

### Constraints Introduced

- Configuration must explicitly match intended deployment mode
- Startup complexity remains somewhat higher than a single-mode system
- New features must be reviewed for mode-specific assumptions

## Evidence

- Mode selection, validation, and branching:
  - Editor/Program.cs
- Dynamic provider and domain-driven tenant configuration contract:
  - Cosmos.ConnectionStrings/IDynamicConfigurationProvider.cs
- Single-tenant provider usage path:
  - Cosmos.ConnectionStrings/SingleTenantConfigurationProvider.cs
