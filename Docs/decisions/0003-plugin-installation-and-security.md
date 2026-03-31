# 0003 — Plugin Installation, Security, and Runtime Management

**Status:** Accepted  
**Date:** 2025-01-01  
**Deciders:** SkyCMS core team  
**Branch:** `feature/plugin-system`  
**Related:** [0001 — Plugin System Architecture](0001-plugin-system-architecture.md), [0002 — Plugin System Implementation Phases](0002-plugin-system-phases.md)

---

## Context

ADR 0001 establishes that plugins are NuGet packages (RCLs) loaded at startup. This ADR records decisions specific to *how* plugins are installed, validated, loaded, monitored, and managed at runtime — including security controls, failure handling, and the administrator dashboard.

The core constraint driving these decisions is that plugins run **in-process with full application trust**. This is unavoidable without out-of-process isolation (which is out of scope for v1 due to complexity). Every decision below is shaped by that fact.

---

## Decision

### 1. Runtime NuGet Install (No SDK Required)

The `NuGet.Protocol` NuGet package (Microsoft's own NuGet client library) provides a full package download and extraction API that works with the .NET runtime only — no .NET SDK installation on the server is required.

**Install flow:**

1. Administrator enters a NuGet package ID (and optionally a version) in the Plugin Dashboard
2. After passing the security approval gate (see §3), the package is marked `pending` in the plugin registry table
3. On the **next application startup**, the plugin loader reads all `pending` entries, downloads each `.nupkg` via `NuGet.Protocol`, extracts the plugin DLL to `App_Plugins/{pluginId}/`, and marks the entry `active`
4. The DLL is loaded as an `ApplicationPart` — Razor Pages, API controllers, and static assets are discovered automatically
5. If download or extraction fails, the entry is marked `failed`, a detailed error is written to the audit log, and startup continues without that plugin

A configured NuGet feed source list controls where packages are fetched from. By default, nuget.org is included. Private feeds (Azure Artifacts, GitHub Packages, etc.) can be added via app configuration.

**Restart requirement:** The DI container and route table are built once at startup. Installing, uninstalling, enabling, or disabling a plugin always requires a restart to take effect. The Plugin Dashboard makes this unambiguous with a persistent "Pending restart" banner.

### 2. Plugin Manifest Validation (`sky-plugin.json`)

Before any assembly is loaded, the loader verifies that the extracted package contains a valid `sky-plugin.json` in its `contentFiles`. This is the first gate and prevents accidental loading of arbitrary NuGet packages that happen to implement a matching interface.

Required fields:

| Field | Purpose |
|---|---|
| `pluginId` | Must match the NuGet package ID |
| `displayName` | Human-readable name for the dashboard |
| `version` | Must match the NuGet package version |
| `minHostVersion` | Minimum SkyCMS version required; loader skips if host is older |

If the manifest is absent or invalid, the plugin is skipped and the failure is written to the audit log.

### 3. Security Controls (Public NuGet Feed)

No automated check can reliably distinguish a malicious plugin from a legitimate one. These controls provide layered defense and accountability, not a guarantee.

**Controls applied at install time (in order):**

1. **Feed allow-list** — Only packages from explicitly configured feeds are accepted. Arbitrary URLs are blocked.
2. **`sky-plugin.json` manifest check** — Validates the package is a SkyCMS plugin (see §2).
3. **Vulnerability database check** — Queries the NuGet vulnerability API (backed by the GitHub Advisory Database) for the specific package ID and version. Any known CVE is shown to the administrator before proceeding.
4. **Package signature verification** — The package signature is verified and the signing certificate's subject and thumbprint are recorded in the audit log. Unsigned packages are permitted but flagged explicitly.
5. **Human approval gate** — The Plugin Dashboard shows all of the above metadata — CVE status, signing details, download count, NuGet.org link — and requires an explicit administrator confirmation before the package is marked `pending`. There is no one-click install.

**Assumption for private feeds:** Private feed packages are considered vetted by the feed owner. Controls 3 and 4 still run, but the approval gate may be streamlined for known-good private feeds (configurable).

### 4. Boot-time Vulnerability Recheck

On every application startup, **all active plugins** are checked against the NuGet vulnerability database. Checks are **batched** — a single HTTP round trip covers all installed plugins regardless of count.

**CVE policy (admin-configurable, default: load-but-flag):**

| Policy | Behavior |
|---|---|
| `load-and-flag` (default) | Plugin loads; vulnerability shown prominently in Plugin Dashboard; no alert sent unless configured |
| `block-on-cve` | Plugin is not loaded; failure written to audit log; admin must acknowledge before the plugin can be re-enabled |

If the vulnerability API is unreachable at startup (network failure, rate limit), the recheck is skipped for that boot, a warning is logged, and startup proceeds normally. The API failure is shown in the Plugin Dashboard.

### 5. Background Vulnerability Check

A hosted service (`IHostedService`) runs a vulnerability recheck daily during low-traffic hours. The schedule is configurable via app settings. Results are written to the plugin registry and surfaced in the Plugin Dashboard. Administrators are not automatically emailed unless a notification integration is configured (future enhancement).

### 6. Dependency Conflict Handling (No `AssemblyLoadContext`)

v1 does not use `AssemblyLoadContext` per-plugin isolation. The decision is: **host compatibility is required; incompatible plugins are skipped with a warning.**

At load time, the loader performs a best-effort compatibility check:
- ✅ Detects: required assembly not found in the host
- ✅ Detects: direct assembly version mismatch (plugin requires `Foo v2.0`, host has `Foo v1.0`)
- ❌ Does not detect: transitive dependency conflicts

Transitive conflicts will manifest as runtime exceptions on first use. The try-catch wrapper around all plugin registration calls (see §7) catches these, marks the plugin `failed`, and writes the full exception to the audit log. The administrator sees this in the Plugin Dashboard.

`AssemblyLoadContext` isolation is deferred to a future phase.

### 7. Fault Isolation

All plugin lifecycle calls (`ConfigureServices`, `GetNavItems`, `GetDesignerPlugins`, and any domain event handler invocations) are wrapped in try-catch. An exception from one plugin:
- Is caught and logged with the plugin ID and full stack trace
- Does NOT propagate to other plugins or to the host application
- Results in the plugin being marked `degraded` in the Plugin Dashboard for the current session

A plugin that consistently fails to load across three consecutive boots is automatically marked `disabled` and requires manual re-enablement by an administrator.

### 8. Plugin Dashboard (`Pages/PluginManager/`)

The Plugin Dashboard is a **core CMS feature**, not a plugin-provided page. It lives at `Pages/PluginManager/Index.cshtml` (route `/PluginManager`), consistent with the existing `Pages/Diagnostics/Index.cshtml` pattern. It is restricted to the `Administrators` role.

**Dashboard shows per plugin:**
- Display name, plugin ID, version, source feed
- Status: Active / Inactive / Failed / Degraded / Vulnerable / Pending restart
- Signing certificate subject and thumbprint
- Last vulnerability check timestamp and CVE summary
- Last boot load result (success, warning, or error with details)
- Install date, installing admin identity
- Full audit log entries

**Dashboard actions:**
- Install (NuGet package ID + optional version → approval gate → mark pending)
- Uninstall (mark for removal on next restart)
- Enable / Disable (toggle activation on next restart)
- "Restart now" button (calls `IHostApplicationLifetime.StopApplication()` where the host process manager will restart the app; shown only when pending changes exist)

**Pending restart banner:** Displayed site-wide in the editor navbar for administrators whenever there are unactivated changes. Lists a summary of what will change (e.g., "1 plugin pending install, 1 pending disable").

### 9. Audit Log

Every plugin-related action is written to a dedicated audit log table with:
- Timestamp (UTC)
- Action type (install-requested, install-completed, install-failed, uninstalled, enabled, disabled, cve-detected, load-failed, etc.)
- Plugin ID and version
- Source feed URL
- Signing certificate thumbprint (if signed)
- Performing administrator identity
- Free-text detail / error message

The audit log is append-only and not editable by any user role.

---

## Consequences

### Positive
- No .NET SDK required on production servers.
- Administrators install plugins through a browser UI — no file transfers, no server access needed.
- NuGet versioning gives upgrade, downgrade, and rollback for free.
- The layered security controls (vulnerability check + signature + human gate + audit log) are stronger than what most comparable CMS plugin systems provide.
- Fault isolation ensures one bad plugin cannot take down the entire site.
- The audit log provides a complete history for compliance and incident response.

### Negative / Trade-offs
- Every install, uninstall, enable, and disable requires a restart. In load-balanced deployments, all nodes must restart. This is a known v1 constraint — document it clearly in the plugin developer guide and the dashboard UI.
- Boot time increases slightly with each installed plugin (manifest validation + vulnerability check). Batching the vulnerability check keeps this sub-second for typical plugin counts.
- Plugins run with full in-process trust. No automated check fully mitigates a sophisticated supply-chain attack. Administrators must be trained to treat plugin installation with the same caution as server access.
- The `block-on-cve` policy can silently remove plugin functionality between restarts if a CVE is published overnight. Default policy is therefore `load-and-flag`.

### Future Enhancements (explicitly deferred)
- `AssemblyLoadContext` per-plugin isolation (resolves transitive dependency conflicts)
- Admin notification (email/webhook) when a CVE is detected on an installed plugin
- Curated SkyCMS plugin marketplace with team-reviewed listings
- Plugin package signing requirement (currently flagged but not enforced)
