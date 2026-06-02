# ADR 0042: ArticleCatalog Lifecycle Read-Model Alignment for Soft Delete

## Status

Proposed

## Context

ADR 0037 documents article lifecycle semantics and currently states that soft delete (`StatusCode = Deleted`) removes the `ArticleCatalog` entry.

That behavior is correct for hiding deleted content from public and editor listing surfaces, but it creates a scaling problem for cross-provider querying (especially Cosmos DB) in editor-only workflows that need one-row-per-article summaries across active and deleted items.

Today, some editor workflows (for example, article file-surface metadata resolution and status summary queries) read from `Articles`, which stores many versions per `ArticleNumber`. In environments where each logical article may have dozens or hundreds of versions, this increases payload size, memory pressure, and RU consumption for Cosmos DB.

`ArticleCatalog` already exists as a denormalized per-article read model and is widely consumed by public and editor query handlers. However, current write semantics are not fully lifecycle-faithful:

- soft delete removes catalog rows,
- status mapping is lossy/inconsistent in some paths,
- some handlers manually write catalog rows instead of using a single projection service.

## Design Goals

1. Preserve one-row-per-article read-model behavior for cross-provider efficiency.
2. Keep deleted content hidden from public and editor surfaces unless explicitly requested.
3. Make lifecycle state representation canonical and consistent in `ArticleCatalog`.
4. Centralize catalog projection/writes to avoid drift across handlers.
5. Maintain backward compatibility for existing consumers during transition.

## Non-Goals

- This ADR does not redesign the full article lifecycle state machine.
- This ADR does not change `Articles` versioning behavior.
- This ADR does not introduce provider-specific data access contracts for consumers.
- This ADR does not remove the legacy `CatalogEntry.Status` string immediately.

## Decision

SkyCMS will treat `ArticleCatalog` as a lifecycle-aware read model and retain catalog rows for soft-deleted articles.

Specifically:

1. Soft delete will no longer remove the catalog row.
2. Permanent trash will continue to remove the catalog row.
3. `CatalogEntry` will store canonical lifecycle state via numeric `StatusCode` aligned to `StatusCodeEnum`.
4. Existing string `Status` will be retained temporarily for compatibility and kept aligned during writes.
5. Public/editor listing consumers that should hide deleted/redirect content must apply lifecycle filtering (`StatusCode == Active` unless explicitly querying deleted state).
6. Catalog writes will be centralized through catalog services/handlers rather than ad hoc row construction.

This ADR supersedes the ADR 0037 statement that soft delete removes `ArticleCatalog` entries.

## Detailed Rationale

### Why keep catalog rows for soft delete

For read scenarios that require per-article metadata (title/status/path), a dedicated one-row-per-article store is more efficient than scanning versioned `Articles` rows. This is especially important for Cosmos DB where cross-partition scans over high-version histories increase RU and payload cost.

### Why explicit lifecycle filtering is required

Retaining deleted rows in catalog introduces a correctness risk if existing queries assume row absence equals non-visibility. Therefore, queries that should surface only live content must explicitly filter by lifecycle.

### Why introduce numeric `StatusCode` in catalog

String status labels are ambiguous and prone to drift. Numeric `StatusCode` aligned to `StatusCodeEnum` gives canonical semantics and enables provider-safe filtering.

### Why keep `Status` string during transition

Some existing code and tests may still read `Status`. Keeping it temporarily avoids a breaking migration and allows phased cleanup.

## Alternatives Considered

### A) Keep current behavior (delete catalog row on soft delete)

Rejected. It preserves old semantics but forces several editor workflows to derive per-article summaries from heavily versioned `Articles`, which is less efficient for Cosmos and high-version datasets.

### B) Add a brand-new summary container/table separate from `ArticleCatalog`

Rejected for now. Functionally valid, but introduces duplicate denormalized models and higher migration complexity while `ArticleCatalog` already fulfills most requirements.

### C) Keep string-only status in catalog

Rejected. It is insufficiently canonical for lifecycle filtering and prone to inconsistent mappings.

## Consequences

### Positive

- Better cross-provider read efficiency for article summary/listing scenarios.
- Cleaner lifecycle semantics in `ArticleCatalog`.
- Reduced risk of write-path drift via centralized projection.

### Required follow-up safeguards

- Add lifecycle filters in consumers that must exclude deleted/redirect rows.
- Update soft delete/restore handlers to use centralized catalog upsert semantics.
- Add migration/backfill for `CatalogEntry.StatusCode`.
- Expand regression tests for delete/restore/trash and downstream readers.

### Risks

- If a consumer omits lifecycle filtering, deleted rows may be surfaced.
- Mixed old/new records during transition require compatibility handling.

## Implementation Notes (summary)

- Add `CatalogEntry.StatusCode` and migrate schema.
- Align catalog writes in `CatalogService` and lifecycle handlers.
- Keep permanent trash hard-delete behavior unchanged.
- Update downstream query/filter logic where visibility assumptions relied on row absence.

## Supersedes

- ADR 0037 section describing soft delete removal of `ArticleCatalog` rows.

## Related ADRs

- ADR 0004: EF Cross-Provider Cosmos-Safe Query Contract
- ADR 0037: Article Lifecycle and Status Code Semantics
- ADR 0038: Article Trash and Permanent Delete Lifecycle
- ADR 0041: Trashed Article Assets Must Be Hidden and Inaccessible in Editor File Surfaces
