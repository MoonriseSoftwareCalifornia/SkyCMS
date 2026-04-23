# ADR 0030: CloudFront Proto Header Normalization Strategy

## Status

Accepted

## Context

SkyCMS can operate behind CloudFront and other edge layers where protocol forwarding may
arrive through provider-specific headers. Downstream middleware expects standard forwarded
header conventions for scheme-aware behavior.

Without normalization, scheme-sensitive behavior (redirects, secure URL generation,
security policy interactions) may be inconsistent.

A lightweight compatibility strategy was needed.

## Design Goals

This decision aims to:

1. Preserve correct request scheme semantics behind CloudFront
2. Normalize provider-specific proto signaling to standard forwarded-header conventions
3. Keep compatibility logic centralized and explicit
4. Avoid broad provider coupling in downstream code
5. Minimize complexity while improving edge compatibility

## Non-Goals

This decision does not attempt to:

- Provide full CDN abstraction for every provider
- Replace forwarded-header trust controls
- Implement generic header rewrite framework
- Define all edge-network integration behavior in this ADR

## Decision

SkyCMS maps CloudFront-Forwarded-Proto to X-Forwarded-Proto before forwarded-header
processing, enabling existing ASP.NET Core forwarding logic to consume normalized scheme
information.

This normalization is executed in startup pipeline before UseForwardedHeaders.

## Detailed Rationale

### Targeted Compatibility Fix

A narrow normalization step addresses a concrete edge integration need without introducing
large abstraction layers.

### Preserve Existing Forwarded-Header Semantics

By normalizing into standard header names, downstream behavior can stay provider-agnostic.

### Startup-Level Visibility

Keeping this behavior in startup composition makes edge compatibility assumptions auditable.

## Alternatives Considered

### No Normalization

Rejected because CloudFront-specific proto header could be ignored by standard forwarding
logic, causing scheme inconsistencies.

### Deep CDN Abstraction Layer

Rejected as unnecessary complexity for the current requirement.

### Header Rewriting in Every Feature

Rejected because cross-cutting concerns belong in centralized middleware composition.

## Consequences

### Positive Outcomes

- Better scheme correctness behind CloudFront
- Minimal, explicit compatibility behavior
- Reduced downstream provider-specific handling

### Constraints Introduced

- Cloud provider behavior assumptions should be revalidated during upgrades
- Additional edge providers may require similar normalization decisions
- Header normalization order remains important

## Evidence

- CloudFront protocol header normalization before forwarded headers:
  - Editor/Program.cs
