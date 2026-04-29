# elFinder File/Directory Object

**Used by:** `open`, `tree`, `parents`, `ls`, `mkdir`, `mkfile`, `rename`, `paste`, `duplicate`, `upload`, `info`, `search`  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#file-object

---

## Shape

Every file and directory returned by the connector is an **elFinder file object** — a flat JSON dictionary. The client reconstructs the hierarchy from `hash`/`phash` links; no nesting occurs in the wire format.

```json
{
  "hash":    "l1_cHViL2ltYWdlcw",
  "phash":   "l1_cHVi",
  "name":    "images",
  "mime":    "directory",
  "size":    0,
  "ts":      1714300000,
  "dirs":    1,
  "read":    1,
  "write":   1,
  "locked":  0,
  "hidden":  0,
  "volumeid":"l1_"
}
```

---

## Fields

### Required for all items

| Field | JSON key | Type | Description |
|-------|----------|------|-------------|
| `Hash` | `hash` | string | Base64url-encoded path prefixed by volume id. Unique per volume. |
| `Name` | `name` | string | Display name (no path separators). |
| `Mime` | `mime` | string | MIME type. Directories always use `"directory"`. |
| `Timestamp` | `ts` | int (unix seconds) | Last-modified time. |
| `Size` | `size` | long | Byte size. Directories should be `0` (or omitted). |
| `Read` | `read` | int (0/1) | Whether the item can be read/downloaded. |
| `Write` | `write` | int (0/1) | Whether the item can be modified. |
| `Locked` | `locked` | int (0/1) | If `1`, item cannot be renamed, moved, or deleted. |

### Optional / conditional

| Field | JSON key | Type | When present |
|-------|----------|------|--------------|
| `ParentHash` | `phash` | string | Parent directory hash. **Omitted for volume roots.** |
| `Dirs` | `dirs` | int (0/1) | Directories only. `1` = has sub-dirs (chevron shown in tree). `0` = leaf. |
| `VolumeId` | `volumeid` | string | Present on volume root entries (e.g. `l1_`). |
| `IsRoot` | `isroot` | int (0/1) | `1` on the volume root object; omitted on all other entries. Required alongside `volumeid` for elFinder to recognise the root. |
| `Tmb` | `tmb` | string | Thumbnail path relative to connector base, or `"1"` to request generation. |
| `Alias` | `alias` | string | Display alias (shown instead of `name` in some UIs). |
| `Target` | `target` | string | Hash of symlink target. |
| `Hidden` | `hidden` | int (0/1) | `1` = hidden file (shown only if `showHidden` option is on). |
| `Width` | `width` | int | Image width in pixels (for inline preview). |
| `Height` | `height` | int | Image height in pixels. |
| `Csscls` | `csscls` | string | Extra CSS class applied to the item icon. |
| `NetKey` | `netkey` | string | For network-mount volumes only. |

---

## Hash encoding

```
hash = volumeId + base64url(path)
```

- **`volumeId`** — e.g. `l1_` (letter + digit + underscore).
- **`path`** — the storage-relative path, e.g. `pub/images` for a directory.
- Base64url uses `-` and `_` instead of `+` and `/`, with `=` padding stripped.

**SkyCMS:** hashes are generated/decoded by `IElFinderStorageAdapter.EncodePath()` / `DecodePath()`.

---

## Directory vs file

| Property | Directory | File |
|----------|-----------|------|
| `mime` | `"directory"` | `"image/png"`, `"text/plain"`, etc. |
| `size` | `0` | actual byte size |
| `dirs` | `0` or `1` (has children?) | omitted |
| `tmb` | omitted | optional |

---

## SkyCMS implementation notes

- `ElFinderObject` is the C# DTO in `Responses/OpenResponse.cs`. It carries `VolumeId` (`[JsonPropertyName("volumeid")]`) and `IsRoot` (`[JsonPropertyName("isroot")]`), both omitted when zero/null via `JsonIgnoreCondition.WhenWritingDefault` / `WhenWritingNull`.
- All JSON property names use `System.Text.Json` `[JsonPropertyName]` attributes (lowercase).
- Responses that go through the CQRS path must be serialized with `System.Text.Json.JsonSerializer.Serialize()` — NOT through the controller's `Json()` helper (which uses Newtonsoft). See [SkyCMS Implementation Notes](skycms-implementation-notes.md).
- For Azure Blob virtual directories, `size` is always `0` and `ts` is synthesized (e.g. `DateTimeOffset.UtcNow.ToUnixTimeSeconds()`).
