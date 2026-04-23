# ADR 0004: EF Cross-Provider Cosmos-Safe Query Contract

## Status
Accepted

## Context

SkyCMS intentionally supports multiple database providers from one codebase:
MS SQL Server, MySQL, SQLite, and Azure Cosmos DB.

This creates an architectural constraint: query and data-access patterns must remain valid
across all supported providers. Cosmos DB is the most restrictive EF Core provider in this
set, especially around translation of LINQ features that are common in relational systems.

Without a shared cross-provider contract, contributors could introduce provider-specific
queries that pass in one environment and fail in another, causing runtime regressions and
fragmented behavior.

## Design Goals

This decision aims to:

1. Preserve one shared EF query code path across all supported providers
2. Prevent provider-specific regressions from entering common application code
3. Make Cosmos compatibility requirements explicit and teachable
4. Keep contributor guidance consistent with runtime behavior
5. Reduce hidden coupling to relational-only query features

## Non-Goals

This decision does not attempt to:

- Eliminate provider-specific optimizations in isolated, guarded code paths
- Ban all provider checks where technically necessary
- Replace Entity Framework Core with provider-specific data layers
- Define migration policy details for every provider in this ADR

## Decision

SkyCMS adopts a Cosmos-safe query contract for shared EF code:

- Do not use cross-entity joins that require cross-container translation.
- Do not use inline casts inside LINQ expressions (for example enum-to-int casts).
- Pre-compute cast values into local variables before using them in predicates.
- Prefer sequential queries with in-memory correlation when a join would be required.

This contract is treated as solution-wide guidance for application and shared library code
intended to remain cross-provider compatible.

## Detailed Rationale

### Lowest Common Denominator as Stability Strategy

Because SkyCMS supports both relational and Cosmos providers, compatibility is strongest
when shared query code targets the strictest provider constraints.

### Predictable Cross-Environment Behavior

By codifying forbidden patterns, teams avoid deployment-time surprises where a query works
locally on one provider but fails for another tenant/provider combination.

### Maintainable Contributor Rules

The contract is simple enough to enforce in code review and documentation, making long-term
maintenance and onboarding easier.

## Alternatives Considered

### Provider-Specific Query Forks Everywhere

Rejected because it creates high maintenance overhead and increases the risk of behavioral
drift between providers.

### Relational-First Query Model with Cosmos Exceptions

Rejected because exception-heavy patches are reactive and fragile, leading to recurring
runtime incompatibilities.

### Cosmos-Only Data Layer

Rejected because SkyCMS intentionally supports relational providers for deployment
flexibility.

## Consequences

### Positive Outcomes

- Higher confidence that shared queries run across all supported providers
- Clear contributor expectations for safe LINQ patterns
- Fewer cross-provider runtime translation failures
- Better alignment between architecture docs and coding standards

### Constraints Introduced

- Some relationally convenient query patterns are disallowed in shared code
- Additional in-memory correlation may be required for some query flows
- Code reviews must explicitly check cross-provider query compatibility

## Evidence

- Cross-provider guidance and examples:
  - SkyCMS.Docs/for-developers/ef-cross-provider-guide.md
- Provider strategy architecture used by EF configuration:
  - AspNetCore.Identity.FlexDb/CosmosDbOptionsBuilder.cs
- Repository-level contributor rules enforcing this contract:
  - .github/copilot-instructions.md
