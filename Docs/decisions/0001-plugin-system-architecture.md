# 0001 — Plugin System Architecture for Sky.Editor

**Status:** Proposed  
**Date:** 2025-01-01  
**Deciders:** SkyCMS core team  
**Branch:** `feature/plugin-system`

---

## Context

Sky.Editor is a multi-tenant CMS with a growing surface area. Features like the Copilot proxy, CDN integrations, and the GrapesJS designer plugin registry (`DesignerPlugin` / `DesignerConfig`) are already partially isolated, but there is no formal extension point. As the platform grows, third parties (and internal teams) need a structured way to add new capabilities — UI panels, back-end services, domain-event reactions, and front-end GrapesJS blocks — without modifying core editor code.

The existing codebase provides useful building blocks:
- **`DesignerPlugin` / `DesignerConfig`** — already drives front-end GrapesJS plugin loading
- **MediatR** — CQRS handlers are natural hook points for plugins to inject behavior
- **`IDomainEvent` / `IDomainEventHandler<T>`** — plugins can react to publish, save, trash, etc.
- **`IDynamicConfigurationProvider`** — per-tenant context is available at request time
- **`IStartupTaskService`** — plugins can register initialization work

---

## Decision

We will build a **three-layer, hybrid plugin system**:

### Layer 1 — Server-Side Plugin Contract (`ISkyCmsPlugin`)

A new interface, defined in `Sky.Cms.Api.Shared` (or a new `Sky.Editor.Abstractions` project), serves as the entry point for every plugin:

```csharp
public interface ISkyCmsPlugin
{
    string PluginId { get; }        // unique reverse-DNS slug, e.g. "acme.seo-analyzer"
    string DisplayName { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
```

Plugins are registered in `Program.cs` via a discoverable extension:

```csharp
builder.Services.AddSkyCmsPlugin<MySeoPlugin>();
```

An optional assembly-scanning helper will also be provided for drop-in plugin scenarios.

### Layer 2 — Front-End / Designer Contribution

Each plugin may also implement `IDesignerContributor` to inject `DesignerPlugin` descriptors into the GrapesJS config at render time:

```csharp
public interface IDesignerContributor
{
    IEnumerable<DesignerPlugin> GetDesignerPlugins(Layout layout);
}
```

`DesignerConfig` will query all registered `IDesignerContributor` instances, replacing the current hard-coded Bootstrap/Tailwind detection with a contributor pipeline.

### Layer 3 — UI / Navigation Contribution

Plugins may implement optional contribution interfaces to extend the editor shell:

```csharp
public interface INavContributor
{
    IEnumerable<NavItem> GetNavItems(ClaimsPrincipal user);
}
```

Navigation items contributed by plugins are rendered in the editor sidebar via a partial view that iterates registered `INavContributor` services.

### Multi-Tenancy

Plugin *enablement* is per-tenant, stored in the existing tenant settings table (or a JSON column extension). The `IDynamicConfigurationProvider` is queried at request time to resolve the active plugin set for the current tenant. Plugins that are registered but disabled for the current tenant are skipped at the service-resolution boundary.

### Distribution

| Scope | Distribution model |
|---|---|
| Back-end (services, domain event handlers) | NuGet package referencing `Sky.Editor.Abstractions` |
| Front-end only (JS/CSS + GrapesJS blocks) | DB-configured URL entries (no redeploy needed) |

---

## Consequences

### Positive
- Third parties can add capabilities without forking core.
- The existing `DesignerPlugin` / `DesignerConfig` path becomes officially supported and extensible.
- Domain-event hooks (`IDomainEventHandler<T>`) mean plugins react to CMS lifecycle events with zero coupling to core controllers.
- Multi-tenant enable/disable means different tenants can have different feature sets from the same deployment.

### Negative / Trade-offs
- Server-side plugins require a redeploy when added or updated (NuGet model).
- Plugin authors must depend on `Sky.Editor.Abstractions`; any breaking change there is a semver-major event.
- Per-tenant plugin enable/disable adds a DB query on the hot path (mitigated by existing caching via `IDynamicConfigurationProvider`).

### Neutral / Follow-on
- The Copilot proxy feature (`Features/Copilot/`) is a good candidate to validate the abstraction as the first "internal plugin" in Phase 3 (see [0002](0002-plugin-system-phases.md)).
- A plugin developer guide should be added to `Docs/` once the interface stabilizes.
- `ISettingsPanelContributor` (a Razor Page route per plugin) is deferred to a later phase.
