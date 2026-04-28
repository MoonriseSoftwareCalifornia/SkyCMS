# Command: `size`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#size

---

## Purpose

Returns the **total byte size** of one or more files and/or directories (directories are measured recursively). Shown in the "Properties" dialog.

---

## HTTP

```
GET/POST ?cmd=size&targets[]={hash}&targets[]={hash}...
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `targets[]` | Yes | One or more item hashes |

---

## Example request

```
GET /elfinder/connector?cmd=size&targets[]=l1_cHViL2ltYWdlcw
```

---

## Example response

```json
{
  "size": 1048576
}
```

The `size` value is a single integer representing the **sum** of all specified targets (recursively for directories).

---

## SkyCMS notes

- Legacy path: `HandleSizeAsync()`.
- CQRS path: `SizeCommand` / `SizeCommandHandler`.
- For Azure Blob, directory size requires iterating all blobs with the path prefix and summing `ContentLength` properties.
- Can be slow for large directories — consider returning a loading state on the client side while awaiting.
- Returns `0` for empty directories.
