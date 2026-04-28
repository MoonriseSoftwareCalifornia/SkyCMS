# Command: `ls`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#ls

---

## Purpose

Returns a list of **item names** (not full file objects) in a directory. Used by the client to check for name conflicts before a rename, paste, or upload operation — so a lightweight name-only response is intentional.

---

## HTTP

```
GET/POST ?cmd=ls&target={hash}&intersect[]={name}&intersect[]={name}...
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of directory to list |
| `intersect[]` | No | List of names to check for conflicts (returns only matching names) |

---

## Example request (conflict check before rename)

```
GET /elfinder/connector?cmd=ls&target=l1_cHViL2ltYWdlcw&intersect[]=logo.png&intersect[]=banner.jpg
```

---

## Example response (full list)

```json
{
  "list": ["logo.png", "banner.jpg", "2024"]
}
```

## Example response (intersect filter)

```json
{
  "list": ["logo.png"]
}
```

Only names from `intersect[]` that actually exist in the directory are returned.

---

## SkyCMS notes

- Legacy path: `HandleLsAsync()`.
- CQRS path: `LsCommand` / `LsCommandHandler`.
- Returns names only, not hashes or metadata.
- If `intersect[]` is provided, the response is filtered server-side.
