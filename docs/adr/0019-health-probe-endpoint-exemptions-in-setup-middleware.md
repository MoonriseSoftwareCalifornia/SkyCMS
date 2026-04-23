# ADR 0019: Health Probe Endpoint Exemptions in Setup Middleware

## Status

Accepted

## Context

SkyCMS uses setup-detection middleware that can redirect requests to setup flows when
configuration or tenant setup is incomplete. Infrastructure health probes must continue
to function during setup phases and partial initialization.

If health endpoints were subject to setup redirects, orchestrators and load balancers
could incorrectly mark healthy-but-initializing services as unavailable.

A deliberate exemption policy was needed for health/probe routes.

## Design Goals

This decision aims to:

1. Keep health probes accessible during setup checks
2. Preserve setup enforcement for normal user/application routes
3. Support container/orchestrator readiness patterns
4. Keep exemption rules explicit and test-covered
5. Prevent accidental redirects on operational probe paths

## Non-Goals

This decision does not attempt to:

- Define full observability strategy for all components
- Replace authentication/authorization for non-probe routes
- Exempt broad route classes beyond operational necessity
- Guarantee probe success when dependencies are truly unavailable

## Decision

SkyCMS setup middleware explicitly bypasses setup checks for health/probe paths,
including health endpoints and well-known readiness routes. These paths proceed through
pipeline flow without setup redirection.

The exemption behavior is covered by middleware tests.

## Detailed Rationale

### Operational Reliability During Initialization

Probe endpoints must remain stable across startup and setup phases to support modern
orchestration platforms.

### Minimal, Explicit Exceptions

By exempting only specific operational paths, SkyCMS preserves setup guardrails for
normal routes while keeping health semantics intact.

### Test-Backed Contract

Explicit tests reduce regression risk when middleware evolves.

## Alternatives Considered

### No Exemptions

Rejected because setup redirects would break health/readiness contracts.

### Global Probe Middleware Outside Setup Logic

Rejected because direct exemption in setup middleware is clearer and easier to verify.

### Broad Static/Anonymous Exemption Rules

Rejected because overly broad bypasses weaken setup enforcement.

## Consequences

### Positive Outcomes

- Stable health checks during setup and early lifecycle
- Better compatibility with orchestrators and load balancers
- Clear middleware contract for operational endpoints

### Constraints Introduced

- Exemption list must be maintained as probe conventions evolve
- Middleware changes require probe-regression test verification
- Teams should avoid expanding exemptions beyond operational scope

## Evidence

- Setup middleware skip-path implementation:
  - Editor/Middleware/SetupMiddlewareExtensions.cs
- Health probe exemption tests:
  - Tests/Editor/Middleware/SetupMiddlewareExtensionsTests.cs
