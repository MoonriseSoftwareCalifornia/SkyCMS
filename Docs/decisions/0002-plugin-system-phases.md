# 0002 — Plugin System Implementation Phases

**Status:** Accepted  
**Date:** 2025-01-01  
**Deciders:** SkyCMS core team  
**Branch:** `feature/plugin-system`  
**Related:** [0001 — Plugin System Architecture](0001-plugin-system-architecture.md), [0003 — Plugin Installation and Security](0003-plugin-installation-and-security.md)

---

## Context

ADR 0001 defines the plugin system architecture. This record documents the agreed-upon phasing so that work can be tracked, reviewed, and merged incrementally without destabilizing the main branch.

Each phase is a focused, independently reviewable PR against `feature/plugin-system`. No phase should be merged if it breaks existing behavior.

---

## Decision

Work will proceed in six phases.

### Phase 1 — Abstractions and Route Reservation

**Goal:** Establish the plugin contract library and reserve the `/_sky/` path.

- Create the `Sky.Editor.Abstractions` project containing:
  - `ISkyCmsPlugin`
  - `PluginNavItem` record
  - `IDesignerContributor`
- Add `AddSkyCmsPlugin<T>()` extension on `IServiceCollection`
- Register `/_sky/` in `IReservedPaths` at startup so content editors cannot create pages there
- **Deliverable:** Contract is defined and path is reserved. No behavioral change to the running app.

### Phase 2 — Designer Contributor Pipeline

**Goal:** Replace the hard-coded Bootstrap/Tailwind detection in `DesignerConfig` with a contributor pipeline.

- Refactor `DesignerConfig` to accept `IEnumerable<IDesignerContributor>` via DI
- Extract the existing Bootstrap 4, Bootstrap 5, and Tailwind detection into a built-in `DefaultCssFrameworkDesignerContributor` implementation
- **Deliverable:** All existing GrapesJS designer behavior is unchanged; the pipeline is open for plugins to contribute custom blocks and components.

### Phase 3 — Plugins Navigation Dropdown

**Goal:** Render plugin nav contributions as a "Plugins" dropdown in the editor navbar.

- Add the "Plugins" `<li>` dropdown to `_CosmosMainMenuPartial.cshtml`, hidden when no items are visible to the current user
- Add the CSS workaround for Bootstrap 5 nested submenus
- Inject `IEnumerable<ISkyCmsPlugin>` into the partial; iterate `GetNavItems(User)` to build the menu
- **Deliverable:** A registered plugin can surface UI entry points in the navbar without touching core layout files. An empty plugin list produces identical HTML to today.

### Phase 4 — NuGet Runtime Install and Plugin Dashboard

**Goal:** Allow administrators to install plugins at runtime (via NuGet) without modifying core code; provide a dashboard to manage and monitor plugins.

- Implement the `NuGet.Protocol`-based plugin loader (download-on-next-restart flow; see ADR 0003)
- Implement `sky-plugin.json` manifest validation before assembly load
- Implement dependency conflict detection (skip + warn on incompatible assemblies)
- Implement the try-catch fault isolation wrapper around all plugin registration calls
- Create `Pages/PluginManager/Index.cshtml` (admin-only) showing:
  - Installed plugins with metadata, status, and last-boot load result
  - Pending-restart banner listing what will change on next boot
  - Install (NuGet package ID input), uninstall, enable, disable controls
  - Per-plugin audit log
- **Deliverable:** An administrator can install, inspect, and manage plugins entirely through the browser. A restart banner prevents silent "why didn't it work?" confusion.

### Phase 5 — Security Layer

**Goal:** Add the full security pipeline to the plugin install flow.

- Integrate vulnerability database check via NuGet vulnerability API (batched, one round trip for all plugins) on every boot
- Add background daily vulnerability recheck (`IHostedService`)
- CVE policy: load-but-flag by default; admin-configurable to block (stored in app settings)
- Add package signature verification; log signing certificate details in the audit log
- Add human approval gate to the install flow in the Plugin Dashboard (show metadata, signature, CVE status, NuGet.org link before confirming)
- Expose vulnerability and signature status in the Plugin Dashboard
- **Deliverable:** Public-feed plugin installs have a full security checkpoint. Private-feed installs follow the feed owner's vetting policy.

### Phase 6 — Internal Plugin Validation (Copilot Feature)

**Goal:** Validate the full abstraction end-to-end by converting the existing Copilot proxy feature into the first internal plugin.

- Wrap `Features/Copilot/` in an `ISkyCmsPlugin` implementation
- Provide a `sky-plugin.json` manifest in the Copilot plugin package
- Confirm the feature works identically before and after via existing tests
- **Deliverable:** Proof that the plugin contract is sufficient for a real, in-production feature. No user-visible change.

---

## Consequences

- Each phase ships as its own PR, keeping reviews focused and diff sizes manageable.
- Phase 1 is a prerequisite for all later phases.
- Phases 2 and 3 are independent of each other and can proceed in parallel after Phase 1.
- Phase 4 is the largest phase and may be split into sub-PRs (loader vs. dashboard UI).
- Phase 5 depends on Phase 4 (the dashboard is where security status is surfaced).
- Phase 6 can proceed after Phase 1 and serves as an integration smoke test for the full system.
- A curated SkyCMS plugin marketplace is explicitly deferred beyond Phase 6 and will require its own ADR when planned.
