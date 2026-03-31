# 0001 — Plugin System Architecture for Sky.Editor

**Status:** Accepted  
**Date:** 2025-01-01  
**Deciders:** SkyCMS core team  
**Branch:** `feature/plugin-system`

---

## Context

Sky.Editor is a multi-tenant CMS with a growing surface area. Features like the Copilot proxy, CDN integrations, and the GrapesJS designer plugin registry (`DesignerPlugin` / `DesignerConfig`) are already partially isolated, but there is no formal extension point. As the platform grows, third parties (and internal teams) need a structured way to add new capabilities — UI pages, back-end services, API endpoints, domain-event reactions, and front-end GrapesJS blocks — without modifying core editor code.

The existing codebase provides useful building blocks:
- **`DesignerPlugin` / `DesignerConfig`** — already drives front-end GrapesJS plugin loading
- **MediatR** — CQRS handlers are natural hook points for plugins to inject behavior
- **`IDomainEvent` / `IDomainEventHandler<T>`** — plugins can react to publish, save, trash, etc.
- **`IDynamicConfigurationProvider`** — per-tenant context is available at request time
- **`IReservedPaths`** — existing service that prevents content editors from creating pages at system paths
- **`IStartupTaskService`** — plugins can register initialization work

---

## Decision

We will build a **three-layer plugin system** where each plugin is a **.NET Razor Class Library (RCL)** distributed as a NuGet package.

### Why Razor Class Libraries?

RCLs are the established ASP.NET Core pattern for distributable UI components. They can contain Razor Pages, MVC/API controllers, Tag Helpers, View Components, and static assets (`wwwroot/`), all compiled into a single DLL. `Microsoft.AspNetCore.Identity.UI` (the default Identity scaffolding in every ASP.NET Core project) is itself an RCL — the pattern is well understood by the .NET developer community. ASP.NET Core's `ApplicationPart` mechanism discovers all pages, controllers, and assets from a loaded RCL assembly automatically.

---

### Layer 1 — Server-Side Plugin Contract (`ISkyCmsPlugin`)

Defined in a new `Sky.Editor.Abstractions` project, this interface is the entry point for every plugin:

```csharp
public interface ISkyCmsPlugin
{
    string PluginId { get; }        // unique reverse-DNS slug, e.g. "acme.seo-analyzer"
    string DisplayName { get; }
    string Version { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    IEnumerable<PluginNavItem> GetNavItems(ClaimsPrincipal user);
}
```

`PluginNavItem` is a simple value object:

```csharp
public record PluginNavItem(
    string Label,
    string Url,
    string? RequiredRole = null,
    IEnumerable<PluginNavItem>? SubItems = null
);
```

Fault isolation is required: any exception thrown by a plugin's `ConfigureServices` or `GetNavItems` must be caught, logged, and skipped. A single failing plugin must never prevent other plugins or the host from starting.

### Layer 2 — Front-End / Designer Contribution

Each plugin may implement `IDesignerContributor` to inject `DesignerPlugin` descriptors into the GrapesJS config at render time:

```csharp
public interface IDesignerContributor
{
    IEnumerable<DesignerPlugin> GetDesignerPlugins(Layout layout);
}
```

`DesignerConfig` will query all registered `IDesignerContributor` instances, replacing the current hard-coded Bootstrap/Tailwind detection with a contributor pipeline. The existing Bootstrap/Tailwind detector becomes the first built-in `IDesignerContributor`.

### Layer 3 — Navigation Contribution (Plugins Dropdown)

Plugins surface UI entry points through `GetNavItems()`. The `_CosmosMainMenuPartial.cshtml` renders a single **"Plugins" top-level dropdown** in the navbar. The dropdown is hidden entirely when no plugins have items visible to the current user. Each plugin appears as a submenu entry; if a plugin returns multiple `SubItems`, Bootstrap 5 renders them as a nested submenu. Bootstrap 5 does not include native submenu support and requires a small CSS addition to enable hover-triggered submenus.

### Plugin Manifest (`sky-plugin.json`)

Every plugin NuGet package **must** include a `sky-plugin.json` file in its `contentFiles`. The loader verifies this file before attempting to load the assembly. This prevents accidental installation of arbitrary NuGet packages and provides machine-readable metadata:

```json
{
  "pluginId": "acme.seo-analyzer",
  "displayName": "SEO Analyzer",
  "version": "1.0.0",
  "minHostVersion": "1.0.0",
  "requiredRole": "Administrators"
}
```

### Reserved Route Path

All plugin-provided Razor Pages and API endpoints must be rooted under `/_sky/{pluginId}/`. Examples:

- `/_sky/seo-analyzer/` — plugin index page
- `/_sky/seo-analyzer/results` — sub-page
- `/_sky/seo-analyzer/api/analyze` — API endpoint inside a plugin

The `/_sky/` prefix is registered in `IReservedPaths` at startup, blocking content editors from ever creating CMS pages at that path. The leading underscore is the near-universal convention for system-reserved paths (see Umbraco's `/_umbraco/`, Orchard Core's `/OrchardCore/`).

### Multi-Tenancy

Plugin *enablement* is per-tenant, stored in the existing tenant settings table. The `IDynamicConfigurationProvider` is queried at request time to resolve the active plugin set for the current tenant. Plugins that are installed but disabled for a given tenant are skipped at the service-resolution boundary.

### Distribution

| Scope | Distribution model |
|---|---|
| Back-end + UI (services, Razor Pages, API controllers) | NuGet package (RCL) referencing `Sky.Editor.Abstractions` |
| Front-end only (JS/CSS + GrapesJS blocks) | DB-configured URL entries (no redeploy needed) |
| Runtime install without redeploy | `NuGet.Protocol`-based download at startup (see ADR 0003) |

---

## Consequences

### Positive
- Third parties can add capabilities without forking core.
- RCL is a well-known .NET standard; plugin authors need no SkyCMS-specific knowledge beyond `ISkyCmsPlugin`.
- The existing `DesignerPlugin` / `DesignerConfig` path becomes officially supported and extensible.
- Domain-event hooks (`IDomainEventHandler<T>`) mean plugins react to CMS lifecycle events with zero coupling to core controllers.
- `sky-plugin.json` provides a verifiable identity check before any assembly is loaded.
- All plugin routes live under `/_sky/`, making them trivially distinguishable from CMS content URLs.

### Negative / Trade-offs
- Bootstrap 5 requires a CSS workaround for nested submenus (no native support).
- Plugins run in-process with full application trust — a malicious or severely buggy plugin has access to the full server process. Mitigations are documented in ADR 0003.
- Plugin authors must depend on `Sky.Editor.Abstractions`; any breaking change there is a semver-major event. The interface must be versioned from day one.
- No `AssemblyLoadContext` isolation in v1 — dependency conflicts between plugins or between a plugin and the host will surface as load failures (see ADR 0003 for handling).

### Neutral / Follow-on
- `ISettingsPanelContributor` (per-plugin settings Razor Page) is deferred to a later phase.
- A curated SkyCMS plugin marketplace is a future enhancement (noted in ADR 0002).
- The Copilot proxy feature (`Features/Copilot/`) is a candidate to become the first validated internal plugin (Phase 5 in ADR 0002).
