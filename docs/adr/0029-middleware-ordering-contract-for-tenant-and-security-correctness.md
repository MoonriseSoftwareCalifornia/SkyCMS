# ADR 0029: Middleware Ordering Contract for Tenant and Security Correctness

## Status

Accepted

## Context

SkyCMS relies on a composed middleware pipeline where tenant resolution, setup checks,
forwarded-header handling, authentication, authorization, and rate limiting interact.

Changing middleware order can silently alter behavior, including tenant context resolution,
redirect flows, security policy enforcement, and endpoint protection semantics.

A formal ordering contract was needed so contributors understand this as architecture,
not incidental startup code arrangement.

## Design Goals

This decision aims to:

1. Preserve tenant-context establishment before tenant-dependent endpoint behavior
2. Preserve setup detection and access-control behavior without redirect loops
3. Preserve transport/proxy normalization before downstream security interpretation
4. Keep authn/authz/rate-limiter ordering explicit and stable
5. Improve maintainability and reviewability of startup changes

## Non-Goals

This decision does not attempt to:

- Freeze all middleware additions forever
- Replace endpoint-level authorization policies
- Define every middleware extension used by feature modules
- Eliminate environment-specific middleware branches

## Decision

SkyCMS treats middleware sequencing as an architectural contract. Core ordering invariants
include:

- Multi-tenant domain middleware executes early
- Setup detection and setup access control run with intentional placement
- Forwarded headers processing runs before routing/auth semantics that depend on it
- Static files are served before endpoint routing
- Authentication runs before authorization
- Rate limiting runs in the endpoint pipeline prior to request execution

Any reorder impacting these invariants requires explicit review and likely ADR update.

## Detailed Rationale

### Order-Dependent Correctness

Several behaviors are fundamentally order-sensitive. Preserving intended sequence avoids
subtle regressions that compile-time checks may not catch.

### Security and Tenant Safety

Tenant and proxy handling must be established before downstream identity and policy
interpretation.

### Operational Predictability

Documented ordering invariants reduce startup fragility during future refactors.

## Alternatives Considered

### Treat Middleware Order as Implementation Detail

Rejected because observed behavior and security/tenant correctness depend on sequencing.

### Fully Dynamic Middleware Composition

Rejected because high configurability would increase operational complexity and drift.

### Feature-Owned Independent Pipelines

Rejected because cross-cutting concerns require centrally coordinated ordering.

## Consequences

### Positive Outcomes

- Stronger confidence in startup refactors
- Clearer guardrails for tenant/security-sensitive middleware changes
- Better incident triage when behavior changes after pipeline edits

### Constraints Introduced

- Pipeline changes require careful architecture review
- Tests should protect critical ordering invariants where possible
- Startup code readability remains a critical maintenance requirement

## Evidence

- Middleware and endpoint pipeline composition:
  - Editor/Program.cs
