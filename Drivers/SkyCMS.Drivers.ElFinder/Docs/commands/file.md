# Command: `file`

**Status:** 📋 TODO — planned, not yet implemented  
**Priority:** High  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#file

---

## Purpose

Streams a file directly to the browser. Used for:

- **Download** — `Content-Disposition: attachment`.
- **Inline preview** — `Content-Disposition: inline` (e.g. PDF, video, audio preview panel).

The `file` command is the only way to serve files that are **not publicly accessible** via a direct CDN/blob URL (e.g. private blobs, access-controlled media).

> **Note:** If all blobs in SkyCMS are already publicly accessible via CDN URL, this command is lower priority. However it is required for the elFinder download toolbar button to function when a file's `options.url` is not set or overridden.

---

## HTTP

```
GET ?cmd=file&target={hash}&download={0|1}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of the file to serve |
| `download` | No | `1` = force `Content-Disposition: attachment` |

---

## Example request

```
GET /elfinder/connector?cmd=file&target=l1_cHViL2ltYWdlcy9sb2dvLnBuZw&download=1
```

---

## Expected response

Raw binary file stream (NOT JSON).

Headers:
```
Content-Type: image/png
Content-Disposition: attachment; filename="logo.png"
Content-Length: 42000
```

---

## Implementation notes

- The response is **not JSON** — it is a raw `FileStreamResult` or `FileContentResult`.
- Set `Content-Type` from the file's stored MIME type.
- If `download=1` or MIME is not inline-safe, set `Content-Disposition: attachment`.
- For inline types (image/*, text/*, application/pdf), use `Content-Disposition: inline` when `download=0`.
- For Azure Blob: use `BlobClient.DownloadStreamingAsync()` and pipe to response body; or redirect to a short-lived SAS URL.

## Suggested implementation path

1. Add `FileCommand : IRequest<IElFinderResponse>` (target + download flag).
2. Add `FileCommandHandler` — get blob stream, return `FileStreamElFinderResponse` (new response type wrapping `FileStreamResult`).
3. Controller special-cases this response type and returns it directly instead of serializing to JSON.
4. Add legacy fallback in controller.
5. Remove from `options.disabled`.
6. Add tests: download header, inline header, not-found error, access-denied error.
