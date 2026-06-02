# ADR 0041 Implementation Progress (Temporary)

- [x] 1. Draft and add ADR 0041 document
- [x] 2. Add ADR 0041 link to docs/adr/README.md
- [x] 3. Add shared deleted-article path access guard service API
- [x] 4. Enforce guard in elFinder CQRS handlers/adapter path
- [x] 5. Enforce guard in VsCodeController direct file/folder endpoints
- [ ] 6. Add/adjust tests for listing + direct-access blocking
- [ ] 7. Build and run targeted tests

## Work Log
- Started implementation.
- Added `docs/adr/0041-trashed-article-assets-hidden-and-inaccessible.md`.
- Updated `docs/adr/README.md` to include ADR 0041.
- Extended `IFileEntryTitleService` with `IsArticlePathDeletedAsync(...)`.
- Implemented deleted-article path checks in `FileEntryTitleService`.
- Added deleted-article access denial in `VsCodeController` direct file/folder endpoints via `DenyDeletedArticlePathAsync(...)`.
- Added/extended deleted-article access denial in `FileManagerController` CQRS command handlers and guard helpers.
- Pending: finalize tests and run build/targeted test validation.
