# ADR 0039 — DRY Controller Unification: File Manager and VS Code Explorer

**Status:** Accepted
**Date:** 2025-07-11
**Authors:** SkyCMS Contributors
**Relates to:** [ADR 0035 — File Explorer Modernization and Connector Adapter Strategy](./0035-file-explorer-modernization-and-connector-adapter-strategy.md)

---

## Context

SkyCMS exposes two file-management surfaces over the same blob-storage back-end:

| Surface | Controller | Auth model |
|---|---|---|
| File Manager UI | `FileManagerController` | Cookie / identity |
| SkyCMS Explorer (VS Code extension) | `VsCodeController` | Bearer token |

Despite sharing an identical storage substrate, the two controllers historically duplicated:

1. **Path normalization and encoding** — `EncodePathHash` / `DecodePathHash`, `UrlEncodePath`,
   `ParsePath`, `TrimPathPart`.
2. **Article/template title resolution** — inline DB queries and deleted-article filtering.
3. **Folder listing logic** — the `/pub/articles`, `/pub/templates`, and generic storage branches.
4. **Upload path-safety validation** — empty-path guard, `..` traversal detection, `/pub` root
   enforcement.
5. **Dangerous-extension blocking** — the upload blocklist check.

Duplicated security logic is especially problematic: a gap in one controller creates an asymmetric
attack surface even though both routes reach the same storage.

---

## Decision

All shared business logic is extracted into static helpers or scoped services consumed by both
controllers. The controllers retain only their auth-surface-specific concerns (cookie session vs
bearer token, FilePond chunked upload protocol vs raw body).

### Extraction map

| Concern | Where extracted | Type |
|---|---|---|
| Path normalization / encoding | `FileEntryPathHelper` | `public static class` |
| Article/template title lookup + deleted-article filter | `IFileEntryTitleService` / `FileEntryTitleService` | Scoped DI service |
| Folder listing (articles, templates, storage) | `IFolderListingService` / `FolderListingService` | Scoped DI service |
| Upload path-safety (empty, traversal, `/pub` root) | `FileEntryPathHelper.IsUploadPathSafe` | `public static` method |
| Dangerous-extension blocking | `FileEntryPathHelper.IsDangerousExtension` | `public static` method |

### Rationale for static helpers vs scoped services

Upload validation (`IsUploadPathSafe`, `IsDangerousExtension`) requires no I/O and carries no
state. Extracting it as static methods on `FileEntryPathHelper` keeps the call-sites simple and
removes the need for an additional DI registration. A dedicated `IUploadValidationService` was
considered and rejected as a pass-through wrapper that would add indirection without reducing real
duplication.

Folder listing and title resolution require database access and tenant context, so they are
modelled as scoped DI services.

### Security parity rule

Any upload security guard present in one controller **must** be present in the other. Extracting
the guards into shared helpers enforces this mechanically: a guard added or changed in
`FileEntryPathHelper` is automatically applied to both surfaces without a second change.

At the time of this ADR the following guards are enforced on both surfaces:

- Path must be non-empty and not consist solely of slashes.
- Path must not contain `..` (traversal sequences).
- Path must be rooted under `/pub`.
- File extension must not appear in `FileStorageConstants.DangerousFileExtensions`.

---

## Consequences

### Positive

- Security behaviour is consistent across both surfaces by construction.
- Each controller is shorter and easier to reason about independently.
- New upload restrictions need to be added in one place only.
- The helper and service layers are independently testable without HTTP context.

### Negative / trade-offs

- `FileEntryPathHelper` now carries a dependency on `SkyCMS.Drivers.ElFinder`
  (for `FileStorageConstants`). This is an existing in-repo project dependency and is acceptable.
- Controllers retain upload-protocol differences (FilePond chunked vs raw body). This divergence
  is intentional and is not considered technical debt.

### What is explicitly not unified

- The FilePond chunked-upload protocol (`FileManagerController.Upload`) vs the single-shot raw-body
  upload (`VsCodeController.UploadFile`). These reflect different client contracts and must remain
  separate.
- CDN purging after upload (`PurgeCdnPath`) is specific to the File Manager surface; the VS Code
  extension does not trigger CDN purges.
- Auth guards (`[Authorize]` policy vs `EnsureVsCodeRequestAuthorized()`) are intentionally
  different and must not be unified.

---

## References

- `Editor/Services/FileEntryPathHelper.cs` — shared static helpers
- `Editor/Services/IFileEntryTitleService.cs` / `FileEntryTitleService.cs` — title resolution service
- `Editor/Services/IFolderListingService.cs` / `FolderListingService.cs` — folder listing service
- `Drivers/SkyCMS.Drivers.ElFinder/FileStorageConstants.cs` — blocked extensions and valid extension lists
- `Tests/Editor/Services/FileEntryPathHelperTests.cs` — validation coverage for all helper methods
