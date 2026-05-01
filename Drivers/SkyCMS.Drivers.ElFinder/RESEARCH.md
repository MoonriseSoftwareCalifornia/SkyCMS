# Phase 1: Research & Audit (elFinder Backend Protocol & SkyCMS Implementation)

## 0. Discovered Architectural Issue (Phase 3 Candidate)

### Path Normalization Inconsistencies in IStorageContext

**Observation**: During Phase 2 testing, path handling differences were observed between test and production storage contexts. Different blob storage providers (Azure, local file system, in-memory test contexts) handle path formats inconsistently.

**Impact**:
- Test assertions may not match production behavior
- Cross-provider switching (e.g., Azure → local development) can cause silent failures
- Path edge cases (trailing slashes, double slashes, leading slashes) handled differently

**Recommendation**: Phase 3+ work to standardize path normalization at the `IStorageContext` level, creating a single source of truth for path handling across all providers. This would:
- Centralize path validation and normalization
- Reduce bugs across storage provider implementations
- Improve testability and predictability
- Support the repo's cross-provider compatibility goals (Cosmos DB, MySQL, SQLite, MS SQL)

**Reference**: Related to repo guidance on maintaining cross-provider EF/Cosmos compatibility.

---

## 1. Official elFinder Client-Server API 2.1 Specification

### 1.1 Core Protocol Overview

The elFinder Client-Server API 2.1 is a **request–response protocol** where:
- The **frontend (browser)** sends **GET or POST requests** with a `cmd` parameter identifying the operation.
- The **backend** returns **JSON responses** with operation-specific data.
- **Path identification** uses **hashes** (opaque base64-like strings) rather than literal file paths.
- **Security**: Hashes obscure the actual filesystem structure and enable per-volume access control.

### 1.2 Hash Encoding (Critical for Tree Navigation)

elFinder hashes are typically:
```
volumeid + base64url_encoded_path
```

SkyCMS implementation:
```
"l1_" + base64(path).replace('+', '-').replace('/', '_').trimEnd('=')
```

Frontend decoding (IndexModern.cshtml):
```javascript
function decodePath(hash) {
    var encoded = hash.substring(3)
        .replace(/-/g, '+')
        .replace(/_/g, '/');
    while (encoded.length % 4 !== 0) encoded += '=';
    return '/' + atob(encoded);
}
```

**Status**: Hash encoding/decoding appears correct and symmetric.

### 1.3 Core Command Reference

| Command | Purpose | Request Args | Response Shape | Implemented? |
|---------|---------|--------------|-----------------|--------------|
| `open` | Initialize or navigate to folder; returns current dir + immediate children + root volume | `cmd`, `target` (opt), `init` (opt) | `{cwd, files, api, uplMaxSize, options}` | ✅ Yes |
| `tree` | Return **only subdirectories** of target folder (for lazy-loading tree) | `cmd`, `target` | `{tree: [...dirs]}` | ✅ Yes |
| `ls` | Return hash→name map of items in folder | `cmd`, `target`, `intersect[]` (opt) | `{list: {hash: name, ...}}` | ✅ Yes |
| `mkdir` | Create directory | `cmd`, `target`, `name` | `{added: [object]}` | ✅ Yes |
| `mkfile` | Create empty file | `cmd`, `target`, `name` | `{added: [object]}` | ✅ Yes |
| `rename` | Rename/move file or dir | `cmd`, `target`, `name` | `{added: [object], removed: [hash]}` | ✅ Yes |
| `rm` | Delete files/dirs | `cmd`, `targets[]` | `{removed: [hash, ...]}` | ✅ Yes |
| `upload` | Upload file(s) | `cmd`, `target`, `upload[]` (files), `overwrite` (opt) | `{added: [object], warning?: [error]}` | ✅ Yes |
| `get` | Retrieve file content | `cmd`, `target`, `conv` (opt) | `{content: string or data-uri}` | ✅ Yes |
| `put` | Write file content | `cmd`, `target`, `content`, `encoding` (opt) | `{changed: [object]}` | ✅ Yes |
| `paste` | Copy/move files between dirs | `cmd`, `targets[]`, `dst`, `cut` (opt) | `{added: [object], removed?: [hash]}` | ✅ Yes |
| `tmb` | Get thumbnail URLs | `cmd`, `targets[]` | `{images: {hash: url, ...}}` | ✅ Yes |
| `info` | Get metadata for items | `cmd`, `targets[]` | `{files: [object, ...]}` | ✅ Yes |
| `size` | Get aggregate size | `cmd`, `targets[]` | `{size: bytes}` | ✅ Yes |
| `parents` | Get all ancestors + their children (for navbar tree rebuild) | `cmd`, `target` | `{tree: [object, ...]}` | ✅ Yes |

### 1.4 File/Directory Object Shape (Response Format)

Required fields for all items:
```javascript
{
    hash: "l1_c29tZXBhdGg=",     // unique identifier (volume-scoped)
    name: "filename.txt",         // display name
    size: 1024,                   // bytes (0 for directories)
    mime: "text/plain",           // MIME type or "directory"
    ts: 1234567890,               // UNIX timestamp (modified)
    read: 1,                      // readable flag (1=yes)
    write: 1,                     // writable flag (1=yes)
    locked: 0                     // locked flag (1=yes, prevents edit)
}
```

Optional fields:
```javascript
{
    phash: "l1_cGFyZW50cGF0aA==",  // parent hash (must be set except for root)
    dirs: 1,                        // has subdirectories (1=yes; elFinder uses for +/- toggle)
    tmb: "/path/to/thumb.jpg",     // thumbnail URL
    url: "http://example.com/file", // public URL for download/preview
    volumeid: "l1_"                 // volume identifier (root only)
}
```

### 1.5 Critical Parent-Hash (phash) Field

**The `phash` field is essential for tree navigation:**
- It identifies the **parent directory** of the current item.
- Without it, elFinder cannot reconstruct the folder hierarchy.
- Root entries set `volumeid` instead of `phash`.
- **If phash is missing or incorrect, ancestor paths collapse in the left navbar.**

### 1.6 The `parents` Command (Critical for Tree Navigation)

The `parents` command is called by elFinder to rebuild the folder breadcrumb/navbar when navigating deeply into the tree.

**Expected behavior:**
1. Return a flat list of all ancestor folders (from root to target's parent).
2. For each ancestor, also include its immediate subdirectories.
3. This allows the UI to reconstruct the full navigable tree from root.

**Example response for path `/pub/articles/1/content`:**
```javascript
{
    "tree": [
        {
            "hash": "l1_cHVi",           // root hash
            "name": "pub",
            "mime": "directory",
            "dirs": 1,
            "volumeid": "l1_",
            ...
        },
        {
            "hash": "l1_cHViL2FydGljbGVz",  // /pub/articles
            "name": "articles",
            "mime": "directory",
            "phash": "l1_cHVi",             // parent hash (back to pub)
            "dirs": 1,
            ...
        },
        {
            "hash": "l1_cHViL2FydGljbGVzLzE=", // /pub/articles/1
            "name": "Article Title",
            "mime": "directory",
            "phash": "l1_cHViL2FydGljbGVz",    // parent hash (back to articles)
            "dirs": 1,
            ...
        },
        // All immediate children of /pub:
        { "hash": "...", "name": "...", "phash": "l1_cHVi", ...},
        // All immediate children of /pub/articles:
        { "hash": "...", "name": "...", "phash": "l1_cHViL2FydGljbGVz", ...},
        // All immediate children of /pub/articles/1:
        { "hash": "...", "name": "...", "phash": "l1_cHViL2FydGljbGVzLzE=", ...}
    ]
}
```

---

## 2. SkyCMS ElFinderConnectorController Audit

### 2.1 Command Implementation Status

**All 14 commands are implemented.** Routing is clean via a switch in `Connector()`.

### 2.2 Hash Encoding/Decoding

**File**: ElFinderConnectorController.cs, methods `EncodeHash()` and `DecodeHash()`

```csharp
private static string EncodeHash(string path)
{
    path = NormalizePath(path);
    var bytes = Encoding.UTF8.GetBytes(path.TrimStart('/'));
    return VolumeId + Convert.ToBase64String(bytes)
        .Replace('+', '-')
        .Replace('/', '_')
        .TrimEnd('=');
}

private static string DecodeHash(string hash)
{
    if (!hash.StartsWith(VolumeId)) return null;
    var encoded = hash.Substring(VolumeId.Length)
        .Replace('-', '+')
        .Replace('_', '/');
    var padding = encoded.Length % 4;
    if (padding > 0) encoded += new string('=', 4 - padding);
    try
    {
        var bytes = Convert.FromBase64String(encoded);
        return NormalizePath("/" + Encoding.UTF8.GetString(bytes));
    }
    catch { return null; }
}
```

**Status**: ✅ Correct and symmetric. Matches frontend decode logic.

### 2.3 Path Normalization & Security

**File**: ElFinderConnectorController.cs, methods `NormalizePath()` and `IsAllowedPath()`

```csharp
private static string NormalizePath(string path)
{
    if (string.IsNullOrWhiteSpace(path)) return null;
    var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (segments.Length == 0) return "/";
    return "/" + string.Join("/", segments);
}

private static bool IsAllowedPath(string path)
{
    if (string.IsNullOrEmpty(path)) return false;
    path = NormalizePath(path);
    if (path.Contains("..")) return false;
    var normalised = NormalizePath(path);
    return normalised == RootPath || normalised.StartsWith(RootPath + "/");
}
```

**Status**: ✅ Solid. Path traversal protection active. Root confinement enforced (`/pub`).

### 2.4 Parent Hash Generation

**File**: ElFinderConnectorController.cs, method `GetParentHash()`

```csharp
private static string GetParentHash(string path)
{
    var parent = GetParentPath(path);
    if (parent == "/" || string.IsNullOrEmpty(parent)) return null;
    return EncodeHash(parent);
}
```

**Status**: ✅ Correct. Returns null for root (consistent with spec).

### 2.5 File/Directory Object Mapping

**File**: ElFinderConnectorController.cs, method `ToElFinderObjectAsync()`

```csharp
private async Task<object> ToElFinderObjectAsync(FileManagerEntry entry, string parentHash)
{
    var fullPath = NormalizePath(entry.Path.StartsWith("/") ? entry.Path : "/" + entry.Path);
    var hash = EncodeHash(fullPath);
    var displayName = await GetFriendlyLeafNameAsync(fullPath, defaultDisplayName);
    var mime = entry.IsDirectory ? "directory" : GetMimeType(entry.Extension);
    var ts = new DateTimeOffset(entry.ModifiedUtc ?? DateTime.UtcNow, TimeSpan.Zero).ToUnixTimeSeconds();
    var isRoot = fullPath == RootPath;

    var obj = new Dictionary<string, object>
    {
        ["hash"] = hash,
        ["name"] = displayName,
        ["size"] = entry.IsDirectory ? 0L : entry.Size,
        ["mime"] = mime,
        ["ts"] = ts,
        ["read"] = 1,
        ["write"] = 1,
        ["locked"] = 0,
    };

    if (entry.IsDirectory && entry.HasDirectories)
        obj["dirs"] = 1;

    if (!isRoot && parentHash != null)
        obj["phash"] = parentHash;  // ← CRITICAL for tree navigation

    if (isRoot)
    {
        obj["volumeid"] = VolumeId;
        obj["dirs"] = 1;
    }

    if (!entry.IsDirectory)
    {
        obj["url"] = $"{blobBase}/{fullPath.TrimStart('/')}";
        // ... thumbnail handling
    }

    return obj;
}
```

**Status**: ✅ Correct. Fields are properly set, including `phash`.

### 2.6 The `open` Command (Initialize/Navigate)

**File**: ElFinderConnectorController.cs, method `HandleOpenAsync()`

**Response shape**:
```csharp
var response = new Dictionary<string, object>
{
    ["cwd"] = cwdObject,           // current directory
    ["files"] = allFiles,           // root + cwd (if not root) + children
    ["api"] = "2.1",
    ["uplMaxSize"] = "64M",
    ["options"] = await BuildOptionsAsync(path),
};
```

**Implementation notes**:
- Includes root node always.
- Includes cwd only if not root (to avoid duplicate).
- Includes immediate children of cwd.
- Builds a synthetic root if storage provider does not return one.

**Status**: ✅ Appears correct. Tree data included for navbar bootstrap.

### 2.7 The `tree` Command (Lazy-Load Subdirectories)

**File**: ElFinderConnectorController.cs, method `HandleTreeAsync()`

```csharp
private async Task<IActionResult> HandleTreeAsync()
{
    var target = GetParam("target");
    var path = DecodeHash(target);
    if (path == null || !IsAllowedPath(path)) return Json(ElFinderError("errAccess"));

    var items = await GetEntriesForPathAsync(path);
    var dirs = new List<object>();
    var parentHash = EncodeHash(path);
    foreach (var item in items.Where(e => e.IsDirectory))
    {
        dirs.Add(await ToElFinderObjectAsync(item, parentHash));
    }

    return Json(new { tree = dirs });
}
```

**Status**: ✅ Correct. Returns only directories with proper parent hash.

### 2.8 The `rm` Command (Delete) – POTENTIAL ISSUE #1

**File**: ElFinderConnectorController.cs, method `HandleRmAsync()`

```csharp
private async Task<IActionResult> HandleRmAsync()
{
    var targets = GetParams("targets[]");
    var removed = new List<string>();

    foreach (var t in targets)
    {
        var path = DecodeHash(t);
        if (path == null || !IsAllowedPath(path)) continue;

        try { entry = await storageContext.GetFileAsync(path); }
        catch { entry = null; }

        var isDir = entry?.IsDirectory ?? false;

        if (isDir)
            await storageContext.DeleteFolderAsync(path);
        else
            await storageContext.DeleteFileAsync(path);

        removed.Add(t);  // ← Add to removed list regardless of actual deletion success
    }

    return Json(new { removed });
}
```

**Potential issues**:
1. **Silent failure**: If `DeleteFolderAsync()` or `DeleteFileAsync()` throws an exception, it is caught implicitly (no try-catch visible but errors may be swallowed).
2. **No post-deletion verification**: The command does not verify that the item was actually deleted before returning success.
3. **No error element in response**: Per spec, unsuccessful deletes could return an `error` field, but this implementation always returns `removed` list without conditional feedback.

**Status**: ⚠️ **Likely culprit for "delete operations appear to do nothing"**.

### 2.9 The `parents` Command (Tree Reconstruction) – POTENTIAL ISSUE #2

**File**: ElFinderConnectorController.cs, method `HandleParentsAsync()`

```csharp
private async Task<IActionResult> HandleParentsAsync()
{
    var target = GetParam("target");
    var path = DecodeHash(target);
    if (path == null || !IsAllowedPath(path)) return Json(ElFinderError("errAccess"));

    var ancestors = new List<string>();
    var current = path;

    // Walk up to root
    while (!string.IsNullOrEmpty(current) && current.StartsWith(RootPath, StringComparison.Ordinal))
    {
        ancestors.Add(current);
        if (string.Equals(current, RootPath, StringComparison.Ordinal)) break;
        var parent = GetParentPath(current);
        if (string.IsNullOrEmpty(parent) || parent == current) break;
        current = parent;
    }

    ancestors.Reverse();
    var tree = new List<object>();

    foreach (var ancestor in ancestors)
    {
        var isRoot = string.Equals(ancestor, RootPath, StringComparison.Ordinal);
        if (isRoot)
        {
            tree.Add(SyntheticDirObject(RootPath, null, isRoot: true));
            continue;
        }

        var parent = GetParentPath(ancestor);
        try
        {
            var items = await GetEntriesForPathAsync(parent);
            foreach (var item in items.Where(e => e.IsDirectory))
            {
                tree.Add(await ToElFinderObjectAsync(item, EncodeHash(parent)));
            }
        }
        catch { /* Best-effort */ }
    }

    // Deduplication
    var seen = new HashSet<string>();
    var deduped = new List<object>();
    foreach (var item in tree)
    {
        var itemDict = item as Dictionary<string, object>;
        if (itemDict != null && itemDict.ContainsKey("hash"))
        {
            var hash = itemDict["hash"].ToString();
            if (seen.Add(hash)) deduped.Add(item);
        }
    }

    return Json(new { tree = deduped });
}
```

**Analysis**:
- **Correct concept**: Walks ancestors from root to target, returning tree data for each level.
- **Deduplication applied**: Prevents duplicate entries.

**Potential issue**:
- **Response structure unclear**: The response returns a flat list of all ancestors and their children, but **the order and grouping may not clearly map to the tree structure** that elFinder expects to rebuild the navbar.
- **Missing current target**: The response includes ancestors but **may not explicitly include the target node itself**, which elFinder may need to expand the tree fully.

**Status**: ⚠️ **Likely culprit for "folder tree path collapse/disappearance"**. The tree data may be incomplete or the structure may not allow elFinder to reconstruct sibling paths correctly.

### 2.10 Synthetic Directory Handling

**File**: ElFinderConnectorController.cs, method `SyntheticDirObject()`

```csharp
private object SyntheticDirObject(string path, string parentHash, bool isRoot, string friendlyName = null)
{
    path = NormalizePath(path);
    var hash = EncodeHash(path);
    var name = friendlyName ?? (isRoot ? "pub" : path.TrimEnd('/').Split('/').Last());
    var obj = new Dictionary<string, object>
    {
        ["hash"] = hash,
        ["name"] = name,
        ["size"] = 0L,
        ["mime"] = "directory",
        ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ["read"] = 1,
        ["write"] = 1,
        ["locked"] = 0,
        ["dirs"] = 1,
    };

    if (isRoot)
        obj["volumeid"] = VolumeId;
    else if (parentHash != null)
        obj["phash"] = parentHash;

    return obj;
}
```

**Status**: ✅ Correct. Creates properly structured synthetic nodes for non-existent storage entries.

### 2.11 Pseudo-Folder Handling (Articles & Templates)

**File**: ElFinderConnectorController.cs, method `GetEntriesForPathAsync()`

SkyCMS synthesizes virtual folder hierarchies:
- `/pub/articles` → list of article folders (named by article number or title).
- `/pub/templates` → list of template folders (named by template ID or title).

**Implementation**:
```csharp
if (string.Equals(normalised, "pub/articles", StringComparison.OrdinalIgnoreCase))
{
    return await dbContext.ArticleCatalog
        .Select(s => new FileManagerEntry
        {
            IsDirectory = true,
            Name = s.Title,
            Path = $"/pub/articles/{s.ArticleNumber}",
            ...
        })
        .ToListAsync();
}
```

**Status**: ✅ Sound. Virtual hierarchies are well-implemented and integrated.

---

## 3. Identified Issues & Mismatches

### Issue #1: Delete Operations (rm Command)

**Symptom**: Delete operations appear to succeed without actually deleting items.

**Root cause (hypothesis)**:
- The `HandleRmAsync()` method does not catch or verify deletion success.
- If the underlying storage call throws an exception, it is swallowed without user feedback.
- The response always returns the `removed` list regardless of actual deletion outcome.

**Fix**: Add try-catch with error reporting and post-deletion verification.

### Issue #2: Folder Tree Path Collapse

**Symptom**: When navigating deeply into folders, ancestor breadcrumbs/nav tree items disappear or collapse.

**Root cause (hypothesis)**:
- The `HandleParentsAsync()` response structure may not provide enough context for elFinder to reconstruct the full navigable tree.
- Missing or incorrect `phash` values in the returned tree nodes would break navbar reconstruction.
- The response may include all ancestors but not in a structure that elFinder can group by parent for UI rebuild.

**Secondary consideration**:
- The `open` command may also need to ensure deeper tree bootstrap (all ancestors are included, not just immediate children).

**Fix**: Review `parents` response structure; ensure all ancestors and their sibling relationships are explicit.

### Issue #3: Rename/Move Edge Case

**File**: ElFinderConnectorController.cs, method `HandleRenameAsync()`

The code handles the case where the resolved name equals the original name:
```csharp
if (string.Equals(newPath, path, StringComparison.OrdinalIgnoreCase))
{
    return Json(new
    {
        added = new[] { await ToElFinderObjectAsync(entry, EncodeHash(parentPath)) },
        removed = Array.Empty<string>(),
    });
}
```

**Status**: ✅ This is correct and prevents redundant operations.

---

## 4. Architecture & Design Observations

### 4.1 Storage Abstraction

SkyCMS uses `IStorageContext` for file/folder operations:
- `GetFileAsync(path)` → retrieve file metadata.
- `DeleteFileAsync(path)` / `DeleteFolderAsync(path)` → delete operations.
- `MoveFileAsync(src, dst)` / `MoveFolderAsync(src, dst)` → rename/move.
- `CreateFolder(path)`, `AppendBlob(stream, metadata)`, `CopyAsync(src, dst)`.

**Status**: Clean abstraction. No direct filesystem access in controller.

### 4.2 Error Handling

**Current approach**: Try-catch with generic fallback (e.g., building synthetic entries if storage fails).

**Status**: Functional but could be more explicit about error cases for users.

### 4.3 Multi-Tenancy

**Observed**: No explicit tenant filtering in connector logic. Tenancy is handled via scoped `IStorageContext` injected per-request.

**Status**: ✅ Correct architectural approach (as per repo instructions).

---

## 5. Phase 1 Recommendations for Phase 2

### Immediate Fixes (High Priority)

1. **Fix `HandleRmAsync()` (Delete)**:
   - Add explicit try-catch around storage operations.
   - Add post-deletion verification (verify item is gone).
   - Return error element in response if deletion fails.

2. **Fix `HandleParentsAsync()` (Tree Reconstruction)**:
   - Ensure the response includes not just ancestors but **all their immediate children** in a way that elFinder can rebuild sibling relationships.
   - Test with deep paths (3+ levels) to ensure breadcrumb doesn't collapse.
   - Consider returning the target node itself in the tree response.

### Medium Priority

3. **Strengthen error handling**:
   - Use a dedicated error response class that includes error codes and user-facing messages.
   - Test error paths and ensure errors are communicated back to frontend.

4. **Add integration tests**:
   - Test delete operations with verification.
   - Test parents command with deep paths and pseudo-folders (articles, templates).
   - Test tree/open consistency.

### Architecture for Phase 2 (New Driver Project)

When building `SkyCMS.Drivers.ElFinder`:

1. **Use CQRS commands** for each elFinder operation (e.g., `DeleteFileCommand`, `GetParentsCommand`).
2. **Create response DTOs** with `[JsonPropertyName]` attributes to ensure exact elFinder compliance.
3. **Define `IElFinderStorageAdapter`** to isolate driver from `IStorageContext` details.
4. **Comprehensive error handling** with typed error responses.
5. **Unit tests** for each command, especially edge cases (deep paths, deletion, rename conflicts).

---

## 6. Phase 1 Deliverables

- ✅ Official elFinder API 2.1 specification captured and summarized.
- ✅ SkyCMS connector code fully audited.
- ✅ Hash encoding/decoding validated.
- ✅ Two critical issues identified: delete operations and tree reconstruction.
- ✅ Recommendations for Phase 2 fixes and architecture provided.

**Next phase**: Implement fixes in Phase 2, then test against the live elFinder UI.

