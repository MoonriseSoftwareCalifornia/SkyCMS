# ADR 0038 — Article Trash and Permanent Delete Lifecycle

**Status:** Accepted
**Date:** 2025-07-11
**Authors:** SkyCMS Contributors
**Relates to:** [ADR 0037 — Article Lifecycle and Status Code Semantics](./0037-article-lifecycle-and-status-code-semantics.md)

---

## Context

SkyCMS articles go through a two-step destruction path: first they are moved to a **Trash** state (recoverable),
and only later optionally **Permanently Deleted** (irreversible). This ADR documents what happens to database
rows, blob storage assets, and published artifacts at each step, and codifies the recovery rules.

The two operations are handled by separate command handlers to keep concerns distinct:

| User action | Command | Handler |
|---|---|---|
| Move to Trash | `DeleteArticleCommand` | `DeleteArticleHandler` |
| Permanently Delete | `TrashArticleCommand` | `TrashArticleHandler` |
| Restore from Trash | `RestoreArticleCommand` | `RestoreArticleHandler` |

> **Naming note:** The handler names reflect historical naming conventions. Conceptually, `DeleteArticleHandler`
> performs the *soft* move-to-trash, while `TrashArticleHandler` performs the *permanent purge*.
> See the XML doc summaries on each handler for the definitive description.

---

## Design Goals

1. **Recoverability** — a trashed article and all its uploaded assets can be fully restored.
2. **No data loss by accident** — permanent deletion requires an explicit second action and a UI confirmation.
3. **Clean live site** — trashed articles are immediately invisible to the public and excluded from file-manager listings.
4. **Conflict safety** — restoring an article whose title is now held by a newer active article must not silently collide.

---

## Decision

### Step 1 — Move to Trash (`DeleteArticleHandler`)

When an article is moved to trash:

| Resource | Action |
|---|---|
| `Articles` rows (all versions) | `StatusCode` set to `Deleted` (value `2`) |
| `Pages` rows | Removed from database |
| `ArticleCatalog` entry | Removed from database |
| Static HTML artifact (if static-web-pages mode) | Deleted from storage |
| Blob folder `/pub/articles/{articleNumber}/` | **Left intact** |
| TOC | Regenerated |

The blob folder is intentionally preserved so that all image and file references inside the article content
remain valid and the article can be restored without any asset re-upload.

File-manager surfaces (`FileManagerController`, `ElFinderConnectorController`, `VsCodeController`) run
`FilterDeletedArticleEntriesAsync` before returning listings, so the `/pub/articles/{articleNumber}/` folder
is hidden from editors even though the blobs still exist.

### Step 2 — Permanent Delete (`TrashArticleHandler`)

Permanent deletion is only permitted on articles already in `StatusCode = Deleted` state (enforced by
`TrashArticleValidator`). When permanently deleted:

| Resource | Action |
|---|---|
| `Articles` rows (all versions) | Hard-deleted from database |
| `Pages` rows | Hard-deleted from database |
| `ArticleCatalog` entry | Hard-deleted from database |
| `ArticleLocks` rows | Hard-deleted from database |
| `ArticleLogs` rows | Hard-deleted from database |
| Blob folder `/pub/articles/{articleNumber}/` | **Deleted permanently** via `storageContext.DeleteFolderAsync` |
| TOC | Regenerated |

There is no recovery path after this step. The UI must present a confirmation dialog before calling the
`TrashPermanently` endpoint (`[HttpPost] EditorController.TrashPermanently`).

### Step 3 — Restore from Trash (`RestoreArticleHandler`)

Restoration is only permitted on articles in `StatusCode = Deleted` state. On restore:

| Resource | Action |
|---|---|
| `Articles` rows (all versions) | `StatusCode` reset to `Active`; `Published` set to `null` |
| `ArticleCatalog` entry | Re-created with `Published = null`, `Status = "Active"` |
| Blob folder | Untouched — still present from the original trash step |

The restored article is **never re-published automatically**. An editor must explicitly publish it again.

#### Title conflict resolution

If an active article already holds the same title as the article being restored (e.g. a replacement was
created while the original was in trash), the restored article's title is renamed:

```
"{original title} ({total article count})"
```

and its URL slug is re-derived from the new title. The conflicting active article is not affected.

---

## State Diagram

```
[Active] ──TrashArticle──▶ [Deleted] ──TrashPermanently──▶ (gone)
                               │
                           RestoreArticle
                               │
                               ▼
                           [Active]
                        (Published = null,
                         title renamed if conflict)
```

---

## Consequences

### Positive

- Editors can recover accidental deletions without re-uploading assets.
- The live site and all file-manager surfaces are immediately clean after a trash action.
- Permanent deletion is a deliberate, guarded second step.
- The conflict-rename rule prevents URL collisions when an article is restored after a replacement was published.
- Blob storage costs are bounded: assets are only held temporarily (between trash and permanent delete).

### Negative / Trade-offs

- Blob storage accumulates "orphaned" assets for trashed articles until they are permanently deleted.
  Sites with many trashed articles and large asset sets should periodically run the permanent-delete sweep.
- The two-handler naming (`DeleteArticleHandler` = soft, `TrashArticleHandler` = hard) is counter-intuitive.
  This is a pre-existing convention that will be addressed in a future renaming ADR.

---

## Evidence

- `Editor/Features/Articles/Delete/DeleteArticleHandler.cs`
- `Editor/Features/Articles/Trash/TrashArticleHandler.cs`
- `Editor/Features/Articles/Trash/TrashArticleValidator.cs`
- `Editor/Features/Articles/Restore/RestoreArticleHandler.cs`
- `Editor/Controllers/EditorController.cs` — `TrashArticle`, `TrashPermanently`, `Restore` actions
- `Editor/Services/PublicFileEntryTitleResolver.cs` — `FilterDeletedArticleEntriesAsync`
- `Tests/Features/Articles/Restore/RestoreArticleHandlerTests.cs`
