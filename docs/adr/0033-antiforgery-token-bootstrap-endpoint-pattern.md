# ADR 0033: Antiforgery Token Bootstrap Endpoint Pattern

## Status

Accepted

## Context

SkyCMS includes API and hybrid UI interactions that require CSRF protections while supporting
client-side workflows that may not start from Razor-rendered form pages.

A bootstrap mechanism was needed so clients can reliably obtain and store antiforgery tokens
for subsequent protected requests.

## Design Goals

This decision aims to:

1. Provide a clear endpoint for antiforgery token initialization
2. Support client-side/API request flows that need CSRF tokens
3. Preserve ASP.NET Core antiforgery integration semantics
4. Keep token bootstrap behavior explicit and discoverable
5. Reduce ad hoc token-fetch patterns across clients

## Non-Goals

This decision does not attempt to:

- Replace all CSRF protection mechanisms
- Define full frontend security architecture for every client app
- Expose detailed token internals to clients
- Remove need for proper authentication and authorization controls

## Decision

SkyCMS exposes an antiforgery bootstrap endpoint that obtains/stores tokens via
IAntiforgery and returns a request token through response headers for client consumption.

Clients can call this endpoint to initialize CSRF protection state before submitting
protected actions.

## Detailed Rationale

### Practical CSRF Bootstrapping

Client-side integrations need a deterministic, reusable way to acquire request tokens.

### Framework-Aligned Implementation

Using IAntiforgery preserves standard ASP.NET Core behavior instead of custom token schemes.

### Reduced Fragmentation

A dedicated endpoint avoids multiple inconsistent token-fetch implementations.

## Alternatives Considered

### Razor-Only Token Provisioning

Rejected because not all client flows originate from Razor forms.

### Custom CSRF Token Mechanism

Rejected because framework-native antiforgery support is preferable and less risky.

### No Explicit Bootstrap Endpoint

Rejected because client integrations would require brittle or duplicated token handling.

## Consequences

### Positive Outcomes

- Clear CSRF token initialization path for clients
- Better support for hybrid/API-driven UX flows
- Consistent antiforgery integration pattern

### Constraints Introduced

- Clients must explicitly fetch/store token state before protected actions
- Endpoint behavior should be documented for integrators
- Changes to antiforgery middleware/config require integration retesting

## Evidence

- Antiforgery token bootstrap endpoint mapping:
  - Editor/Program.cs
