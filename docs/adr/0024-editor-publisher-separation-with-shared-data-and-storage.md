# ADR 0024: Editor-Publisher Separation with Shared Data and Storage

## Status

Accepted

## Context

SkyCMS has two major runtime applications with different responsibilities:

- Editor for authoring, setup, and publishing operations
- Publisher for serving public-facing content

A monolithic runtime would blur operational boundaries and make scaling or hardening
more difficult. SkyCMS needed explicit architectural separation while preserving shared
content state.

## Design Goals

This decision aims to:

1. Separate authoring and serving concerns into independently deployable applications
2. Preserve shared content truth through common database and storage contracts
3. Avoid direct runtime coupling between Editor and Publisher over HTTP
4. Support static and dynamic serving strategies in Publisher independently
5. Improve operational flexibility and security posture by role separation

## Non-Goals

This decision does not attempt to:

- Eliminate all shared libraries between applications
- Remove requirement for coordinated schema/storage compatibility
- Define full deployment topology for every environment
- Replace integration testing across Editor/Publisher boundaries

## Decision

SkyCMS treats Editor and Publisher as separate applications that share persistence and
storage substrates, rather than calling each other directly over HTTP.

Editor writes publishing artifacts (database records and static outputs), and Publisher
reads and serves those artifacts according to its operating mode.

## Detailed Rationale

### Clear Responsibility Boundaries

Editor and Publisher have distinct runtime concerns; separation keeps each service focused
and easier to evolve independently.

### Operational Independence

Independent deployment supports varied scaling, hardening, and hosting strategies.

### Reduced Tight Coupling

Avoiding direct runtime HTTP dependency between Editor and Publisher lowers integration
fragility and keeps data/storage as the contract boundary.

## Alternatives Considered

### Single Combined Runtime

Rejected because it mixes concerns and reduces operational flexibility.

### Direct Editor-to-Publisher HTTP Coupling

Rejected because it introduces tighter runtime dependency and failure coupling.

### Fully Separate Persistence Stores per App

Rejected because shared content truth and publishing flow require common data/storage model.

## Consequences

### Positive Outcomes

- Clear runtime role separation
- Independent deployment and scaling options
- Simpler boundary contract via shared persistence and storage

### Constraints Introduced

- Schema and storage contract compatibility is critical across both apps
- Release coordination may still be required for shared contract changes
- Cross-application integration tests remain important

## Evidence

- Publisher architecture documentation (separate runtime, shared DB/storage, no direct HTTP):
  - SkyCMS.Docs/for-developers/publisher-architecture.md
- Publisher startup mode composition:
  - Publisher/Program.cs
