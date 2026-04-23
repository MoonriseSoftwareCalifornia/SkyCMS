# ADR 0032: Environment-Specific Error Handling Strategy

## Status

Accepted

## Context

SkyCMS must provide detailed diagnostics in development while avoiding verbose error
exposure in production. Different environments have different priorities for debugging,
user-facing stability, and information disclosure risk.

A mode-specific error-handling strategy was needed in startup composition.

## Design Goals

This decision aims to:

1. Provide rich debugging feedback in development
2. Provide controlled user-facing error handling in production
3. Route production status-code failures through a consistent error surface
4. Keep error strategy explicit in startup configuration
5. Align error behavior with transport hardening in production

## Non-Goals

This decision does not attempt to:

- Define full observability and incident response tooling
- Eliminate all production error detail in logs
- Replace feature-level exception handling where needed
- Standardize error-page UX design in this ADR

## Decision

SkyCMS applies environment-specific error middleware behavior:

- Development: Developer exception page is enabled
- Non-development: exception handler routes to /Error and status-code pages redirect to /Error
- Non-development: HSTS is enabled in this branch

This creates distinct diagnostics posture by environment.

## Detailed Rationale

### Developer Productivity

Detailed exception pages accelerate iteration and root-cause analysis in development.

### Production Safety

Centralized production error surface reduces accidental information disclosure and provides
consistent user-facing failure behavior.

### Explicit Operational Posture

Environment-based startup branching keeps error strategy transparent and maintainable.

## Alternatives Considered

### Developer Exception Page in All Environments

Rejected due to production information exposure risk.

### Generic Errors in Development

Rejected because it harms debugging speed and developer feedback loops.

### Fragmented Per-Controller Error Behavior

Rejected because centralized middleware strategy is more consistent and robust.

## Consequences

### Positive Outcomes

- Better development debugging experience
- Safer and more consistent production error handling
- Clear environment-specific error posture

### Constraints Introduced

- /Error route behavior must remain maintained and reliable
- Environment misclassification can produce incorrect error behavior
- Teams should validate both branches during release testing

## Evidence

- Environment-conditional error middleware configuration:
  - Editor/Program.cs
