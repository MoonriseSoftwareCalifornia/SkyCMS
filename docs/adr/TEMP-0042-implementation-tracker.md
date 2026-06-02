# TEMP Tracker: ADR 0042 Implementation (ArticleCatalog Lifecycle Read-Model Alignment)

> Temporary execution tracker for branch-level implementation work.
> Remove this file after implementation is complete and validated.

## Scope

Implement ADR 0042 without breaking downstream `ArticleCatalog` consumers.

## Commit Plan (recommended)

### Commit 1 — Model + migration scaffolding
- [x] Add `CatalogEntry.StatusCode` (canonical lifecycle field).
- [x] Add migration for SQL providers.
- [x] Ensure Cosmos compatibility for missing/legacy docs.
- [x] Keep `Status` string for transitional compatibility.

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

- [ ] **MySQL migrations re-enablement deferred to a separate branch/effort.**
  - MySQL migrations were disabled in commit `184e556e` due to provider-drift (SQL Server→MySQL type conversion artifacts) and Pomelo EF Core 10 incompatibility.
  - The project now uses `Microting.EntityFrameworkCore.MySql` (in `Directory.Packages.props`) but it is not yet referenced in `Sky.Editor.csproj`.
  - Completing MySQL re-enablement requires: (1) add `PackageReference` to `Sky.Editor.csproj`, (2) rename the four `.disabled` files back to `.cs`, (3) hand-author a MySQL `ApplicationDbContextModelSnapshot.cs`, and (4) add a `.Designer.cs` for `20260602120020_AddCatalogEntryStatusCode_MySql`.
  - The `20260602120020_AddCatalogEntryStatusCode_MySql.cs.disabled` file is already correct and safe; it just needs to remain disabled until the above prerequisites are met.
  - Suggested branch name: `feat/reenable-mysql-migrations`.

### Commit 8 — Article lifecycle documentation
- [ ] Create `docs/adr/0043-article-lifecycle-events.md` documenting every lifecycle event with:
  - Trigger (what user action or system event causes it)
  - `StatusCode` value before and after
  - `Published` value before and after
  - What happens to the `CatalogEntry` row (created / upserted / preserved / deleted)
  - What happens to blob storage assets
  - What happens to static HTML artifact
  - What downstream systems are notified (TOC, sitemap, etc.)
  - Whether the event is reversible and how
- [ ] Events to cover at minimum: Create, Save/Edit, Publish, Schedule (future publish), Unpublish, Soft-delete (trash), Restore, Permanent delete, URL redirect assignment, Title change, Template change.

## Completion

- [ ] All checklist items complete.
- [ ] TEMP tracker removed before merge.
