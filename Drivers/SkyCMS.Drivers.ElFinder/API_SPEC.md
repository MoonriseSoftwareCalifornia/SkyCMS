# elFinder 2.1 API Specification – Quick Reference

This document provides a concise command-by-command reference for elFinder's Client-Server API 2.1, extracted from the official specification. Use this when implementing or auditing backend connectors.

---

## Core Concepts

### Hash (Path Identifier)

```
hash = volumeid + base64url_encoded_path
volumeid = "l1_" (standard for single-volume connectors)
base64url = base64(utf8_encode(path)).replace('+', '-').replace('/', '_').trimEnd('=')
```

### File/Directory Object (Standard Response Shape)

All file/directory objects MUST include:

```javascript
{
    "hash": "l1_cHViL2FpbGU=",      // Unique path identifier (volume-scoped)
    "name": "file.txt",             // Display name (user-visible)
    "size": 1024,                   // Bytes (0 for directories)
    "mime": "text/plain",           // MIME type or "directory"
    "ts": 1234567890,               // UNIX timestamp (last modified)
    "read": 1,                      // Readable (1=yes, 0=no)
    "write": 1,                     // Writable (1=yes, 0=no)
    "locked": 0                     // Locked (1=yes, 0=no)
}
```

Optional fields (important for tree navigation):

```javascript
{
    "phash": "l1_cHVi",             // Parent hash (REQUIRED except for root)
    "dirs": 1,                      // Has subdirs (1=yes; elFinder uses for +/- toggle)
    "volumeid": "l1_",              // Volume ID (root only)
    "url": "http://example.com/...", // Public URL (for download/preview)
    "tmb": "/path/to/thumb.jpg"     // Thumbnail URL
}
```

### Error Response

When an operation fails, return:

```javascript
{
    "error": "errNotFound"  // or other error code
}
```

Standard error codes:
- `errUnknownCmd` — Unknown command
- `errAccess` — Access denied / path not allowed
- `errOpen` — Cannot open directory
- `errNotFound` — Item not found
- `errInvName` — Invalid name
- `errUploadFile` — Cannot upload (file type blocked, etc.)
- `errUploadNoFiles` — No files in upload request
- `errReplace` — Cannot replace (item exists, overwrite disabled)
- `errRm` — Cannot delete

---

## Command Reference

### 1. `open` — Initialize or Navigate to Directory

**Purpose**: Initialize the file manager UI or navigate to a new directory. Returns the current directory, its immediate children, and the root node (for navbar bootstrap).

**Request**:
```
GET/POST /connector
  cmd=open
  target=l1_cHViL3RhcmdldA==  (optional; defaults to root if missing)
  init=1                       (optional; 1 if this is the initial request)
```

**Response**:
```javascript
{
    "cwd": {                    // Current working directory (file object)
        "hash": "l1_cHViL3RhcmdldA==",
        "name": "target",
        "mime": "directory",
        "phash": "l1_cHVi",
        "dirs": 1,
        "ts": 1234567890,
        // ... standard fields
    },
    "files": [                  // Root + cwd (if not root) + all immediate children
        {
            "hash": "l1_cHVi",
            "name": "pub",
            "mime": "directory",
            "volumeid": "l1_",
            "dirs": 1,
            // ... standard fields
        },
        // More children...
    ],
    "api": "2.1",               // API version
    "uplMaxSize": "64M",        // Max upload size
    "options": {                // Directory options (see below)
        "path": "pub/target",   // Human-readable path
        "url": "http://blob.../pub/target/",
        "separator": "/",
        "copyOverwrite": 1,
        "uploadOverwrite": 1,
        // ... more options
    }
}
```

**Key points**:
- If `init=1`, elFinder uses this to bootstrap the tree.
- The `files` array must include the root node (for navbar) and all immediate children (for folder contents display).
- If `target` directory is not the root, `cwd` should have a `phash` pointing to its parent.

---

### 2. `tree` — Get Subdirectories (Lazy-Load)

**Purpose**: Return only subdirectories of a folder, allowing elFinder to lazily expand the tree on demand.

**Request**:
```
GET/POST /connector
  cmd=tree
  target=l1_cHViL3RhcmdldA==
```

**Response**:
```javascript
{
    "tree": [                   // Array of directory objects
        {
            "hash": "l1_cHViL3RhcmdldC9zdWJkaXI=",
            "name": "subdir",
            "mime": "directory",
            "phash": "l1_cHViL3RhcmdldA==",
            "dirs": 1,          // Indicates it has subdirectories
            // ... standard fields
        },
        // More subdirectories...
    ]
}
```

**Key points**:
- **Return only directories**, not files.
- Each directory must have a `phash` pointing to the target.
- Set `dirs: 1` if the subdirectory itself has subdirectories.

---

### 3. `ls` — List Items as Hash→Name Map

**Purpose**: Return a compact hash→name mapping for items in a directory, used for existence checks and quick name resolution.

**Request**:
```
GET/POST /connector
  cmd=ls
  target=l1_cHViL3RhcmdldA==
  intersect[]=l1_aXRlbTE=     (optional; only return these items if present)
```

**Response**:
```javascript
{
    "list": {
        "l1_c3ViZGlyMQ==": "subdir1",
        "l1_c3ViZGlyMg==": "subdir2",
        "l1_aW1hZ2UuanBn": "image.jpg",
        "l1_dGV4dC50eHQ": "text.txt"
    }
}
```

---

### 4. `mkdir` — Create Directory

**Request**:
```
GET/POST /connector
  cmd=mkdir
  target=l1_cHViL3RhcmdldA==   (parent directory hash)
  name=newfolder
```

**Response**:
```javascript
{
    "added": [                  // Array of newly created directories
        {
            "hash": "l1_cHViL3RhcmdldC9uZXdmb2xkZXI=",
            "name": "newfolder",
            "mime": "directory",
            "phash": "l1_cHViL3RhcmdldA==",
            "dirs": 0,          // Typically empty at creation
            "ts": 1234567890,
            // ... standard fields
        }
    ]
}
```

---

### 5. `mkfile` — Create Empty File

**Request**:
```
GET/POST /connector
  cmd=mkfile
  target=l1_cHViL3RhcmdldA==   (parent directory hash)
  name=newfile.txt
```

**Response**:
```javascript
{
    "added": [
        {
            "hash": "l1_cHViL3RhcmdldC9uZXdmaWxlLnR4dA==",
            "name": "newfile.txt",
            "mime": "text/plain",
            "phash": "l1_cHViL3RhcmdldA==",
            "size": 0,
            "ts": 1234567890,
            // ... standard fields
        }
    ]
}
```

---

### 6. `rename` — Rename or Move File/Directory

**Request**:
```
GET/POST /connector
  cmd=rename
  target=l1_cHViL3RhcmdldC9vbGRuYW1l  (item to rename)
  name=newname                          (new name or new location)
```

**Response**:
```javascript
{
    "added": [                  // Renamed item (same hash if no move, same name if file)
        {
            "hash": "l1_cHViL3RhcmdldC9uZXduYW1l",
            "name": "newname",
            "mime": "text/plain",
            "phash": "l1_cHViL3RhcmdldA==",
            // ... standard fields
        }
    ],
    "removed": [                // Old hash (elFinder removes from UI)
        "l1_cHViL3RhcmdldC9vbGRuYW1l"
    ]
}
```

**Key points**:
- If new name is same as old (case-insensitive), return both `added` (current state) and `removed` (old hash) to refresh UI.
- If move involves a different parent, update `phash` in the added entry.

---

### 7. `rm` — Delete Files/Directories

**Request**:
```
GET/POST /connector
  cmd=rm
  targets[]=l1_aXRlbTE=
  targets[]=l1_aXRlbTI=
```

**Response**:
```javascript
{
    "removed": [                // Hashes of successfully deleted items
        "l1_aXRlbTE=",
        "l1_aXRlbTI="
    ]
}
```

Or, if some items fail:
```javascript
{
    "removed": ["l1_aXRlbTE="],
    "warning": [
        "Cannot delete file: in use",
        "Cannot delete directory: contains locked items"
    ]
}
```

**Key points**:
- Return only the hashes of successfully deleted items.
- If deletion fails for any item, optionally return a `warning` array with error messages.
- **Critical**: Do not return hashes of items that were not actually deleted.

---

### 8. `upload` — Upload Files

**Request** (HTTP POST, multipart/form-data):
```
cmd=upload
target=l1_cHViL3RhcmdldA==    (parent directory hash)
upload[]=<file1>              (multipart file)
upload[]=<file2>
overwrite=0                   (optional; 0=rename if exists, 1=overwrite)
```

**Response**:
```javascript
{
    "added": [                  // Successfully uploaded files
        {
            "hash": "l1_cHViL3RhcmdldC91cGxvYWQx",
            "name": "upload1.jpg",
            "mime": "image/jpeg",
            "phash": "l1_cHViL3RhcmdldA==",
            "size": 102400,
            "ts": 1234567890,
            // ... standard fields
        }
    ],
    "warning": [                // Optional; files that could not be uploaded
        "File 'bad.exe' rejected: not allowed"
    ]
}
```

**Chunked uploads**:
```
cmd=upload
chunk=filename.ext.1_3.part   (current chunk identifier: name.START_TOTAL.part)
cid=unique-id                  (unique chunk session ID)
range=0,1048576,3145728       (Bytes: start, length, total)
```

Response during chunking:
```javascript
{
    "added": [],               // Empty until all chunks received
    // No _chunkmerged yet
}
```

Response when final chunk received:
```javascript
{
    "added": [],
    "_chunkmerged": "filename",   // Final merged filename
    "_name": "filename"           // Uploading file name
}
```

Then client sends merge request:
```
cmd=upload
upload[]=filename
chunk=filename
```

Response:
```javascript
{
    "added": [
        {
            "hash": "...",
            "name": "filename",
            // ... full file object
        }
    ]
}
```

---

### 9. `get` — Get File Content

**Request**:
```
GET/POST /connector
  cmd=get
  target=l1_aW1hZ2UuanBn             (file hash)
  current=l1_cHViL2ltYWdlcw==        (optional; containing directory)
  conv=1                              (optional; auto-detect encoding)
```

**Response** (for text files):
```javascript
{
    "content": "Hello, world!",       // UTF-8 text
    "encoding": "cp1252"              // Optional; original encoding
}
```

Response for binary files (Data URI Scheme):
```javascript
{
    "content": "data:image/jpeg;base64,/9j/4AAQSkZJRgABA..."
}
```

If encoding detection failed (when `conv=0`):
```javascript
{
    "doconv": "unknown"               // Ask user for encoding
}
```

---

### 10. `put` — Write File Content

**Request**:
```
POST /connector
  cmd=put
  target=l1_dGV4dC50eHQ              (file hash)
  content=New content here...
  encoding=utf-8                      (optional; assume UTF-8 if missing)
```

**Response**:
```javascript
{
    "changed": [                       // Successfully written file
        {
            "hash": "l1_dGV4dC50eHQ",
            "name": "text.txt",
            "mime": "text/plain",
            "size": 18,                // Updated size
            "ts": 1234567891,          // Updated timestamp
            // ... standard fields
        }
    ]
}
```

---

### 11. `paste` — Copy or Move Files/Directories

**Request**:
```
GET/POST /connector
  cmd=paste
  targets[]=l1_aXRlbTE=
  targets[]=l1_aXRlbTI=
  dst=l1_cHViL2Rlc3RpbmF0aW9u
  cut=1                               (optional; 1=move, 0=copy)
```

**Response**:
```javascript
{
    "added": [                         // Copied/moved items (in new location)
        {
            "hash": "l1_cHViL2Rlc3RpbmF0aW9uL2l0ZW0x",
            "name": "item1",
            "phash": "l1_cHViL2Rlc3RpbmF0aW9u",
            // ... standard fields
        }
    ],
    "removed": [                       // Old hashes (if move/cut)
        "l1_aXRlbTE=",
        "l1_aXRlbTI="
    ]
}
```

---

### 12. `tmb` — Get Thumbnail URLs

**Request**:
```
GET/POST /connector
  cmd=tmb
  targets[]=l1_aW1hZ2UuanBn
  targets[]=l1_2nV0aWMuanBn
```

**Response**:
```javascript
{
    "images": {
        "l1_aW1hZ2UuanBn": "http://example.com/.tmb/image.png",
        "l1_2nV0aWMuanBn": "http://example.com/.tmb/video.png"
    }
}
```

---

### 13. `info` — Get File/Directory Metadata

**Request**:
```
GET/POST /connector
  cmd=info
  targets[]=l1_aXRlbTE=
  targets[]=l1_aXRlbTI=
```

**Response**:
```javascript
{
    "files": [                         // Full file objects
        {
            "hash": "l1_aXRlbTE=",
            "name": "item1",
            "size": 1024,
            "mime": "text/plain",
            "phash": "l1_cHVi",
            // ... standard fields
        },
        {
            "hash": "l1_aXRlbTI=",
            // ...
        }
    ]
}
```

---

### 14. `size` — Calculate Aggregate Size

**Request**:
```
GET/POST /connector
  cmd=size
  targets[]=l1_Zm9sZGVyMQ==
  targets[]=l1_Zm9sZGVyMg==
```

**Response**:
```javascript
{
    "size": 10485760                   // Total size in bytes
}
```

---

### 15. `parents` — Get Ancestor Tree (Critical for Breadcrumb/Navbar)

**Request**:
```
GET/POST /connector
  cmd=parents
  target=l1_cHViL2RlZXAvcGF0aA==
```

**Purpose**: Return all ancestors of a path plus their sibling directories, allowing the UI to rebuild the full navigable tree from root to the target. This is essential for breadcrumb/navbar reconstruction when navigating deeply into the folder tree.

**Response**:
```javascript
{
    "tree": [
        // 1. Root volume
        {
            "hash": "l1_cHVi",
            "name": "pub",
            "mime": "directory",
            "volumeid": "l1_",
            "dirs": 1,
            "ts": 1234567890,
            // ... standard fields (no phash for root)
        },
        // 2. All immediate children of root (siblings for tree expansion)
        {
            "hash": "l1_cHViL2FydGljbGVz",
            "name": "articles",
            "mime": "directory",
            "phash": "l1_cHVi",
            "dirs": 1,
            // ...
        },
        {
            "hash": "l1_cHViL3RlbWg=",
            "name": "templates",
            "mime": "directory",
            "phash": "l1_cHVi",
            // ...
        },
        // ... more root children

        // 3. Direct child (if target is not a direct child of root)
        {
            "hash": "l1_cHViL2FydGljbGVzLzEw",
            "name": "article 10",
            "mime": "directory",
            "phash": "l1_cHViL2FydGljbGVz",
            "dirs": 1,
            // ...
        },
        // 4. All immediate children of the parent's parent (grandparent's children)
        // ... etc., repeating this pattern up the hierarchy
    ]
}
```

**Key points**:
- **Response is a flat list**, not a nested tree.
- Include **all ancestors** from root to the target's parent (in order, usually root first).
- For **each ancestor**, include its **immediate children** (siblings at that level).
- This allows elFinder to rebuild the full navigable tree and keep navbar/breadcrumb items expanded.
- **Missing or incorrect `phash` values** will cause the navbar tree to collapse or disappear.
- The response should **not include the target node itself** (only its ancestors and their children).

**Example for path `/pub/articles/10/content/drafts`**:
```javascript
{
    "tree": [
        { "hash": "l1_cHVi", "name": "pub", "volumeid": "l1_", "dirs": 1 },
        { "hash": "l1_cHViL2FydGljbGVz", "name": "articles", "phash": "l1_cHVi", "dirs": 1 },
        { "hash": "l1_cHViL3Rlbmg=", "name": "templates", "phash": "l1_cHVi", "dirs": 0 },
        // ... more root children
        { "hash": "l1_cHViL2FydGljbGVzLzEw", "name": "article 10", "phash": "l1_cHViL2FydGljbGVz", "dirs": 1 },
        { "hash": "l1_cHViL2FydGljbGVzLzEx", "name": "article 11", "phash": "l1_cHViL2FydGljbGVz", "dirs": 0 },
        // ... more articles (siblings of article 10)
        { "hash": "l1_cHViL2FydGljbGVzLzEwL2NvbnRlbnQ=", "name": "content", "phash": "l1_cHViL2FydGljbGVzLzEw", "dirs": 1 },
        { "hash": "l1_cHViL2FydGljbGVzLzEwL21ldGEtZGF0YQ==", "name": "meta-data", "phash": "l1_cHViL2FydGljbGVzLzEw", "dirs": 0 },
        // ... more children of article 10
        { "hash": "l1_cHViL2FydGljbGVzLzEwL2NvbnRlbnQvZHJhZnRz", "name": "drafts", "phash": "l1_cHViL2FydGljbGVzLzEwL2NvbnRlbnQ=", "dirs": 0 },
        { "hash": "l1_cHViL2FydGljbGVzLzEwL2NvbnRlbnQvcHVibGlzaGVk", "name": "published", "phash": "l1_cHViL2FydGljbGVzLzEwL2NvbnRlbnQ=", "dirs": 0 },
        // ... more children of /pub/articles/10/content
    ]
}
```

This structure allows elFinder to:
1. Find and expand all ancestors (root, articles, article 10, content).
2. Populate sibling lists at each level so the left navbar tree is complete.
3. Navigate from root all the way to the target directory without collapsing the path.

---

## Implementation Checklist

When implementing or auditing an elFinder connector backend, verify:

- [ ] **Hash encoding**: Base64 URL-safe with correct padding and volume ID prefix.
- [ ] **File objects**: All required fields present (`hash`, `name`, `size`, `mime`, `ts`, `read`, `write`, `locked`).
- [ ] **Parent hash (`phash`)**: Present for non-root items, absent for root.
- [ ] **Directory flag (`dirs`)**: Set to 1 if directory has subdirectories, 0 otherwise.
- [ ] **Volume ID**: Present only on root node.
- [ ] **Error responses**: Include `error` field with appropriate error code.
- [ ] **Delete (`rm`)**: Return only successfully deleted hashes; don't return failures as removals.
- [ ] **Parents command**: Return ancestors + all their children; ensure `phash` is correct for tree rebuild.
- [ ] **Open command**: Include root + cwd (if not root) + all immediate children.
- [ ] **Rename/move**: Update `phash` if moving to different parent; return both `added` and `removed`.
- [ ] **Path validation**: Block path traversal (`../`), enforce root confinement, validate safe names.
- [ ] **Pseudo-folders**: If using virtual hierarchies (articles, templates), ensure consistent path handling.

---

## Common Pitfalls

1. **Missing `phash`**: Tree navigation breaks; navbar collapses.
2. **Incorrect hash encoding**: Hash mismatches; client cannot decode paths.
3. **Missing `dirs` flag**: UI cannot expand directories; "expand" button (+) does not appear.
4. **Delete success without verification**: User appears to delete but items remain.
5. **Parents response structure**: Flat vs nested mismatch; tree rebuild fails.
6. **Inconsistent error responses**: Mix of error codes; frontend cannot handle gracefully.
7. **Path traversal vulnerability**: `../` allowed; security hole.

---

## Testing

### Manual Test Cases

1. **Navigate deep** (3+ levels): Verify breadcrumb and left navbar remain intact.
2. **Delete file**: Verify removed from directory and from UI; no error if already deleted.
3. **Delete directory**: Verify children are removed; parent updates.
4. **Rename**: Verify old item removed, new item appears with updated hash.
5. **Create folder/file**: Verify appears in directory immediately.
6. **Upload**: Verify file appears with correct hash and metadata.
7. **Copy/paste**: Verify file appears in destination with new hash; original remains.
8. **Breadcrumb click**: Navigate to ancestor; verify tree doesn't collapse.

### Automated Test (Example)

```csharp
[Test]
public async Task TestParentsCommandIncludesAllAncestors()
{
    var targetPath = "/pub/articles/10/content/drafts";
    var response = await connector.HandleParentsAsync(targetPath);
    var tree = response["tree"] as List<object>;

    // Verify root is present
    Assert.That(tree, Has.One.Matches<object>(o => 
        ((Dictionary<string, object>)o)["name"].ToString() == "pub" &&
        ((Dictionary<string, object>)o).ContainsKey("volumeid")
    ));

    // Verify all ancestors have correct phash
    var articles = tree.FirstOrDefault(o => 
        ((Dictionary<string, object>)o)["name"].ToString() == "articles");
    Assert.That(articles, Is.Not.Null);
    Assert.That(((Dictionary<string, object>)articles)["phash"].ToString(), 
        Is.EqualTo(EncodeHash("/pub")));
}
```

---

## Additional Resources

- Official elFinder GitHub: https://github.com/Studio-42/elFinder
- API 2.1 Wiki: https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1
- ASP.NET Core Reference: https://github.com/gordon-matt/elFinder.NetCore
