# ADR 0040 — Use Human-Readable Article Titles in File Manager Display Paths

**Status:** Accepted  
**Date:** 2025-01-18  
**Authors:** SkyCMS Contributors  
**Relates to:** [ADR 0037 — Article Lifecycle and Status Code Semantics](./0037-article-lifecycle-and-status-code-semantics.md), [ADR 0039 — DRY Controller Unification: File Manager and VS Code Explorer](./0039-dry-controller-unification-file-manager-and-vscode.md)

---

## Quick Reference (Evaluation Checklist)

Use this section to quickly validate implementation against design intent:

### ✅ Core Principle: Dual-Path Architecture
- **Canonical Path** (storage): `/pub/articles/123/banner.jpg` ← Always used for blob operations
- **Display Path** (UI): `/pub/articles/Getting Started Guide/banner.jpg` ← Shown to users
- **Implementation:** `FileManagerEntry.Path` vs `FileManagerEntry.DisplayPath`

### ✅ Server-Side Responsibilities
1. Fetch article titles/status from `ArticleCatalog` table (with legacy backfill support)
2. In `FileManagerController.FilterEntries(...)`, filter deleted article entries and shape Open responses with friendly display metadata
3. Encode **only canonical paths** in elFinder hash values (`ElFinderObject.Hash`)
4. Rewrite `ElFinderObject.Name` to article title only for direct children when listing `/pub/articles`
5. Cache deleted/article-status lookups (30s TTL) to minimize database queries

### ✅ Client-Side Responsibilities (elFinder UI)
1. Display `ElFinderObject.Name` field (friendly title) in folder trees and breadcrumbs
2. Send `ElFinderObject.Hash` (canonical path) for all operations (open, upload, delete, etc.)
3. Never perform client-side path rewriting or title substitution
4. Handle errors gracefully when titles cannot be resolved

### ⚠️ Known Gaps (Validation Focus)
- **Reverse Resolution:** Friendly → Canonical path conversion is incomplete
- **Title Collisions:** No disambiguation when two articles have identical titles
- **Client Tests:** Missing breadcrumb/navigation validation tests
- **VS Code Extension:** Not yet implemented (future work)

### 📋 Key Files to Validate
- **Server:** `Editor/Controllers/FileManagerController.cs` (`HandleOpenViaCqrsAsync`, `FilterEntries`), `FileEntryPathHelper.cs`, `FileEntryTitleService.cs`, `FolderListingService.cs`
- **Client:** `Editor/wwwroot/js/file-manager.js` (or elFinder integration code)
- **Models:** `FileManagerEntry.cs` (`Path` vs `DisplayPath`), `ElFinderObject.cs` (`Hash` vs `Name`)

---

## Context

SkyCMS stores article assets in Azure Blob Storage using a canonical path structure based on article numbers:

```
/pub/articles/{articleNumber}/
/pub/articles/{articleNumber}/banner.jpg
/pub/articles/{articleNumber}/assets/styles.css
```

While this numeric approach provides stable URLs that remain valid regardless of article title changes, it creates a poor user experience for content editors who work with the File Manager UI. Editors think in terms of article titles (e.g., "Getting Started Guide") rather than numeric identifiers (e.g., "123"). Navigating a file tree with numeric folder names is confusing and error-prone, especially when managing dozens or hundreds of articles.

### Business Problem

Content editors need to:
- Quickly locate article assets by recognizing article titles
- Navigate the file system intuitively without memorizing article numbers
- Understand folder structure at a glance when browsing the File Manager

### Technical Constraints

- **Blob storage paths cannot change**: The canonical path `/pub/articles/123/banner.jpg` must remain stable because:
  - Article titles can change over time
  - External references and bookmarks rely on stable URLs
  - Renaming folders in blob storage would break existing links
- **Template folders use GUIDs**: `/pub/templates/{GUID}/` follows a similar pattern and has the same UX challenges
- **Performance**: Title resolution requires database lookups which must not degrade File Manager performance

---

## Design Goals

This decision aims to achieve:

1. **Improved Editor Experience**: Content editors can navigate by recognizing article titles instead of memorizing numbers
2. **Intuitive File Organization**: Folder structure is immediately understandable without consulting external documentation
3. **URL Stability**: Canonical paths remain unchanged when titles are updated, preventing broken links
4. **Reduced Cognitive Load**: Editors can work efficiently without learning article numbering conventions
5. **Future-Proof Architecture**: The dual-path system supports similar transformations for templates (GUIDs → titles) and potential future entity types
6. **Separation of Concerns**: Storage layer and presentation layer remain cleanly decoupled

## Non-Goals

This decision explicitly does **not** attempt to:

1. **Replace canonical paths in storage**: Blob storage will continue using numeric paths; only the UI display changes
2. **Address VSCode extension immediately**: The SkyCMS Explorer extension will be updated in a separate follow-on project
3. **Cache article titles aggressively**: Title updates must reflect immediately in the UI (within cache window constraints)
4. **Support URL slugs**: We are not introducing slug-based paths (e.g., `getting-started-guide`) as an alternative
5. **Migrate existing URLs**: All existing canonical path URLs will continue to work indefinitely
6. **Solve template GUID display**: While the architecture supports it, template title display is future work

---

## Decision

We will implement a **dual-path architecture** that maintains canonical numeric paths in blob storage while displaying human-readable titles in the File Manager UI.

### Core Principles

1. **Canonical Path (Storage Layer)**: The "real" path using article numbers
   - Used for all storage operations (upload, delete, copy, rename, move)
   - Used when inserting file references into editors
   - Used for downloads and URL copying
   - Stored in blob storage metadata and database records
   - **Example**: `/pub/articles/123/banner.jpg`

2. **Display Path (Presentation Layer)**: The "friendly" path using article titles
   - Used **only** for UI display in File Manager and breadcrumbs
   - Resolved dynamically from current article titles
   - Never persisted to storage or database
   - **Example**: `/pub/articles/Getting Started Guide/banner.jpg`

3. **Bi-directional Resolution**: The system must support:
   - **Forward**: Canonical → Display (for rendering UI)
   - **Reverse**: Display → Canonical (for user-typed paths or navigation)

### Path Transformation Rules

#### Applies to Paths Matching These Patterns

```
/pub/articles/{integer}
/pub/articles/{integer}/
/pub/articles/{integer}/filename.ext
/pub/articles/{integer}/subdirectory/filename.ext
/pub/articles/{integer}/nested/deep/path/filename.ext
```

#### Transformation Behavior

| Canonical Path | Article 123 Title | Display Path |
|----------------|-------------------|--------------|
| `/pub/articles/123` | "Getting Started Guide" | `/pub/articles/Getting Started Guide` |
| `/pub/articles/123/` | "Getting Started Guide" | `/pub/articles/Getting Started Guide/` |
| `/pub/articles/123/banner.jpg` | "Getting Started Guide" | `/pub/articles/Getting Started Guide/banner.jpg` |
| `/pub/articles/123/assets/styles.css` | "Getting Started Guide" | `/pub/articles/Getting Started Guide/assets/styles.css` |

**Rule**: Only the third path segment (the article number) is replaced with the article title. All subsequent path segments (subdirectories, filenames) remain unchanged.

### Title Change Handling

- **Canonical paths remain unchanged** when article titles are updated
- **Display paths automatically reflect the new title** on next UI render
- **No storage migration required** when titles change
- **Existing file URLs continue to work** because canonical paths are stable
- **Files inserted in editors use canonical paths** and are unaffected by title changes

### Title Uniqueness

- **Active articles must have unique titles** (enforced at the application layer)
- **Trashed/deleted articles may share titles with active articles** (soft-deleted records are filtered from listings)
- **No disambiguation strategy needed** because title collisions are prevented

### Special Characters in Titles

- **Display paths show titles verbatim** without URL encoding or sanitization
- **UI rendering handles escaping** as needed for HTML/JavaScript contexts
- **No length limits on titles** in display paths (though practical limits exist in the article title field itself)

### Implementation Architecture

#### Layer 1: Path Extraction Utilities (`FileEntryPathHelper`)

Pure utility functions for:
- Extracting article numbers from canonical paths (`TryGetArticleNumberFromPath`)
- Extracting template GUIDs from canonical paths (`TryGetTemplateId`)
- Batch-collecting article numbers from file listings (`ExtractArticleNumbersFromEntries`)
- Transforming paths between canonical and display formats (`ResolveFriendlyDisplayPath`)
- Transforming individual folder names (`ResolveFriendlyDisplayName`)
- Path validation and normalization

**Key Methods**:
```csharp
// Extract article number from canonical path
bool TryGetArticleNumberFromPath(string path, out int articleNumber);

// Transform canonical path to display path
string ResolveFriendlyDisplayPath(string canonicalPath, IReadOnlyDictionary<int, string> articleTitlesByNumber);

// Transform canonical folder name to display name
string ResolveFriendlyDisplayName(string parentPath, FileManagerEntry entry, 
	IReadOnlyDictionary<int, string> articleTitlesByNumber, 
	IReadOnlyDictionary<Guid, string> templateTitlesById);
```

#### Layer 2: Database Resolution (`FileEntryTitleService`)

Async service responsible for:
- Batch lookup of article titles by article number from `ArticleCatalog` table
- Fallback to `Articles` table for draft articles not yet in catalog
- Version handling (selects latest version title when multiple versions exist)
- Soft-delete filtering (excludes articles where all versions are marked `StatusCodeEnum.Deleted`)
- Batch lookup of template titles by GUID from `Templates` table

**Caching Strategy**:
- **Deleted article numbers cached for 30 seconds** (sliding expiration)
- **Cache key scoped by tenant domain** to prevent cross-tenant bleed in multi-tenant deployments
- **Title lookups are NOT cached** to ensure immediate reflection of title changes in the UI
- **Rationale**: Caching by article number (not title) prevents cache invalidation issues when titles change

**Performance Characteristics**:
- Uses batch queries to minimize database round-trips
- Single query fetches all article titles in a directory listing
- Client-side filtering for Cosmos DB EF compatibility (no server-side `Contains()` on partition keys)
- Acceptable performance trade-off: small cache window ensures near-real-time title updates while reducing DB load

**Key Methods**:
```csharp
Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<FileManagerEntry> entries);
Task<IReadOnlyDictionary<Guid, string>> GetTemplateTitlesByIdAsync(IEnumerable<FileManagerEntry> entries);
Task FilterDeletedArticleEntriesAsync(IList<FileManagerEntry> entries, IMemoryCache cache, string tenantDomain);
```

#### Layer 3: Integration Points

**3a. `FileManagerController.FilterEntries` (Open Response Shaping)**

`HandleOpenViaCqrsAsync()` dispatches the CQRS `OpenCommand`, then applies `FilterEntries(...)` to the returned `OpenResponse.Files`.

**Behavior**:
- Filters out deleted article entries before serialization
- Rewrites only the third path segment (article number) in `DisplayPath` to the article title
- Rewrites `Name` only when the active listing parent is `/pub/articles` and the entry is a direct child (`/pub/articles/{number}`)
- Leaves nested child names unchanged (for example, `/pub/articles/{number}/assets` keeps `Name = "assets"`)

**Example**:
```
Listing parent: /pub/articles
Entry real path: /pub/articles/123
Result: Name = "Getting Started Guide", DisplayPath = "/pub/articles/Getting Started Guide"

Listing parent: /pub/articles/123
Entry real path: /pub/articles/123/assets
Result: Name = "assets", DisplayPath = "/pub/articles/Getting Started Guide/assets"
```

**3b. `FolderListingService` (Virtual Root Listings)**

Provides catalog-driven virtual listings for special root directories:

- **`/pub/articles` root**: Queries `ArticleCatalog` table and synthesizes `FileManagerEntry` objects with:
  - `Path = "/pub/articles/{articleNumber}"` (canonical)
  - `DisplayPath = "/pub/articles/{articleTitle}"` (friendly)
  - `Name = articleTitle` (friendly)

- **`/pub/templates` root**: Queries `Templates` table and synthesizes entries with:
  - `Path = "/pub/templates/{GUID}"` (canonical)
  - `DisplayPath = "/pub/templates/{templateTitle}"` (friendly)
  - `Name = templateTitle` (friendly)

- **All other paths**: Delegates to blob storage for real directory listings, then applies soft-delete filtering for article subfolders

**Rationale**: Virtual listings allow the File Manager to show article/template titles directly in the root folder view without requiring blob storage folder renames.

#### Layer 4: File Manager UI Integration

The elFinder-based File Manager UI:
- Receives `FileManagerEntry` objects with both `Path` (canonical) and `DisplayPath` (friendly) properties
- Renders `DisplayPath` in tree views, breadcrumbs, and file listings
- Uses `Path` (canonical) for all backend operations:
  - File uploads
  - Downloads
  - Deletes
  - Renames
  - Copies/moves
  - URL generation for inserted files

**User Interaction Flow**:
1. User opens File Manager → sees "Getting Started Guide" folder
2. User clicks folder → UI sends canonical path `/pub/articles/123` to backend
3. Backend retrieves files from blob storage using canonical path
4. Backend resolves article title "Getting Started Guide" from database
5. UI displays "Getting Started Guide" breadcrumb while working with canonical path internally

**Bi-directional Path Resolution**:
- **User types friendly path**: UI reverse-resolves to canonical path before sending to backend
- **User pastes friendly path**: Same reverse-resolution logic applies
- **Bookmarks/URLs**: Always use canonical paths to ensure stability

---

## Data Flow & API Contracts

### FileManagerEntry Schema

The internal `FileManagerEntry` object (from `Cosmos.BlobService`) carries both canonical and display paths:

```csharp
public class FileManagerEntry
{
    public string Name { get; set; }              // Display name (e.g., "Getting Started Guide" or "banner.jpg")
    public string Path { get; set; }              // Canonical path: "/pub/articles/123/banner.jpg"
    public string DisplayPath { get; set; }       // Friendly path: "/pub/articles/Getting Started Guide/banner.jpg"
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    // ... additional properties
}
```

**Key Contracts:**
- `Path` **always** contains the canonical numeric path (used for storage operations)
- `DisplayPath` contains the friendly title-based path (used for UI rendering)
- `Name` for article folders contains the **title** (not the number)
- When `DisplayPath` is not explicitly set, it defaults to `Path`

### elFinder Protocol Integration

The elFinder driver translates `FileManagerEntry` objects into `ElFinderObject` responses:

```csharp
public sealed class ElFinderObject
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; }              // Base64-encoded canonical path

    [JsonPropertyName("phash")]
    public string ParentHash { get; set; }        // Base64-encoded parent canonical path

    [JsonPropertyName("name")]
    public string Name { get; set; }              // Display name (title or filename)

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("mime")]
    public string Mime { get; set; }              // "directory" or file MIME type

    // ... read, write, timestamp, etc.
}
```

**Critical Encoding Rule:**
- `Hash` and `PHash` encode **canonical paths only** (not display paths)
- This ensures all storage operations use stable numeric paths
- The `Name` field shows the **friendly title** for display

**Example Transformation:**

```
FileManagerEntry:
  Path = "/pub/articles/123"
  DisplayPath = "/pub/articles/Getting Started Guide"
  Name = "Getting Started Guide"

↓ (FileManagerController.FilterEntries applied for Open responses)

ElFinderObject:
  Hash = "bDFfL3B1Yi9hcnRpY2xlcy8xMjM"  ← Base64("/pub/articles/123")
  PHash = "bDFfL3B1Yi9hcnRpY2xlcw"       ← Base64("/pub/articles")
  Name = "Getting Started Guide"         ← Friendly display name
```

### HTTP Request/Response Flow

#### Scenario: User Opens /pub/articles/123 in File Manager

**1. Client Request**
```http
GET /FileManager/ElFinderConnector?cmd=open&target=bDFfL3B1Yi9hcnRpY2xlcy8xMjM&init=0&tree=0
```

- `target` parameter contains Base64-encoded **canonical path**: `/pub/articles/123`

**2. Server Processing**

```
FileManagerController.HandleOpenViaCqrsAsync()
  ↓
1. Decode target hash → "/pub/articles/123"
2. Dispatch OpenCommand through elFinder CQRS dispatcher
3. Receive OpenResponse containing canonical-path-backed ElFinder objects
4. Call FilterEntries(openResponse.Files, targetPath)
5. FilterEntries loads article title/status lookup and removes deleted entries
6. FilterEntries rewrites DisplayPath article segment id → title for article paths
7. FilterEntries rewrites Name only for direct `/pub/articles/{id}` entries when listing `/pub/articles`
8. Return JSON with canonical hashes unchanged for protocol operations
```

**3. Server Response**
```json
{
  "cwd": {
    "hash": "bDFfL3B1Yi9hcnRpY2xlcy8xMjM",
    "phash": "bDFfL3B1Yi9hcnRpY2xlcw",
    "name": "Getting Started Guide",
    "mime": "directory",
    "size": 0,
    "ts": 1705622400,
    "read": 1,
    "write": 1,
    "dirs": 1
  },
  "files": [
    {
      "hash": "bDFfL3B1Yi9hcnRpY2xlcy8xMjMvYmFubmVyLmpwZw",
      "phash": "bDFfL3B1Yi9hcnRpY2xlcy8xMjM",
      "name": "banner.jpg",
      "mime": "image/jpeg",
      "size": 245678,
      "ts": 1705622400,
      "read": 1,
      "write": 1
    }
  ],
  "uplMaxSize": "2G"
}
```

**4. Client Rendering**

The elFinder UI receives:
- `name: "Getting Started Guide"` ← **Displays this in the folder tree**
- `hash: "bDFfL3B1Yi9hcnRpY2xlcy8xMjM"` ← **Uses this for navigation/operations**

When the user clicks a file or performs an operation (upload, delete, rename), the client sends the **hash** (canonical path) back to the server.

### Reverse Resolution (Friendly → Canonical)

**Current Implementation Status:** ⏳ **Partially implemented**

The system supports reverse resolution through these mechanisms:

1. **Canonical-First elFinder protocol flow**
   - The client sends hashes that decode to canonical paths (for example, `/pub/articles/123/banner.jpg`)
   - Friendly titles are applied server-side only to response display fields (`Name`/`DisplayPath`)
   - **Limitation**: Free-form friendly path input is still not resolved end-to-end

2. **Manual Reverse Lookup (Not Yet Implemented)**
   - **Missing**: Direct API endpoint for friendly path → canonical path conversion
   - **Workaround**: Client must maintain a mapping from titles to article numbers
   - **Future Work**: Add `IFileEntryTitleService.ResolveCanonicalPathAsync(string friendlyPath)`

**Expected Behavior for Edge Cases:**

| User Input | Expected Resolution | Error Handling |
|------------|---------------------|----------------|
| `/pub/articles/Getting Started Guide/` | Lookup title → article #123 → `/pub/articles/123/` | 404 if title not found |
| `/pub/articles/Nonexistent Title/` | Title not in catalog | Return error or empty result |
| `/pub/articles/123/` (numeric input) | Already canonical | Pass through unchanged |
| `/pub/articles/Draft Article/` (draft only) | Lookup in Articles table (not just catalog) | Find latest version title |

**Current Gap:** The reverse resolution logic is **incomplete**. Users typing friendly paths directly would not be properly resolved to canonical paths in all scenarios.

---

## Detailed Rationale

### Why Dual-Path Architecture?

The dual-path approach was chosen because it satisfies competing constraints:

1. **UX Requirement**: Editors need human-readable folder names
2. **Storage Stability**: Blob storage paths must never change (external references, CDN caching)
3. **Performance**: Dynamic resolution is fast enough with proper caching (30-second window for deleted articles)
4. **Maintainability**: Clean separation between storage (canonical) and presentation (display) layers

### Why Not Rename Folders in Storage?

Renaming blob storage folders when titles change was considered and rejected because:

- **Breaking change**: Existing URLs and bookmarks would fail
- **Expensive operation**: Recursive blob moves are slow and error-prone
- **Race conditions**: Files being edited/uploaded during rename would fail
- **CDN incompatibility**: Azure CDN caching assumes stable paths

### Why Not Use URL Slugs?

Slug-based paths (e.g., `/pub/articles/getting-started-guide/`) were rejected because:

- **Collision handling**: Similar titles create disambiguation problems
- **Slug regeneration**: Title changes would still require path changes (breaks URLs)
- **Length constraints**: Slugs impose artificial limits on title length
- **Reverse lookup required**: Still need article number for database operations
- **Lost clarity**: Slugs don't always match exact article titles

### Why Cache Deleted Articles But Not Titles?

- **Deleted articles**: Status changes rarely (only on trash/restore), so 30-second cache is safe
- **Article titles**: Must update immediately when editors change titles, so no title caching
- **Cache by article number**: Ensures cache entries remain valid even when titles change

### Why Verbatim Title Display?

Titles are displayed with special characters intact (no URL encoding or sanitization) because:

- **Clarity**: Editors see the exact article title they assigned
- **Consistency**: Display matches the title shown in the article editor
- **Simplicity**: No transformation rules to document or maintain
- **Safety**: HTML/JavaScript escaping is handled by the rendering layer

---

## Consequences

### Positive

1. **Improved Editor Experience**: Content editors can navigate by recognizing article titles instead of memorizing numbers
2. **Intuitive File Organization**: Folder structure is immediately understandable without consulting external documentation
3. **Reduced Training Time**: New editors can navigate the file system without learning article numbering conventions
4. **URL Stability**: Canonical paths remain unchanged when titles are updated, preventing broken links
5. **Future-Proof**: Architecture supports similar transformations for templates (GUIDs → titles) and potential future entity types
6. **Clean Separation of Concerns**: Storage layer and presentation layer remain decoupled

### Negative

1. **Added Complexity**: Dual-path architecture requires careful handling throughout the codebase
2. **Performance Overhead**: Title resolution requires database lookups on every directory listing
3. **Caching Considerations**: Must balance real-time title updates with performance (30-second cache window chosen)
4. **Testing Burden**: Must test both canonical and display path code paths
5. **Migration Challenge**: Existing URLs/documentation may reference numeric paths (though they continue to work)
6. **Edge Case Handling**: Must handle scenarios like:
   - Article title contains special characters
   - Article title is very long
   - Multiple rapid title changes
   - Database unavailable during title resolution (graceful degradation to numeric paths)

### Trade-offs

| Aspect | Choice | Alternative | Rationale |
|--------|--------|-------------|-----------|
| **Cache duration** | 30 seconds sliding | No cache / Longer cache | Balances real-time updates with DB load |
| **Cache scope** | Deleted article numbers only | All titles | Titles must update immediately; deleted status changes rarely |
| **Title display** | Verbatim with special chars | URL-encoded / Sanitized | Preserves exact article title for clarity |
| **Path resolution** | On-demand per request | Pre-computed at storage time | Ensures display always reflects current title |
| **Reverse resolution** | Supported | Not supported | Allows users to type/paste friendly paths |
| **Cosmos DB compatibility** | Client-side filtering | Server-side filtering | Required by EF Core provider limitations |

## Alternatives Considered

### Alternative 1: Store Friendly Paths Directly in Blob Storage

**Description**: Rename folders in blob storage when article titles change.

**Rejected Because**:
- Breaking change for existing URLs and bookmarks
- Expensive operation (requires recursive blob moves)
- Race conditions during title updates
- Breaks files currently being edited or uploaded
- Incompatible with Azure CDN caching strategies

### Alternative 2: Slug-Based Paths

**Description**: Use URL slugs (e.g., `getting-started-guide`) instead of article numbers.

**Rejected Because**:
- Slug collisions when similar titles exist (requires disambiguation)
- Slug regeneration when titles change (breaks URLs)
- Slug length limits create artificial title constraints
- Still requires reverse-lookup mechanism (slug → article number)
- Loses the clarity of showing exact article title

### Alternative 3: Show Both Number and Title

**Description**: Display paths like `/pub/articles/123 - Getting Started Guide/`.

**Rejected Because**:
- Cluttered UI with redundant information
- Longer paths harder to scan visually
- Doesn't solve the core problem (editors still see numbers)
- Non-standard path format may confuse users

### Alternative 4: Client-Side Only Title Resolution

**Description**: Resolve titles in browser JavaScript without server support.

**Rejected Because**:
- Additional API calls from client (worse performance)
- Inconsistent behavior when JavaScript fails
- Duplicate logic between client and server
- Harder to maintain and debug

## Implementation Notes

### Affected Components

- ✅ `Editor/Services/FileEntryPathHelper.cs` - Path transformation utilities
- ✅ `Editor/Services/FileEntryTitleService.cs` - Database title/status lookups and deleted checks
- ✅ `Editor/Services/FolderListingService.cs` - Virtual root listings
- ✅ `Editor/Controllers/FileManagerController.cs` - Open response shaping (`FilterEntries`) and deleted-path guards
- ✅ `Drivers/SkyCMS.Drivers.ElFinder/Handlers/OpenCommandHandler.cs` - canonical open payload provider (pre-controller shaping)
- ⏳ `Editor/wwwroot/js/file-manager.js` - File Manager UI (if applicable)

---

## Edge Cases & Error Handling

### Article Lifecycle Transitions

| Scenario | Path Behavior | Display Behavior | Error Handling |
|----------|---------------|------------------|----------------|
| **Article Published** | Canonical path `/pub/articles/123` remains stable | Folder name shows current published title | Title updates on next catalog sync |
| **Article Title Changed** | Canonical path unchanged | New title displayed after catalog sync (30s cache) | Old title shown until cache expires |
| **Article Unpublished** | Canonical path still exists in blob storage | Folder **hidden** from listings (filtered by `FilterDeletedArticleEntriesAsync`) | 404 or access denied if user navigates directly |
| **Article Re-published** | Same canonical path `/pub/articles/123` reused | Folder reappears with current title | Cached deleted state expires (30s) |
| **Article Deleted** | Blob storage may retain files (soft delete) | Folder **removed** from listings permanently | Number may be reused for future articles |

**Critical Behavior:** `FileEntryTitleService.FilterDeletedArticleEntriesAsync()` actively removes entries for article numbers not present in the `ArticleCatalog`. This means **unpublished articles disappear from File Manager** even if their blob storage files remain.

### Title Conflict Resolution

**Problem:** Two articles could theoretically have identical titles.

**Current Approach:**
- Display paths show the title **without numeric disambiguation**
- Canonical paths remain unique due to integer-based structure
- User sees: `/pub/articles/Introduction/`
- Storage uses: `/pub/articles/123/` vs `/pub/articles/456/`

**Known Limitation:** Users cannot visually distinguish folders with identical titles in File Manager. This is considered **acceptable** because:
1. Article titles are typically unique in practice
2. Canonical paths preserve uniqueness for all operations
3. Adding numeric suffixes (`Introduction (123)`) was rejected as too verbose

**Future Enhancement Consideration:** If title collisions become problematic, consider showing article ID in a tooltip or secondary display field rather than in the folder name.

### Non-Article Content

| Path | Canonical | Display | Notes |
|------|-----------|---------|-------|
| `/pub/templates/abc-123-guid/` | Template GUID | Template title (e.g., "Blog Template") | Similar title resolution via `GetTemplateTitlesByIdAsync()` |
| `/pub/static/logo.png` | No transformation | Shows actual filename | Static assets bypass article-specific logic |
| `/pub/uploads/temp-file.jpg` | No transformation | Shows actual filename | User uploads not linked to articles |

### Invalid Path Handling

**Malformed Paths:**
```csharp
// Safe handling in FileEntryPathHelper
TryGetArticleNumberFromPath("/pub/articles/")       → false, articleNumber = 0
TryGetArticleNumberFromPath("/pub/articles/abc")    → false (non-integer segment)
TryGetArticleNumberFromPath("/pub/123")             → false (not under /articles/)
TryGetArticleNumberFromPath("/pub/articles/123/")   → true, articleNumber = 123
```

**Out-of-Bounds Access Prevention:**
- All segment indexing (`segments[2]`) is now **bounds-checked** before access
- Invalid paths return early with defaults rather than throwing exceptions

### Caching & Staleness

**Title Resolution Cache:**
- **TTL:** 30 seconds (configurable per tenant via `MemoryCache`)
- **Cache Key:** `article-titles-{tenantDomain}`
- **Invalidation:** Time-based expiration only (no active invalidation on publish/unpublish)

**Deleted Article Cache:**
- **TTL:** 30 seconds (tenant-scoped)
- **Cache Key:** `deleted-article-numbers-{tenantDomain}`
- **Purpose:** Avoid querying `ArticleCatalog` on every request for known-deleted articles

**Staleness Implications:**
- Title changes may take **up to 30 seconds** to reflect in File Manager
- Deleted articles may still appear for **up to 30 seconds** after unpublish
- This is considered acceptable for the current use case (content authoring is not millisecond-sensitive)

### File Upload & Path Safety

**Upload Target Validation:**
```csharp
FileEntryPathHelper.IsUploadPathSafe(path)
```

- **Blocks:** `..` traversal, absolute paths, paths outside `/pub/` root
- **Allows:** Uploads to article folders (`/pub/articles/123/image.jpg`)
- **Ensures:** All uploads go to canonical paths, not display paths

**Dangerous Extensions:**
```csharp
FileEntryPathHelper.IsDangerousExtension(filename)
```

- **Blocks:** `.exe`, `.dll`, `.bat`, `.sh`, `.ps1`, executable scripts
- **Allows:** Common web assets (images, videos, PDFs, text files)

---

## Acceptance Criteria

### Server-Side Implementation Must:

1. ✅ **Canonical Path Preservation**
   - All storage operations (read, write, delete) use canonical integer paths
   - Hash encoding for elFinder always uses canonical paths
   - No display paths ever reach blob storage APIs

2. ✅ **Title Resolution Correctness**
   - Article titles fetched from `ArticleCatalog` (published versions)
   - Fallback to `Articles` table for draft-only content
   - Template titles fetched from template metadata
   - Batch lookups minimize database queries

3. ✅ **Security Boundaries**
   - Path traversal attempts blocked
   - Uploads restricted to `/pub/` hierarchy
   - Dangerous file extensions rejected
   - Tenant isolation maintained via `IDynamicConfigurationProvider`

4. ⏳ **Edge Case Handling** (Partially Complete)
   - ✅ Deleted articles filtered from listings
   - ✅ Invalid path segments return safe defaults
   - ⚠️ **Gap:** Reverse resolution (friendly → canonical) incomplete
   - ⚠️ **Gap:** No explicit handling for title collisions

5. ✅ **Performance**
   - Title resolution cached (30s TTL)
   - Batch queries for multiple articles
   - No N+1 query patterns

### Client-Side Implementation Must:

1. **Display Behavior**
   - Show article titles in folder tree (not numeric IDs)
   - Show friendly paths in breadcrumbs/address bar
   - Preserve canonical hash in all operation requests

2. **Navigation**
   - Clicking a folder sends canonical hash to server
   - Breadcrumb navigation reconstructs path from elFinder response
   - No client-side path rewriting or transformation

3. **Error Handling**
   - Display meaningful errors when title resolution fails
   - Handle 404s gracefully when navigating to deleted articles
   - Show loading state during title cache refresh

### Validation Tests Required:

**Server-Side:**
- ✅ `TryGetArticleNumberFromPath` handles valid/invalid paths
- ✅ `ResolveFriendlyDisplayPath` returns titles when present
- ✅ `FilterDeletedArticleEntriesAsync` removes unpublished articles
- ⏳ **Missing:** Reverse resolution test (friendly path input → canonical path output)
- ⏳ **Missing:** Title collision scenario test

**Integration:**
- ✅ elFinder open command returns correct `name` and `hash` values
- ⏳ **Missing:** Full round-trip test (open folder → upload file → verify storage path)
- ⏳ **Missing:** Test deleted article handling in file manager UI

**Client-Side (File Manager JavaScript):**
- ⏳ **Missing:** Verify breadcrumbs show friendly paths
- ⏳ **Missing:** Verify operations send canonical hashes
- ⏳ **Missing:** Test error display when article not found

**VS Code Extension (Future):**
- ⏳ **Not Yet Implemented:** Extension does not yet support article title resolution
- ⏳ **Future Work:** Add similar logic to workspace file trees

---

## Implementation Notes

### Future Work

1. **SkyCMS Explorer VSCode Extension**: Apply same dual-path architecture to VSCode extension file tree (separate project)
2. **Template GUID Display**: Extend to show template titles instead of GUIDs (similar pattern)
3. **Performance Monitoring**: Instrument title resolution to identify slow queries and optimize caching strategy
4. **Graceful Degradation**: Handle database unavailability by falling back to numeric paths in UI
5. **Audit Logging**: Log when reverse path resolution fails (user typed invalid friendly path)

### Migration Path

No migration required. Changes are additive:
- Existing canonical paths continue to work
- Display paths introduced alongside canonical paths
- Gradual rollout: File Manager first, then VSCode extension

## References

- Implementation: `Editor/Services/FileEntryPathHelper.cs`
- Tests: `Tests/Editor/Services/FileEntryPathHelperTests.cs`
- elFinder Driver: `Drivers/SkyCMS.Drivers.ElFinder/`
- Azure Blob Storage docs: https://learn.microsoft.com/azure/storage/blobs/

