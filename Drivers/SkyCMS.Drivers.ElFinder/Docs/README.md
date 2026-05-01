# SkyCMS elFinder Command Reference

**elFinder API version:** 2.1  
**SkyCMS driver project:** `SkyCMS.Drivers.ElFinder`  
**Last reviewed:** 2026-04-28  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1

---

## Purpose

This document set exists for four reasons:

1. **Implementation reference** — tells developers and AI exactly what each command must send and receive.
2. **Drift tracking** — as elFinder releases new point versions (e.g. 2.1024, 2.1029, 2.1030), the "Min API version" field on each command makes changes auditable.
3. **Test authoring** — each command page includes an expected-response section that maps directly to unit test assertions.
4. **Backlog visibility** — unimplemented commands are documented so nothing gets accidentally missed or duplicated.

---

## Architecture

Commands reach the connector via `ElFinderConnectorController`. Two dispatch paths exist:

- **Legacy path** — inline handler methods on the controller (all 15 commands have this).
- **CQRS path** — MediatR command/handler pairs in this library, opt-in per command via config or `__cqrs` query param.

The CQRS path is preferred for new work; the legacy path is the fallback.

**Serialization note:** The app uses `Newtonsoft.Json` with `DefaultContractResolver` (PascalCase). CQRS responses use `System.Text.Json` attributes (`[JsonPropertyName]`, `[JsonIgnore]`) and must be serialized via `System.Text.Json.JsonSerializer.Serialize()` — NOT via the controller's `Json()` helper — to honour those attributes. See `HandleParentsViaCqrsAsync` in the controller for the pattern.

**Blob storage note:** Azure Blob Storage uses virtual directory paths — no marker blob exists for a folder. `GetFileAsync` returns `null` for directory paths. Always use `GetFilesAndDirectories` as a fallback. See [SkyCMS Implementation Notes](skycms-implementation-notes.md).

---

## Command Status Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Fully implemented (CQRS + legacy) |
| 🔶 | Implemented legacy path only |
| 📋 | TODO — planned, not yet implemented |
| ⛔ | Intentionally disabled / out of scope |

---

## Command Index

### Navigation

| Command | Status | Min API | Summary |
|---------|--------|---------|---------|
| [open](commands/open.md) | ✅ | 2.1 | Open a directory; returns cwd, files, options |
| [tree](commands/tree.md) | ✅ | 2.1 | Return immediate subdirectories of a folder |
| [parents](commands/parents.md) | ✅ | 2.1 | Return full ancestor chain for tree panel |
| [ls](commands/ls.md) | ✅ | 2.1 | List item names in a directory |

### File Operations

| Command | Status | Min API | Summary |
|---------|--------|---------|---------|
| [mkdir](commands/mkdir.md) | ✅ | 2.1 | Create a new directory |
| [mkfile](commands/mkfile.md) | ✅ | 2.1 | Create a new empty file |
| [rename](commands/rename.md) | ✅ | 2.1 | Rename a file or folder |
| [rm](commands/rm.md) | ✅ | 2.1 | Delete files and/or folders |
| [paste](commands/paste.md) | ✅ | 2.1 | Copy or move files/folders |
| [duplicate](commands/duplicate.md) | 📋 | 2.1 | Create a named copy of files/folders |

### File Content

| Command | Status | Min API | Summary |
|---------|--------|---------|---------|
| [upload](commands/upload.md) | ✅ | 2.1 | Upload one or more files |
| [get](commands/get.md) | ✅ | 2.1 | Return plain-text file content as string |
| [put](commands/put.md) | ✅ | 2.1 | Save text content to a file |
| [file](commands/file.md) | 📋 | 2.1 | Output file to browser (download / inline preview) |

### Metadata & Utilities

| Command | Status | Min API | Summary |
|---------|--------|---------|---------|
| [info](commands/info.md) | ✅ | 2.1 | Return metadata for one or more items |
| [size](commands/size.md) | ✅ | 2.1 | Return total size of files/folders |
| [tmb](commands/tmb.md) | ✅ | 2.1 | Generate thumbnails for images |
| [search](commands/search.md) | 📋 | 2.1 | Search for files/folders by name |
| [dim](commands/dim.md) | 📋 | 2.1 | Return image/video dimensions |
| [url](commands/url.md) | 📋 | 2.1 | Return the public URL of a file |

### Image Editing

| Command | Status | Min API | Summary |
|---------|--------|---------|---------|
| [resize](commands/resize.md) | 📋 | 2.1 | Resize, crop, or rotate an image |

### Disabled / Out of Scope

| Command | Status | Min API | Reason |
|---------|--------|---------|--------|
| [archive](commands/archive.md) | ⛔ | 2.1029 | Disabled — no server-side archive support |
| [extract](commands/extract.md) | ⛔ | 2.1029 | Disabled — no server-side archive support |
| [chmod](commands/chmod.md) | ⛔ | 2.1 | Disabled — blob storage has no Unix permissions |
| [zipdl](commands/zipdl.md) | ⛔ | 2.1012 | Disabled — no server-side zip support |
| [netmount](commands/netmount.md) | ⛔ | 2.1 | Out of scope — blob storage only |
| [ping](commands/ping.md) | ⛔ | 2.1 | Deprecated — was a Safari upload workaround |
| [callback](commands/callback.md) | ⛔ | 2.1 | Out of scope — OAuth netmount only |
| [editor](commands/editor.md) | ⛔ | 2.1030 | Out of scope — editor plugin gateway |
| [abort](commands/abort.md) | ⛔ | 2.1029 | Not implemented — chunked upload cancellation |

---

## Shared Object Schemas

- [File/Directory Object](elfinder-file-object.md) — the `ElFinderObject` shape used by nearly every response
- [Error Response](elfinder-error-response.md) — the `{ "error": [...] }` shape returned on failure

---

## SkyCMS-Specific Notes

- [Implementation Notes](skycms-implementation-notes.md) — blob storage virtual directory quirks, serialization pipeline, CQRS routing config, disabled command list
