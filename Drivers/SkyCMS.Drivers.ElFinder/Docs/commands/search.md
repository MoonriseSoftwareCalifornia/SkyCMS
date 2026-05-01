# Command: `search`

**Status:** 📋 TODO — planned, not yet implemented  
**Priority:** High  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#search

---

## Purpose

Searches for files and directories by name (substring match) within a given target directory (and optionally its descendants). Powers the search toolbar in the elFinder UI.

---

## HTTP

```
GET/POST ?cmd=search&q={query}&target={hash}&mimes[]={mime}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `q` | Yes | Search string (substring match against item name) |
| `target` | No | Hash of directory to search within (defaults to volume root) |
| `mimes[]` | No | Filter results to these MIME types (e.g. `image/png`, `image/`) |

---

## Example request

```
GET /elfinder/connector?cmd=search&q=logo&target=l1_cHVi
```

---

## Expected response

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
      "locked": 0
    }
  ]
}
```

---

## Implementation notes

- Azure Blob SDK: use `GetBlobsAsync(prefix: targetPath)` and filter client-side where `blob.Name` contains `q` (case-insensitive).
- For prefix-scoped search, derive prefix from the `target` hash.
- For `mimes[]` filter, match against inferred MIME type of each result.
- Return file objects using the same shape as `open`'s `files` array.
- Empty result returns `{ "files": [] }` — not an error.
- Searches are **not** real-time indexed; large containers may be slow — consider a page size / result cap.

## Suggested implementation path

1. Add `SearchCommand : IRequest<IElFinderResponse>` (query, target, mimes).
2. Add `SearchCommandHandler` — list blobs with prefix, filter by name substring and MIME.
3. Add legacy fallback in controller.
4. Remove from `options.disabled`.
5. Add tests: exact match, substring match, MIME filter, no results, target-scoped vs root-scoped.
