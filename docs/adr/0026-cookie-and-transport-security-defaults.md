# ADR 0026: Cookie and Transport Security Defaults

## Status

Accepted

## Context

SkyCMS handles authenticated sessions and must enforce secure defaults for transport and
cookie behavior across deployment environments. Weak defaults increase risk of session
interception, misuse, and inconsistent security posture.

A centralized security-default decision was needed in startup composition to establish
baseline protections.

## Design Goals

This decision aims to:

1. Enforce secure cookie handling defaults
2. Enforce long-lived HSTS policy for HTTPS transport hardening
3. Keep cookie redirect behavior controlled and explicit
4. Preserve compatibility with multi-tenant cookie domain isolation logic
5. Keep security defaults centralized in startup configuration

## Non-Goals

This decision does not attempt to:

- Replace all application authorization controls
- Define complete threat modeling across all components
- Eliminate need for infrastructure TLS best practices
- Standardize every security header in this ADR

## Decision

SkyCMS configures secure authentication-cookie and transport defaults in startup,
including:

- Cookie SecurePolicy set to Always
- Cookie HttpOnly enabled
- Cookie SameSite set to Lax
- HSTS enabled with preload, subdomain inclusion, and one-year max age

These defaults apply alongside multi-tenant cookie-domain validation behavior where relevant.

## Detailed Rationale

### Secure-by-Default Session Handling

Cookie flags should default to secure settings so deployments begin from a hardened baseline.

### Transport Hardening

HSTS policy reduces downgrade and mixed transport risks for browser-based access.

### Centralized Security Composition

Placing these settings in startup keeps security expectations explicit and reviewable.

## Alternatives Considered

### Environment-Optional Cookie Security Flags

Rejected because permissive defaults risk inconsistent and insecure deployments.

### No HSTS by Default

Rejected because browser-enforced HTTPS policy is a practical baseline protection.

### Distributed Security Config Across Feature Modules

Rejected because fragmented configuration is harder to audit and maintain.

## Consequences

### Positive Outcomes

- Stronger default session and transport protections
- More consistent security posture across environments
- Easier auditing of baseline web security configuration

### Constraints Introduced

- Deployments must be compatible with HTTPS-first expectations
- Changes to cookie behavior require careful regression/security review
- Additional security headers may still be required by policy context

## Evidence

- HSTS and application-cookie security configuration:
  - Editor/Program.cs
