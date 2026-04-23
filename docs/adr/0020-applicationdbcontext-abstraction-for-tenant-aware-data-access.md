# ADR 0020: ApplicationDbContext Abstraction for Tenant-Aware Data Access

## Status

Accepted

## Context

SkyCMS data access spans many features and providers while requiring tenant-aware behavior
and testability. Direct coupling to one concrete DbContext everywhere makes substitution,
mocking, and cross-layer consistency harder.

A formal abstraction layer was needed to define the expected data contract used across
editor and publisher pipelines while preserving provider flexibility.

## Design Goals

This decision aims to:

1. Decouple feature code from concrete DbContext implementation details
2. Provide a shared contract for tenant-aware data operations
3. Improve testability through interface-based substitution
4. Preserve compatibility with multi-provider database strategies
5. Keep core entity-set surface discoverable and consistent

## Non-Goals

This decision does not attempt to:

- Hide all EF Core behavior behind repository wrappers
- Remove the concrete ApplicationDbContext implementation
- Define domain boundaries for every feature module
- Replace query-level tenant-safety discipline

## Decision

SkyCMS uses IApplicationDbContext as the primary abstraction for core data access
contract expectations. The interface exposes required DbSet collections and core context
operations, including save behavior and database provisioning/check contracts.

Feature services can depend on this abstraction to reduce concrete coupling and improve
test composition.

## Detailed Rationale

### Interface Contract for Core Data Surface

Defining the expected data surface in one interface improves consistency and onboarding
for contributors across feature areas.

### Better Testing Ergonomics

Interface-based dependencies simplify mocking and substitution in unit tests.

### Provider and Tenant Flexibility

The abstraction supports a consistent contract while provider-specific behavior remains
handled in context implementation and supporting infrastructure.

## Alternatives Considered

### Concrete DbContext Dependency Everywhere

Rejected because it increases coupling and reduces testability flexibility.

### Full Repository Layer for Every Entity

Rejected because it can add boilerplate without clear value for all query scenarios.

### Dynamic Data Access Contracts by Feature

Rejected because fragmented contracts reduce shared architectural consistency.

## Consequences

### Positive Outcomes

- Cleaner dependency boundaries around data access
- Improved testability for application services
- Shared contract for core entity and persistence operations

### Constraints Introduced

- Interface and implementation must evolve in sync
- Contributors must still apply tenant-safe query and provider-safe patterns
- Some EF-specific capabilities may still require concrete-context-aware handling

## Evidence

- Data access abstraction contract:
  - Common/Data/IApplicationDbContext.cs
- Core implementation and tenant-aware context behavior:
  - Common/Data/ApplicationDbContext.cs
