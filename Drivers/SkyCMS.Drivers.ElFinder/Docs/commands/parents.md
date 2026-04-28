# Command: `parents`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#parents

---

## Purpose

Returns a flat list of **every directory the client needs to reconstruct the open tree path** from root down to the target. Specifically:

- All ancestor directories of `target` (root → parent).
- All **sibling directories** at each ancestor level (so each level of the tree panel shows its peers).
- The **direct children** of the target itself (so the opened folder's expand state is rendered).

This is called after `open` to restore the tree panel's visual state — without it, opening a deeply nested folder would collapse all parent nodes.

---

## HTTP

```
GET/POST ?cmd=parents&target={hash}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of the directory that was just opened |

---

## Example request

```
GET /elfinder/connector?cmd=parents&target=l1_cHViL2ltYWdlcy8yMDI0
```

---

## Example response

The `tree` array is a **flat list** — NOT nested. It includes the root, all intermediary dirs, their siblings, and children of `target`.

```json
{
  "tree": [
    {
      "hash":     "l1_cHVi",
      "name":     "pub",
      "mime":     "directory",
      "ts":       1714000000,
      "size":     0,
      "dirs":     1,
      "read":     1,
      "write":    1,
      "locked":   0,
      "volumeid": "l1_"
    },
    {
      "hash":   "l1_cHViL2ltYWdlcw",
      "phash":  "l1_cHVi",
      "name":   "images",
      "mime":   "directory",
      "ts":     1714100000,
      "size":   0,
      "dirs":   1,
      "read":   1,
      "write":  1,
      "locked": 0
    },
    {
      "hash":   "l1_cHViL2RvY3M",
      "phash":  "l1_cHVi",
      "name":   "docs",
      "mime":   "directory",
      "ts":     1714100000,
      "size":   0,
      "dirs":   0,
      "read":   1,
      "write":  1,
      "locked": 0
    },
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

## Wire format requirements

The response JSON key must be `"tree"` (lowercase). The elFinder client reads `data.tree` — any casing mismatch results in a silently empty tree and UI collapse.

Each entry's keys must also be lowercase (`hash`, `phash`, `name`, `mime`, `ts`, `size`, `dirs`, `read`, `write`, `locked`) — see [File Object](../elfinder-file-object.md).

---

## SkyCMS notes

- Legacy path: `HandleParentsAsync()`.
- CQRS path: `ParentsCommand` / `ParentsCommandHandler`.
- **Serialization**: `HandleParentsViaCqrsAsync()` in the controller uses `System.Text.Json.JsonSerializer.Serialize(response)` + `Content(..., "application/json")` — bypassing the Newtonsoft pipeline that would produce PascalCase keys and leak `[JsonIgnore]`-marked properties.
- **Virtual directories**: Azure Blob has no marker blob for directories. `GetEntryAsync()` falls back to `GetFilesAndDirectories` when `GetFileAsync` returns `null`, and synthesises a directory entry. See [SkyCMS Implementation Notes](../skycms-implementation-notes.md).
- Volume root entry must include `"volumeid"` field and must **omit** `phash`.
