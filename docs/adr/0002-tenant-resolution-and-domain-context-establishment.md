# ADR 0002: Tenant Resolution and Domain Context Establishment

## Status
Accepted

## Context

SkyCMS supports both single-tenant and multi-tenant deployments, but in multi-tenant mode,
the platform must reliably determine which tenant is being requested before domain-specific
configuration, database access, and security checks can proceed.

Without a consistent tenant resolution strategy, the platform risks:
- loading the wrong tenant configuration,
- serving content from the wrong tenant database,
- and weakening authentication boundaries across hostnames.

Tenant resolution is therefore a foundational architectural concern, not merely a routing
detail. The decision affects middleware behavior, dynamic configuration lookup, and how
request-scoped services derive tenant context.

## Design Goals

This decision aims to:

1. Resolve tenant identity consistently for every request
2. Support reverse proxy scenarios where external host differs from internal host
3. Establish tenant context early in the middleware pipeline
4. Keep tenant resolution logic centralized and reusable
5. Protect multi-tenant boundaries through early domain validation

## Non-Goals

This decision does not attempt to:

- Define tenant provisioning workflows
- Define DNS onboarding or certificate management processes
- Specify tenant-specific authorization policies beyond domain validation
- Replace per-feature tenant checks where still required

## Decision

SkyCMS standardizes tenant resolution around domain identity with this ordering:

1. Use request header x-origin-hostname when present
2. Otherwise use the request Host value
3. Normalize to lowercase for consistent matching

In multi-tenant mode, tenant validity is checked through IDynamicConfigurationProvider,
and domain context is established early via DomainMiddleware. Requests for unknown domains
are rejected with 404.

## Detailed Rationale

### Proxy-Aware Domain Resolution

In real deployments, SkyCMS may run behind reverse proxies or gateways that rewrite Host.
Prioritizing x-origin-hostname preserves the original external domain used by the client,
which is the tenant identity boundary that matters for isolation.

### Early Validation as a Guardrail

By validating tenant domain availability in middleware, SkyCMS prevents deeper pipeline
components from operating with invalid or ambiguous tenant context.

### Centralized Resolution Contract

Using IDynamicConfigurationProvider for tenant-domain lookup keeps implementation details
in one place and avoids ad hoc domain parsing across services.

## Alternatives Considered

### Host Header Only

Rejected because proxy and forwarding setups can obscure the externally requested domain.
This would reduce reliability in common cloud/network topologies.

### Query String or Route-Based Tenant Keys

Rejected because SkyCMS already uses domain-based tenant identity. Introducing URL-level
tenant keys would duplicate concepts and increase accidental misconfiguration risk.

### Late Tenant Validation in Controllers

Rejected because it allows invalid requests to progress too far into the application
pipeline, increasing complexity and failure surface.

## Consequences

### Positive Outcomes

- Consistent tenant resolution behavior across the application
- Better support for reverse-proxy and edge-network deployments
- Clear and testable contract for tenant identity determination
- Early rejection of invalid tenant requests

### Constraints Introduced

- Upstream proxies must correctly provide x-origin-hostname when used
- Domain resolution rules must remain stable unless superseded by a new ADR
- Features should rely on provider/middleware tenant context instead of ad hoc logic

## Evidence

- Editor startup helper and resolution precedence:
  - Editor/Program.cs
- Dynamic configuration provider contract:
  - Cosmos.ConnectionStrings/IDynamicConfigurationProvider.cs
- Early domain validation middleware:
  - Cosmos.ConnectionStrings/DomainMiddleware.cs
