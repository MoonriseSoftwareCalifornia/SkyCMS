# Command: `mkfile`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#mkfile

---

## Purpose

Creates a new **empty file** with the specified name in the target directory. The client then typically opens it in the inline editor via `put`.

---

## HTTP

```
GET/POST ?cmd=mkfile&target={hash}&name={name}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of parent directory |
| `name` | Yes | Name of the new file (including extension) |

---

## Example request

```
POST /elfinder/connector
cmd=mkfile&target=l1_cHViL2ltYWdlcw&name=notes.txt
```

---

## Example response

```json
{
  "added": [
    {
      "hash":   "l1_cHViL2ltYWdlcy9ub3Rlcy50eHQ",
      "phash":  "l1_cHViL2ltYWdlcw",
      "name":   "notes.txt",
      "mime":   "text/plain",
      "ts":     1714300000,
      "size":   0,
      "read":   1,
      "write":  1,
      "locked": 0
    }
  ]
}
```

---

## SkyCMS notes

- Legacy path: `HandleMkfileAsync()`.
- CQRS path: `MkfileCommand` / `MkfileCommandHandler`.
- Returns `errExists` if a file with that name already exists.
- Returns `errMkfile` on storage failure.
- MIME type is inferred from file extension at creation time.
