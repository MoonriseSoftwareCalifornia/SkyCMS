# ADR 0023: Dynamic Email Provider Resolution with NoOp Fallback

## Status

Accepted

## Context

SkyCMS must send emails across different deployment types and tenant configurations,
including SMTP, SendGrid, Azure Communication Services, and scenarios where no email
provider is configured yet.

Hard-binding one provider at startup would reduce flexibility and complicate multi-tenant
runtime behavior where provider settings may be tenant-specific.

A dynamic resolution strategy was needed to select email sending behavior at runtime while
remaining safe under incomplete configuration.

## Design Goals

This decision aims to:

1. Resolve email provider behavior dynamically per request/tenant context
2. Support both single-tenant and multi-tenant configuration sources
3. Keep startup registration simple and provider-agnostic
4. Provide safe fallback behavior when no provider is configured
5. Preserve extensibility for additional providers

## Non-Goals

This decision does not attempt to:

- Guarantee deliverability for all provider/environment combinations
- Replace operational monitoring for email outcomes
- Enforce one provider standard across all tenants
- Eliminate provider-specific option validation needs

## Decision

SkyCMS registers email sending through AddCosmosEmailServices, which provides a dynamic
email sender resolution path with the following model:

- Resolve provider from tenant or environment-backed configuration as available
- Support configured providers (for example SMTP, SendGrid, Azure Communication)
- Fallback to a NoOp sender when no valid provider configuration is present

The runtime registration is scoped for tenant-aware behavior.

## Detailed Rationale

### Runtime Flexibility

Dynamic resolution supports heterogeneous deployments and tenant-specific configuration
without requiring separate startup builds.

### Safe Degradation

NoOp fallback avoids startup failure in environments that are not yet fully email-configured
while still making missing-provider state explicit in logs/behavior.

### Cleaner Composition

Provider-agnostic startup registration keeps application composition simpler.

## Alternatives Considered

### One Statically Selected Provider

Rejected because it does not match multi-tenant and multi-environment needs.

### Fail Startup If Provider Missing

Rejected because email capability may be optional during setup and early operations.

### Per-Provider Manual Registration in App Startup

Rejected because it increases branching complexity and operational coupling.

## Consequences

### Positive Outcomes

- Flexible provider selection across deployment scenarios
- Lower startup friction during setup/partial-configuration states
- Clear extension point for additional email providers

### Constraints Introduced

- Runtime behavior depends on configuration quality and source precedence
- Fallback behavior must remain visible and documented for operators
- Provider-specific issues still require targeted diagnostics

## Evidence

- Dynamic email service registration and resolution-order documentation:
  - Cosmos.EmailServices/ServiceCollectionExtensions.cs
- Application startup use of dynamic registration:
  - Editor/Program.cs
