# ADR 0011: Policy-Based Rate Limiting Architecture

## Status

Accepted

## Context

SkyCMS exposes endpoints with distinct risk and usage profiles, including deployment,
contact form submissions, docs import, and AI-assisted operations.

A single global throttle policy would be either too restrictive for operational endpoints or
too permissive for abuse-prone endpoints. The platform needed explicit per-purpose rate
limiting policies with environment-aware behavior where appropriate.

## Design Goals

This decision aims to:

1. Apply endpoint-appropriate limits instead of one-size-fits-all throttling
2. Protect abuse-prone public endpoints with strict defaults
3. Allow more permissive limits in development for productivity
4. Keep policy definitions centralized and auditable in startup composition
5. Enable operational tuning via configuration where needed

## Non-Goals

This decision does not attempt to:

- Provide complete DDoS mitigation by itself
- Replace edge-network/CDN rate controls
- Eliminate need for endpoint-level authorization checks
- Define long-term SLA values for every policy in this ADR

## Decision

SkyCMS standardizes on policy-based ASP.NET Core rate limiting with named fixed-window
policies, including:

- fixed
- deployment
- docs-import
- contact-form
- copilot-inline
- copilot-chat

Policies are centrally configured at startup, with selected environment-aware thresholds and
configuration-driven values for docs import.

## Detailed Rationale

### Granularity by Endpoint Purpose

Different workloads require different limits; named policies provide precise control and
clear intent.

### Environment-Aware Defaults

Development workflows need higher practical limits while production endpoints require stricter
abuse resistance.

### Centralized Governance

Single-point startup configuration keeps policy behavior visible and easier to review.

## Alternatives Considered

### One Global Limit

Rejected because it cannot balance diverse endpoint workloads safely and efficiently.

### No Application-Level Rate Limiting

Rejected because edge controls alone do not capture all app-specific abuse vectors.

### Ad Hoc Per-Controller Custom Throttling

Rejected because distributed policy logic becomes inconsistent and hard to audit.

## Consequences

### Positive Outcomes

- Better endpoint-specific resilience
- Clear and maintainable policy configuration
- Improved abuse protection for sensitive/public endpoints
- Tunable behavior for operational and import workflows

### Constraints Introduced

- Policy values require periodic review as usage evolves
- New endpoints must be deliberately mapped to appropriate policies
- Contributors must consider environment-specific behavior during testing

## Evidence

- Central rate limiter configuration and named policies:
  - Editor/Program.cs
