# ADR 0028: Domain Validation Fail-Open Availability Posture

## Status

Accepted

## Context

SkyCMS multi-tenant requests pass through domain validation middleware that checks whether
the incoming domain maps to a known tenant connection.

When this validation check fails due to transient resolution errors (for example temporary
config-database connectivity failures), a strict fail-closed model would deny all traffic,
including potentially valid tenants, causing broad availability impact.

An explicit posture was needed for handling validation exceptions versus explicit invalid
domain results.

## Design Goals

This decision aims to:

1. Reject explicitly invalid tenant domains decisively
2. Preserve availability under transient validation exceptions
3. Keep abnormal validation behavior visible via logging
4. Establish predictable middleware behavior for domain checks
5. Minimize blast radius from temporary dependency failures

## Non-Goals

This decision does not attempt to:

- Eliminate all security risk from dependency outages
- Replace upstream traffic filtering and monitoring
- Define incident-response policy for repeated validation exceptions
- Remove need for robust tenant configuration data services

## Decision

SkyCMS domain middleware applies a mixed posture:

- If domain validation completes and domain is invalid: return 404
- If domain validation throws an exception: log error and continue request pipeline
  (fail-open for availability)

Domain value is still attached to request context for downstream components.

## Detailed Rationale

### Explicit Invalid vs. Indeterminate States

Known-invalid domains are blocked, but transient validation errors are treated as
indeterminate rather than definitively unauthorized.

### Availability Preservation

Fail-open on validation exceptions limits full-site outage risk when configuration
resolution dependencies are impaired.

### Observability Requirement

Error and warning logs provide operational signals to investigate degraded validation paths.

## Alternatives Considered

### Fail-Closed for All Validation Errors

Rejected due to high availability risk during transient dependency failures.

### Always Fail-Open Without Invalid-Domain Blocking

Rejected because explicit invalid domains should still be denied.

### Remove Domain Middleware Validation

Rejected because domain validation remains a core tenant boundary control.

## Consequences

### Positive Outcomes

- Better resilience during transient validation dependency failures
- Clear rejection of explicitly invalid domains
- Observable middleware behavior during degraded states

### Constraints Introduced

- Security/availability tradeoff must remain intentional and documented
- Repeated validation exceptions require active monitoring and remediation
- Downstream tenant isolation controls remain essential defense layers

## Evidence

- Domain middleware invalid-domain 404 handling and exception fail-open behavior:
  - Cosmos.ConnectionStrings/DomainMiddleware.cs
