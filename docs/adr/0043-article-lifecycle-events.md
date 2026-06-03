# ADR 0043: Article Lifecycle Events and Catalog/Asset Side-Effects

## Status

Proposed

## Context

ADR 0037, ADR 0038, and ADR 0042 define core lifecycle semantics, including soft-delete behavior and `ArticleCatalog` alignment. During implementation, lifecycle behavior became distributed across handlers/services (`Save`, `Publish`, `DeleteArticle`, `RestoreArticle`, `TrashArticle`, title and template workflows).

This ADR provides one canonical event-level reference so contributors can reason about state transitions and side-effects consistently across providers (SQL Server, MySQL, SQLite, Cosmos DB).

## Decision

SkyCMS lifecycle behavior is documented by event with explicit before/after contracts for:

- `Article.StatusCode`
- `Article.Published`
- `CatalogEntry` row behavior
- Blob asset behavior
- Static HTML artifact behavior
- Downstream notifications/projection updates
- Reversibility

## Event Matrix

### 1) Create Article

- **Trigger**: User creates a new article in Editor.
- **StatusCode**: `N/A -> Active` (new article row).
- **Published**: `null -> null` (not published on create by default).
- **CatalogEntry**: Created/upserted for the article number.
- **Blob assets**: No forced changes.
- **Static HTML artifact**: Not guaranteed until publish/static generation flow.
- **Downstream notifications**: Catalog projection updated.
- **Reversible**: Yes, via delete/trash workflows.

### 2) Save/Edit Article (including new version creation)

- **Trigger**: User edits content/properties and saves.
- **StatusCode**: Typically remains `Active` for working versions; version row changes may occur.
- **Published**: Unchanged for the edited draft/version unless publish action occurs.
- **CatalogEntry**: Upserted to reflect latest lifecycle-faithful metadata (title/path/status/etc.).
- **Blob assets**: Preserved.
- **Static HTML artifact**: Unchanged unless publish/static write occurs.
- **Downstream notifications**: Catalog projection refreshed.
- **Reversible**: Yes, subsequent save/publish/delete actions may alter visible state.

### 3) Publish

- **Trigger**: User publishes an article/version now.
- **StatusCode**: Remains lifecycle-appropriate (`Active` for publishable content).
- **Published**: Set to publish timestamp on published version; previously published version for same article number is cleared where applicable.
- **CatalogEntry**: Upserted; `Published` reflects publish timestamp for Active content.
- **Blob assets**: Preserved.
- **Static HTML artifact**: Written/updated when static publishing mode is enabled.
- **Downstream notifications**: TOC/site output pipeline updated (for example, publish service TOC write).
- **Reversible**: Partially; can unpublish or soft-delete.

### 4) Schedule (future publish)

- **Trigger**: User schedules publication for a future time.
- **StatusCode**: Remains current lifecycle state until schedule executes.
- **Published**: Typically remains `null` until scheduled execution publishes.
- **CatalogEntry**: Upserted as needed for metadata changes; publish visibility follows actual publish execution.
- **Blob assets**: Preserved.
- **Static HTML artifact**: Created/updated only when publish actually occurs.
- **Downstream notifications**: Scheduler pipeline enqueues and later triggers publish flow.
- **Reversible**: Yes, via schedule update/cancel and regular lifecycle actions.

### 5) Unpublish

- **Trigger**: User unpublishes currently published content.
- **StatusCode**: Usually remains `Active` unless additional lifecycle action is applied.
- **Published**: Cleared (`timestamp -> null`).
- **CatalogEntry**: Upserted with `Published = null` while preserving row.
- **Blob assets**: Preserved.
- **Static HTML artifact**: Removed/invalidated according to publishing mode and unpublish implementation path.
- **Downstream notifications**: TOC/site output updated.
- **Reversible**: Yes, by republishing.

### 6) Soft-delete (Move to Trash)

- **Trigger**: User trashes an article (`DeleteArticleHandler`).
- **StatusCode**: `Active/Inactive -> Deleted` for **all versions** of the article number.
- **Published**: Cleared for deleted versions (catalog projection also enforces non-public deleted state).
- **CatalogEntry**: **Preserved** and upserted with `StatusCode = Deleted` (row is not removed).
- **Blob assets**: Preserved to allow recovery.
- **Static HTML artifact**: Deleted in static mode (if path is valid and allowed).
- **Downstream notifications**: TOC/site output updated after delete flow.
- **Reversible**: Yes, via restore.

### 7) Restore from Trash

- **Trigger**: User restores trashed article (`RestoreArticleHandler`).
- **StatusCode**: `Deleted -> Active` for **all versions**.
- **Published**: Cleared to `null` on restore (restored content is not auto-republished).
- **CatalogEntry**: Preserved and upserted back to active lifecycle state.
- **Blob assets**: Preserved (already retained during trash).
- **Static HTML artifact**: Not automatically republished by restore alone; publish action may be required depending on mode.
- **Downstream notifications**: Catalog projection refreshed; standard publish outputs update when republished.
- **Reversible**: Yes, can be trashed again.

### 8) Permanent Delete (Trash handler hard delete)

- **Trigger**: User permanently deletes trashed article (`TrashArticleHandler` flow).
- **StatusCode**: Article rows removed permanently.
- **Published**: No longer applicable after removal.
- **CatalogEntry**: Removed permanently for that article number.
- **Blob assets**: Removed as part of permanent deletion policy.
- **Static HTML artifact**: Removed if present.
- **Downstream notifications**: TOC/site output updated to exclude removed content.
- **Reversible**: No (except external backup restore).

### 9) URL Redirect Assignment

- **Trigger**: User/flow marks article/route as redirect target/state.
- **StatusCode**: Transition to `Redirect` where applicable.
- **Published**: Forced to `null` in catalog projection for redirect state.
- **CatalogEntry**: Preserved/upserted with `StatusCode = Redirect` and non-public published semantics.
- **Blob assets**: Preserved unless separate cleanup flow runs.
- **Static HTML artifact**: May be removed or replaced by redirect behavior depending on publishing mode.
- **Downstream notifications**: Routing/navigation outputs updated to respect redirect semantics.
- **Reversible**: Yes, by changing lifecycle state/path rules.

### 10) Title Change

- **Trigger**: User renames article title.
- **StatusCode**: Usually unchanged.
- **Published**: Usually unchanged by title change alone.
- **CatalogEntry**: Upserted with new title and related metadata/path adjustments.
- **Blob assets**: Preserved.
- **Static HTML artifact**: May be regenerated on publish if URL/path changes affect output location.
- **Downstream notifications**: Catalog-based readers and title conflict checks observe updated title.
- **Reversible**: Yes, by editing title again.

### 11) Template Change

- **Trigger**: User assigns a different template to an article.
- **StatusCode**: Unchanged.
- **Published**: Unchanged until publish operation writes output.
- **CatalogEntry**: Upserted with updated `TemplateId` and latest projection fields.
- **Blob assets**: Preserved.
- **Static HTML artifact**: Updated on next publish output generation.
- **Downstream notifications**: Template usage/count queries rely on lifecycle-aware catalog filtering.
- **Reversible**: Yes, template can be changed again.

## Cross-Cutting Invariants

1. `ArticleCatalog` is one-row-per-article-number read model.
2. Soft-delete preserves `CatalogEntry`; permanent delete removes it.
3. `CatalogEntry.Published` must be `null` for `Deleted` and `Redirect` statuses.
4. Lifecycle-aware consumers must filter by `StatusCode` explicitly for public/live views.
5. Blob retention differs by delete type:
   - soft-delete: retain
   - permanent delete: remove

## Consequences

### Positive

- Shared lifecycle contract reduces drift between handlers/services/tests.
- Easier onboarding and safer future refactors.
- Aligns cross-provider behavior expectations for Cosmos and relational providers.

### Ongoing Responsibilities

- Keep this ADR updated when lifecycle handlers add or change side-effects.
- Ensure tests cover any changed event contract.

## Related ADRs

- ADR 0004: EF Cross-Provider Cosmos-Safe Query Contract
- ADR 0037: Article Lifecycle and Status Code Semantics
- ADR 0038: Article Trash and Permanent Delete Lifecycle
- ADR 0042: ArticleCatalog Lifecycle Read-Model Alignment for Soft Delete
