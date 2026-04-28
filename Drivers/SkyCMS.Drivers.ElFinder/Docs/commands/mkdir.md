# Command: `mkdir`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#mkdir

---

## Purpose

Creates one or more new directories. Supports both a single named directory and a batch of directories via `dirs[]`.

---

## HTTP

```
GET/POST ?cmd=mkdir&target={hash}&name={name}
POST     cmd=mkdir&target={hash}&dirs[]={name}&dirs[]={name}...
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of the parent directory |
| `name` | One of | Name for a single new directory |
| `dirs[]` | One of | Batch of directory names to create |

---

## Example request (single)

```
POST /elfinder/connector
cmd=mkdir&target=l1_cHViL2ltYWdlcw&name=thumbnails
```

---

## Example response

```json
{
  "added": [
    {
      "hash":   "l1_cHViL2ltYWdlcy90aHVtYm5haWxz",
      "phash":  "l1_cHViL2ltYWdlcw",
      "name":   "thumbnails",
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

For batch creation, `added` contains one entry per successfully created directory.

---

## SkyCMS notes

- Legacy path: `HandleMkdirAsync()`.
- CQRS path: `MkdirCommand` / `MkdirCommandHandler`.
- Azure Blob: directories are virtual; a marker blob (zero-byte `__dir__` or `.keep`) may be created to anchor the path, depending on adapter implementation.
- Returns `errExists` if the directory already exists.
- Returns `errMkdir` on storage failure.
