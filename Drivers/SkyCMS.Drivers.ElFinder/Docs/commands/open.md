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
