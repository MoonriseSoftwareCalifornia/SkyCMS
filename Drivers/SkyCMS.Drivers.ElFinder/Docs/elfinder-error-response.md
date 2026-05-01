# elFinder Error Response

**Used by:** all commands on failure  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#response

---

## Shape

```json
{ "error": ["errUnknown"] }
```

The `error` value is always a **JSON array of strings**. Each string is an elFinder error token. The client maps these tokens to localised UI messages.

---

## Common error tokens

| Token | Meaning |
|-------|---------|
| `errUnknown` | Generic / unclassified error |
| `errUnknownCmd` | Command not recognised |
| `errJSONData` | Connector returned malformed JSON |
| `errOpen` | Failed to open directory |
| `errNotFound` | Target item not found |
| `errAccess` | Access denied |
| `errLocked` | Target is locked |
| `errExists` | An item with that name already exists |
| `errInvParams` | Invalid parameters supplied |
| `errUploadFile` | Upload failed |
| `errMkdir` | Failed to create directory |
| `errMkfile` | Failed to create file |
| `errRename` | Rename failed |
| `errCopy` | Copy failed |
| `errMove` | Move failed |
| `errRm` | Delete failed |
| `errResize` | Image resize failed |
| `errUsupportType` | Unsupported file type |
| `errFileNotFound` | Specific file not found |
| `errTrgFolderNotFound` | Target folder not found |
| `errCopyFrom` | Cannot copy from this volume |
| `errCopyTo` | Cannot copy to this volume |

---

## Multiple errors

When a batch operation (e.g. `rm`, `paste`) partially succeeds, the response may include both result data **and** an `error` array:

```json
{
  "removed": ["l1_abc"],
  "error": ["errRm", "l1_def"]
}
```

The second element of the array is typically the hash of the failing item.

---

## SkyCMS implementation

`ElFinderErrorResponse` is the C# class in `Responses/IElFinderResponse.cs`. Factory helpers:

| Method | Emitted token |
|--------|---------------|
| `ElFinderErrorResponse.Unknown(msg)` | `errUnknown` |
| `ElFinderErrorResponse.NotFound(msg)` | `errFileNotFound` |
| `ElFinderErrorResponse.Access(msg)` | `errAccess` |
| `ElFinderErrorResponse.InvalidParams(msg)` | `errInvParams` |
| `ElFinderErrorResponse.Exists(msg)` | `errExists` |

Handlers should always return a typed `ElFinderErrorResponse` rather than throwing exceptions, so the client receives a parseable token and not an HTML 500 page.
