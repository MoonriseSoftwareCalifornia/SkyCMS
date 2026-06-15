# ADR 0041 — Trashed Article Assets Must Be Hidden and Inaccessible in Editor File Surfaces

**Status:** Proposed  
**Date:** 2026-05-30  
**Authors:** SkyCMS Contributors  
**Relates to:**  
- [ADR 0037 — Article Lifecycle and Status Code Semantics](./0037-article-lifecycle-and-status-code-semantics.md)  
- [ADR 0038 — Article Trash and Permanent Delete Lifecycle](./0038-article-trash-and-permanent-delete-lifecycle.md)  
- [ADR 0039 — DRY Controller Unification: File Manager and VS Code Explorer](./0039-dry-controller-unification-file-manager-and-vscode.md)  
- [ADR 0040 — Use Human-Readable Article Titles in File Manager Display Paths](./0040-article-title-display-paths.md)

---

## Context

SkyCMS uses a two-step deletion lifecycle for articles:

1. **Move to Trash** (`StatusCode = Deleted`) — recoverable.
2. **Permanent Delete** — irreversible and physically removes `/pub/articles/{articleNumber}`.

This model intentionally preserves blob assets during trash state to support restore.  
However, preserved assets for deleted articles must not remain visible or operable in editor file surfaces. If they are visible or editable, users can interact with content that is semantically deleted, creating lifecycle inconsistency and policy confusion.

Current architecture already intends deleted-article filtering in file listings. This ADR formalizes and hardens the rule to include **both visibility and access control**, including direct-path/hash operations.

---

## Design Goals

1. Enforce article lifecycle semantics consistently across all editor file surfaces.
2. Preserve recoverability while an article is in Trash.
3. Prevent read/write/edit/delete operations against trashed article assets.
4. Ensure consistent behavior between File Manager (elFinder/CQRS path) and VS Code APIs.
5. Avoid leaking deleted-article existence through API behavior.

---

## Non-Goals

1. This ADR does not change soft-delete vs permanent-delete storage retention rules.
2. This ADR does not introduce immediate blob deletion on trash.
3. This ADR does not redefine article status semantics from ADR 0037/0038.
4. This ADR does not change canonical storage path structure (`/pub/articles/{number}`).

---

## Decision

For any article where all versions are `StatusCode = Deleted`, assets under `/pub/articles/{articleNumber}/` are treated as **logically inaccessible** in editor tooling.

### Normative rules

1. **Hidden in listings**  
   File listings MUST exclude deleted article folders and descendants.

2. **Not accessible by direct path/hash**  
   Direct operations targeting deleted article asset paths MUST be denied, including when discovered via old hashes, stale UI state, or manually constructed API calls.

3. **No read/write/edit**  
   Deleted article asset paths MUST reject:
   - read/open/download/stat/info
   - write/upload/put/resize
   - create/mkdir/mkfile
   - rename/move/copy/paste/duplicate
   - delete/rm

4. **Surface parity**  
   The same policy MUST apply across:
   - File Manager (`FileManagerController` + elFinder handlers/adapter pipeline)
   - VS Code file endpoints (`VsCodeController`)

5. **Restore behavior**  
   When an article is restored to active, assets immediately become visible/accessible again (subject to normal cache windows and tenant scoping).

6. **Permanent delete behavior**  
   Permanent delete continues to physically remove `/pub/articles/{articleNumber}/` from storage.

---

## Detailed Rationale

- **Lifecycle integrity:** “Deleted” must mean non-operable in editor UX, not merely hidden in some views.
- **Defense in depth:** Listing-only filters are insufficient; direct endpoint access must also enforce policy.
- **Consistency:** Editors should not see different behavior between File Manager and VS Code views.
- **Recoverability preserved:** Assets remain physically present for restore, but are quarantined from editor operations until restore.
- **Security/least privilege:** Denying operations reduces accidental or intentional modification of logically deleted content.

---

## Error and Response Semantics

To reduce information leakage and keep UI behavior stable:

- Listing endpoints return normal results excluding deleted paths.
- Direct operations against deleted-article paths SHOULD return the same class of response as non-existent or inaccessible targets (implementation may map to existing `errAccess` / `errOpen` / `404` conventions per endpoint contract).
- Implementations SHOULD avoid returning details that confirm whether a deleted article exists.

---

## Implementation Guidance (Non-Normative)

- Reuse central path parsing (`/pub/articles/{articleNumber}/...`) and deleted-article resolution logic.
- Keep tenant-scoped cache keys for deleted article sets.
- Enforce checks at shared integration points (adapter/service layer) where practical, then validate at controller/handler edges.
- Maintain cross-provider compatibility for EF queries (Cosmos/SQL/MySQL/SQLite) per existing ADR/provider constraints.

---

## Consequences

### Positive
- Stronger lifecycle correctness.
- Predictable editor experience.
- Reduced risk of edits against trashed article assets.
- Better parity between File Manager and VS Code APIs.

### Negative / Trade-offs
- Additional checks on file operations add small runtime overhead.
- Cache windows may briefly delay visibility flip after restore/delete transitions.
- More tests required across multiple command surfaces.

---

## Validation and Testing

Add/extend tests to verify:

1. Deleted article folders are not listed in File Manager and VS Code listing APIs.
2. Direct operations against deleted article paths are blocked on both surfaces.
3. Restored articles regain access without data loss.
4. Permanent delete removes DB entities and storage folder.
5. Tenant-scoped behavior remains isolated.
6. Behavior is consistent for both root and nested paths under `/pub/articles/{n}/...`.

---

## Adoption Notes

This ADR clarifies and hardens existing intent from ADR 0038/0040.  
It should be implemented as behavior-preserving lifecycle enforcement rather than a storage-model change.
