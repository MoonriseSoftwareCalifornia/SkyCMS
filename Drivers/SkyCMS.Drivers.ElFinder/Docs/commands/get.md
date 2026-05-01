# Command: `get`

**Status:** ✅ Implemented (CQRS + legacy)  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#get

---

## Purpose

Returns the **text content** of a file as a JSON string. Used by the inline text editor to load a file before editing. Only appropriate for text-based files (HTML, CSS, JS, plain text, etc.).

---

## HTTP

```
GET/POST ?cmd=get&target={hash}&conv={encoding}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of the file to read |
| `conv` | No | Encoding conversion hint (e.g. `0` = auto-detect, `1` = UTF-8) |

---

## Example request

```
GET /elfinder/connector?cmd=get&target=l1_cHViL2ZpbGVzL25vdGVzLnR4dA
```

---

## Example response

```json
{
  "content": "Hello, world!\nThis is a plain text file.\n"
}
```

---

## SkyCMS notes

- Legacy path: `HandleGetAsync()`.
- CQRS path: `GetCommand` / `GetCommandHandler`.
- The client does **not** use this for binary files (images, PDFs) — it uses the direct URL or `file` command instead.
- Returns `errFileNotFound` if the target does not exist.
- Returns `errAccess` if the file is not readable.
- `conv` parameter handling is optional; UTF-8 is the safe default for blob storage.
