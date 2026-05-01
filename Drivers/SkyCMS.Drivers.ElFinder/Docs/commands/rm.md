# Command: `rm`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#rm

---

## Purpose

Deletes one or more files and/or directories. Directories are deleted recursively. The response lists the hashes of every successfully deleted item so the client can remove them from its internal model.

---

## HTTP

```
GET/POST ?cmd=rm&targets[]={hash}&targets[]={hash}...
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `targets[]` | Yes | One or more hashes of items to delete |

---

## Example request

```
POST /elfinder/connector
cmd=rm&targets[]=l1_cHViL2ltYWdlcy9sb2dvLnBuZw&targets[]=l1_cHViL2RvY3M
```

---

## Example response (all succeed)

```json
{
  "removed": [
    "l1_cHViL2ltYWdlcy9sb2dvLnBuZw",
    "l1_cHViL2RvY3M"
  ]
}
```

## Example response (partial failure)

```json
{
  "removed": ["l1_cHViL2ltYWdlcy9sb2dvLnBuZw"],
  "error":   ["errRm", "l1_cHViL2RvY3M"]
}
```

---

## SkyCMS notes

- Legacy path: `HandleRmAsync()`.
- CQRS path: `RmCommand` / `RmCommandHandler`.
- For directories: iterates all blobs with the path prefix and deletes them individually (Azure Blob has no recursive-delete primitive; use `GetBlobsAsync` with prefix filter).
- Returns `errLocked` for items with `locked=1`.
- Locked-item check should be done before starting any deletions to avoid partial state.
