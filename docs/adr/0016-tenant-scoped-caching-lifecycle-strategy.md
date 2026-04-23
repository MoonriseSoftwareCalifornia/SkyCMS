# ADR 0016: Tenant-Scoped Caching Lifecycle Strategy

## Status

Accepted

## Context

SkyCMS serves multi-tenant workloads where cached data and cache keys must not bleed across
tenants. Improper cache service lifetime choices can create cross-tenant contamination risk
or stale-shared state behavior.

A lifecycle strategy was needed for cache services and key generation so tenant context is
respected while preserving performance.

## Design Goals

This decision aims to:

1. Preserve tenant isolation in caching behavior
2. Avoid singleton cache service pitfalls in multi-tenant scenarios
3. Keep cache key generation stable and reusable
4. Maintain clear lifetime semantics in startup composition
5. Support scalable in-memory caching patterns without violating tenant boundaries

## Non-Goals

This decision does not attempt to:

- Define every cache key format in this ADR
- Replace distributed caching or CDN caching strategies
- Eliminate cache invalidation complexity in all feature areas
- Enforce one cache backend for all deployment topologies

## Decision

SkyCMS configures caching with tenant-aware lifetimes:

- Generic ICacheService registrations are scoped to align with tenant/request context
- ICacheKeyProvider is registered as singleton because it is stateless and thread-safe
- Tenant isolation is preserved through tenant-aware key generation and scoped usage patterns

## Detailed Rationale

### Scoped Cache Service for Tenant Safety

Scoped lifetime reduces risk of cross-tenant state leakage that can occur when tenant-sensitive
cache behavior is implemented through singleton service instances.

### Singleton Key Provider for Efficiency

Key generation logic is stateless and thread-safe, so singleton lifetime is appropriate and
avoids unnecessary allocations.

### Explicit Lifetime Governance

Declaring these lifetimes at startup makes tenant isolation assumptions visible and easier to
review.

## Alternatives Considered

### Singleton Cache Service

Rejected due to elevated multi-tenant leakage risk and lifecycle mismatch.

### Scoped Key Provider

Rejected because stateless key generation does not require scoped lifecycle.

### No Shared Caching Abstractions

Rejected because ad hoc caching patterns increase inconsistency and maintenance overhead.

## Consequences

### Positive Outcomes

- Better tenant isolation in application-level caching
- Clear separation of cache usage and key-generation responsibilities
- Predictable DI lifetimes for cache components

### Constraints Introduced

- Contributors must preserve tenant-aware keying conventions
- Cache-related changes should validate multi-tenant behavior explicitly
- Feature code should avoid bypassing shared cache abstractions when tenant context matters

## Evidence

- Cache service and key provider lifetime registrations with rationale comments:
  - Editor/Program.cs
