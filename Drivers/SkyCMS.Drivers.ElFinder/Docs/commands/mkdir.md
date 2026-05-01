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

For batch creation (`dirs[]`), `added` contains one entry per directory, and `hashes` maps each requested name to its assigned hash:

```json
{
  "added": [
    { "hash": "l1_cHViL3RodW1ibmFpbHM", "name": "thumbnails", "mime": "directory", "..." : "..." },
    { "hash": "l1_cHViL2ljb25z",       "name": "icons",      "mime": "directory", "..." : "..." }
  ],
  "hashes": {
    "thumbnails": "l1_cHViL3RodW1ibmFpbHM",
    "icons":      "l1_cHViL2ljb25z"
  }
}
```

---

## SkyCMS notes

- Legacy path: `HandleMkdirAsync()`.
- CQRS path: `MkdirCommand` / `MkdirCommandHandler`.
- `MkdirCommand.Dirs` carries the `dirs[]` batch list; the handler creates each directory, adds it to `Added`, and populates `Hashes` (name → hash map).
- The controller reads `dirs[]` from the form, validates each name with `IsSafeName()`, and passes the list as `MkdirCommand.Dirs`.
- Azure Blob: directories are virtual; a marker blob may be created to anchor the path depending on adapter implementation.
- Returns `errExists` if the directory already exists.
- Returns `errMkdir` on storage failure.
