# ADR 0018: Early Configuration Validation and Diagnostic-Only Startup Mode

## Status

Accepted

## Context

SkyCMS startup depends on correct configuration for database, storage, and core settings.
When configuration is invalid, allowing normal startup can produce cascading failures,
unclear errors, and poor operator experience.

A controlled fallback mode was needed to surface actionable diagnostics without running
full application behavior under invalid configuration conditions.

## Design Goals

This decision aims to:

1. Validate critical configuration before full runtime startup
2. Provide a safe fallback mode when validation fails
3. Expose clear diagnostics for operators and developers
4. Avoid partial startup behavior under invalid configuration
5. Keep diagnostic mode explicit and intentional

## Non-Goals

This decision does not attempt to:

- Replace all runtime health monitoring
- Guarantee automatic configuration remediation
- Eliminate all startup exceptions in every failure mode
- Replace deployment validation pipelines

## Decision

SkyCMS performs early configuration validation through ConfigurationValidator when
diagnostic mode is enabled. If configuration is invalid, startup enters a
Diagnostic-Only mode that serves a dedicated diagnostics route and bypasses normal
application startup paths.

This mode intentionally registers a minimal service pipeline and redirects requests
toward diagnostics content.

## Detailed Rationale

### Fail Safe, Explain Clearly

Diagnostic-only fallback avoids running a broken full pipeline while still giving
operators immediate visibility into root causes.

### Reduced Troubleshooting Time

Structured checks for core settings and connectivity provide clearer recovery steps
than deferred runtime failures.

### Explicit Startup Branching

Keeping this behavior in startup composition makes operational behavior auditable.

## Alternatives Considered

### Always Attempt Full Startup Then Surface Errors In Logs

Rejected because operators can face noisy failures and unclear fault boundaries.

### Hard Fail Without Diagnostic Surface

Rejected because it slows troubleshooting, especially in remote deployment contexts.

### External-Only Diagnostics Tooling

Rejected because built-in diagnostics are valuable where external tooling is unavailable.

## Consequences

### Positive Outcomes

- Faster diagnosis of configuration issues
- Safer startup behavior under invalid configuration
- Clear operational path to remediation

### Constraints Introduced

- Diagnostic mode must remain maintained as startup evolves
- Teams must understand when diagnostic mode is enabled
- Minimal pipeline in diagnostic mode intentionally limits normal app behavior

## Evidence

- Early validation and diagnostic-only startup branch:
  - Editor/Program.cs
- Configuration validation implementation:
  - Editor/Services/Diagnostics/ConfigurationValidator.cs
