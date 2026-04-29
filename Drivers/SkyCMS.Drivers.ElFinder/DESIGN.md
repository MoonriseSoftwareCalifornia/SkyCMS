# SkyCMS.Drivers.ElFinder — Design Document

**Document Purpose**: Capture architecture, design decisions, and implementation approach for the elFinder driver.

**Status**: Implementation Complete  
**Last Updated**: Phase 5 (elFinder 2.1 protocol compliance)

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Command Mapping](#command-mapping)
3. [DTO Design](#dto-design)
4. [Storage Adapter Interface](#storage-adapter-interface)
5. [Error Handling Strategy](#error-handling-strategy)
6. [Testing Strategy](#testing-strategy)
7. [Dependency Injection](#dependency-injection)
8. [Phase 2 Action Items](#phase-2-action-items)

---

## Architecture Overview

### Layered Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  elFinder UI (JavaScript)                                   │
│  - Sends HTTP requests with cmd parameter                   │
│  - Expects elFinder 2.1 JSON responses                       │
└──────────────────────┬──────────────────────────────────────┘
                       │ HTTP (GET/POST)
                       ▼
┌─────────────────────────────────────────────────────────────┐
│  ElFinderConnectorController                                │
│  - Parses cmd parameter                                     │
│  - Dispatches to MediatR handler                            │
│  - Returns JSON response                                    │
└──────────────────────┬──────────────────────────────────────┘
                       │ IRequest<IResponse>
                       ▼
┌─────────────────────────────────────────────────────────────┐
│  IRequestHandler<ElFinderCommand, ElFinderResponse>         │
│  - CQRS command handler                                     │
│  - Calls driver methods                                     │
│  - Transforms responses to elFinder DTOs                    │
└──────────────────────┬──────────────────────────────────────┘
                       │ IElFinderStorageAdapter
                       ▼
┌─────────────────────────────────────────────────────────────┐
│  IElFinderStorageAdapter (abstraction)                      │
│  - File/folder operations                                   │
│  - Path encoding/decoding                                   │
│  - MIME type resolution                                     │
└──────────────────────┬──────────────────────────────────────┘
                       │ IStorageContext / IApplicationDbContext
                       ▼
┌─────────────────────────────────────────────────────────────┐
│  SkyCMS Backend Services                                    │
│  - Blob storage (IStorageContext)                           │
│  - Database (ApplicationDbContext)                          │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

#### 1. Commands & Handlers (CQRS)

**Location**: `SkyCMS.Drivers.ElFinder/Commands/`

Each elFinder command maps to a CQRS command:

```csharp
// SkyCMS.Drivers.ElFinder/Commands/OpenCommand.cs
public class OpenCommand : IRequest<OpenResponse>
{
    public string? Target { get; }
    public bool Init { get; }
    public bool Tree { get; }
    public string? BlobPublicUrl { get; }
    public string? TmbUrl { get; }
    public string? RootPath { get; }
}

// SkyCMS.Drivers.ElFinder/Handlers/OpenCommandHandler.cs
public class OpenCommandHandler : IRequestHandler<OpenCommand, OpenResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public async Task<OpenResponse> Handle(
        OpenCommand request,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

#### 2. DTOs (Data Transfer Objects)

**Location**: `SkyCMS.Drivers.ElFinder/Responses/`

Strongly-typed classes with `[JsonPropertyName]` attributes:

```csharp
public class OpenResponse
{
    [JsonPropertyName("cwd")]
    public ElFinderObject? Cwd { get; set; }

    [JsonPropertyName("files")]
    public List<ElFinderObject> Files { get; set; } = new();

    [JsonPropertyName("api")]
    public string Api { get; set; } = "2.1049";

    [JsonPropertyName("options")]
    public ElFinderOptions? Options { get; set; }

    [JsonPropertyName("netDrivers")]
    public List<object>? NetDrivers { get; set; }

    // ... additional fields
}

public class ElFinderObject
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    // ... per spec
}
```

#### 3. Storage Adapter

**Location**: `SkyCMS.Drivers.ElFinder/Adapters/`

Abstraction insulating driver from storage implementation:

```csharp
public interface IElFinderStorageAdapter
{
    Task<FileEntry> GetFileAsync(string path);
    Task<List<FileEntry>> ListAsync(string path);
    Task<FileEntry> CreateFolderAsync(string path);
    Task<FileEntry> CreateFileAsync(string path, Stream content);
    Task DeleteFileAsync(string path);
    Task DeleteFolderAsync(string path);
    Task RenameAsync(string oldPath, string newPath);
    Task<Stream> GetFileStreamAsync(string path);
    // ... additional methods per elFinder commands
}

public class StorageContextAdapter : IElFinderStorageAdapter
{
    private readonly IStorageContext storageContext;

    public async Task<FileEntry> GetFileAsync(string path)
    {
        // Adapt IStorageContext.GetFileAsync
    }

    // ... implementations
}
```

#### 4. Utilities

**Location**: `SkyCMS.Drivers.ElFinder/Utilities/`

- `PathEncoder`: Base64 encoding/decoding with elFinder-specific rules
- `MimeTypeResolver`: MIME type detection
- `ValidationHelper`: Path validation, safe name checking
- `ElFinderErrorFactory`: Standard error response creation

---

## Command Mapping

### Commands Overview

| elFinder Cmd | CQRS Command | Handler | Response |
|--------------|--------------|---------|----------|
| `open` | `ElFinderOpenCommand` | `OpenCommandHandler` | `ElFinderOpenResponse` |
| `tree` | `ElFinderTreeCommand` | `TreeCommandHandler` | `ElFinderTreeResponse` |
| `ls` | `ElFinderListCommand` | `ListCommandHandler` | `ElFinderListResponse` |
| `mkdir` | `ElFinderMkdirCommand` | `MkdirCommandHandler` | `ElFinderMkdirResponse` |
| `mkfile` | `ElFinderMkfileCommand` | `MkfileCommandHandler` | `ElFinderMkfileResponse` |
| `rm` | `ElFinderRemoveCommand` | `RemoveCommandHandler` | `ElFinderRemoveResponse` |
| `rename` | `ElFinderRenameCommand` | `RenameCommandHandler` | `ElFinderRenameResponse` |
| `upload` | `ElFinderUploadCommand` | `UploadCommandHandler` | `ElFinderUploadResponse` |
| `paste` | `ElFinderPasteCommand` | `PasteCommandHandler` | `ElFinderPasteResponse` |
| `get` | `ElFinderGetCommand` | `GetCommandHandler` | `ElFinderGetResponse` |
| `put` | `ElFinderPutCommand` | `PutCommandHandler` | `ElFinderPutResponse` |
| `tmb` | `ElFinderTmbCommand` | `TmbCommandHandler` | `ElFinderTmbResponse` |
| `info` | `ElFinderInfoCommand` | `InfoCommandHandler` | `ElFinderInfoResponse` |
| `size` | `ElFinderSizeCommand` | `SizeCommandHandler` | `ElFinderSizeResponse` |
| `parents` | `ElFinderParentsCommand` | `ParentsCommandHandler` | `ElFinderParentsResponse` |

---

## DTO Design

### Response Classes (Per Spec)

**Structure**: All responses inherit from base or follow explicit elFinder format.

```csharp
// Base response (all success responses)
public class ElFinderResponse
{
    [JsonPropertyName("api")]
    public string ApiVersion { get; set; } = "2.1";
}

// Error response
public class ElFinderErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; }
}

// Open response
public class ElFinderOpenResponse : ElFinderResponse
{
    [JsonPropertyName("cwd")]
    public ElFinderFileObject CurrentWorkingDirectory { get; set; }

    [JsonPropertyName("files")]
    public List<object> Files { get; set; }

    [JsonPropertyName("uplMaxSize")]
    public string MaxUploadSize { get; set; } = "64M";

    [JsonPropertyName("options")]
    public Dictionary<string, object> Options { get; set; }
}

// File object (used in multiple responses)
public class ElFinderFileObject
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("mime")]
    public string Mime { get; set; }

    [JsonPropertyName("ts")]
    public long Timestamp { get; set; }

    [JsonPropertyName("read")]
    public int Read { get; set; } = 1;

    [JsonPropertyName("write")]
    public int Write { get; set; } = 1;

    [JsonPropertyName("locked")]
    public int Locked { get; set; } = 0;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("dirs")]
    public int? Dirs { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("phash")]
    public string ParentHash { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("volumeid")]
    public string VolumeId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("tmb")]
    public string Thumbnail { get; set; }
}
```

### Null Handling Strategy

- Use `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` for optional fields
- This ensures elFinder doesn't see null values for fields it doesn't expect
- Cleaner JSON, matches spec exactly

---

## Storage Adapter Interface

### Design

The adapter abstracts SkyCMS storage operations from the driver logic:

```csharp
/// <summary>
/// Adapter interface for elFinder to interact with SkyCMS storage.
/// Abstracts storage implementation details; enables testing.
/// </summary>
public interface IElFinderStorageAdapter
{
    /// <summary>
    /// Get metadata for a file or folder.
    /// </summary>
    Task<ElFinderStorageEntry> GetEntryAsync(string path);

    /// <summary>
    /// List directory contents (files and folders).
    /// </summary>
    Task<List<ElFinderStorageEntry>> ListAsync(string path);

    /// <summary>
    /// Create a new folder.
    /// </summary>
    Task<ElFinderStorageEntry> CreateFolderAsync(string path, string name);

    /// <summary>
    /// Create a new empty file.
    /// </summary>
    Task<ElFinderStorageEntry> CreateFileAsync(string path, string name);

    /// <summary>
    /// Delete a file.
    /// </summary>
    Task DeleteFileAsync(string path);

    /// <summary>
    /// Delete a folder and all contents.
    /// </summary>
    Task DeleteFolderAsync(string path);

    /// <summary>
    /// Rename or move a file/folder.
    /// </summary>
    Task<ElFinderStorageEntry> MoveAsync(string fromPath, string toPath);

    /// <summary>
    /// Copy a file or folder.
    /// </summary>
    Task CopyAsync(string fromPath, string toPath);

    /// <summary>
    /// Get file content as stream.
    /// </summary>
    Task<Stream> GetFileStreamAsync(string path);

    /// <summary>
    /// Write content to a file (overwrite).
    /// </summary>
    Task WriteFileAsync(string path, Stream content);

    /// <summary>
    /// Check if path exists.
    /// </summary>
    Task<bool> ExistsAsync(string path);

    /// <summary>
    /// Get total size of files in path (recursive for folders).
    /// </summary>
    Task<long> GetTotalSizeAsync(string path);
}

/// <summary>
/// Internal representation of a file/folder in storage.
/// </summary>
public class ElFinderStorageEntry
{
    public string Path { get; set; }
    public string Name { get; set; }
    public string Extension { get; set; }
    public long Size { get; set; }
    public bool IsDirectory { get; set; }
    public DateTime Modified { get; set; }
    public bool HasSubdirectories { get; set; }
}
```

### Implementation: StorageContextAdapter

```csharp
/// <summary>
/// Adapts SkyCMS IStorageContext to IElFinderStorageAdapter.
/// </summary>
public class StorageContextAdapter : IElFinderStorageAdapter
{
    private readonly IStorageContext storageContext;

    public StorageContextAdapter(IStorageContext storageContext)
    {
        this.storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));
    }

    public async Task<ElFinderStorageEntry> GetEntryAsync(string path)
    {
        var entry = await storageContext.GetFileAsync(path);
        return MapToElFinderEntry(entry);
    }

    // ... remaining implementations

    private ElFinderStorageEntry MapToElFinderEntry(FileManagerEntry entry)
    {
        // Convert SkyCMS FileManagerEntry to ElFinderStorageEntry
    }
}
```

---

## Error Handling Strategy

### Error Keys (Per elFinder Spec)

Standard error keys used in all error responses:

```csharp
public static class ElFinderErrorKeys
{
    public const string UnknownCommand = "errUnknownCmd";
    public const string Access = "errAccess";
    public const string UploadFile = "errUploadFile";
    public const string UploadNoFiles = "errUploadNoFiles";
    public const string InvalidName = "errInvName";
    public const string Exists = "errExists";
    public const string NotFound = "errNotFound";
    public const string NotDirectory = "errNotDir";
    public const string NotFile = "errNotFile";
    public const string TmpFile = "errTmpFile";
}
```

### Error Factory

```csharp
public static class ElFinderErrorFactory
{
    public static object CreateErrorResponse(string errorKey)
    {
        return new { error = errorKey };
    }

    public static ElFinderErrorResponse CreateError(string errorKey)
    {
        return new ElFinderErrorResponse { Error = errorKey };
    }
}
```

### Handler Pattern

All command handlers follow this pattern:

```csharp
try
{
    // Validate input
    if (!IsValidPath(path))
    {
        throw new ElFinderException(ElFinderErrorKeys.Access);
    }

    // Execute operation
    var result = await storageAdapter.OperationAsync(path);

    // Return success response
    return new ElFinderOperationResponse { /* ... */ };
}
catch (ElFinderException ex)
{
    return ElFinderErrorFactory.CreateError(ex.ErrorKey);
}
catch (Exception ex)
{
    logger.LogError(ex, "Unexpected error");
    return ElFinderErrorFactory.CreateError(ElFinderErrorKeys.TmpFile);
}
```

---

## Testing Strategy

### Unit Testing Approach

**Location**: `SkyCMS.Drivers.ElFinder.Tests/`

1. **Command Handler Tests**
   - Mock `IElFinderStorageAdapter`
   - Test each handler's business logic
   - Verify response format matches spec

2. **Storage Adapter Tests**
   - Mock `IStorageContext`
   - Test path handling, encoding/decoding
   - Verify error handling

3. **Utility Tests**
   - Path encoding/decoding roundtrips
   - MIME type resolution
   - Validation rules

4. **Integration Tests** (Phase 4)
   - End-to-end with real storage
   - Verify controller integration

### Example Test

```csharp
[Fact]
public async Task OpenCommandHandler_WithValidTarget_ReturnsOpenResponse()
{
    // Arrange
    var adapter = new Mock<IElFinderStorageAdapter>();
    var entry = new ElFinderStorageEntry { /* ... */ };
    adapter.Setup(x => x.GetEntryAsync("/pub"))
        .ReturnsAsync(entry);

    var handler = new ElFinderOpenCommandHandler(adapter.Object);
    var command = new ElFinderOpenCommand { Target = "l1_L3B1Yg==", IsInit = true };

    // Act
    var response = await handler.Handle(command, CancellationToken.None);

    // Assert
    Assert.NotNull(response);
    Assert.NotNull(response.CurrentWorkingDirectory);
    Assert.Single(response.Files); // At least cwd
}
```

---

## Dependency Injection

### Service Registration

In `Program.cs` (or startup configuration):

```csharp
services.AddScoped<IElFinderStorageAdapter, StorageContextAdapter>();

// Register MediatR handlers (if not auto-registered)
services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssembly(typeof(ElFinderOpenCommand).Assembly);
});
```

### In Controller

```csharp
public class ElFinderConnectorController : Controller
{
    private readonly IMediator mediator;

    public ElFinderConnectorController(IMediator mediator)
    {
        this.mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Connector()
    {
        var cmd = GetParam("cmd");

        var response = cmd switch
        {
            "open" => await mediator.Send(new ElFinderOpenCommand { /* ... */ }),
            "mkdir" => await mediator.Send(new ElFinderMkdirCommand { /* ... */ }),
            // ... other commands
            _ => ElFinderErrorFactory.CreateError(ElFinderErrorKeys.UnknownCommand)
        };

        return Json(response);
    }
}
```

---

## Phase 2 Action Items

### To Be Completed

- [ ] Design finalized and reviewed
- [ ] Command/handler interfaces defined
- [ ] DTO classes created (all 14+ response types)
- [ ] Storage adapter interface and implementation sketched
- [ ] Error handling strategy finalized
- [ ] Dependency injection approach confirmed
- [ ] Testing strategy documented
- [ ] This design doc completed

### Transition to Phase 3

Once Phase 2 is complete:
- Begin core command implementation
- Start with `open`, `ls`, `mkdir`, `rm` (most critical)
- Write unit tests for each
- Validate against spec with actual elFinder UI

---

**Next**: Await Phase 1 research completion, then proceed with Phase 2 design refinement and finalization.

---

# Phase 2: Design & Implementation Plan (elFinder Driver Architecture)

## 1. Overview

**Phase 2** will implement fixes to the current `ElFinderConnectorController` and, optionally, migrate the logic into the new `SkyCMS.Drivers.ElFinder` driver project as a long-term modernization. This document outlines the architectural decisions, CQRS command mapping, and concrete response DTOs to ensure exact elFinder protocol compliance.

---

## 2. Critical Fixes (Phase 2 – Immediate)

### 2.1 Fix: Delete Operations (`rm` Command)

**Current Issue**:
- `HandleRmAsync()` does not verify that items were actually deleted.
- If underlying storage calls fail, no error is reported to the client.
- elFinder removes deleted items from the UI based on the `removed` array, so returning false positives causes user confusion.

**Fix (apply to ElFinderConnectorController first)**:

```csharp
private async Task<IActionResult> HandleRmAsync()
{
    var targets = GetParams("targets[]");
    if (targets.Length == 0) targets = GetParams("targets");

    var removed = new List<string>();
    var errors = new List<string>();

    foreach (var t in targets)
    {
        var path = DecodeHash(t);
        if (path == null || !IsAllowedPath(path))
        {
            errors.Add($"Access denied: {t}");
            continue;
        }

        try
        {
            FileManagerEntry entry;
            try
            {
                entry = await storageContext.GetFileAsync(path);
            }
            catch
            {
                entry = null;
            }

            var isDir = entry?.IsDirectory ?? false;

            if (isDir)
                await storageContext.DeleteFolderAsync(path);
            else
                await storageContext.DeleteFileAsync(path);

            // VERIFY: Check that item was actually deleted
            try
            {
                var stillExists = await storageContext.GetFileAsync(path);
                // If we get here, deletion failed silently
                errors.Add($"Could not delete: {(isDir ? "directory" : "file")} still exists");
                continue;
            }
            catch (FileNotFoundException)
            {
                // Expected: item was deleted
                removed.Add(t);
            }
            catch (Exception ex)
            {
                errors.Add($"Error verifying deletion: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting item '{Path}'", path);
            errors.Add($"Deletion failed: {ex.Message}");
        }
    }

    var response = new Dictionary<string, object> { ["removed"] = removed };
    if (errors.Count > 0)
    {
        response["warning"] = errors;
    }

    return Json(response);
}
```

**Test**:
```csharp
[Test]
public async Task TestRmVerifiesDeletion()
{
    // Create a test file
    var filePath = "/pub/test.txt";
    await storageContext.AppendBlob(new MemoryStream(Encoding.UTF8.GetBytes("test")), metadata);
    
    var hash = EncodeHash(filePath);
    var result = await controller.HandleRmAsync(new[] { hash });
    var json = JsonConvert.DeserializeObject<dynamic>(result);
    
    // Verify the file is in the removed list only if actually deleted
    Assert.That((json["removed"] as JArray)?.Count, Is.GreaterThan(0));
    
    // Verify file no longer exists
    Assert.ThrowsAsync<FileNotFoundException>(
        () => storageContext.GetFileAsync(filePath)
    );
}
```

---

### 2.2 Fix: Tree Navigation (`parents` Command)

**Current Issue**:
- The `HandleParentsAsync()` response may not include enough context for elFinder to rebuild the full navigable tree.
- Specifically, when navigating to a deep path, elFinder needs to expand all ancestor folders in the left navbar, which requires knowing all siblings at each level.
- Missing or incomplete sibling data causes the navbar path to collapse.

**Fix (apply to ElFinderConnectorController first)**:

```csharp
private async Task<IActionResult> HandleParentsAsync()
{
    var target = GetParam("target");
    var path = DecodeHash(target);

    if (path == null || !IsAllowedPath(path))
    {
        return Json(ElFinderError("errAccess"));
    }

    // Build the list of all ancestors (from root to target's parent)
    var ancestors = new List<string>();
    var current = path;

    while (!string.IsNullOrEmpty(current) && current.StartsWith(RootPath, StringComparison.Ordinal))
    {
        ancestors.Add(current);
        if (string.Equals(current, RootPath, StringComparison.Ordinal))
        {
            break;
        }

        var parent = GetParentPath(current);
        if (string.IsNullOrEmpty(parent) || parent == current)
        {
            break;
        }

        current = parent;
    }

    ancestors.Reverse();  // Start from root
    var tree = new List<object>();
    var seen = new HashSet<string>(StringComparer.Ordinal);

    // Include root volume
    var rootObject = SyntheticDirObject(RootPath, null, isRoot: true);
    var rootHash = ((Dictionary<string, object>)rootObject)["hash"].ToString();
    tree.Add(rootObject);
    seen.Add(rootHash);

    // For each ancestor level, include all sibling directories
    foreach (var ancestor in ancestors)
    {
        var isRoot = string.Equals(ancestor, RootPath, StringComparison.Ordinal);
        if (isRoot) continue;  // Already added root

        var parent = GetParentPath(ancestor);

        try
        {
            var items = await GetEntriesForPathAsync(parent);
            var parentHash = EncodeHash(parent);

            // Add all subdirectories of this ancestor's parent (siblings of ancestor + ancestor itself)
            foreach (var item in items.Where(e => e.IsDirectory))
            {
                var itemObject = await ToElFinderObjectAsync(item, parentHash);
                var itemDict = itemObject as Dictionary<string, object>;
                if (itemDict != null && itemDict.ContainsKey("hash"))
                {
                    var itemHash = itemDict["hash"].ToString();
                    if (!seen.Contains(itemHash))
                    {
                        tree.Add(itemObject);
                        seen.Add(itemHash);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Warning: Could not fetch siblings for ancestor '{Ancestor}'", ancestor);
            // Best-effort: continue walking up even if this level fails
        }
    }

    // IMPORTANT: Also add the immediate children of the target path itself
    // so the UI can display the folder contents
    try
    {
        var targetItems = await GetEntriesForPathAsync(path);
        var targetHash = EncodeHash(path);

        foreach (var item in targetItems.Where(e => e.IsDirectory))
        {
            var itemObject = await ToElFinderObjectAsync(item, targetHash);
            var itemDict = itemObject as Dictionary<string, object>;
            if (itemDict != null && itemDict.ContainsKey("hash"))
            {
                var itemHash = itemDict["hash"].ToString();
                if (!seen.Contains(itemHash))
                {
                    tree.Add(itemObject);
                    seen.Add(itemHash);
                }
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Warning: Could not fetch children of target path '{Path}'", path);
    }

    return Json(new { tree });
}
```

**Key changes**:
1. Ensures **all ancestors** are included (from root to target's parent).
2. For **each ancestor**, includes **all sibling directories** (by fetching from parent).
3. Also includes **immediate children of the target path** for full tree context.
4. Deduplicates entries to avoid redundant nodes.

**Test**:
```csharp
[Test]
public async Task TestParentsIncludesAllAncestorsAndSiblings()
{
    // Create a deep path: /pub/articles/10/content/drafts
    var deepPath = "/pub/articles/10/content/drafts";
    
    var response = await controller.HandleParentsAsync(EncodeHash(deepPath));
    var json = JsonConvert.DeserializeObject<dynamic>(response);
    var tree = json["tree"] as JArray;

    // Verify root is present
    var root = tree.FirstOrDefault(o => o["name"].ToString() == "pub" && o["volumeid"] != null);
    Assert.That(root, Is.Not.Null);

    // Verify all ancestors are represented
    var articleNode = tree.FirstOrDefault(o => o["name"].ToString() == "articles");
    Assert.That(articleNode, Is.Not.Null);
    Assert.That((string)articleNode["phash"], Is.EqualTo(EncodeHash("/pub")));

    var article10Node = tree.FirstOrDefault(o => o["name"].ToString() == "article 10");
    Assert.That(article10Node, Is.Not.Null);
    Assert.That((string)article10Node["phash"], Is.EqualTo(EncodeHash("/pub/articles")));

    // Verify siblings are present (other articles at same level as article 10)
    var siblingsCount = tree.Count(o => (string)o["phash"] == EncodeHash("/pub/articles"));
    Assert.That(siblingsCount, Is.GreaterThan(1), "Should include siblings of article 10");
}
```

---

## 3. CQRS Command Architecture (Phase 2+ Migration)

As the system evolves, migrate the connector logic into CQRS commands within the new driver project.

### 3.1 Command Structure

Each elFinder command maps to a CQRS Query or Command:

```csharp
namespace SkyCMS.Drivers.ElFinder.Commands
{
    // Example: Delete command
    public class DeleteItemsCommand : IRequest<DeleteItemsResponse>
    {
        public required IReadOnlyList<string> ItemHashes { get; init; }
    }

    public class DeleteItemsCommandHandler : IRequestHandler<DeleteItemsCommand, DeleteItemsResponse>
    {
        private readonly IElFinderStorageAdapter storage;
        private readonly ILogger<DeleteItemsCommandHandler> logger;

        public async Task<DeleteItemsResponse> Handle(DeleteItemsCommand request, CancellationToken ct)
        {
            var removed = new List<string>();
            var warnings = new List<string>();

            foreach (var hash in request.ItemHashes)
            {
                try
                {
                    var path = ElFinderHash.Decode(hash);
                    if (!IsAllowedPath(path))
                    {
                        warnings.Add($"Access denied: {hash}");
                        continue;
                    }

                    // Delete and verify
                    var existed = await storage.DeleteAsync(path);
                    if (existed)
                        removed.Add(hash);
                    else
                        warnings.Add($"Item not found: {hash}");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error deleting {Hash}", hash);
                    warnings.Add($"Deletion error: {ex.Message}");
                }
            }

            return new DeleteItemsResponse 
            { 
                Removed = removed, 
                Warnings = warnings.Count > 0 ? warnings : null 
            };
        }
    }
}
```

### 3.2 Query Structure (Example: ListDirectory)

```csharp
namespace SkyCMS.Drivers.ElFinder.Commands
{
    public class ListDirectoryQuery : IRequest<ListDirectoryResponse>
    {
        public required string DirectoryHash { get; init; }
        public bool IncludeRoot { get; init; } = true;
    }

    public class ListDirectoryQueryHandler : IRequestHandler<ListDirectoryQuery, ListDirectoryResponse>
    {
        private readonly IElFinderStorageAdapter storage;
        private readonly ElFinderPathResolver pathResolver;

        public async Task<ListDirectoryResponse> Handle(ListDirectoryQuery request, CancellationToken ct)
        {
            var path = ElFinderHash.Decode(request.DirectoryHash);
            if (!IsAllowedPath(path))
                throw new UnauthorizedAccessException();

            var entries = await storage.ListAsync(path);
            var fileObjects = new List<ElFinderFileObject>();

            foreach (var entry in entries)
            {
                fileObjects.Add(MapToElFinderObject(entry, request.DirectoryHash));
            }

            var response = new ListDirectoryResponse 
            { 
                Files = fileObjects 
            };

            if (request.IncludeRoot)
            {
                response.Files.Insert(0, CreateRootObject());
            }

            return response;
        }
    }
}
```

---

## 4. Response DTOs (Exact elFinder Protocol Match)

Use `[JsonPropertyName]` attributes to ensure exact JSON field names:

```csharp
namespace SkyCMS.Drivers.ElFinder.Responses
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Standard elFinder file/directory object shape (REQUIRED for all responses).
    /// </summary>
    public class ElFinderFileObject
    {
        [JsonPropertyName("hash")]
        public required string Hash { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("size")]
        public required long Size { get; init; }

        [JsonPropertyName("mime")]
        public required string Mime { get; init; }  // "directory" or MIME type

        [JsonPropertyName("ts")]
        public required long Timestamp { get; init; }  // UNIX timestamp

        [JsonPropertyName("read")]
        public required int Read { get; init; } = 1;

        [JsonPropertyName("write")]
        public required int Write { get; init; } = 1;

        [JsonPropertyName("locked")]
        public required int Locked { get; init; } = 0;

        // Optional fields
        [JsonPropertyName("phash")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ParentHash { get; init; }

        [JsonPropertyName("dirs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int? HasDirectories { get; init; }

        [JsonPropertyName("volumeid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? VolumeId { get; init; }

        [JsonPropertyName("url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Url { get; init; }

        [JsonPropertyName("tmb")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Thumbnail { get; init; }
    }

    public class DeleteItemsResponse
    {
        [JsonPropertyName("removed")]
        public required IReadOnlyList<string> Removed { get; init; }

        [JsonPropertyName("warning")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? Warnings { get; init; }
    }

    public class GetParentsResponse
    {
        [JsonPropertyName("tree")]
        public required IReadOnlyList<ElFinderFileObject> Tree { get; init; }
    }

    public class OpenDirectoryResponse
    {
        [JsonPropertyName("cwd")]
        public required ElFinderFileObject CurrentWorkingDirectory { get; init; }

        [JsonPropertyName("files")]
        public required IReadOnlyList<ElFinderFileObject> Files { get; init; }

        [JsonPropertyName("api")]
        public required string ApiVersion { get; init; } = "2.1";

        [JsonPropertyName("uplMaxSize")]
        public required string UploadMaxSize { get; init; }

        [JsonPropertyName("options")]
        public required DirectoryOptions Options { get; init; }
    }

    public class DirectoryOptions
    {
        [JsonPropertyName("path")]
        public required string Path { get; init; }

        [JsonPropertyName("url")]
        public required string Url { get; init; }

        [JsonPropertyName("tmbUrl")]
        public required string ThumbnailUrl { get; init; }

        [JsonPropertyName("separator")]
        public required string Separator { get; init; } = "/");

        [JsonPropertyName("copyOverwrite")]
        public required int CopyOverwrite { get; init; } = 1;

        [JsonPropertyName("uploadOverwrite")]
        public required int UploadOverwrite { get; init; } = 1;

        [JsonPropertyName("disabled")]
        public IReadOnlyList<string> DisabledCommands { get; init; } = new[] { "chmod", "zipdl", "archive", "extract" };
    }

    public class ErrorResponse
    {
        [JsonPropertyName("error")]
        public required string ErrorCode { get; init; }
    }
}
```

---

## 5. Storage Adapter Interface

Define a clean abstraction for storage operations:

```csharp
namespace SkyCMS.Drivers.ElFinder.Adapters
{
    /// <summary>
    /// Storage adapter for elFinder commands. Encapsulates all file system operations.
    /// Implementations may use IStorageContext, Azure Blob Storage, Local Filesystem, etc.
    /// </summary>
    public interface IElFinderStorageAdapter
    {
        /// <summary>
        /// Get file/directory metadata.
        /// </summary>
        Task<FileEntry> GetFileAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// List files and directories in a path.
        /// </summary>
        Task<IReadOnlyList<FileEntry>> ListAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// Create a directory.
        /// </summary>
        Task<FileEntry> CreateDirectoryAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// Delete a file or directory (recursively for directories).
        /// Returns true if item existed and was deleted, false if not found.
        /// Throws exception on permission/storage errors.
        /// </summary>
        Task<bool> DeleteAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// Move or rename a file/directory.
        /// </summary>
        Task MoveAsync(string sourcePath, string destinationPath, CancellationToken ct = default);

        /// <summary>
        /// Copy a file or directory.
        /// </summary>
        Task CopyAsync(string sourcePath, string destinationPath, CancellationToken ct = default);

        /// <summary>
        /// Read file content (as UTF-8 string).
        /// </summary>
        Task<string> ReadTextAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// Write file content.
        /// </summary>
        Task WriteTextAsync(string path, string content, CancellationToken ct = default);

        /// <summary>
        /// Upload file from stream.
        /// </summary>
        Task UploadAsync(string path, Stream content, string contentType, CancellationToken ct = default);
    }

    public class FileEntry
    {
        public required string Path { get; init; }
        public required string Name { get; init; }
        public required bool IsDirectory { get; init; }
        public long Size { get; init; }
        public DateTime Modified { get; init; }
        public bool HasSubdirectories { get; init; }
    }
}
```

---

## 6. Dependency Injection (DI) Registration

Register CQRS handlers and adapters:

```csharp
// In Program.cs or DI module
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DeleteItemsCommand>());

// Register storage adapter (bind to SkyCMS IStorageContext)
services.AddScoped<IElFinderStorageAdapter>(provider =>
    new SkyCmsStorageAdapter(
        provider.GetRequiredService<IStorageContext>(),
        provider.GetRequiredService<ILogger<SkyCmsStorageAdapter>>()
    )
);
```

---

## 7. Testing Strategy

### 7.1 Unit Tests (Command/Query Handlers)

```csharp
namespace SkyCMS.Drivers.ElFinder.Tests
{
    using Xunit;
    using Moq;
    using MediatR;

    public class DeleteItemsCommandHandlerTests
    {
        [Fact]
        public async Task Handle_DeletesExistingFile_ReturnsHashInRemovedList()
        {
            // Arrange
            var mockAdapter = new Mock<IElFinderStorageAdapter>();
            mockAdapter
                .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);  // File existed and was deleted

            var handler = new DeleteItemsCommandHandler(mockAdapter.Object, Mock.Of<ILogger<DeleteItemsCommandHandler>>());
            var command = new DeleteItemsCommand 
            { 
                ItemHashes = new[] { "l1_aW1hZ2UuanBn" } 
            };

            // Act
            var response = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Single(response.Removed);
            Assert.Equal("l1_aW1hZ2UuanBn", response.Removed[0]);
            Assert.Null(response.Warnings);
        }

        [Fact]
        public async Task Handle_FileNotFound_ReturnsWarning()
        {
            var mockAdapter = new Mock<IElFinderStorageAdapter>();
            mockAdapter
                .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);  // File did not exist

            var handler = new DeleteItemsCommandHandler(mockAdapter.Object, Mock.Of<ILogger<DeleteItemsCommandHandler>>());
            var command = new DeleteItemsCommand 
            { 
                ItemHashes = new[] { "l1_dm9vYmFy" } 
            };

            // Act
            var response = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Empty(response.Removed);
            Assert.Single(response.Warnings);
            Assert.Contains("Item not found", response.Warnings[0]);
        }
    }
}
```

### 7.2 Integration Tests

Test the full connector pipeline with a real storage context:

```csharp
[Test]
public async Task TestParentsCommandWithRealStorage()
{
    // Setup: Create a deep path hierarchy in test storage
    var testStorage = new TestStorageContext();
    await testStorage.CreateDirectoryAsync("/pub/articles/1/content/drafts");

    var handler = new GetParentsQueryHandler(testStorage, pathResolver);
    var query = new GetParentsQuery { DirectoryHash = ElFinderHash.Encode("/pub/articles/1/content/drafts") };

    // Act
    var response = await handler.Handle(query, CancellationToken.None);

    // Assert: Verify tree includes all ancestors
    var treeHashes = response.Tree.Select(t => t.Hash).ToList();
    Assert.Contains(ElFinderHash.Encode("/pub"), treeHashes);
    Assert.Contains(ElFinderHash.Encode("/pub/articles"), treeHashes);
    Assert.Contains(ElFinderHash.Encode("/pub/articles/1"), treeHashes);

    // Verify phash is correct for each ancestor
    var articlesNode = response.Tree.First(t => t.Name == "articles");
    Assert.Equal(ElFinderHash.Encode("/pub"), articlesNode.ParentHash);
}
```

---

## 8. Rollout Plan (Phase 2 → Phase 3)

1. **Apply immediate fixes** to `ElFinderConnectorController` (delete verification, parents breadth).
2. **Test thoroughly** with integration tests to verify tree navigation and deletion.
3. **Migrate to CQRS** (optional): Move handler logic to `SkyCMS.Drivers.ElFinder` project (Phase 3+).
4. **Maintain backward compatibility**: Keep controller as a facade until full migration is complete.
5. **Monitor user feedback**: Track elFinder UI behavior post-deployment.

---

## 9. Acceptance Criteria (Phase 2 Complete)

- [ ] Delete operations show visual feedback; deleted items do not reappear.
- [ ] Folder tree breadcrumb remains stable when navigating deeply.
- [ ] All ancestors expand correctly in left navbar.
- [ ] Tree siblings are visible at each level (no collapsed paths).
- [ ] Integration tests pass for all 15 elFinder commands.
- [ ] No regressions in existing file manager functionality.
- [ ] Error responses are clear and actionable (no generic 500 errors).

---

## 10. Documentation to Maintain

- Keep `API_SPEC.md` updated with any protocol changes.
- Maintain command-by-command test coverage documentation.
- Document any custom extensions or deviations from the official spec.

