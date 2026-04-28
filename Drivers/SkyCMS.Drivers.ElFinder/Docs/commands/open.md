# Command: `open`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#open

---

## Purpose

Opens a directory and returns everything the client needs to render it:

- **`cwd`** — the opened directory's own file object (metadata for the header/breadcrumb).
- **`files`** — flat list of all items in the directory (children), plus any additional context items (e.g. ancestors needed to restore tree state on init).
- **`options`** — volume-level settings (path separator, URL base, disabled commands, etc.).
- **`api`** (init only) — protocol version string, e.g. `"2.1"`.
- **`uplMaxSize`** (init only) — max upload size string, e.g. `"128M"`.

`open` is called:
1. **On init** (`init=1`) — to bootstrap the entire UI; returns volume list in `files` plus `api`/`uplMaxSize`.
2. **On navigation** — when the user clicks a folder in the tree or file list.
3. **After reload** — to refresh a directory after an operation.

---

## HTTP

```
GET/POST ?cmd=open&target={hash}&init={0|1}&tree={0|1}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes (unless `init=1`) | Hash of directory to open |
| `init` | No | `1` on first load; triggers volume info |
| `tree` | No | `1` to include ancestor path in `files` for tree restoration |

---

## Example request (navigation)

```
GET /elfinder/connector?cmd=open&target=l1_cHViL2ltYWdlcw
```

---

## Example response

```json
{
  "cwd": {
    "hash":   "l1_cHViL2ltYWdlcw",
    "phash":  "l1_cHVi",
    "name":   "images",
    "mime":   "directory",
    "ts":     1714300000,
    "size":   0,
    "dirs":   1,
    "read":   1,
    "write":  1,
    "locked": 0
  },
  "files": [
    {
      "hash":   "l1_cHViL2ltYWdlcw",
      "phash":  "l1_cHVi",
      "name":   "images",
      "mime":   "directory",
      "ts":     1714300000,
      "size":   0,
      "dirs":   1,
      "read":   1,
      "write":  1,
      "locked": 0
    },
    {
      "hash":   "l1_cHViL2ltYWdlcy9sb2dvLnBuZw",
      "phash":  "l1_cHViL2ltYWdlcw",
      "name":   "logo.png",
      "mime":   "image/png",
      "ts":     1714200000,
      "size":   42000,
      "read":   1,
      "write":  1,
      "locked": 0,
      "tmb":    "l1_cHViL2ltYWdlcy9sb2dvLnBuZw.png"
    }
  ],
  "options": {
    "path":         "pub/images",
    "url":          "https://cdn.example.com/pub/images/",
    "tmbUrl":       "/elfinder/thumbnail/",
    "separator":    "/",
    "disabled":     ["archive", "extract", "chmod"],
    "archivers":    { "create": [], "extract": [] },
    "copyOverwrite": 1,
    "uploadMaxSize": 134217728
  }
}
```

---

## SkyCMS notes

- Legacy path: `HandleOpenAsync()` in `ElFinderConnectorController`.
- CQRS path: `OpenCommand` / `OpenCommandHandler`.
- `options.disabled` is populated from the driver's `DisabledCommands` list (configured via `ElFinderOptions`).
- `options.url` is the public CDN/blob base URL; used by client to build download links.
- On `init=1`, `files` includes volume root entries (one per configured volume).

---

## ⚠️ Ancestor chain requirement (important)

The `files` array in an `open` response must contain **every directory node from the volume root down to the opened folder**, not just the folder's immediate children. This is required so elFinder can resolve the `phash` chain — each directory links to its parent via `phash`, and elFinder traverses that chain to position the folder in the tree panel.

### What the client does with `files`

elFinder's tree panel is entirely client-side. When you open `/pub/content/pages/2025`, the client walks the `phash` chain upwards:

```
2025  →(phash)→  pages  →(phash)→  content  →(phash)→  pub (root)
```

If any node in that chain is **absent from `files`**, the client cannot place `2025` in the tree and silently falls back to issuing a `parents` request. If that also fails or returns stale data the tree collapses to root only, showing a broken/empty expand state.

### What `files` must contain

For an `open` targeting `/pub/a/b/c`, the minimum correct `files` array includes:

| Entry | Why |
|-------|-----|
| `/pub` (root) | Volume root — must include `volumeid`, must omit `phash` |
| Siblings of `/pub/a` (i.e. all direct children of `/pub`) | So the tree can display the full contents of root |
| Siblings of `/pub/a/b` (i.e. all direct children of `/pub/a`) | So the tree can display the full contents of `/pub/a` |
| `/pub/a/b/c` (the cwd) | The opened folder itself |
| Direct children of `/pub/a/b/c` | File/folder listing for the main panel |

This is the same data the `parents` command returns — the `open` handler mirrors that logic so the tree is correct from the initial response without a round-trip.

### Regression history

A previous version of `HandleOpenAsync()` returned only `[root, cwd, cwd_children]`. Navigating to a deeply-nested folder caused the tree to collapse to just the `pub` root and threw a client-side error when the user tried to expand it. The fix walks from `cwd` up to root, loads directory siblings at each ancestor level, and deduplicates via a `seenHashes` set — mirroring `HandleParentsAsync`.

See [skycms-implementation-notes.md — phash chain contract](../skycms-implementation-notes.md#phash-chain-contract--commands-that-must-return-the-full-ancestor-path) for the cross-cutting rule and the list of affected commands.
