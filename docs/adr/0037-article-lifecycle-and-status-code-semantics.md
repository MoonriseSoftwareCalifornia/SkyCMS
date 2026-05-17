# ADR 0037: Article Lifecycle and Status Code Semantics

## Status

Accepted

## Context

SkyCMS articles pass through several distinct states between initial creation and
permanent removal. These states are encoded in the `StatusCode` column of the `Articles`
table via the `StatusCodeEnum` type defined in `Cosmos.Common.Data.Logic`.

As the platform evolved, the meaning of each value and the distinction between
`StatusCode` and the `Published` timestamp became unclear to contributors. This led to:

- confusion between "active" and "published" (they are independent axes),
- uncertainty about what `Inactive` means and whether it is still used,
- file-listing code that accidentally exposed soft-deleted article content through the
  file manager and VS Code explorer because the blob folder is retained after soft delete,
- duplicated and inconsistent path-parsing logic spread across controllers.

A code review and refactoring effort in 2025 (branch `fix/code-scanning-security`)
clarified the full lifecycle, introduced a shared filtering service
(`PublicFileEntryTitleResolver.FilterDeletedArticleEntriesAsync`), and established this
ADR as the canonical reference for future contributors.

## Design Goals

1. Provide a single authoritative reference for the article lifecycle that contributors
   and documentation authors can find without reading handler source code.
2. Make the distinction between `StatusCode` and `Published` unambiguous.
3. Document blob-retention behaviour so file-listing code can be written correctly.
4. Record the vestigial status of `Inactive` so it is not accidentally adopted or
   removed without a deliberate decision.
5. Serve as source material for the SkyCMS.Docs documentation project.

## Non-Goals

- This ADR does not define UI copy or workflow labels shown to content authors.
- It does not specify how future states (e.g. a formal `Review` state) should be
  implemented — that requires a separate ADR.
- It does not cover template or layout lifecycle, which follow different rules.

## Decision

The article lifecycle is governed by two independent fields:

| Field        | Type              | Meaning |
|--------------|-------------------|---------|
| `StatusCode` | `StatusCodeEnum`  | The lifecycle/administrative state of the article row. |
| `Published`  | `DateTimeOffset?` | The date/time at which the article version goes live. `null` means unpublished. |

These fields are **orthogonal**. An article can be `Active` and unpublished, or `Active`
and published, or `Deleted` and still have a `Published` timestamp on old rows. The
`StatusCode` governs visibility and administrative state; `Published` governs whether
content is surfaced to the public website.

### StatusCodeEnum Values

#### `Active` (0) — Default editable state

The article exists and is being authored or managed. This is the value set when an
article is first created (`CreateArticleHandler`) and is preserved through all normal
save/edit/publish/unpublish cycles (`SaveArticleHandler`).

An `Active` article may be:
- unpublished (no live public page),
- published with `Published` set to a past date (live now),
- scheduled with `Published` set to a future date (live later, via `ArticleScheduler`).

`Active` does **not** mean "currently visible to the public." Use `Published` for that.

#### `Inactive` (1) — Vestigial / reserved

**No current production code sets an article to `Inactive`.** This value was added as a
reserved state for a future "soft disable without trashing" feature that was never
implemented.

Contributors should treat `Inactive` the same as `Active` in filtering and listing
logic until a future ADR formally adopts this value. Do **not** repurpose it without an
ADR.

#### `Deleted` (2) — Soft delete ("Send to Trash")

Set by `DeleteArticleHandler` on **all versions** of an article when a user sends it to
the trash.

Key consequences:
- The article catalog entry (`ArticleCatalog`) is removed.
- The article's static webpage is deleted.
- The blob storage folder (`/pub/articles/{ArticleNumber}`) is **retained**.
- The article does **not** appear in editorial listings.
- The article's blob folder **must be hidden** from all file-manager and VS Code explorer
  listings. This is the responsibility of `FilterDeletedArticleEntriesAsync`.

A soft-deleted article can be restored (all versions set back to `Active`) or
permanently trashed (see below).

#### Permanently Trashed — no status code

Permanent trashing is performed by `TrashArticleHandler`. It:
- hard-deletes all `Article`, `ArticleCatalog`, `Pages`, `ArticleLocks`, and
  `ArticleLogs` rows,
- deletes the blob folder (`/pub/articles/{ArticleNumber}`) from storage.

After permanent trashing there are no DB rows and no blob data, so no filtering is
required and file listings naturally show nothing.

#### `Redirect` (3) — URL redirect stub

Reserved for articles whose canonical URL has changed and a redirect record is needed.
Not commonly set in current production flows. File-listing code should treat `Redirect`
articles the same as `Deleted` — their content should not be surfaced.

### Lifecycle State Diagram

```
                    ┌──────────────────────────────────┐
                    │          CreateArticleHandler     │
                    │  StatusCode = Active              │
                    │  Published  = null (or explicit)  │
                    └────────────────┬─────────────────┘
                                     │
                          ┌──────────▼──────────┐
                          │   Active (editing)   │◄────────────────┐
                          │  Published = null    │                 │
                          └──────────┬──────────┘           Restore from
                                     │                         Trash
                        ┌────────────┼────────────┐               │
                        │            │            │               │
               Publish  │    Schedule│    Unpublish│               │
                        ▼            ▼            ▼               │
              ┌──────────────┐ ┌──────────┐ ┌──────────┐          │
              │    Active    │ │  Active  │ │  Active  │          │
              │  Published=  │ │Published=│ │Published=│          │
              │  past date   │ │future dt │ │  null    │          │
              └──────────────┘ └──────────┘ └──────────┘          │
                        │            │            │               │
                        └────────────┼────────────┘               │
                                     │                            │
                             Send to Trash                        │
                                     │                            │
                          ┌──────────▼──────────┐                 │
                          │  Deleted (soft)      │─────────────────┘
                          │  Blob folder kept    │
                          └──────────┬──────────┘
                                     │
                            Permanent Trash
                                     │
                          ┌──────────▼──────────┐
                          │  (no rows, no blobs) │
                          │  Permanently removed │
                          └─────────────────────┘
```

### Blob Retention Rule

Blob storage under `/pub/articles/{ArticleNumber}` is **not deleted on soft delete**.
It is only deleted on permanent trash (by `TrashArticleHandler`).

Any code that lists the contents of `/pub/articles` or its subdirectories **must**
filter out entries belonging to soft-deleted articles before returning results. Use
`PublicFileEntryTitleResolver.FilterDeletedArticleEntriesAsync` for this purpose — do
not duplicate the logic inline.

### Publishing Is Not a Status Transition

Setting `Published` to a past date makes an article live. Setting it to `null` or a
future date makes it not live. Neither action changes `StatusCode`. The scheduler
(`ArticleScheduler`) activates scheduled versions by manipulating `Published` only —
it never changes `StatusCode`.

## Detailed Rationale

### Why `StatusCode` and `Published` Are Separate Axes

Articles need to be in an "editable but not live" state as well as a "live" state. If
`Published` were encoded into `StatusCode`, there would be no clean way to represent a
scheduled future publication or to temporarily unpublish without losing the lifecycle
state. Keeping them separate makes each concern independently queryable and avoids
complex multi-value status logic.

### Why Blob Storage Is Retained After Soft Delete

Soft delete is reversible — a user can restore a trashed article. If blobs were deleted
on soft delete, restoration would lose all uploaded assets. The platform therefore
retains blobs and relies on filtering at the listing layer to hide them from the editor
UI. Only permanent trash removes blobs.

### Why `Inactive` Was Not Removed

Removing an enum value would be a breaking schema change for any deployment that has
rows with `StatusCode = 1`. Even though no production code currently creates `Inactive`
articles, rows may exist from earlier experimental builds or future features. The value
is therefore retained and explicitly documented as vestigial rather than silently
removed.

## Alternatives Considered

### Encode Lifecycle Fully in StatusCode (no separate Published field)

Rejected. Adding states like `PublishedActive`, `UnpublishedActive`, `ScheduledActive`,
etc. would create a combinatorial explosion and make scheduling and restore logic
significantly more complex.

### Delete Blobs on Soft Delete and Restore from Backup

Rejected. SkyCMS targets environments without guaranteed blob versioning or point-in-time
restore. Retaining blobs in the folder during soft delete is safer and reversal is
instant.

### Add a New `SoftDeleted` StatusCode and Deprecate `Deleted`

Considered. Rejected because `Deleted` already means soft delete in all current handlers
and renaming it would require a data migration across all existing deployments.

## Consequences

### Positive Outcomes

- Contributors have a single authoritative reference for article lifecycle semantics.
- The file-manager, elFinder, and VS Code explorer now share one filtering service
  (`FilterDeletedArticleEntriesAsync`) instead of duplicating logic.
- `Inactive` is explicitly documented so it cannot be accidentally misused.
- The deleted-article cache key is scoped to the tenant domain, so separate tenants
  sharing a single in-process `IMemoryCache` cannot bleed their deleted-article sets
  into each other's file listings. The domain is obtained from
  `IDynamicConfigurationProvider.GetTenantDomainNameFromRequest()` in each controller
  and passed as an optional parameter; an empty string is safe for single-tenant and
  test scenarios.
- This ADR feeds directly into SkyCMS.Docs when imported via the docs pipeline.

### Constraints Introduced

- All new file-listing code touching `/pub/articles` must call
  `FilterDeletedArticleEntriesAsync` — this is a new contributor responsibility.
- Any future decision to adopt `Inactive` for a real feature must create a superseding
  ADR and a data migration plan.

## Evidence

- `StatusCodeEnum` definition:
  - `Common/Data/Logic/StatusCodeEnum.cs`
- Lifecycle handlers:
  - `Editor/Features/Articles/Create/CreateArticleHandler.cs`
  - `Editor/Features/Articles/Save/SaveArticleHandler.cs`
  - `Editor/Features/Articles/Delete/DeleteArticleHandler.cs`
  - `Editor/Features/Articles/Trash/TrashArticleHandler.cs`
  - `Editor/Services/Scheduling/ArticleScheduler.cs`
- Shared filtering service:
  - `Editor/Services/PublicFileEntryTitleResolver.cs` — `FilterDeletedArticleEntriesAsync`
- Shared path helper:
  - `Editor/Services/PublicFileEntryHelper.cs` — `ExtractArticleNumbersFromEntries`
- Integration points (file listing consumers):
  - `Editor/Controllers/FileManagerController.cs`
  - `Editor/Controllers/ElFinderConnectorController.cs`
  - `Editor/Controllers/VsCodeController.cs`
