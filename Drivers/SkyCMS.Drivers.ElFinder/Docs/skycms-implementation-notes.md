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

## phash chain contract — commands that must return the full ancestor path

elFinder's tree panel is a client-side structure built entirely from the `phash` links returned by the connector. Every directory file object carries a `phash` that names its parent's hash. The client walks that chain upwards from any directory to reconstruct the path to root and render the correct expand/select state.

**If any node in the chain is missing from the server response, the client cannot resolve the tree and collapses to root.**

### Commands affected

| Command | Response key | Must include |
|---------|-------------|-------------|
| `open` | `files[]` | Root node, every ancestor directory between root and the opened folder (with their siblings at each level), the cwd, and the cwd's direct children |
| `parents` | `tree[]` | Same set: root, all ancestors with siblings, the target's direct children |
| `tree` | `tree[]` | All direct children of `target`; called when the user expands a node that has not been loaded yet — does **not** need ancestors (tree already loaded them on the way down) |

### What the full set looks like

For a target path `/pub/a/b/c`:

```
files[] (for open) or tree[] (for parents)
─────────────────────────────────────────────
/pub                         ← root (has volumeid, no phash)
/pub/a                       ┐
/pub/other-child-of-pub      ┘ siblings at depth 1
/pub/a/b                     ┐
/pub/a/other-child-of-a      ┘ siblings at depth 2
/pub/a/b/c                   ← the cwd / target
/pub/a/b/c/child1            ┐
/pub/a/b/c/child2            ┘ direct children of target
```

### Implementation rule

When implementing or modifying any command that returns a `files[]` or `tree[]` containing a directory that is not the volume root:

1. Walk from the target directory up to root, collecting each ancestor path.
2. For each ancestor, load all its directory siblings (i.e. call `GetFilesAndDirectories(parent_of_ancestor)`).
3. Deduplicate entries using a `seenHashes` set to prevent duplicate entries if a node appears both as an ancestor and as a sibling.
4. Always include the volume root with `volumeid` set and `phash` absent.
5. Include the target's direct children last.

The reference implementation is `HandleParentsAsync()` in `ElFinderConnectorController`. The `HandleOpenAsync()` fix mirrors this exactly.

### Why it cannot be worked around client-side

elFinder does send a `parents` command as a fallback when `open` is missing ancestor nodes, but this causes a second round-trip, temporarily broken tree state, and a visible UI flicker. The `parents` fallback also does not always fire reliably (it depends on client state at the time of the initial response). The correct approach is to return the full ancestor chain from `open` directly.

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

---

## elFinder client-side caching contract

This behaviour is not documented in the official elFinder wiki but is critical to understand before modifying any command that returns `files[]` or `tree[]`.

The elFinder JavaScript client maintains a per-directory child cache. When a response arrives, the client groups all returned file/directory objects by their `phash` value. **For each `phash` group, the incoming set completely replaces any previously cached children for that parent directory.**

This has an important consequence for the `open` command:

> If you include ancestor directories in a navigation-mode `open` response, the client will overwrite its cached child list for those ancestors' parents with only the subset you returned — hiding every sibling folder the client had already loaded.

### What this means in practice

Assume the user has previously expanded `/pub/images` and `/pub/docs` (both are cached as children of `/pub`). They then click `/pub/images/logos` to navigate into it. If the connector's `open` response for `/pub/images/logos` includes `/pub/images` in `files[]`, the client now treats `/pub`'s children as `[/pub/images]` alone — `/pub/docs` vanishes from the tree until the next full reload.

### The rule

| Mode | `files[]` must contain | `files[]` must NOT contain |
|------|------------------------|---------------------------|
| **Tree-restoration** (`init=1` or `tree=1`) | Root + siblings at every ancestor level (including cwd's own sibling level) + cwd + direct children | Nothing extra; the full set above is correct and complete |
| **Navigation** (neither flag) | Only the direct children of the opened folder | Any ancestor of the opened folder — including root |

The SkyCMS connector enforces this split in `HandleOpenAsync()` via the `isTreeMode` flag. See also [open.md](commands/open.md) for the `⚠️ Ancestor chain requirement` section.

---

## `open` command — cwd sibling level requirement

A subtlety not described by the official docs: in tree-restoration mode, siblings must be included at **every ancestor level including the cwd's own level**.

Without the cwd's own siblings in `files[]`, the client's view of the cwd's parent shows only the cwd — all other folders at the same depth are invisible to the user until they navigate away and back.

For a target `/pub/a/b/c`, the complete tree-restoration set is:

```
/pub                         ← root (volumeid present, phash absent)
/pub/a                       ┐
/pub/other-child-of-pub      ┘ siblings at depth 1  (children of /pub)
/pub/a/b                     ┐
/pub/a/sibling-of-b          ┘ siblings at depth 2  (children of /pub/a)
/pub/a/b/c                   ← cwd
/pub/a/b/sibling-of-c        ← cwd's sibling       (also child of /pub/a/b)
/pub/a/b/c/child1            ┐
/pub/a/b/c/child2            ┘ direct children of cwd
```

This was confirmed by HAR comparison with the reference PHP connector (studio-42/elFinder).

---

## Protocol reference — undocumented behaviour

The official elFinder 2.1 documentation at <https://github.com/Studio-42/elFinder/wiki> does not document the caching contract, the two-mode `open` distinction, the sibling level requirement, or the navigation-mode negative constraint described above.

When the documented behaviour and the actual client behaviour diverge, treat these as the authoritative sources in priority order:

1. **studio-42 PHP connector source** — `elFinderVolumeDriver::dir()` and the `open` command dispatcher. This is the reference implementation the JavaScript client was written against.
2. **HAR trace comparison** — record network traffic from a working PHP connector deployment alongside the SkyCMS connector for the same `open` request (with and without `init=1`). Differences in `files[]` shape reveal undocumented contracts.
3. **This document** — captures what has been discovered and verified for the SkyCMS implementation.

