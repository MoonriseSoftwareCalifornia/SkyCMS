# Command: `upload`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#upload

---

## Purpose

Uploads one or more files to a target directory. Supports standard multipart form uploads and chunked uploads for large files (API 2.1+).

---

## HTTP

```
POST /elfinder/connector   (multipart/form-data)
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `cmd` | Yes | `"upload"` |
| `target` | Yes | Hash of destination directory |
| `upload[]` | Yes | One or more file form fields |
| `upload_path[]` | No | Relative sub-paths for each uploaded file (for drag-drop directory upload) |
| `mtime[]` | No | Client-side modification timestamps (parallel with `upload[]`) |
| `overwrite` | No | `0` = auto-rename on collision instead of overwriting |
| `chunk` | No | Chunk index (0-based) for chunked upload |
| `cid` | No | Chunk group ID (unique per upload session) |
| `range` | No | `start,length,total` — byte range for chunked upload |

---

## Example request (simple single file)

```
POST /elfinder/connector
Content-Type: multipart/form-data

cmd=upload
target=l1_cHViL2ltYWdlcw
upload[]=<binary data of logo.png>
```

---

## Example response

```json
{
  "added": [
    {
      "hash":   "l1_cHViL2ltYWdlcy9sb2dvLnBuZw",
      "phash":  "l1_cHViL2ltYWdlcw",
      "name":   "logo.png",
      "mime":   "image/png",
      "ts":     1714300000,
      "size":   42000,
      "read":   1,
      "write":  1,
      "locked": 0
    }
  ]
}
```

For chunked uploads, intermediate chunk responses return `{ "_chunkmerged": "filename", "_name": "filename" }` when the final chunk is received and merged.

---

## SkyCMS notes

- Legacy path: `HandleUploadAsync()`.
- CQRS path: `UploadCommand` / `UploadCommandHandler`.
- `uplMaxSize` returned on `init` controls the client-side size warning (configured in `ElFinderOptions`).
- Chunked upload support allows files larger than the request size limit.
- MIME type is inferred from file extension and content sniffing.
