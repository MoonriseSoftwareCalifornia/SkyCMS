# Command: `paste`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#paste

---

## Purpose

Copies or moves one or more files/directories to a destination directory. The `cut` parameter distinguishes move (1) from copy (0).

---

## HTTP

```
POST ?cmd=paste&dst={hash}&targets[]={hash}&targets[]={hash}&cut={0|1}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `dst` | Yes | Hash of the destination directory |
| `targets[]` | Yes | Hashes of items to copy/move |
| `cut` | Yes | `1` = move, `0` = copy |
| `renames[]` | No | New names for items that would collide (parallel with `targets[]`) |
| `suffix` | No | Auto-rename suffix when `renames[]` not supplied (e.g. `~`) |

---

## Example request (copy)

```
POST /elfinder/connector
cmd=paste&dst=l1_cHViL2FyY2hpdmU&targets[]=l1_cHViL2ltYWdlcy9sb2dvLnBuZw&cut=0
```

---

## Example response

```json
{
  "added": [
    {
      "hash":   "l1_cHViL2FyY2hpdmUvbG9nby5wbmc",
      "phash":  "l1_cHViL2FyY2hpdmU",
      "name":   "logo.png",
      "mime":   "image/png",
      "ts":     1714300000,
      "size":   42000,
      "read":   1,
      "write":  1,
      "locked": 0
    }
  ],
  "removed": []
}
```

For **move** (`cut=1`), `removed` contains the original hashes.

---

## SkyCMS notes

- Legacy path: `HandlePasteAsync()`.
- CQRS path: `PasteCommand` / `PasteCommandHandler`.
- Azure Blob has no server-side copy-and-delete primitive for directories; each blob must be individually copied then deleted.
- Overwrite behaviour is controlled by `options.copyOverwrite` (default `1`).
- Returns `errCopyTo` / `errCopyFrom` if the volume does not support the operation.
- Cross-volume paste (different `volumeid` prefixes) should be treated as copy regardless of `cut`.
