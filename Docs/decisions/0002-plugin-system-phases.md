# 0002 — Plugin System Implementation Phases

**Status:** Proposed  
**Date:** 2025-01-01  
**Deciders:** SkyCMS core team  
**Branch:** `feature/plugin-system`  
**Related:** [0001 — Plugin System Architecture](0001-plugin-system-architecture.md)

---

## Context

ADR 0001 defines the plugin system architecture. This record documents the agreed-upon phasing so that work can be tracked, reviewed, and merged incrementally without destabilizing the main branch.

---

## Decision

Work will proceed in four phases, each targeted at a single focused PR.

### Phase 1 — Contracts and Front-End Contributor Pipeline

**Goal:** Define all interfaces; wire `DesignerConfig` to use a contributor pipeline.

- Create `Sky.Editor.Abstractions` (or extend `Sky.Cms.Api.Shared`) with:
  - `ISkyCmsPlugin`
  - `IDesignerContributor`
  - `NavItem`
  - `INavContributor`
- Refactor `DesignerConfig` to accept `IEnumerable<IDesignerContributor>` instead of hard-coding Bootstrap/Tailwind detection.
- Register the built-in Bootstrap/Tailwind detector as the first `IDesignerContributor` implementation.
- Add `AddSkyCmsPlugin<T>()` extension method on `IServiceCollection`.
- **Deliverable:** All existing designer behavior is unchanged; the new contributor pipeline is in place.

### Phase 2 — Editor Shell Navigation Contributions

**Goal:** Render nav items contributed by plugins in the editor sidebar.

- Add `INavContributor` resolution to the layout/nav partial.
- Verify that an empty contributor list produces identical HTML to today.
- **Deliverable:** A plugin can add sidebar nav items without touching core layout files.

### Phase 3 — Internal Plugin Validation (Copilot Feature)

**Goal:** Validate the abstraction by converting the existing Copilot proxy feature into the first internal plugin.

- Wrap `Features/Copilot/` in an `ISkyCmsPlugin` implementation.
- Confirm the feature works identically before and after.
- **Deliverable:** Proof that the plugin contract is sufficient for a real feature; no user-visible change.

### Phase 4 — Per-Tenant Plugin Enable / Disable

**Goal:** Store plugin enablement per tenant and gate resolution at request time.

- Add a `PluginSettings` column or table to the tenant settings.
- Implement an `IDynamicConfigurationProvider`-aware plugin resolver that filters the active set per request.
- Add a settings UI panel in the editor admin area.
- **Deliverable:** Different tenants can activate different plugins from the same deployment.

---

## Consequences

- Each phase ships as its own PR against `feature/plugin-system`, keeping reviews focused.
- Phase 1 is a prerequisite for all later phases.
- Phases 2 and 3 are independent of each other and can proceed in parallel.
- Phase 4 depends on Phase 1 and benefits from Phase 3 as a real-world test case.
