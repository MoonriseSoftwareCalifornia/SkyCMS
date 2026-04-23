# ADR 0008: Cookie Domain Isolation for Multi-Tenant Authentication

## Status

Accepted

## Context

In multi-tenant deployments, users can interact with different tenant hostnames served by
the same application instance. Authentication cookies must not be treated as universally
valid across domains, or a session established on one tenant hostname could be replayed on
another.

SkyCMS therefore needed an explicit authentication isolation decision tied to tenant domain
identity.

## Design Goals

This decision aims to:

1. Prevent cross-tenant reuse of authenticated session cookies
2. Bind authenticated principals to the originating tenant domain
3. Enforce domain checks during principal validation
4. Keep implementation integrated with ASP.NET Core Identity cookie events
5. Preserve compatibility with proxy-aware hostname resolution

## Non-Goals

This decision does not attempt to:

- Replace full authorization policies within tenant features
- Define external identity provider tenant mapping rules
- Introduce custom token formats outside Identity cookie pipeline
- Eliminate need for broader tenant isolation controls

## Decision

In multi-tenant mode, SkyCMS records the resolved tenant domain as a CookieDomain claim
when signing in. During principal validation, if the current resolved domain does not match
the CookieDomain claim, the principal is rejected and the user is signed out.

## Detailed Rationale

### Session Boundaries Must Match Tenant Boundaries

Cookie-based sessions are convenient, but in multi-tenant systems they require domain
binding to maintain tenant trust boundaries.

### Event-Based Enforcement Fits Existing Identity Pipeline

Using OnSigningIn and OnValidatePrincipal events provides a focused implementation without
replacing standard ASP.NET Core Identity mechanisms.

### Defense Against Cross-Domain Session Confusion

Domain mismatch rejection prevents accidental or malicious use of a valid cookie in the
wrong tenant context.

## Alternatives Considered

### Shared Auth Cookie Across All Tenant Domains

Rejected due to unacceptable tenant boundary risk.

### Per-Tenant Cookie Names Without Claim Validation

Rejected because naming alone does not enforce principal-domain congruence once issued.

### Custom Authentication Middleware Outside Identity Events

Rejected because it duplicates Identity behavior and increases complexity.

## Consequences

### Positive Outcomes

- Stronger tenant isolation for authenticated sessions
- Clear and explicit domain-bound authentication model
- Reuse of standard Identity extensibility points

### Constraints Introduced

- Domain resolution correctness is security-relevant
- Sign-in and principal validation flows must preserve CookieDomain behavior
- Multi-tenant auth changes require careful regression testing

## Evidence

- Cookie event wiring and CookieDomain claim logic:
  - Editor/Program.cs
- Tenant domain resolution helper used by cookie logic:
  - Editor/Program.cs
