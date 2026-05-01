# Command: `url`

**Status:** 📋 TODO — planned, not yet implemented  
**Priority:** Low  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#url

---

## Purpose

Returns the public URL for a file. Used by plugins that need the direct URL (e.g. the "Copy URL" toolbar button, rich-text editor integrations). Distinct from the `options.url` base — this returns the full URL for a specific file hash.

---

## HTTP

```
GET/POST ?cmd=url&target={hash}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of the file |

---

## Example request

```
GET /elfinder/connector?cmd=url&target=l1_cHViL2ltYWdlcy9sb2dvLnBuZw
```

---

## Expected response

```json
{
  "url": "https://cdn.example.com/pub/images/logo.png"
}
```

---

## Implementation notes

- For Azure Blob with public access: construct URL as `{blobServiceUrl}/{container}/{path}`.
- For private blobs: generate a short-lived SAS URL (define TTL in `ElFinderOptions`).
- If `options.url` is already set on the volume, the client may construct URLs itself — but `url` command is the authoritative source for plugins that don't read `options`.
- Return `errFileNotFound` if the hash does not resolve to an existing blob.

## Suggested implementation path

1. Add `UrlCommand : IRequest<IElFinderResponse>` (target).
2. Add `UrlCommandHandler` — resolve path, generate public or SAS URL via adapter.
3. Add `GetPublicUrlAsync(string path)` method to `IElFinderStorageAdapter` if not present.
4. Add legacy fallback in controller.
5. Remove from `options.disabled`.
6. Add tests: public URL format, private blob SAS URL, not-found error.
