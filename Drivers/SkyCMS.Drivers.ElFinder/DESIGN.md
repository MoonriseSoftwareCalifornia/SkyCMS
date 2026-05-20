# SkyCMS.Drivers.ElFinder — Design Document

**Document Purpose**: Capture architecture, design decisions, and implementation notes for the elFinder driver.

**Status**: Implementation Complete (Phases 0–5)  
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

**Location**: `SkyCMS.Drivers.ElFinder/Commands/` and `SkyCMS.Drivers.ElFinder/Handlers/`

All commands implement `IElFinderRequest`, which extends `IRequest<IElFinderResponse>` (MediatR). Each command carries its `Command` name string and an optional `VolumeId`.

Example:

```csharp
// Commands/OpenCommand.cs
public sealed class OpenCommand : IElFinderRequest
{
    public string? Target { get; }
    public bool Init { get; }
    public string VolumeId { get; }
    public bool Tree { get; }
    public string? BlobPublicUrl { get; }
    public string? TmbUrl { get; }
    public string? RootPath { get; }
    public string Command => "open";
}

// Handlers/OpenCommandHandler.cs
public class OpenCommandHandler : IRequestHandler<OpenCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public async Task<IElFinderResponse> Handle(
        OpenCommand request,
        CancellationToken cancellationToken)
    {
        // ...
    }
}
```

#### 2. DTOs (Data Transfer Objects)

**Location**: `SkyCMS.Drivers.ElFinder/Responses/`

All response types implement `IElFinderResponse` (marker interface). `ElFinderErrorResponse` is the shared error type.

```csharp
// Responses/IElFinderResponse.cs
public interface IElFinderResponse { }

// Responses/OpenResponse.cs
public sealed class OpenResponse : IElFinderResponse
{
    [JsonPropertyName("cwd")]
    public ElFinderObject Cwd { get; set; }

    [JsonPropertyName("files")]
    public List<ElFinderObject> Files { get; set; } = new();

    [JsonPropertyName("api")]
    public string Api { get; set; } = "2.1049";

    [JsonPropertyName("uplMaxSize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string UplMaxSize { get; set; }

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ElFinderOptions Options { get; set; }

    [JsonPropertyName("netDrivers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object> NetDrivers { get; set; }
}

// ElFinderObject is defined in OpenResponse.cs and reused across all responses.
// Key fields: hash, name, size, mime, ts, read, write, locked, phash, dirs, volumeid, url, tmb.
```

`ElFinderOptions` is a typed class (not a dictionary) in `Responses/ElFinderOptions.cs`. It includes:
`url`, `tmbUrl`, `path`, `separator`, `archivers`, `disabled`, `copyOverwrite`, `trashHash`, `uploadMaxConn`, `uploadMaxSize`.

#### 3. Storage Adapter

**Location**: `SkyCMS.Drivers.ElFinder/Adapters/`

Abstraction insulating driver from storage implementation:

```csharp
public interface IElFinderStorageAdapter
{
    string EncodePath(string path);
    string? DecodePath(string hash);
    Task<List<FileManagerEntry>> GetEntriesAsync(string path, CancellationToken ct = default);
    Task<FileManagerEntry?> GetEntryAsync(string path, CancellationToken ct = default);
    Task<FileManagerEntry?> CreateFolderAsync(string path, CancellationToken ct = default);
    Task<FileManagerEntry?> CreateFileAsync(string path, CancellationToken ct = default);
    Task<FileManagerEntry?> RenameAsync(string sourcePath, string destinationPath, CancellationToken ct = default);
    Task<FileManagerEntry?> RenameAsync(FileManagerEntry entry, string destinationPath, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(FileManagerEntry entry, CancellationToken ct = default);
    Task<FileManagerEntry?> MoveAsync(string sourcePath, string destinationPath, CancellationToken ct = default);
    Task<FileManagerEntry?> MoveAsync(FileManagerEntry entry, string destinationPath, CancellationToken ct = default);
    Task<FileManagerEntry?> CopyAsync(string sourcePath, string destinationPath, CancellationToken ct = default);
    Task<Stream?> GetReadStreamAsync(string path, CancellationToken ct = default);
    Task<FileManagerEntry?> UploadFileAsync(string path, Stream stream, string mimeType, CancellationToken ct = default);
    Task<List<FileManagerEntry>> GetAncestorsAsync(string path, CancellationToken ct = default);
    Task<bool> IsAccessibleAsync(string path, CancellationToken ct = default);
    Task<long> GetSizeAsync(string path, CancellationToken ct = default);
    Task<List<(FileManagerEntry Entry, string FullPath)>> SearchAsync(string query, string rootPath, CancellationToken ct = default);
}
```

`FileManagerEntry` is the SkyCMS storage model from `Cosmos.BlobService`. The concrete implementation is `ElFinderStorageAdapter`, which also depends on `IPathNormalizer` and `IPathValidator` (both from `Cosmos.BlobService`).

#### 4. Path Encoding

**Location**: `SkyCMS.Drivers.ElFinder/ElFinderHashEncoder.cs`

Encodes and decodes elFinder path hashes using URL-safe Base64. Format: `<volumeId><base64url(path)>`. Default volume ID is `"l1_"`.

```csharp
ElFinderHashEncoder.Encode("pub/images");  // → "l1_cHViL2ltYWdlcw"
ElFinderHashEncoder.Decode("l1_cHViL2ltYWdlcw");  // → "pub/images"
```

#### 5. MIME Helper

**Location**: `SkyCMS.Drivers.ElFinder/Helpers/ElFinderMimeHelper.cs`

Internal static helper that resolves MIME types by file extension via the `MimeTypes` NuGet package. Falls back to `application/octet-stream` for unknown extensions.

---

## Command Mapping

### Commands Overview

| elFinder Cmd | Command Class | Handler | Response Class |
|--------------|---------------|---------|----------------|
| `open` | `OpenCommand` | `OpenCommandHandler` | `OpenResponse` |
| `tree` | `TreeCommand` | `TreeCommandHandler` | `TreeResponse` |
| `ls` | `LsCommand` | `LsCommandHandler` | `LsResponse` |
| `mkdir` | `MkdirCommand` | `MkdirCommandHandler` | `MkdirResponse` |
| `mkfile` | `MkfileCommand` | `MkfileCommandHandler` | `MkfileResponse` |
| `rm` | `RmCommand` | `RmCommandHandler` | `RmResponse` |
| `rename` | `RenameCommand` | `RenameCommandHandler` | `RenameResponse` |
| `upload` | `UploadCommand` | `UploadCommandHandler` | `UploadResponse` |
| `paste` | `PasteCommand` | `PasteCommandHandler` | `PasteResponse` |
| `get` | `GetCommand` | `GetCommandHandler` | `GetResponse` |
| `put` | `PutCommand` | `PutCommandHandler` | `PutResponse` |
| `tmb` | `TmbCommand` | `TmbCommandHandler` | `TmbResponse` |
| `info` | `InfoCommand` | `InfoCommandHandler` | `InfoResponse` |
| `size` | `SizeCommand` | `SizeCommandHandler` | `SizeResponse` |
| `parents` | `ParentsCommand` | `ParentsCommandHandler` | `ParentsResponse` |
| `duplicate` | `DuplicateCommand` | `DuplicateCommandHandler` | `DuplicateResponse` |
| `dim` | `DimCommand` | `DimCommandHandler` | `DimResponse` |
| `file` | `FileCommand` | `FileCommandHandler` | `FileResponse` |
| `url` | `UrlCommand` | `UrlCommandHandler` | `UrlResponse` |
| `resize` | `ResizeCommand` | `ResizeCommandHandler` | `ResizeResponse` |
| `search` | `SearchCommand` | `SearchCommandHandler` | `SearchResponse` |

All command and response classes live in the `SkyCMS.Drivers.ElFinder.Commands` namespace.

---

## DTO Design

### Response Classes

All response types implement `IElFinderResponse` (marker interface in `Responses/IElFinderResponse.cs`). Response classes use simple names without an `ElFinder` prefix where the naming is already contextually clear within the namespace.

**Error response** (`ElFinderErrorResponse` in `IElFinderResponse.cs`):
```csharp
public sealed class ElFinderErrorResponse : IElFinderResponse
{
    // Serializes as: {"error":["errCode"]} or {"error":["errCode","message"]}
    [JsonPropertyName("error")]
    public List<string> Error { get; }  // token array per elFinder 2.1 spec

    public string ErrorCode { get; }    // [JsonIgnore]
    public string ErrorMessage { get; } // [JsonIgnore]
}
```
Note: `error` serializes as a **JSON array of strings** (elFinder 2.1 protocol), not a plain string.

**Open response** (`OpenResponse` in `Responses/OpenResponse.cs`):
```csharp
public sealed class OpenResponse : IElFinderResponse
{
    [JsonPropertyName("cwd")]
    public ElFinderObject Cwd { get; set; }

    [JsonPropertyName("files")]
    public List<ElFinderObject> Files { get; set; } = new();

    [JsonPropertyName("api")]
    public string Api { get; set; } = "2.1049";

    [JsonPropertyName("uplMaxSize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string UplMaxSize { get; set; }

    [JsonPropertyName("volumeid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string VolumeId { get; set; }

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ElFinderOptions Options { get; set; }

    [JsonPropertyName("netDrivers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object> NetDrivers { get; set; }
}
```

**File/directory object** (`ElFinderObject`, defined in `Responses/OpenResponse.cs`, reused across all response types):
```csharp
public sealed class ElFinderObject
{
    [JsonPropertyName("hash")]    public string Hash { get; set; }
    [JsonPropertyName("volumeid")] public string VolumeId { get; set; }  // root only
    [JsonPropertyName("phash")]   public string Phash { get; set; }      // omit for root
    [JsonPropertyName("name")]    public string Name { get; set; }
    [JsonPropertyName("mime")]    public string Mime { get; set; }        // "directory" or MIME type
    [JsonPropertyName("ts")]      public long Ts { get; set; }            // UNIX timestamp
    [JsonPropertyName("read")]    public int Read { get; set; } = 1;
    [JsonPropertyName("write")]   public int Write { get; set; } = 1;
    [JsonPropertyName("locked")]  public int Locked { get; set; } = 0;
    [JsonPropertyName("size")]    public long Size { get; set; }
    [JsonPropertyName("dirs")]    public int? Dirs { get; set; }          // 1 if has subdirs
    [JsonPropertyName("isroot")]  public int? Isroot { get; set; }        // 1 for volume root
    [JsonPropertyName("url")]     public string Url { get; set; }
    [JsonPropertyName("tmb")]     public string Tmb { get; set; }
}
```

**Volume options** (`ElFinderOptions` in `Responses/ElFinderOptions.cs`):
Typed class (not a dictionary). Fields: `Url`, `TmbUrl`, `Path`, `Separator` (= `"/"`), `Archivers` (`ElFinderArchivers`), `Disabled` (List<string> — includes "callback", "chmod", "editor", "netmount", "ping", "extract", "archive"), `CopyOverwrite` (= 1), `TrashHash` (= ""), `UploadMaxConn` (= -1), `UploadMaxSize` (nullable long).

### Null Handling Strategy

- Use `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` for optional fields.
- This ensures elFinder does not see null values for fields it does not expect.
- Produces cleaner JSON that matches the protocol spec exactly.

---

## Storage Adapter Interface

The `IElFinderStorageAdapter` interface (in `Adapters/IElFinderStorageAdapter.cs`) bridges the elFinder protocol layer to SkyCMS storage. It also owns path encoding/decoding so handlers never need to call `ElFinderHashEncoder` directly.

```csharp
public interface IElFinderStorageAdapter
{
    // Hash encoding
    string EncodePath(string path);
    string? DecodePath(string hash);

    // Directory listing / entry retrieval
    Task<List<FileManagerEntry>> GetEntriesAsync(string path, CancellationToken ct = default);
    Task<FileManagerEntry?> GetEntryAsync(string path, CancellationToken ct = default);

    // Create
    Task<FileManagerEntry?> CreateFolderAsync(string path, CancellationToken ct = default);
    Task<FileManagerEntry?> CreateFileAsync(string path, CancellationToken ct = default);

    // Rename / Move / Copy / Delete (overloads accept pre-resolved entry to avoid redundant lookups)
    Task<FileManagerEntry?> RenameAsync(string sourcePath, string destinationPath, CancellationToken ct = default);
    Task<FileManagerEntry?> RenameAsync(FileManagerEntry entry, string destinationPath, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(FileManagerEntry entry, CancellationToken ct = default);
    Task<FileManagerEntry?> MoveAsync(string sourcePath, string destinationPath, CancellationToken ct = default);
    Task<FileManagerEntry?> MoveAsync(FileManagerEntry entry, string destinationPath, CancellationToken ct = default);
    Task<FileManagerEntry?> CopyAsync(string sourcePath, string destinationPath, CancellationToken ct = default);

    // File content
    Task<Stream?> GetReadStreamAsync(string path, CancellationToken ct = default);
    Task<FileManagerEntry?> UploadFileAsync(string path, Stream stream, string mimeType, CancellationToken ct = default);

    // Tree / navigation
    Task<List<FileManagerEntry>> GetAncestorsAsync(string path, CancellationToken ct = default);

    // Misc
    Task<bool> IsAccessibleAsync(string path, CancellationToken ct = default);
    Task<long> GetSizeAsync(string path, CancellationToken ct = default);
    Task<List<(FileManagerEntry Entry, string FullPath)>> SearchAsync(string query, string rootPath, CancellationToken ct = default);
}
```

`FileManagerEntry` is the SkyCMS storage model from `Cosmos.BlobService` — it is the shared data model throughout the driver.

**Concrete implementation**: `ElFinderStorageAdapter` (in `Adapters/ElFinderStorageAdapter.cs`).

Constructor signature:
```csharp
public ElFinderStorageAdapter(
    IStorageContext storageContext,
    IPathNormalizer pathNormalizer,
    IPathValidator pathValidator)
```

Both `IPathNormalizer` and `IPathValidator` come from `Cosmos.BlobService` and are registered as singletons by `AddElFinderDriver()`.

---

## Error Handling Strategy

### Error Keys

All command handlers return `ElFinderErrorResponse` on failure. Standard elFinder error codes are used as string literals:

| Code | Meaning |
|------|---------|
| `errUnknownCmd` | Unknown command |
| `errAccess` | Access denied / path not allowed |
| `errOpen` | Cannot open directory |
| `errNotFound` | Item not found |
| `errInvName` | Invalid name |
| `errUploadFile` | Cannot upload |
| `errUploadNoFiles` | No files in upload request |
| `errReplace` | Cannot replace |
| `errRm` | Cannot delete |

`ElFinderErrorResponse` is constructed via its constructor: `new ElFinderErrorResponse(errorCode, optionalMessage)`. The `Error` property serializes as a JSON array per the elFinder 2.1 protocol: `{"error": ["errCode"]}` or `{"error": ["errCode", "message"]}`.

The controller uses `ElFinderErrorResponse.UnknownCommand()` static factory for unrecognized commands.

### Handler Pattern

Handlers return `IElFinderResponse` — either a typed success response (e.g., `OpenResponse`) or `ElFinderErrorResponse`. Common patterns:

- Invalid/inaccessible paths → `new ElFinderErrorResponse("errAccess")`
- Not found → `new ElFinderErrorResponse("errNotFound")`

---

## Testing Strategy

Tests live in the matching test project. Command handler tests mock `IElFinderStorageAdapter`. Use `FileManagerEntry` (not any fictional `ElFinderStorageEntry`) in test setups.

Example test pattern:
```csharp
[Fact]
public async Task OpenCommandHandler_WithValidTarget_ReturnsOpenResponse()
{
    var adapter = new Mock<IElFinderStorageAdapter>();
    adapter.Setup(x => x.GetEntryAsync("/pub", default))
        .ReturnsAsync(new FileManagerEntry { Path = "/pub", Name = "pub", IsDirectory = true });
    adapter.Setup(x => x.GetEntriesAsync("/pub", default))
        .ReturnsAsync(new List<FileManagerEntry>());

    var handler = new OpenCommandHandler(adapter.Object);
    var command = new OpenCommand(target: null, init: true, rootPath: "/pub");

    var response = await handler.Handle(command, CancellationToken.None);

    var openResponse = Assert.IsType<OpenResponse>(response);
    Assert.NotNull(openResponse.Cwd);
    Assert.NotNull(openResponse.Api);
}
```

---

## Dependency Injection

### Service Registration

Use the provided extension method:

```csharp
// In Program.cs or a DI module
services.AddElFinderDriver();
```

`AddElFinderDriver()` (in `ElFinderServiceCollectionExtensions.cs`) registers:
1. MediatR from the driver assembly (all command handlers auto-discovered)
2. `IElFinderStorageAdapter` → `ElFinderStorageAdapter` (scoped, via `TryAddScoped`)
3. `IPathNormalizer` → `PathNormalizer` (singleton)
4. `IPathValidator` → `PathValidator` (singleton)

The `TryAddScoped` pattern means tests or the host app can override `IElFinderStorageAdapter` with a custom implementation by registering before calling `AddElFinderDriver()`.

### In Controller

```csharp
public class FileManagerController : Controller
{
    private readonly IMediator _mediator;

    public FileManagerController(IMediator mediator, ...)
    {
        _mediator = mediator;
    }

    [HttpGet, HttpPost]
    public async Task<IActionResult> Connector(...)
    {
        IElFinderResponse response = cmd switch
        {
            "open"      => await _mediator.Send(new OpenCommand(...)),
            "mkdir"     => await _mediator.Send(new MkdirCommand { ... }),
            "mkfile"    => await _mediator.Send(new MkfileCommand { ... }),
            "rm"        => await _mediator.Send(new RmCommand { ... }),
            "rename"    => await _mediator.Send(new RenameCommand { ... }),
            "upload"    => await _mediator.Send(new UploadCommand { ... }),
            "paste"     => await _mediator.Send(new PasteCommand { ... }),
            "get"       => await _mediator.Send(new GetCommand { ... }),
            "put"       => await _mediator.Send(new PutCommand { ... }),
            "tree"      => await _mediator.Send(new TreeCommand { ... }),
            "ls"        => await _mediator.Send(new LsCommand { ... }),
            "parents"   => await _mediator.Send(new ParentsCommand { ... }),
            "tmb"       => await _mediator.Send(new TmbCommand { ... }),
            "info"      => await _mediator.Send(new InfoCommand { ... }),
            "size"      => await _mediator.Send(new SizeCommand { ... }),
            "duplicate" => await _mediator.Send(new DuplicateCommand { ... }),
            "dim"       => await _mediator.Send(new DimCommand { ... }),
            "file"      => await _mediator.Send(new FileCommand { ... }),
            "url"       => await _mediator.Send(new UrlCommand { ... }),
            "resize"    => await _mediator.Send(new ResizeCommand { ... }),
            "search"    => await _mediator.Send(new SearchCommand { ... }),
            _           => ElFinderErrorResponse.UnknownCommand(),
        };

        return JsonCqrs(response);  // Must use System.Text.Json (for [JsonPropertyName] attrs)
    }
}
```

**Important**: The controller must use `System.Text.Json` serialization (not Newtonsoft.Json) so that `[JsonPropertyName]` attributes on response classes are respected.
