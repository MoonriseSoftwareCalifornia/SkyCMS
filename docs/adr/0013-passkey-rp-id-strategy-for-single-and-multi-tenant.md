# ADR 0013: Passkey RP ID Strategy for Single and Multi-Tenant Deployments

## Status

Accepted

## Context

SkyCMS supports passkey-based authentication through ASP.NET Core Identity passkey options.
Passkey relying party (RP) identity behavior differs between single-tenant and multi-tenant
deployments.

In single-tenant deployments, explicit RP ID configuration can be stable and beneficial.
In multi-tenant deployments, a fixed RP ID can break host correctness because authentication
must align with the actual tenant host per request.

A mode-aware passkey strategy was needed to avoid authentication mismatch and preserve
multi-tenant correctness.

## Design Goals

This decision aims to:

1. Keep passkey behavior correct across both deployment modes
2. Allow explicit RP ID in single-tenant environments when needed
3. Avoid fixed RP ID misconfiguration in multi-tenant environments
4. Keep passkey behavior predictable via startup composition
5. Minimize operational confusion in host-bound authentication

## Non-Goals

This decision does not attempt to:

- Define all passkey UX and registration flows
- Replace Identity passkey internals
- Introduce custom WebAuthn provider integrations
- Eliminate other authentication methods

## Decision

SkyCMS configures IdentityPasskeyOptions with mode-aware RP ID behavior:

- Single-tenant: allow explicit ServerDomain from IdentityPasskey:ServerDomain
- Multi-tenant: do not set ServerDomain explicitly so host derivation occurs per request

This behavior is controlled at startup based on deployment mode.

## Detailed Rationale

### Host Correctness Is Mandatory for Passkeys

Passkey verification depends on relying-party domain semantics. Multi-tenant deployments
require request-host alignment rather than one global server domain.

### Single-Tenant Operational Flexibility

Allowing explicit ServerDomain in single-tenant mode supports environments where operators
need deterministic RP ID configuration.

### Reduced Misconfiguration Risk

Skipping explicit RP ID in multi-tenant mode prevents accidental global-domain assumptions.

## Alternatives Considered

### Always Set One Global ServerDomain

Rejected because it is incompatible with multi-tenant host requirements.

### Never Allow Explicit ServerDomain

Rejected because single-tenant deployments can benefit from explicit RP ID configuration.

### Per-Tenant Static RP ID Mapping in Startup

Rejected because tenant host derivation per request is the safer default model.

## Consequences

### Positive Outcomes

- Correct passkey host behavior across deployment modes
- Lower risk of multi-tenant passkey domain mismatches
- Clear startup-level expression of RP ID policy

### Constraints Introduced

- Multi-tenant correctness depends on host derivation behavior
- Operators must understand mode-specific passkey configuration expectations
- Authentication changes should validate both deployment modes

## Evidence

- Identity and passkey options configuration with mode split:
  - Editor/Program.cs
- Explicit config key used for single-tenant RP ID:
  - Editor/Program.cs
