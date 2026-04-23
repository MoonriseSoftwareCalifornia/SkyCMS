# ADR 0009: CQRS with Custom Mediator and Vertical Slices

## Status

Accepted

## Context

SkyCMS contains a large set of feature behaviors that include both state-changing operations
and read-heavy query paths. As the application grew, direct service coupling and mixed
read/write orchestration would increase complexity, make testing harder, and blur boundaries
between command and query responsibilities.

A consistent application-layer dispatch model was needed so feature code remains organized,
composable, and tenant-safe across Editor and shared components.

## Design Goals

This decision aims to:

1. Separate write operations from read operations through clear contracts
2. Reduce coupling between callers and concrete handler implementations
3. Enable scalable feature organization via vertical slices
4. Keep request orchestration testable and explicit
5. Preserve multi-tenant safety through centralized dispatch integration

## Non-Goals

This decision does not attempt to:

- Enforce event sourcing
- Introduce external broker infrastructure for command/query dispatch
- Replace all service abstractions with handlers
- Guarantee strict transactional boundaries across all features by itself

## Decision

SkyCMS uses a custom mediator abstraction with CQRS-style contracts:

- IMediator exposes SendAsync for commands and QueryAsync for queries
- Handlers are auto-registered from Editor and Common assemblies
- A single mediator registration is used in composition, including multi-tenant security decoration

This establishes vertical-slice oriented feature wiring while retaining explicit command/query separation.

## Detailed Rationale

### Explicit Read/Write Separation

Distinct command and query contracts reduce ambiguity and make intent visible at call sites.

### Centralized Dispatch

Mediator-based dispatch allows shared cross-cutting behavior to be applied consistently,
including multi-tenant guardrails.

### Feature Scalability

Handler-based slices support incremental growth without forcing large shared service classes.

## Alternatives Considered

### Service-Layer-Only Orchestration

Rejected because large service aggregations become difficult to maintain and test over time.

### Third-Party Mediator Dependency as Primary Contract

Rejected in favor of a tailored internal contract aligned to SkyCMS conventions and constraints.

### Direct Controller-to-Repository Calls

Rejected because it weakens separation of concerns and reduces extensibility.

## Consequences

### Positive Outcomes

- Clear command/query intent in application flows
- Better testability through handler-level unit testing
- Reduced coupling between request initiators and implementations
- More maintainable vertical-slice feature structure

### Constraints Introduced

- Contributors must follow handler registration and CQRS conventions
- Some scenarios require additional discipline to avoid bypassing mediator boundaries
- Debugging call chains may involve mediator dispatch indirection

## Evidence

- Mediator contract and CQRS method split:
  - Common/Features/Shared/IMediator.cs
- Single mediator and handler auto-registration in startup:
  - Editor/Program.cs
