# Command: `rename`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#rename

---

## Purpose

Renames a single file or directory. The item's hash changes after rename (because the hash encodes the path); the client swaps the old entry for the new one using `added`/`removed`.

---

## HTTP

```
GET/POST ?cmd=rename&target={hash}&name={newName}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of the item to rename |
| `name` | Yes | New name (no path separators) |

---

## Example request

```
POST /elfinder/connector
cmd=rename&target=l1_cHViL2ltYWdlcy9sb2dvLnBuZw&name=logo-new.png
```

---

## Example response

```json
{
  "added": [
    {
      "hash":   "l1_cHViL2ltYWdlcy9sb2dvLW5ldy5wbmc",
      "phash":  "l1_cHViL2ltYWdlcw",
      "name":   "logo-new.png",
      "mime":   "image/png",
      "ts":     1714300000,
      "size":   42000,
      "read":   1,
      "write":  1,
      "locked": 0
    }
  ],
  "removed": ["l1_cHViL2ltYWdlcy9sb2dvLnBuZw"]
}
```

---

## SkyCMS notes

- Legacy path: `HandleRenameAsync()`.
- CQRS path: `RenameCommand` / `RenameCommandHandler`.
- Returns `errExists` if the target name already exists.
- Returns `errRename` on storage failure.
- For directories, all descendant blobs must be moved (copy + delete each blob with the new prefix) — no atomic directory rename in Azure Blob Storage.
