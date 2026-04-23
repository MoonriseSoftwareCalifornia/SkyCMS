# ADR 0021: Docs Import Pipeline with Idempotent Hash Tracking

## Status

Accepted

## Context

SkyCMS supports importing external documentation content into CMS-managed articles.
The import flow must be repeatable, automation-friendly, and safe to run on frequent
content updates without reprocessing unchanged files.

A pipeline architecture was needed to convert markdown + front matter into structured
import payloads while preserving idempotency and operational simplicity.

## Design Goals

This decision aims to:

1. Enable automated documentation ingestion into SkyCMS
2. Preserve idempotent behavior across repeated imports
3. Parse and map markdown/front matter into explicit CMS payload structures
4. Support CI/CD-driven documentation publishing workflows
5. Keep import behavior auditable and scriptable

## Non-Goals

This decision does not attempt to:

- Replace full editorial workflows for all content types
- Define all article templating conventions in this ADR
- Eliminate need for source-repo content governance
- Guarantee zero-failure imports under all network/runtime conditions

## Decision

SkyCMS documentation import architecture uses a scripted markdown ingestion pipeline that:

- Reads markdown sources and YAML front matter
- Generates structured API payloads for docs import endpoints
- Tracks source hashes in an import map for idempotent reruns
- Integrates with automated workflows for recurring sync

Only changed content is re-imported based on hash-state tracking.

## Detailed Rationale

### Repeatable Automation-Friendly Content Sync

Hash-based state tracking prevents unnecessary re-imports and supports frequent CI-driven
runs.

### Clear Source-to-CMS Transformation

Front matter and markdown parsing provide deterministic mapping from docs repos into
SkyCMS import API payloads.

### Operational Simplicity

Scripted workflows are easier to reason about and troubleshoot than opaque import tooling.

## Alternatives Considered

### Full Reimport on Every Run

Rejected because it increases runtime cost, risk, and noise for unchanged content.

### Manual Import-Only Workflows

Rejected because they do not scale for active documentation repos.

### Tight Coupling of Docs Source and SkyCMS Runtime Repo

Rejected because independent docs repositories and CI flows are a deliberate capability.

## Consequences

### Positive Outcomes

- Efficient incremental docs publishing
- Clear import state tracking and rerun behavior
- Better fit for CI/CD documentation pipelines

### Constraints Introduced

- Hash-map integrity becomes important to import correctness
- Parser and mapping scripts must be maintained with schema changes
- Import API and script contracts should be version-aware over time

## Evidence

- Docs import script and hash-map behavior:
  - SkyCMS.DocsPublisher/.skycms/scripts/import-docs.js
- Template-repo documentation of import architecture:
  - SkyCMS.DocsPublisher/README.md
- SkyCMS docs for DocsPublisher integration:
  - SkyCMS.Docs/installation/docs-publisher.md
