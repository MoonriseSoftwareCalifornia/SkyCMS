# TEMP Tracker: ADR 0042 Implementation (ArticleCatalog Lifecycle Read-Model Alignment)

> Temporary execution tracker for branch-level implementation work.
> Remove this file after implementation is complete and validated.

## Scope

Implement ADR 0042 without breaking downstream `ArticleCatalog` consumers.

## Commit Plan (recommended)

### Commit 1 — Model + migration scaffolding
- [ ] Add `CatalogEntry.StatusCode` (canonical lifecycle field).
- [ ] Add migration for SQL providers.
- [ ] Ensure Cosmos compatibility for missing/legacy docs.
- [ ] Keep `Status` string for transitional compatibility.

### Commit 2 — Centralize lifecycle-faithful catalog writes
- [ ] Update `CatalogService.UpsertAsync` to map canonical `StatusCode`.
- [ ] Keep `Status` string aligned with `StatusCode`.
- [ ] Ensure Deleted/Redirect entries are non-public in catalog projection (e.g., `Published = null`).

### Commit 3 — Lifecycle handlers alignment
- [ ] `DeleteArticleHandler`: stop deleting catalog row on soft delete; upsert instead.
- [ ] `RestoreArticleHandler`: remove manual catalog row construction; use catalog upsert.
- [ ] `TrashArticleHandler`: keep permanent catalog row removal unchanged.

### Commit 4 — Downstream guardrails
- [ ] `FolderListingService`: exclude Deleted/Redirect from `/pub/articles` virtual root listing.
- [ ] `DeleteTemplateHandler`: count only Active catalog rows for template usage blocking.
- [ ] Add/confirm lifecycle filters in public-facing catalog query paths where needed.

### Commit 5 — FileEntryTitleService optimization path
- [ ] Move `GetArticleNumberTitleStatusList()` to read from `ArticleCatalog`.
- [ ] Use `StatusCode` for filtering.
- [ ] Keep compatibility fallback if required during migration window.

### Commit 6 — Legacy parity cleanup
- [ ] Align obsolete `ArticleEditLogic` catalog status mapping to canonical semantics.

### Commit 7 — Tests
- [ ] Soft delete keeps catalog row + marks Deleted.
- [ ] Restore returns catalog row to Active and unpublished.
- [ ] Permanent trash removes catalog row.
- [ ] `/pub/articles` root listing excludes Deleted/Redirect.
- [ ] Template delete ignores deleted catalog rows.
- [ ] `GetArticleNumberTitleStatusList()` behaves correctly from catalog.
- [ ] Public/search/sitemap/blog navigation regressions stay green.

## Validation Checklist

- [ ] `dotnet build SkyCMS.sln`
- [ ] Run targeted lifecycle/catalog tests.
- [ ] Run broader affected test projects.
- [ ] Manual sanity pass: delete, restore, trash, template delete, file listing, TOC/search/sitemap.

## Notes / Decisions Log

- [ ] (log item)
- [ ] (log item)

## Completion

- [ ] All checklist items complete.
- [ ] TEMP tracker removed before merge.
