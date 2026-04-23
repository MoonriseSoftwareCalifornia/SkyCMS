# ADR 0010: SignalR Tenant Isolation and Scoped Progress Reporting

## Status

Accepted

## Context

SkyCMS uses real-time updates for operations such as publishing progress. In multi-tenant
environments, real-time channels must not leak updates between tenant users.

Using default identity mapping without tenant-aware constraints could allow cross-tenant
signal routing errors or ambiguous user mapping in shared infrastructure.

A deliberate architecture decision was required for tenant-safe SignalR identity routing and
progress reporting service lifetime.

## Design Goals

This decision aims to:

1. Scope SignalR user identity resolution to tenant-safe identifiers
2. Prevent cross-tenant real-time message leakage
3. Keep progress reporting tied to request-scoped tenant context
4. Preserve clear startup composition for real-time infrastructure
5. Support development diagnostics without compromising isolation semantics

## Non-Goals

This decision does not attempt to:

- Define every hub method authorization policy
- Replace broader authentication and authorization controls
- Introduce distributed event streaming infrastructure
- Guarantee ordering semantics for all progress events

## Decision

SkyCMS configures SignalR with tenant-aware identity and scoped progress reporting:

- A custom IUserIdProvider (SubClaimUserIdProvider) is registered for tenant-safe user mapping
- Publishing progress reporting is registered as scoped (IPublishingProgressReporter)
- SignalR is composed in startup with these isolation-oriented services

## Detailed Rationale

### Custom User Identity Mapping

A custom user ID provider allows the platform to enforce tenant-safe identity semantics
instead of relying on generic defaults.

### Scoped Reporter Lifetime

Progress reporting must resolve tenant context correctly per request/session; scoped lifetime
supports this requirement better than singleton patterns.

### Explicit Composition

Placing these decisions in startup wiring makes isolation assumptions visible and auditable.

## Alternatives Considered

### Default SignalR User Mapping

Rejected because default mapping may not encode tenant boundary requirements sufficiently.

### Singleton Progress Reporter

Rejected due to higher risk of tenant-context bleed and lifecycle mismatch.

### Out-of-Band Polling Instead of SignalR

Rejected because it increases latency and complexity for real-time user feedback.

## Consequences

### Positive Outcomes

- Better tenant boundary protection for real-time updates
- Clear composition of isolation-related SignalR services
- Improved correctness for user-specific publishing progress signals

### Constraints Introduced

- SignalR identity behavior now depends on custom provider correctness
- Contributors must preserve scoped lifecycle for tenant-sensitive reporting
- Real-time flow changes require tenant-isolation regression validation

## Evidence

- SignalR configuration and custom IUserIdProvider registration:
  - Editor/Program.cs
- Scoped publishing progress reporter registration:
  - Editor/Program.cs
