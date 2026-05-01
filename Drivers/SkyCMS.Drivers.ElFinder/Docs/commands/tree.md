# Command: `tree`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#tree

---

## Purpose

Returns the **immediate subdirectories** of a given folder so the tree panel can render the expand chevron's children. Called lazily when the user expands a folder node in the left-hand tree panel.

Only directories are returned — no files.

---

## HTTP

```
GET/POST ?cmd=tree&target={hash}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of the directory to expand |

---

## Example request

```
GET /elfinder/connector?cmd=tree&target=l1_cHViL2ltYWdlcw
```

---

## Example response

```json
{
  "tree": [
    {
      "hash":   "l1_cHViL2ltYWdlcy8yMDI0",
      "phash":  "l1_cHViL2ltYWdlcw",
      "name":   "2024",
      "mime":   "directory",
      "ts":     1714300000,
      "size":   0,
      "dirs":   0,
      "read":   1,
      "write":  1,
      "locked": 0
    }
  ]
}
```

---

## Relation to `parents`

| Command | When called | What it returns |
|---------|-------------|-----------------|
| `tree` | User **expands** a node | Children of that node only |
| `parents` | User **opens** a directory | Full ancestor chain + siblings (to restore the tree state) |
| `open` with `tree=1` | Init or navigation | Children of opened dir + ancestor chain |

---

## SkyCMS notes

- Legacy path: `HandleTreeAsync()`.
- CQRS path: `TreeCommand` / `TreeCommandHandler`.
- Azure Blob: subdirectories are enumerated via `GetFilesAndDirectories` filtered to `DirectoryItem` results only.
- `dirs` flag on each returned entry signals whether that subdirectory itself has children (drives the expand-chevron visibility).
