# ADR 0031: Setup Enablement Gate via CosmosAllowSetup

## Status

Accepted

## Context

SkyCMS includes setup and diagnostics capabilities that are essential during provisioning
but should be controlled in ongoing runtime operation.

Unconditionally enabling setup pathways in all environments can widen operational and
security exposure, while disabling setup entirely can block legitimate initialization.

A clear enablement gate was needed.

## Design Goals

This decision aims to:

1. Enable setup behavior only when explicitly configured
2. Support safe operation after setup completion
3. Keep setup middleware and access-control behavior environment-aware
4. Preserve setup capabilities for initial provisioning and troubleshooting
5. Make setup runtime posture explicit in configuration

## Non-Goals

This decision does not attempt to:

- Define full setup workflow details
- Replace authorization checks in setup pages
- Eliminate diagnostics for all non-setup contexts
- Automate setup enable/disable transitions beyond current checks

## Decision

SkyCMS gates setup detection and setup access-control behavior with CosmosAllowSetup.
When setup is not allowed, setup pathways are restricted and runtime avoids setup redirect
logic intended for provisioning mode.

Single-tenant access-control specifically checks setup-allow configuration before granting
setup wizard access.

## Detailed Rationale

### Principle of Explicit Enablement

Setup behavior should be intentional and configuration-driven rather than always-on.

### Operational Hardening

Restricting setup behavior post-provisioning reduces accidental exposure and confusion.

### Clear Runtime Modes

Configuration-gated setup behavior creates more predictable environment behavior.

## Alternatives Considered

### Setup Always Enabled

Rejected due to unnecessary operational and security exposure.

### Setup Fully Removed After First Completion

Rejected because troubleshooting and controlled re-entry scenarios can require setup tooling.

### Runtime Heuristics Without Config Flag

Rejected because explicit operator intent is preferable to inferred behavior.

## Consequences

### Positive Outcomes

- Clear control over setup-capable runtime behavior
- Improved post-installation hardening posture
- Better operator understanding of setup availability

### Constraints Introduced

- Misconfiguration can unintentionally block setup access
- Teams must manage setup flag intentionally per environment
- Changes to setup gating require careful testing of both enabled/disabled modes

## Evidence

- Setup middleware enablement based on allowSetup:
  - Editor/Program.cs
- Setup access-control behavior and configuration checks:
  - Editor/Middleware/SetupMiddlewareExtensions.cs
