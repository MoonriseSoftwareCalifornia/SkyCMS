# Command: `tmb`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#tmb

---

## Purpose

Requests thumbnail generation for a list of image files. The client sends hashes of images it wants thumbnails for, and the connector returns a map of `hash → thumbnail URL` for those it could generate. Thumbnails not yet ready can be polled by repeating the request.

---

## HTTP

```
GET/POST ?cmd=tmb&targets[]={hash}&targets[]={hash}...
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `targets[]` | Yes | Hashes of image files needing thumbnails |

---

## Example request

```
POST /elfinder/connector
cmd=tmb&targets[]=l1_cHViL2ltYWdlcy9sb2dvLnBuZw&targets[]=l1_cHViL2ltYWdlcy9iYW5uZXIuanBn
```

---

## Example response

```json
{
  "images": {
    "l1_cHViL2ltYWdlcy9sb2dvLnBuZw":    "/elfinder/thumbnail/l1_cHViL2ltYWdlcy9sb2dvLnBuZw.png",
    "l1_cHViL2ltYWdlcy9iYW5uZXIuanBn": "/elfinder/thumbnail/l1_cHViL2ltYWdlcy9iYW5uZXIuanBn.png"
  }
}
```

Only hashes for which a thumbnail **was successfully generated or already existed** appear in `images`. Missing hashes are silently omitted — the client retries on the next poll.

---

## SkyCMS notes

- Legacy path: `HandleTmbAsync()`.
- CQRS path: `TmbCommand` / `TmbCommandHandler`.
- Thumbnail URL must be a path the browser can `GET` directly (served from the connector's `/thumbnail/{hash}` route or a CDN path).
- Thumbnail generation can be deferred/async; the client polls `tmb` until all hashes appear.
- For Azure Blob, thumbnails should be generated on-demand and cached as blobs in a `.tmb/` prefix or a dedicated container.

### Known issue (fixed)
An earlier bug caused the thumbnail URL to include a double-encoded or incorrect hash format. The fix aligned the URL pattern with the connector's thumbnail route. See git history on `TmbCommandHandler`.
