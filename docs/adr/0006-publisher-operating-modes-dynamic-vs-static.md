# ADR 0006: Publisher Operating Modes (Dynamic vs Static)

## Status

Accepted

## Context

SkyCMS Publisher serves public site traffic and must support different deployment profiles.
Some environments prioritize real-time rendering with database-backed page resolution, while
others prioritize edge performance, simplified runtime infrastructure, and static delivery.

A single serving model cannot optimize equally for both goals. SkyCMS therefore needed an
explicit architecture decision for Publisher operating modes.

## Design Goals

This decision aims to:

1. Support both dynamic and static serving models from one Publisher codebase
2. Keep mode selection explicit and configuration-driven
3. Enable high-performance static delivery when desired
4. Preserve dynamic rendering capabilities when required
5. Keep mode behavior understandable to operators and contributors

## Non-Goals

This decision does not attempt to:

- Automatically switch modes at runtime based on load
- Eliminate dynamic mode in favor of static-only deployments
- Eliminate static mode in favor of dynamic-only deployments
- Define all publishing workflows in this ADR

## Decision

Publisher mode is selected at startup by configuration key CosmosStaticWebPages:

- true: run StaticWebsiteProxy boot path
- false: run DynamicPublisherWebsite boot path

This startup split defines two first-class operating modes:

- Dynamic mode for database-backed, real-time content serving
- Static mode for serving pre-generated files from storage with lightweight proxy behavior

## Detailed Rationale

### Explicit Startup Selection

Configuration-driven startup mode keeps deployment intent clear and avoids ambiguous,
partially-dynamic runtime behavior.

### Performance and Cost Flexibility

Static mode supports edge-friendly deployments with reduced runtime dependencies,
while dynamic mode preserves richer runtime capabilities.

### Operational Predictability

Separate boot paths allow each mode to register only the services it needs, improving
clarity around behavior and troubleshooting.

## Alternatives Considered

### Always Dynamic

Rejected because it does not provide the lightweight static serving profile needed by
some deployments.

### Always Static

Rejected because it cannot satisfy use cases requiring dynamic database-backed behavior.

### Hybrid Runtime With Per-Request Mode Decisions

Rejected because it increases complexity and reduces operational transparency.

## Consequences

### Positive Outcomes

- Clear deployment choice for operators
- Better fit across performance, cost, and feature requirements
- Explicit and maintainable startup architecture

### Constraints Introduced

- Operators must choose and configure mode intentionally
- Testing and documentation must account for both modes
- Feature changes may require validation in both boot paths

## Evidence

- Publisher startup mode selection:
  - Publisher/Program.cs
- Developer architecture explanation:
  - SkyCMS.Docs/for-developers/publisher-architecture.md
