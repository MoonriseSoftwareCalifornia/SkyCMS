# Command: `duplicate`

**Status:** 📋 TODO — planned, not yet implemented  
**Priority:** Medium  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#duplicate

---

## Purpose

Creates a named copy of one or more files/directories **within the same parent directory**. Functionally identical to `paste` with `cut=0` and `dst` equal to the item's own parent — but the client presents it as a distinct "Duplicate" toolbar action and expects the connector to auto-generate a non-colliding name (e.g. appending `~` or `(copy)`).

---

## HTTP

```
GET/POST ?cmd=duplicate&targets[]={hash}&targets[]={hash}...
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `targets[]` | Yes | Hashes of items to duplicate |

---

## Example request

```
POST /elfinder/connector
cmd=duplicate&targets[]=l1_cHViL2ltYWdlcy9sb2dvLnBuZw
```

---

## Expected response

```json
{
  "added": [
    {
      "hash":   "l1_cHViL2ltYWdlcy9sb2dvfi5wbmc",
      "phash":  "l1_cHViL2ltYWdlcw",
      "name":   "logo~.png",
      "mime":   "image/png",
      "ts":     1714305000,
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

- Resolve the parent from the source item's `phash`.
- Generate a unique name: try `{base}~.{ext}`, then `{base}~~.{ext}`, etc. until no collision.
- For directories, deep-copy all descendant blobs (prefix copy, like `paste`).
- Reuse the `IElFinderStorageAdapter` copy primitive already written for `paste`.

## Suggested implementation path

1. Add `DuplicateCommand : IRequest<IElFinderResponse>` (targets list).
2. Add `DuplicateCommandHandler` — resolve parents, generate names, call adapter copy for each.
3. Add legacy fallback handler in `ElFinderConnectorController.HandleDuplicateAsync()`.
4. Register in the command dispatch switch.
5. Remove from `options.disabled` list in `ElFinderOptions`.
6. Add unit tests: single file, multiple files, name-collision auto-suffix, directory.
