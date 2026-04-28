# SkyCMS elFinder Implementation Notes

Cross-cutting implementation details specific to the SkyCMS connector. Read alongside the individual command docs.

---

## Architecture: Legacy vs CQRS

The connector supports two routing paths, selectable per-command via `UseCqrsForCommand(cmd)` in `ElFinderConnectorController`:

| Path | Entry point | When to use |
|------|-------------|-------------|
| **Legacy** | `Handle*Async()` methods on the controller | Commands not yet migrated; fallback |
| **CQRS** | `IMediator.Send(command)` → `*CommandHandler` | All new command work; preferred |

When implementing a new command, add both:
- A `*Command` / `*CommandHandler` pair in `Drivers/SkyCMS.Drivers.ElFinder/Handlers/`
- A legacy fallback `Handle*Async()` on the controller (can delegate to the handler via MediatR)
- Register the command name in the `UseCqrsForCommand` switch so the CQRS path is used

---

## JSON serialization — critical caveat

The app-level JSON pipeline uses **Newtonsoft.Json** with `DefaultContractResolver` (PascalCase property names, no `[JsonIgnore]` support for STJ attributes). All elFinder DTOs use **System.Text.Json** `[JsonPropertyName]` attributes (lowercase).

**This mismatch means you must NOT use `Json(response)` for CQRS responses.** The controller's `Json()` helper runs through Newtonsoft and will:
- Produce PascalCase keys (`Hash`, `Phash`, `Name`, …) instead of `hash`, `phash`, `name`
- Ignore `[JsonIgnore]` attributes, leaking internal-only properties

**Correct pattern for CQRS command responses:**

```csharp
var json = System.Text.Json.JsonSerializer.Serialize(response);
return Content(json, "application/json");
```

This is already applied to `HandleParentsViaCqrsAsync()`. Apply the same pattern to any new CQRS command handler wired into the controller.

The legacy path is unaffected — legacy handlers return `IActionResult` directly and manage their own serialization.

---

## Azure Blob virtual directories

Azure Blob Storage has no real directory objects. Directories are inferred from blob name prefixes (e.g. `pub/images/logo.png` implies a virtual directory `pub/images/`).

### Implications

| Operation | Behaviour |
|-----------|-----------|
| `GetFileAsync(path)` | Returns `null` for a path that is a virtual directory (no marker blob) |
| `GetFilesAndDirectories(path)` | Returns `DirectoryItem` entries for virtual sub-prefixes |
| Directory existence | Must be inferred by listing children — a prefix with at least one blob "exists" |
| Directory metadata (`ts`, `size`) | Must be synthesised: `ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()`, `size = 0` |
| Rename/move directory | No atomic primitive — must copy then delete all blobs under the prefix individually |
| Delete directory | Must enumerate and delete all blobs under the prefix individually |

### GetEntryAsync virtual-directory fallback

`ElFinderStorageAdapter.GetEntryAsync()` handles this with a fallback:

```csharp
var file = await _context.GetFileAsync(path);
if (file is null)
{
    // May be a virtual directory — check if any children exist
    var children = await _context.GetFilesAndDirectories(path);
    if (children.Any())
        return SynthesizeDirectoryEntry(path);
}
```

### GetAncestorsAsync

When building the `parents` tree, each ancestor segment is resolved via `GetEntryAsync` (with the above fallback) rather than `GetFileAsync` directly, to avoid missing virtual-directory ancestors.

---

## Hash encoding

```
hash = volumeId + Base64Url(storagePath)
```

- `volumeId` — e.g. `l1_` (letter + digit + underscore, unique per volume).
- `storagePath` — the container-relative path, e.g. `pub/images`.
- Base64Url: standard Base64 with `+→-`, `/→_`, padding `=` stripped.

Encode/decode via `IElFinderStorageAdapter.EncodePath()` / `DecodePath()`.

Volume root entries must:
- Include `"volumeid": "l1_"` in the file object
- Omit `"phash"` entirely (not null, not empty string — absent)

---

## ElFinderOptions

Key configuration values (set in `appsettings.json` or DI options):

| Option | Purpose |
|--------|---------|
| `DisabledCommands` | List of command names surfaced as `options.disabled` to client |
| `MaxUploadSizeMb` | Returned as `uplMaxSize` on `init` |
| `ThumbnailRoute` | Base path for thumbnail URLs (e.g. `/elfinder/thumbnail/`) |
| `VolumeId` | Volume identifier prefix (e.g. `l1_`) |
| `RootPath` | Storage root path within the blob container |
| `PublicUrl` | CDN/blob base URL returned as `options.url` |

---

## Thumbnail URL pattern

Thumbnail URLs must be routable by the browser directly (not through the connector JSON endpoint). The pattern used is:

```
{ThumbnailRoute}{hash}.png
```

e.g. `/elfinder/thumbnail/l1_cHViL2ltYWdlcy9sb2dvLnBuZw.png`

The connector exposes a separate `GET /elfinder/thumbnail/{hash}` route that streams the thumbnail blob.

> **Known fix:** an earlier bug produced double-encoded or mismatched hash values in thumbnail URLs. Resolved — see `TmbCommandHandler` history.

---

## Disabled commands (current)

Commands listed in `options.disabled` that the client will hide from its toolbar:

| Command | Reason |
|---------|--------|
| `archive` | No server-side archive tooling |
| `extract` | No server-side archive tooling |
| `chmod` | Not applicable to blob storage |
| `search` | Not yet implemented (📋 TODO) |
| `duplicate` | Not yet implemented (📋 TODO) |
| `file` | Not yet implemented (📋 TODO) |
| `resize` | Not yet implemented (📋 TODO) |
| `url` | Not yet implemented (📋 TODO) |
| `dim` | Not yet implemented (📋 TODO) |

Remove a command from this list when its implementation is complete.

---

## Related docs

- [File Object Schema](elfinder-file-object.md)
- [Error Response Schema](elfinder-error-response.md)
- [Command Index](README.md)
