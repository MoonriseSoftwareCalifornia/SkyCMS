# ADR 0022: Dynamic Configuration Provider Singleton with Proxy-Aware Domain Resolution

## Status

Accepted

## Context

SkyCMS multi-tenant operation depends on resolving tenant-specific database and storage
connections at request time. This resolution must work behind reverse proxies while
reducing repeated configuration database lookups.

A consistent provider lifecycle and trusted-header strategy was needed to keep tenant
resolution safe, reliable, and efficient.

## Design Goals

This decision aims to:

1. Resolve tenant configuration per request while using shared provider infrastructure
2. Support proxy-aware hostname handling with trust controls
3. Cache tenant connection metadata to reduce repeated config-db reads
4. Allow non-HTTP/background callers to resolve tenant config explicitly by domain
5. Keep tenant-resolution behavior centralized and auditable

## Non-Goals

This decision does not attempt to:

- Eliminate all runtime dependency on the configuration database
- Trust arbitrary forwarding headers from untrusted sources
- Replace tenant-domain validation middleware behavior
- Define all proxy/network hardening requirements in this ADR

## Decision

SkyCMS uses DynamicConfigurationProvider as a singleton IDynamicConfigurationProvider
that resolves tenant database/storage connections by normalized domain, with in-memory
caching and proxy-aware header trust rules.

x-origin-hostname is considered only under trusted-proxy conditions and configured
trust settings; otherwise host-based resolution is used.

Outside HTTP context, callers must provide domain explicitly for connection resolution.

## Detailed Rationale

### Singleton Provider, Per-Request Resolution

A singleton provider centralizes resolution logic and shared caches, while actual tenant
selection still occurs per request/domain.

### Trust-Bounded Header Use

Limiting forwarding-header trust to known proxies prevents simple header spoofing from
becoming a tenant-resolution vulnerability.

### Explicit Background-Context Behavior

Requiring explicit domain input when no HttpContext is available avoids ambiguous tenant
resolution in jobs and out-of-band workflows.

## Alternatives Considered

### Scoped Provider Instances Everywhere

Rejected because it duplicates stateful helper behavior and cache opportunities without
improving domain-resolution correctness.

### Always Trust x-origin-hostname

Rejected due to spoofing risk in untrusted network paths.

### Resolve Tenant by Host Header Only

Rejected because proxy topologies can require original-host forwarding support.

## Consequences

### Positive Outcomes

- Centralized and efficient tenant configuration resolution
- Better proxy compatibility with explicit trust boundaries
- Safer behavior for non-HTTP resolution contexts

### Constraints Introduced

- Trusted proxy configuration becomes security-relevant
- Header-trust behavior must be preserved during networking changes
- Tenant cache behavior requires operational observability and invalidation awareness

## Evidence

- Dynamic configuration provider lifecycle, caching, and trusted-proxy domain handling:
  - Cosmos.ConnectionStrings/DynamicConfigurationProvider.cs
- Provider contract and request-domain semantics:
  - Cosmos.ConnectionStrings/IDynamicConfigurationProvider.cs
