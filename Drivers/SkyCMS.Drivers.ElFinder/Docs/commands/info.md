# Command: `info`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#info

---

## Purpose

Returns the full file object metadata for one or more items. Used by the client for the "File info" properties panel and to refresh stale cached entries.

---

## HTTP

```
GET/POST ?cmd=info&targets[]={hash}&targets[]={hash}...
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `targets[]` | Yes | One or more item hashes |

---

## Example request

```
GET /elfinder/connector?cmd=info&targets[]=l1_cHViL2ltYWdlcy9sb2dvLnBuZw
```

---

## Example response

```json
{
  "files": [
    {
      "hash":   "l1_cHViL2ltYWdlcy9sb2dvLnBuZw",
      "phash":  "l1_cHViL2ltYWdlcw",
      "name":   "logo.png",
      "mime":   "image/png",
      "ts":     1714200000,
      "size":   42000,
      "read":   1,
      "write":  1,
      "locked": 0,
      "tmb":    "l1_cHViL2ltYWdlcy9sb2dvLnBuZw.png"
    }
  ]
}
```

---

## SkyCMS notes

- Legacy path: `HandleInfoAsync()`.
- CQRS path: `InfoCommand` / `InfoCommandHandler`.
- Missing items are silently omitted from `files` (no error token per item).
- Can be called with a single hash or many; batch is common after paste/upload to refresh the client cache.
