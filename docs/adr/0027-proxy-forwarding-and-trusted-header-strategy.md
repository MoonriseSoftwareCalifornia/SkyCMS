# ADR 0027: Proxy Forwarding and Trusted Header Strategy

## Status

Accepted

## Context

SkyCMS commonly runs behind reverse proxies/load balancers. Correct request context,
client IP handling, and hostname forwarding semantics are critical for security and
multi-tenant correctness.

Proxy-related behavior must balance compatibility with hardened trust boundaries.
A coordinated strategy was required for forwarded headers and trusted origin-host
handling.

## Design Goals

This decision aims to:

1. Support reverse-proxy deployments with forwarded protocol and client-IP context
2. Restrict trust of x-origin-hostname to explicitly trusted proxy scenarios
3. Keep proxy trust behavior configurable and explicit
4. Align tenant domain resolution with forwarding trust controls
5. Centralize forwarding/trust behavior in startup and provider infrastructure

## Non-Goals

This decision does not attempt to:

- Replace infrastructure-level proxy hardening
- Trust all forwarding headers by default
- Define every network topology supported by SkyCMS
- Remove need for operational validation of proxy IP trust lists

## Decision

SkyCMS applies a proxy-forwarding and trust model with these elements:

- Forwarded headers processing for X-Forwarded-For and X-Forwarded-Proto
- Trusted-proxy configuration via ProxySettings (including trust toggle and IP list)
- x-origin-hostname considered only when trust settings and proxy validation permit

Tenant-domain resolution logic uses these trust checks before accepting forwarded host data.

## Detailed Rationale

### Compatibility with Modern Hosting

Forwarded headers are required for correct behavior behind common reverse proxies.

### Trust-Bounded Header Usage

Forwarded host signals should be accepted only from explicitly trusted proxy origins.

### Configuration-Driven Control

Proxy trust behavior must be adjustable to match deployment topology without code changes.

## Alternatives Considered

### Trust All Forwarded Host Headers

Rejected due to spoofing and tenant-resolution risk.

### Ignore Forwarded Header Context Entirely

Rejected because it breaks correctness in proxied deployments.

### Hardcoded Proxy Trust Rules

Rejected because environments vary and require configuration-level adaptability.

## Consequences

### Positive Outcomes

- Better reverse-proxy compatibility
- Improved tenant-resolution security boundaries
- Explicit operational control over trusted header behavior

### Constraints Introduced

- Proxy trust configuration becomes a critical operational responsibility
- Changes in network topology require trust-list updates
- Misconfiguration can impact tenant-resolution correctness

## Evidence

- Forwarded headers startup configuration:
  - Editor/Program.cs
- Trusted-proxy and x-origin-hostname trust settings:
  - Cosmos.ConnectionStrings/Configurations/ProxySettings.cs
- Trust-bounded x-origin-hostname handling in tenant resolution:
  - Cosmos.ConnectionStrings/DynamicConfigurationProvider.cs
