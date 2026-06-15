# ADR 0041: FileManagerController Legacy Method Audit and Removal Plan

## Status
Accepted

## Context

`FileManagerController` grew from two sources over time:

1. The **original** file manager (Kendo grid / classic UI) with direct HTTP actions for
   copy, move, delete, rename, etc.
2. The **elFinder consolidation** (see ADR 0035, ADR 0039), which merged
   `ElFinderConnectorController` into `FileManagerController` and introduced a parallel
   CQRS dispatch path alongside legacy fallback handlers as a staged-rollout safety net.

After the consolidation was validated in production, a full audit was conducted (May 2025)
to identify which methods are still load-bearing and which are safe to remove. This ADR
records those findings so the work can be completed incrementally without re-deriving the
same research.

The audit covered:
- All `private` elFinder handler methods
- All CQRS scaffolding / staged-rollout infrastructure
- All `public` action methods from the original file manager UI
- Cross-references in views (`.cshtml`), JavaScript (`.js`, `.ts`), and C# callers

---

## Findings

### Group 1 — CQRS Staged-Rollout Scaffolding (safe to remove)

These exist solely to support the gradual migration from direct storage calls to CQRS
handlers. They can all be removed once the CQRS path is the only path.

| Member | Notes |
|---|---|
| `UseCqrsForCommand(string)` | Feature flag + `__cqrs` / `__cqrs_{cmd}` query-param opt-in |
| `GetElFinderMediatorOrNull()` | Returns `null` if MediatR not registered — always registered in production |
| `TranslateCqrsErrorToLegacy(…)` | Error code mapping shim between the two code paths |
| `ExecuteCqrsCommandOrFallback<TCommand, TResponse>(…)` | Generic dispatcher that falls back to legacy handler |
| `Connector()` switch arms | Currently dual-arm (`"cmd" when UseCqrsForCommand(…)` + `"cmd"`) for every command — will collapse to one arm each |

---

### Group 2 — Legacy elFinder Fallback Handlers (safe to remove with Group 1)

These `private` methods are only ever called as the `fallbackHandler` argument to
`ExecuteCqrsCommandOrFallback` or inline when `GetElFinderMediatorOrNull()` returns `null`.
Once the CQRS scaffolding is removed they become unreachable.

| Method | Notes |
|---|---|
| `HandleOpenAsync()` | Replaced by `HandleOpenViaCqrsAsync()` |
| `HandleTreeAsync()` | Replaced by `HandleTreeViaCqrsAsync()` |
| `HandleLsAsync()` | Replaced by `HandleLsViaCqrsAsync()` |
| `HandleMkdirAsync()` | Replaced by `HandleMkdirViaCqrsAsync()` |
| `HandleMkfileAsync()` | Replaced by `HandleMkfileViaCqrsAsync()` |
| `HandleRenameAsync()` | Replaced by `HandleRenameViaCqrsAsync()` |
| `HandleRmAsync()` | Replaced by `HandleRmViaCqrsAsync()` |
| `HandleUploadAsync()` | Replaced by `HandleUploadViaCqrsAsync()` |
| `HandleGetAsync()` | Replaced by `HandleGetViaCqrsAsync()` |
| `HandlePutAsync()` | Replaced by `HandlePutViaCqrsAsync()` |
| `HandlePasteAsync()` | Replaced by `HandlePasteViaCqrsAsync()` |
| `HandleTmbAsync()` | Replaced by `HandleTmbViaCqrsAsync()` |
| `HandleInfoAsync()` | Replaced by `HandleInfoViaCqrsAsync()` |
| `HandleSizeAsync()` | Replaced by `HandleSizeViaCqrsAsync()` |
| `HandleParentsAsync()` | **Already dead** — `HandleParentsViaCqrsAsync` never calls it as a fallback; it returns `errUnknownCmd` directly if mediator is null |
| `HandleSearchAsync()` | Replaced by `HandleSearchViaCqrsAsync()` |
| `HandleFileAsync()` | Replaced by `HandleFileViaCqrsAsync()` |
| `HandleDuplicateAsync()` | Replaced by `HandleDuplicateViaCqrsAsync()` |
| `HandleResizeAsync()` | **Already a stub** — returns `errCmdNoSupport` |
| `HandleUrlAsync()` | Replaced by `HandleUrlViaCqrsAsync()` |
| `HandleDimAsync()` | **Already a stub** — returns `errCmdNoSupport` |

---

### Group 3 — Original File Manager UI Actions

These are `public` actions from the Kendo grid / classic file manager era.

#### 3a — Safe to remove (no callers found in any view, JS, or C# file)

| Action | Reason |
|---|---|
| `Copy(MoveFilesViewModel)` | Zero callers. Previously served the Kendo grid copy button. |
| `Move(MoveFilesViewModel)` | Zero callers. Previously served the Kendo grid move button. |
| `NewFile(NewFileViewModel)` | Zero callers. Previously served the old new-file dialog. |
| `NewFolder(NewFolderViewModel)` | Zero callers. Previously served the old new-folder dialog. |
| `Create(string, FileManagerEntry)` | Zero callers targeting `FileManager`. All `Create` references found point to `Editor`, `Templates`, or `Layouts` controllers. |
| `Delete(DeleteBlobItemsViewModel)` | Zero callers targeting `FileManager`. All `Delete` references found point to other controllers. |
| `Rename(RenameBlobViewModel)` | Zero callers targeting `FileManager`. |

#### 3b — Active callers — keep

| Action | Active Callers |
|---|---|
| `Index()` | `_CosmosMainMenuPartial.cshtml` (nav link), `_LayoutEditor.cshtml` (×3 — folder picker, edit redirect, and open button), `EditImage.cshtml` (back-navigation). Flag `UseModernFileExplorer` defaults `true`; there is no `Views/FileManager/Index.cshtml` fallback view — the `false` branch in `Index()` is effectively dead but the method and route must stay as the entry point. |
| `EditCode()` GET + POST | `EditCode.cshtml` (self-referencing POST), `ChatHub.cs` (server-side redirect), `_DocsHelpFloatingWindow.cshtml` (URL pattern match for help overlay), tests. |
| `EditImage()` GET + POST | `EditImage.cshtml` (self-referencing POST via `Url.Action("EditImage","FileManager")`), linked from file manager edit flows. |
| `ImportPage()` GET + POST | `ImportPage.cshtml` (uses `.Save("ImportPage", "FileManager", …)`). |
| `SimpleUpload()` | `Edit.cshtml` (CKEditor article editor), `Designer.cshtml` (GrapesJS — articles and templates), `ckeditor-widget.301.js` (`/FileManager/SimpleUpload/` hardcoded). |
| `UploadImage()` | `image-widget.js` (`/FileManager/UploadImage` hardcoded, documented in file header). |
| `GetImageAssets()` | `_GrapesJsEditor.cshtml`, `image-widget.js`, `designer.js` (all hardcoded to `/FileManager/GetImageAssets`). |
| `Process()` POST | `Index.cshtml` (FilePond `server.process` and `server.patch`). |
| `Process()` PATCH | `Index.cshtml` (FilePond chunked upload). |
| `Process()` DELETE | `Index.cshtml` (FilePond revert). |
| `GetImageThumbnail()` | `Index.cshtml` sets `tmbUrl` to `/FileManager/GetImageThumbnail?target=`; also referenced in `ToElFinderObject()` for per-file thumbnail URLs. |
| `Download()` | No URL callers found, but is a `public` action with no `[NonAction]` guard. Treat as keep pending explicit removal decision. |

---

### Group 4 — Legacy Constructor (safe to remove)

The second constructor (lines ~190–217) self-instantiates `FileOperationsService` and
`MemoryCache` inline. Its XML doc comment says "for existing tests and call sites."
Verified: no test or production call site uses it directly — all resolved through DI.
Safe to remove once confirmed no callers remain in test projects.

---

### Group 5 — Unused Private Field (safe to remove)

`blobPublicAbsoluteUrl` — assigned in the primary constructor from
`editorSettings?.BlobPublicUrl?.TrimStart('/')` but never read. All production code
accesses `editorSettings.BlobPublicUrl` directly.

---

### Group 6 — Dead Branch in `Index()` (safe to remove, method stays)

The `UseModernFileExplorer` feature flag inside `Index()` has two branches:
- `true` → `return View("~/Views/Shared/FileExplorer/index.cshtml", …)`
- `false` → `return View(ddata)` / `return View(…, data)` — resolves to
  `Views/FileManager/Index.cshtml` which **does not exist**

The flag defaults to `true` in `EditorConfig.cs`. The `false` branch can be removed;
`Index()` itself must remain as the route entry point.

---

## Design Goals

- Arrive at a single, linear code path through `Connector()` — no feature flags, no
  fallback dispatch, no dead handlers.
- Keep the controller surface area minimal: only actions with active callers survive.
- Leave a documented trail so future contributors know which actions were removed and why.

## Non-Goals

- Removing or restructuring any of the active actions listed in Group 3b.
- Changing the CQRS command handlers themselves (`HandleXxxViaCqrsAsync` methods).
- Touching `VsCodeController` (covered by ADR 0039).

## Decision

Remove the items in Groups 1, 2, 3a, 4, and 5. Collapse the dual-arm `Connector()` switch
to single arms. Strip the dead `false` branch from `Index()` (Group 6). Retain everything
in Group 3b unchanged.

Removal should be done in a single focused PR on a dedicated branch. Build and existing
tests must pass before merge.

## Consequences

### Positive
- `FileManagerController.cs` shrinks by roughly 1,400–1,600 lines.
- `Connector()` becomes a straightforward single-dispatch switch with no conditional arms.
- No CQRS scaffolding noise — the controller unambiguously uses MediatR for all elFinder
  commands.
- Future readers do not need to re-derive which fallback methods are dead.

### Risks / Mitigations
- **Risk:** A caller was missed in the audit. **Mitigation:** Run the full test suite and
  verify a build with no warnings before merging. The methods in Group 3a have no route
  attributes or `[HttpGet]`/`[HttpPost]` decorations beyond the controller default, making
  a missed external call unlikely but possible via convention routing.
- **Risk:** `Download()` has an unknown caller. **Mitigation:** Kept in Group 3b until
  explicitly verified clear.

## References

- `Editor/Controllers/FileManagerController.cs` — subject of this audit
- `Editor/Views/Shared/FileExplorer/Index.cshtml` — the active elFinder UI view
- `Editor/Views/FileManager/EditCode.cshtml` — active FileManager code editor view
- `Editor/Views/FileManager/EditImage.cshtml` — active image editor view
- `Editor/Views/FileManager/ImportPage.cshtml` — active page import view
- `Editor/Views/Shared/_CosmosMainMenuPartial.cshtml` — nav link to `Index()`
- `Editor/Views/Shared/_LayoutEditor.cshtml` — folder picker and edit redirects to `Index()`
- `Editor/wwwroot/js/image-widget.js` — hardcoded `/FileManager/UploadImage` and `/FileManager/GetImageAssets`
- `Editor/wwwroot/js/designer.js` — hardcoded `/FileManager/GetImageAssets`
- `Editor/wwwroot/lib/grapesjsui/js/ckeditor-widget.301.js` — hardcoded `/FileManager/SimpleUpload`
- `docs/adr/0035-file-explorer-modernization-and-connector-adapter-strategy.md`
- `docs/adr/0039-dry-controller-unification-file-manager-and-vscode.md`
