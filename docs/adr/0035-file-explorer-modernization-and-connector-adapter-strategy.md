# ADR 0035: File Explorer Modernization and Connector Adapter Strategy

## Status

Proposed

## Context

SkyCMS currently uses a custom file explorer implementation in the editor area with
substantial inline UI and interaction logic. The current implementation is feature-rich
but has become difficult to evolve from a UX and maintainability perspective.

The platform already has an established backend contract for file and folder operations,
including selection modes, image-only browsing, directory-only flows, chunked upload,
thumbnail generation, rename/delete/move/copy semantics, and security validation rules.

The modernization effort needs to improve user experience while preserving behavioral
compatibility and low operational risk during rollout.

This decision also requires clarity on whether the integration contract should be
implemented as raw ASP.NET Core middleware or as endpoint/controller-based connector
adapters.

## Design Goals

This decision aims to:

1. Deliver a more modern and intuitive file explorer UX
2. Preserve existing operational behavior and storage semantics
3. Minimize migration risk through feature-flagged side-by-side rollout
4. Keep architecture consistent with ASP.NET Core responsibilities
5. Enable first-pass light and dark theme support
6. Keep dependency licensing permissive-first where practical

## Non-Goals

This decision does not attempt to:

- Rebuild all editor functionality as a full SPA
- Replace existing storage abstractions or blob provider semantics
- Re-architect all upload and media processing flows in the first pass
- Define final branding/theme details for every tenant
- Commit to non-permissive dependencies as a default path

## Decision

SkyCMS will modernize the file explorer by introducing an embedded file-manager shell
behind a feature flag, with a connector adapter implemented via endpoint/controller
handlers that map shell commands to existing file-management services and actions.

The connector adapter will not be implemented as raw middleware for business commands.
Middleware remains reserved for cross-cutting concerns such as tenancy context,
authentication, correlation, and other request pipeline concerns.

The initial candidate direction is an elFinder-style shell integration with:

- behavior-preserving backend command mapping,
- first-pass light and dark theme support,
- staged rollout with rapid rollback path.

## Detailed Rationale

### Endpoint/Controller Adapters Match Existing Contracts

File operations in SkyCMS are business actions with explicit validation, access rules,
and result shaping. Endpoint/controller adapters make these concerns testable and easier
to reason about than command execution inside raw middleware.

### Lower Migration Risk Through Compatibility Mapping

A command adapter allows modernization of the UI surface while keeping existing backend
behavior stable, reducing risk of regressions in copy/move/upload/thumbnail flows.

### Feature-Flag Rollout Enables Safe Adoption

Running legacy and modern explorers side-by-side under a feature flag enables practical
validation and rollback without service interruption.

### Dark Mode Included in Initial Delivery

Dark mode is a core UX requirement for this modernization initiative and should be
implemented in first pass rather than postponed. This avoids duplicated styling work
and reduces rework for component-level accessibility.

### Permissive-First Dependency Posture

Given project constraints and contributor expectations, permissive licensing is preferred.
Copyleft options may be evaluated, but permissive options are the default decision path.

## Alternatives Considered

### Keep Existing Custom Explorer and Restyle Only

Rejected as the primary strategy because it improves appearance but does not adequately
reduce long-term complexity of highly coupled inline UI logic.

### Full SPA Rewrite for File Explorer

Rejected for first pass due to larger scope, migration risk, and delayed value delivery.

### Implement Connector Business Commands as Middleware

Rejected because middleware is not the ideal boundary for operation-level business command
execution and tends to complicate routing, validation, and testability.

### Product-Style Standalone File Manager Integration

Considered but not selected as default because standalone product patterns can be less
aligned with direct reuse of SkyCMS endpoint contracts and deployment assumptions.

## Consequences

### Positive Outcomes

- Improved user experience with modern file manager interaction patterns
- Better separation of concerns between cross-cutting middleware and business actions
- Lower-risk migration path through adapter compatibility and feature flags
- Early availability of light/dark theming support

### Constraints Introduced

- Adapter command mapping must be carefully maintained for parity
- The shell integration introduces external UI dependency management overhead
- Feature flag and dual-route support temporarily add rollout complexity

## Evidence

- Existing custom explorer view and interaction logic:
  - Editor/Views/Shared/FileExplorer/Index.cshtml
- Existing backend operations and behavior contract:
  - Editor/Controllers/FileManagerController.cs
- Tracking feature request:
  - https://github.com/CWALabs/SkyCMS/issues/68
- Candidate shell reference:
  - https://studio-42.github.io/elFinder/
- Candidate theme reference discussed during design:
  - https://github.com/DennisSuitters/LibreICONS/tree/master/themes/elFinder
