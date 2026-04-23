# ADR 0012: Setup Wizard Dual-Flow Architecture

## Status

Accepted

## Context

SkyCMS setup requirements differ significantly between single-tenant and multi-tenant
deployments. Single-tenant onboarding includes broader infrastructure and publishing steps,
while multi-tenant onboarding emphasizes tenant-focused setup with a simplified path.

A single undifferentiated setup flow would either overcomplicate multi-tenant onboarding or
under-specify single-tenant initialization requirements.

## Design Goals

This decision aims to:

1. Provide setup paths aligned to deployment mode complexity
2. Keep route conventions explicit and predictable
3. Reduce onboarding friction by avoiding unnecessary steps per mode
4. Preserve maintainability by declaring setup flow routing centrally
5. Support future evolution of mode-specific setup steps without conflating concerns

## Non-Goals

This decision does not attempt to:

- Define every UI behavior of setup pages
- Replace configuration validation logic outside setup flow routing
- Merge all setup steps into one universal wizard
- Define tenant provisioning lifecycle beyond setup entry points

## Decision

SkyCMS configures setup wizard routing as dual flows in startup:

- Single-tenant flow: full multi-step wizard route set
  (mode, storage, admin, publisher, email, review, complete)
- Multi-tenant flow: simplified tenant-focused route set
  (tenant index, admin, complete)

The flow is selected by deployment mode and wired through Razor Pages conventions.

## Detailed Rationale

### Mode-Appropriate Onboarding

Different deployment models have materially different setup responsibilities; tailored flows
improve clarity and reduce cognitive overhead.

### Explicit Route Conventions

Centralized route mapping keeps setup URL behavior stable, discoverable, and testable.

### Maintainable Separation

Dual-flow routing avoids conditional UI overload within one monolithic wizard definition.

## Alternatives Considered

### One Universal Setup Wizard

Rejected because it introduces branching complexity and unnecessary steps for some modes.

### Completely Separate Setup Applications

Rejected because it duplicates shared setup infrastructure and increases maintenance burden.

### Runtime Step Hiding Without Route Separation

Rejected because route semantics become unclear and harder to reason about.

## Consequences

### Positive Outcomes

- Clearer onboarding for each deployment mode
- Reduced setup friction for multi-tenant scenarios
- Explicit and maintainable setup route architecture

### Constraints Introduced

- Setup route changes must preserve mode-specific intent
- Tests and docs should validate both setup flows
- Contributors must avoid accidental cross-flow assumptions

## Evidence

- Mode-based setup route conventions:
  - Editor/Program.cs
