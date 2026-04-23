# ADR 0005: Database Provider Auto-Detection Strategy

## Status

Accepted

## Context

SkyCMS supports multiple database providers from the same application code:
Azure Cosmos DB, SQL Server, MySQL, and SQLite.

If provider selection is configured manually in many places, deployment setup becomes error-prone,
inconsistent, and harder to operate across environments. Contributors could also accidentally
bind the wrong provider or duplicate provider decision logic in application startup paths.

A single strategy for provider detection was required so EF configuration remains consistent
across Editor, Publisher, and shared libraries.

## Design Goals

This decision aims to:

1. Support multi-provider deployments from one codebase
2. Centralize provider detection logic in one reusable component
3. Minimize provider-specific branching at application composition points
4. Keep EF setup deterministic and testable
5. Fail clearly when connection strings do not match supported providers

## Non-Goals

This decision does not attempt to:

- Force all environments to use one database provider
- Hide provider-specific behavior where unavoidable
- Replace provider-specific migration/runtime concerns
- Define tenant resolution or connection lookup policy

## Decision

SkyCMS standardizes provider detection through CosmosDbOptionsBuilder.

CosmosDbOptionsBuilder uses an ordered strategy list to identify the provider from a
connection string and configure DbContextOptionsBuilder accordingly.

If no strategy matches, configuration fails with an explicit error listing supported providers.

## Detailed Rationale

### Single Entry Point for Provider Detection

Keeping provider detection in one component prevents drift and removes duplicated
if/else logic from startup and composition layers.

### Strategy Pattern for Extensibility

Using discrete provider strategies keeps the decision model explicit and allows future
provider additions without rewriting all startup paths.

### Operational Safety

Explicit failure when no strategy matches prevents silent fallback to the wrong provider,
which is especially important in multi-tenant and multi-environment deployments.

## Alternatives Considered

### Provider Flag Required in Every Environment

Rejected because it increases configuration burden and risk of mismatch with actual
connection string values.

### Separate Startup Code Paths Per Provider

Rejected because it duplicates composition logic and complicates maintenance/testing.

### Implicit Fallback to Default Provider

Rejected because silent fallbacks can mask configuration errors and cause runtime failures.

## Consequences

### Positive Outcomes

- Consistent EF provider setup across the platform
- Reduced startup composition complexity
- Clear error behavior for unsupported/invalid connection strings
- Easier contributor onboarding for provider wiring

### Constraints Introduced

- Connection strings must match strategy detection expectations
- New providers require adding and validating a new strategy
- Shared code must continue honoring cross-provider constraints

## Evidence

- Provider strategy implementation and ordered selection:
  - AspNetCore.Identity.FlexDb/CosmosDbOptionsBuilder.cs
- Cross-provider developer guidance:
  - SkyCMS.Docs/for-developers/ef-cross-provider-guide.md
