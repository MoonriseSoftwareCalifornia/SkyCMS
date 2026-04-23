# ADR 0007: Content Delivery Path Segregation

## Status

Accepted

## Context

SkyCMS serves different categories of content with different security and caching
requirements: authenticated files, public static pages, and built-in static assets.

When these concerns are mixed into a single route pattern, it becomes harder to apply
correct cache headers, authorization policies, and fallback behavior. The platform needed
a clear route-level architecture for content delivery responsibilities.

## Design Goals

This decision aims to:

1. Separate content delivery concerns by URL path and controller responsibility
2. Apply appropriate authorization and cache policies per path category
3. Support static and dynamic serving scenarios without route ambiguity
4. Keep delivery behavior explainable in architecture documentation
5. Preserve tenant cache isolation through scoped key generation

## Non-Goals

This decision does not attempt to:

- Define all publishing lifecycle mechanics
- Replace CDN responsibilities with application-only caching
- Eliminate static middleware for built-in assets
- Standardize every cache TTL value permanently

## Decision

SkyCMS standardizes content delivery into distinct path categories:

- /pub/* handled by PubController for protected file serving and article-level checks
- Catch-all static proxy path in static mode for pre-generated file delivery
- ASP.NET static file middleware for built-in assets (wwwroot)

Each category uses purpose-specific caching and authorization behavior.

## Detailed Rationale

### Route Clarity Enables Policy Clarity

Separating route responsibilities allows security and caching policy to align cleanly with
content type and access expectations.

### Better Multi-Tenant Cache Isolation

Per-tenant/per-path cache keys avoid collisions and leakage in multi-tenant environments.

### Support for Both Dynamic and Static Delivery Models

Path segregation complements Publisher mode split by keeping static-proxy behavior explicit
without interfering with protected file endpoints.

## Alternatives Considered

### Single Unified Content Endpoint

Rejected because mixed concerns increase conditional complexity and policy mistakes.

### CDN-Only Delivery Without App-Level Segregation

Rejected because application still needs explicit authentication and protected-file logic.

### Static-Only Route Model

Rejected because protected paths and authenticated scenarios still require dedicated handling.

## Consequences

### Positive Outcomes

- Clear route-to-responsibility mapping
- Stronger alignment of cache and auth behavior
- Better maintainability for content serving code
- Easier architecture communication to contributors

### Constraints Introduced

- Route changes must preserve this responsibility model
- Delivery logic remains distributed across controllers and middleware by design
- Contributors must validate behavior in both static and dynamic contexts

## Evidence

- Developer architecture reference for delivery paths and cache behavior:
  - SkyCMS.Docs/for-developers/content-delivery-architecture.md
- Publisher mode context:
  - SkyCMS.Docs/for-developers/publisher-architecture.md
