# ADR 0025: Setup Detection Middleware Cache and Access-Control Strategy

## Status

Accepted

## Context

SkyCMS setup state must be enforced consistently while minimizing repeated expensive checks.
Both single-tenant and multi-tenant deployments require setup gating, but with different
service checks and responsiveness expectations.

A middleware strategy was needed to combine setup enforcement, path exemptions, and cache
behavior in a predictable way.

## Design Goals

This decision aims to:

1. Redirect normal traffic to setup when setup is required
2. Exempt setup/static/health paths from setup gating
3. Use cache to reduce repeated setup-status computation
4. Support mode-specific setup checks for single-tenant and multi-tenant
5. Block setup-wizard access after setup completion where appropriate

## Non-Goals

This decision does not attempt to:

- Replace full setup workflow/business logic
- Define all cache invalidation events outside middleware responsibilities
- Remove the need for configuration-level setup controls
- Exempt non-operational routes from setup gating

## Decision

SkyCMS uses setup middleware extensions with two coordinated behaviors:

- Setup detection middleware computes requires-setup state and redirects to setup when needed
- Setup access-control middleware prevents unnecessary setup access once setup is complete

Setup status is cached per hostname with mode-aware TTLs (longer when complete, shorter when
incomplete). Path-based skip rules exempt setup pages, static assets, and health/probe routes.

## Detailed Rationale

### Performance with Correctness

Per-host cached setup status reduces repetitive checks while preserving tenant-aware behavior.

### Mode-Aware Responsiveness

Different incomplete-setup TTL choices reflect practical differences between single-tenant
and multi-tenant setup progression.

### Explicit Route Guardrails

Path exemptions and setup-access control prevent redirect loops and preserve operational paths.

## Alternatives Considered

### No Caching for Setup Checks

Rejected because repeated checks increase overhead without proportional benefit.

### Global Setup State Without Host Scoping

Rejected because host-scoped behavior is required for tenant-aware correctness.

### Setup Gating in Controllers Only

Rejected because middleware-level gating is more consistent and less error-prone.

## Consequences

### Positive Outcomes

- Consistent setup enforcement across requests
- Better startup/setup performance via targeted caching
- Clear route behavior for setup and operational endpoints

### Constraints Introduced

- Cache semantics and TTL values become behaviorally significant
- Middleware path rules must be maintained carefully
- Setup-state invalidation must stay aligned with setup-completion flows

## Evidence

- Setup detection/access-control middleware and cache TTL logic:
  - Editor/Middleware/SetupMiddlewareExtensions.cs
- Path-exemption and behavior tests:
  - Tests/Editor/Middleware/SetupMiddlewareExtensionsTests.cs
