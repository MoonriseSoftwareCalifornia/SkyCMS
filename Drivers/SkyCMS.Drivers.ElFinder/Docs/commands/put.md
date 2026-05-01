# Command: `put`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#put

---

## Purpose

Saves **text content** to an existing file. Counterpart to `get`. Called by the inline editor when the user saves a file.

---

## HTTP

```
POST ?cmd=put&target={hash}&content={text}&encoding={enc}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of the file to overwrite |
| `content` | Yes | New file content (URL-encoded text) |
| `encoding` | No | Character encoding, e.g. `"UTF-8"` |

---

## Example request

```
POST /elfinder/connector
cmd=put&target=l1_cHViL2ZpbGVzL25vdGVzLnR4dA&content=Hello%2C%20updated!
```

---

## Example response

```json
{
  "changed": [
    {
      "hash":   "l1_cHViL2ZpbGVzL25vdGVzLnR4dA",
      "phash":  "l1_cHViL2ZpbGVz",
      "name":   "notes.txt",
      "mime":   "text/plain",
      "ts":     1714305000,
      "size":   17,
      "read":   1,
      "write":  1,
      "locked": 0
    }
  ]
}
```

The `changed` array contains the updated file object (with new `ts` and `size`).

---

## SkyCMS notes

- Legacy path: `HandlePutAsync()`.
- CQRS path: `PutCommand` / `PutCommandHandler`.
- Returns `errFileNotFound` if the target does not exist.
- Returns `errAccess` if the file is not writable.
- Content is typically UTF-8; the connector should set blob `ContentType` to match the file's MIME type on write.
