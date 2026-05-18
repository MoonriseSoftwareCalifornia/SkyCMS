# Architecture & Design Decision Records (ADRs)

SkyCMS uses Architecture & Design Decision Records (ADRs) to document
significant decisions about the platform’s architecture, UX, naming,
and long-term direction.

ADRs exist to preserve **intent**, not just outcomes.

## Why ADRs Exist in SkyCMS

Over time, platforms evolve and contributors change. Without a written
record of *why* decisions were made, teams are forced to rediscover
context through code archaeology or repeated debate.

In SkyCMS, ADRs serve as:

- a historical record of important decisions,
- guidance for future contributors,
- and a safeguard against accidental conceptual drift.

## When to Create an ADR

An ADR should be created when a decision:

- affects UX patterns, naming, or conceptual models,
- introduces or changes architectural direction,
- establishes a standard or convention,
- or is likely to be questioned or revisited in the future.

Not every change requires an ADR. Small, localized implementation details
generally do not.

## SkyCMS Philosophy: Be Thorough, Not Brief

SkyCMS favors **complete and thoughtful ADRs** over minimal or terse ones.

ADR authors are encouraged to:

- document meaningful context,
- explain trade-offs and alternatives,
- and capture intent for future developers who were not present.

Clarity for the future is more important than brevity in the present.

## How to Create an ADR

1. Copy `ADR-TEMPLATE.md`
2. Rename it using the next available number:
   `000X-short-decision-title.md`
3. Fill out all relevant sections
4. Submit the ADR via a pull request
5. Once accepted, set the status to `Accepted`

ADRs should be added intentionally and reviewed carefully.

## Current ADRs

- [0001: Editor Naming and Icon Standards](0001-editor-naming-and-icon-standards.md)
- [0002: Tenant Resolution and Domain Context Establishment](0002-tenant-resolution-and-domain-context-establishment.md)
- [0003: Editor Deployment Mode Split (Single-Tenant vs Multi-Tenant)](0003-editor-deployment-mode-single-tenant-vs-multi-tenant.md)
- [0004: EF Cross-Provider Cosmos-Safe Query Contract](0004-ef-cross-provider-cosmos-safe-query-contract.md)
- [0005: Database Provider Auto-Detection Strategy](0005-database-provider-auto-detection-strategy.md)
- [0006: Publisher Operating Modes (Dynamic vs Static)](0006-publisher-operating-modes-dynamic-vs-static.md)
- [0007: Content Delivery Path Segregation](0007-content-delivery-path-segregation.md)
- [0008: Cookie Domain Isolation for Multi-Tenant Authentication](0008-cookie-domain-isolation-for-multi-tenant-auth.md)
- [0009: CQRS with Custom Mediator and Vertical Slices](0009-cqrs-with-custom-mediator-and-vertical-slices.md)
- [0010: SignalR Tenant Isolation and Scoped Progress Reporting](0010-signalr-tenant-isolation-and-scoped-progress-reporting.md)
- [0011: Policy-Based Rate Limiting Architecture](0011-policy-based-rate-limiting-architecture.md)
- [0012: Setup Wizard Dual-Flow Architecture](0012-setup-wizard-dual-flow-architecture.md)
- [0013: Passkey RP ID Strategy for Single and Multi-Tenant Deployments](0013-passkey-rp-id-strategy-for-single-and-multi-tenant.md)
- [0014: Conditional OAuth Provider Registration Strategy](0014-conditional-oauth-provider-registration-strategy.md)
- [0015: Startup Migration Orchestration for Schema and Data](0015-startup-migration-orchestration-schema-and-data.md)
- [0016: Tenant-Scoped Caching Lifecycle Strategy](0016-tenant-scoped-caching-lifecycle-strategy.md)
- [0017: Storage Provider Auto-Detection by Connection Pattern](0017-storage-provider-auto-detection-by-connection-pattern.md)
- [0018: Early Configuration Validation and Diagnostic-Only Startup Mode](0018-early-configuration-validation-and-diagnostic-only-mode.md)
- [0019: Health Probe Endpoint Exemptions in Setup Middleware](0019-health-probe-endpoint-exemptions-in-setup-middleware.md)
- [0020: ApplicationDbContext Abstraction for Tenant-Aware Data Access](0020-applicationdbcontext-abstraction-for-tenant-aware-data-access.md)
- [0021: Docs Import Pipeline with Idempotent Hash Tracking](0021-docs-import-pipeline-with-idempotent-hash-tracking.md)
- [0022: Dynamic Configuration Provider Singleton with Proxy-Aware Domain Resolution](0022-dynamic-configuration-provider-singleton-with-proxy-aware-domain-resolution.md)
- [0023: Dynamic Email Provider Resolution with NoOp Fallback](0023-dynamic-email-provider-resolution-with-noop-fallback.md)
- [0024: Editor-Publisher Separation with Shared Data and Storage](0024-editor-publisher-separation-with-shared-data-and-storage.md)
- [0025: Setup Detection Middleware Cache and Access-Control Strategy](0025-setup-detection-middleware-cache-and-access-control-strategy.md)
- [0026: Cookie and Transport Security Defaults](0026-cookie-and-transport-security-defaults.md)
- [0027: Proxy Forwarding and Trusted Header Strategy](0027-proxy-forwarding-and-trusted-header-strategy.md)
- [0028: Domain Validation Fail-Open Availability Posture](0028-domain-validation-fail-open-availability-posture.md)
- [0029: Middleware Ordering Contract for Tenant and Security Correctness](0029-middleware-ordering-contract-for-tenant-and-security-correctness.md)
- [0030: CloudFront Proto Header Normalization Strategy](0030-cloudfront-proto-header-normalization-strategy.md)
- [0031: Setup Enablement Gate via CosmosAllowSetup](0031-setup-enablement-gate-via-cosmosallowsetup.md)
- [0032: Environment-Specific Error Handling Strategy](0032-environment-specific-error-handling-strategy.md)
- [0033: Antiforgery Token Bootstrap Endpoint Pattern](0033-antiforgery-token-bootstrap-endpoint-pattern.md)
- [0034: Domain Events and Setup Audit Log Observability Model](0034-domain-events-and-setup-audit-log-observability-model.md)
- [0035: File Explorer Modernization and Connector Adapter Strategy](0035-file-explorer-modernization-and-connector-adapter-strategy.md)
- [0036: Layout Terminology Standardization and Documentation Strategy](0036-layout-terminology-standardization-and-documentation-strategy.md)
- [0037: Article Lifecycle and Status Code Semantics](0037-article-lifecycle-and-status-code-semantics.md)
- [0038: Article Trash and Permanent Delete Lifecycle](0038-article-trash-and-permanent-delete-lifecycle.md)
- [0039: DRY Controller Unification: File Manager and VS Code Explorer](0039-dry-controller-unification-file-manager-and-vscode.md)

## Changing or Superseding ADRs

ADRs are not deleted.

If a decision must change:

- create a new ADR that supersedes the existing one,
- and reference the older ADR explicitly.

This preserves the historical record and makes change intentional.

## What Does Not Require an ADR

An ADR should NOT be created for:

- Routine bug fixes
- Minor refactors limited to a single module
- UI tweaks that do not establish new patterns
- Purely internal implementation optimizations
- Changes that can be trivially reversed
- Decisions whose impact is strictly local and short‑lived

As a rule of thumb:

> If future developers will not ask “why was this done this way?”  
> an ADR is probably not needed.
