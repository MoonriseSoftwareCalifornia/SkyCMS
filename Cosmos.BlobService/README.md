# Cosmos.BlobService — Developer README

Purpose
- Blob storage helpers and abstractions for storing binary assets in Cosmos/Blob-backed stores.

Quick start
```powershell
dotnet build SkyCMS.sln
```

Where to look
- Storage context and helpers: `Cosmos.BlobService/StorageContext.cs` and related classes.

Notes
- Review storage configuration and container/partition strategies before making changes.
README.md
# Cosmos.BlobService - Sky CMS Azure Storage Provider

**Version:** 9.2.0.2  
**License:** MIT  
**Repository:** [https://github.com/CWALabs/Cosmos.BlobService](https://github.com/CWALabs/Cosmos.BlobService)

## Overview

Cosmos.BlobService is a comprehensive multi-cloud blob storage abstraction layer that provides a unified interface for managing files across different cloud storage providers. It supports Azure Blob Storage, Amazon S3, and Cloudflare R2 (S3-compatible), allowing applications to seamlessly switch between providers or use multiple providers simultaneously.

This library is part of the [Sky CMS ecosystem](https://sky-cms.com) and is built on .NET 9.0.

## Features

### Multi-Cloud Support

- **Azure Blob Storage**: Full support with managed identity authentication and Azure Files Share
- **Amazon S3**: Complete AWS S3 integration with chunked upload support
- **Cloudflare R2**: Full S3-compatible support with custom endpoint configuration
- **Unified Interface**: Single API for all storage operations regardless of provider
- **Runtime Provider Selection**: Dynamic configuration of storage providers based on connection strings

### File Management Operations

- **Upload/Download**: Single and chunked file uploads with metadata tracking
- **Copy/Move/Rename**: File and folder operations across cloud providers
- **Delete**: File and folder deletion with recursive support
- **Metadata Management**: Comprehensive file metadata handling including image dimensions
- **Directory Operations**: Create, list, and manage virtual directory structures
- **Stream Operations**: Efficient streaming for large file operations

### Advanced Features

- **Chunked Uploads**: Support for large file uploads with progress tracking (AWS multi-part and Azure append blobs)
- **Static Website Hosting**: Enable/disable Azure static website hosting programmatically
- **Data Protection**: Integration with ASP.NET Core data protection
- **Multi-Tenant Support**: Support for single and multi-tenant configurations with dynamic configuration
- **Memory Caching**: Efficient caching for improved performance (especially for S3 multi-part uploads)
- **Metadata Operations**: Upsert, delete, and retrieve custom metadata for blobs

## Architecture

### Core Components

#### StorageContext

The main service class that provides the unified interface to all storage operations. It automatically selects the appropriate driver based on configuration and supports both single-tenant and multi-tenant scenarios.

#### ICosmosStorage Interface

Defines the contract for all storage drivers, ensuring consistent behavior across different cloud providers. Key methods include:
- Blob existence checks
- File and folder CRUD operations
- Streaming operations
- Metadata management
- Storage consumption metrics

#### Storage Drivers

- **AzureStorage**: Implements Azure Blob Storage operations with support for both block and append blobs
- **AmazonStorage**: Implements Amazon S3 operations with multi-part upload support
- **AzureFileStorage**: Provides Azure Files Share support

### Driver Selection Pattern

The service uses automatic driver selection based on connection string format:
- Connection strings starting with `DefaultEndpointsProtocol=` → Azure Blob Storage
- Connection strings containing `AccountId=` → Cloudflare R2
- Connection strings containing `Bucket=` → Amazon S3

## Installation

### From Source

This package can be obtained by cloning the [Sky CMS GitHub repository](https://github.com/CWALabs/SkyCMS):

### NuGet Package

The package can be built and referenced directly from your project or published to your private NuGet feed.

## Dependencies

- **.NET 9.0**: Target framework
- **Azure.Storage.Blobs**: Azure Blob Storage SDK
- **Azure.Storage.Files.Shares** (v12.24.0): Azure Files Share SDK
- **Azure.Extensions.AspNetCore.DataProtection.Blobs** (v1.5.1): Data protection integration
- **Azure.Identity** (v1.17.0): Azure authentication
- **AWSSDK.S3** (v4.0.9): Amazon S3 SDK
- **Microsoft.Extensions.Caching.Memory**: Memory caching
- **StyleCop.Analyzers** (v1.1.118): Code style enforcement

## Configuration

### Connection String Precedence

The Blob Service looks for the following configuration keys (in order):

1. `ConnectionStrings:StorageConnectionString` (preferred)
2. `ConnectionStrings:AzureBlobStorageConnectionString` (fallback for legacy configurations)

### Azure Blob Storage

**Standard Connection:**

```json
{ "ConnectionStrings": {
	"StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=yourkey;EndpointSuffix=core.windows.net" }
}
```

**Managed Identity (DefaultAzureCredential):**

```json
{ "ConnectionStrings": {
	"StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=AccessToken;EndpointSuffix=core.windows.net" }
}
```

````````

> **Note:** Using `AccountKey=AccessToken` enables DefaultAzureCredential. Ensure your app identity has the appropriate Azure RBAC roles (e.g., Storage Blob Data Contributor).

### Amazon S3

```json
{ "ConnectionStrings": {
	"StorageConnectionString": "Bucket=your-bucket-name;Region=us-west-2;AccessKey=your-access-key;SecretKey=your-secret`

### Cloudflare R2

```json
{ "ConnectionStrings": {
	"StorageConnectionString": "Bucket=your-bucket-name;AccountId=your-account-id;AccessKey=your-access-key;SecretKey=your-secret;Endpoint=https://your-account-id.r2.cloudflarestorage.com" }
}

````````

When `MultiTenantEditor` is `true`, the service uses `IDynamicConfigurationProvider` to retrieve tenant-specific connection strings at runtime.

## Usage

```csharp
using Cosmos.BlobService;
using Azure.Identity;
```

### Service Registration

In your `Program.cs`:

```csharp
// Register storage context
builder.Services.AddCosmosStorageContext(builder.Configuration);

// Optional: Add data protection with blob storage
builder.Services.AddCosmosCmsDataProtection(builder.Configuration, new DefaultAzureCredential());
```

### Basic File Operations

```csharp
using Cosmos.BlobService; using Cosmos.BlobService.Models;

public class FileService { private readonly StorageContext _storageContext;

     public FileService(StorageContext storageContext)
     {
         _storageContext = storageContext;
     }

     // Upload a file
     public async Task<string> UploadFileAsync(IFormFile file, string directory = "")
     {
         var fileName = Path.GetFileName(file.FileName);
         var relativePath = Path.Combine(directory, fileName).Replace('\\', '/');
    
         var metadata = new FileUploadMetaData
         {
             FileName = fileName,
             RelativePath = relativePath,
             ContentType = file.ContentType,
             ChunkIndex = 0,
             TotalChunks = 1,
             TotalFileSize = file.Length,
             UploadUid = Guid.NewGuid().ToString()
         };

         using var stream = new MemoryStream();
         await file.CopyToAsync(stream);
    
         await _storageContext.AppendBlob(stream, metadata, "block");
    
         return relativePath;
     }

     // Download a file
     public async Task<Stream> DownloadFileAsync(string path)
     {
         return await _storageContext.GetStreamAsync(path);
     }

     // Check if file exists
     public async Task<bool> FileExistsAsync(string path)
     {
         return await _storageContext.BlobExistsAsync(path);
     }

     // Delete a file
     public void DeleteFile(string path)
     {
         _storageContext.DeleteFile(path);
     }

     // Copy a file
     public async Task CopyFileAsync(string source, string destination)
     {
         await _storageContext.CopyAsync(source, destination);
     }

     // Move/rename a file
     public async Task MoveFileAsync(string source, string destination)
     {
         await _storageContext.MoveFileAsync(source, destination);
     }

     // List files and folders
     public async Task<List<FileManagerEntry>> ListContentsAsync(string path)
     {
         return await _storageContext.GetFilesAndDirectories(path);
     }

     // Create a folder
     public async Task<FileManagerEntry> CreateFolderAsync(string path)
     {
         return await _storageContext.CreateFolder(path);
     }

     // Delete a folder
     public async Task DeleteFolderAsync(string path)
     {
         await _storageContext.DeleteFolderAsync(path);
     }
}
```

### Chunked File Upload (Large Files)

```csharp
public async Task<IActionResult> UploadChunk( byte[] chunkData, long chunkIndex, long totalChunks, string fileName, string uploadUid) { var metadata = new FileUploadMetaData { FileName = fileName, RelativePath = $"uploads/{fileName}", ContentType = "application/octet-stream", ChunkIndex = chunkIndex, TotalChunks = totalChunks, TotalFileSize = chunkData.Length * totalChunks, UploadUid = uploadUid };
     using var memoryStream = new MemoryStream(chunkData);
     await _storageContext.AppendBlob(memoryStream, metadata, "append");

     return Ok(new { 
         uploaded = chunkIndex + 1, 
         total = totalChunks 
     });
}
```

### Azure Static Website Management

```csharp
// Enable static website hosting (Azure only) await _storageContext.EnableAzureStaticWebsite();
// Disable static website hosting (Azure only) await _storageContext.DisableAzureStaticWebsite();
```

### Working with Metadata

```csharp
// Get file metadata
var fileInfo = await _storageContext.GetFileAsync("path/to/file.jpg");
Console.WriteLine($"Size: {fileInfo.Size}, Modified: {fileInfo.Modified}");
```

### Metadata Operations

BlobService provides rich metadata management capabilities for tracking file properties, custom tags, and image dimensions.

**Upsert Metadata:**
```csharp
using Cosmos.BlobService.Models;

public async Task UpdateFileMetadataAsync(string filePath)
{
    var metadata = new BlobMetadataItem
    {
        Name = "Author",
        Value = "John Doe"
    };
    
    await _storageContext.UpsertMetadataAsync(filePath, metadata);
}
```

**Retrieve Metadata:**
```csharp
public async Task<FileMetadata> GetFileDetailsAsync(string filePath)
{
    var metadata = await _storageContext.GetFileMetadataAsync(filePath);
    
    Console.WriteLine($"Content Type: {metadata.ContentType}");
    Console.WriteLine($"Size: {metadata.Size} bytes");
    Console.WriteLine($"ETag: {metadata.ETag}");
    Console.WriteLine($"Last Modified: {metadata.LastModified}");
    
    // Image-specific properties (if available)
    if (metadata.Width.HasValue && metadata.Height.HasValue)
    {
        Console.WriteLine($"Dimensions: {metadata.Width}x{metadata.Height}");
    }
    
    // Custom metadata
    foreach (var item in metadata.Metadata)
    {
        Console.WriteLine($"{item.Key}: {item.Value}");
    }
    
    return metadata;
}
```

**Delete Metadata:**
```csharp
public async Task RemoveCustomMetadataAsync(string filePath, string metadataKey)
{
    await _storageContext.DeleteMetadataAsync(filePath, metadataKey);
}
```

**Image Properties Detection:**
```csharp
public async Task<(int? width, int? height)> GetImageDimensionsAsync(string imagePath)
{
    var fileInfo = await _storageContext.GetFileAsync(imagePath);
    return (fileInfo.Width, fileInfo.Height);
}
```

**Batch Metadata Operations:**
```csharp
public async Task TagMultipleFilesAsync(List<string> filePaths, string category)
{
    var metadata = new BlobMetadataItem
    {
        Name = "Category",
        Value = category
    };
    
    foreach (var path in filePaths)
    {
        await _storageContext.UpsertMetadataAsync(path, metadata);
    }
}
```

### Provider Configuration Reference

| Provider | Connection String Pattern | Required Parameters | Optional Parameters |
|----------|---------------------------|---------------------|---------------------|
| **Azure Blob Storage** | `DefaultEndpointsProtocol=https;...` | AccountName, AccountKey (or AccessToken) | EndpointSuffix, BlobEndpoint |
| **Amazon S3** | `Bucket=...;Region=...;AccessKey=...` | Bucket, Region, AccessKey, SecretKey | ServiceUrl (for S3-compatible) |
| **Cloudflare R2** | `AccountId=...;Bucket=...;AccessKey=...` | AccountId, Bucket, AccessKey, SecretKey | - |
| **Google Cloud Storage** | `Bucket=...;Region=...;ServiceUrl=https://storage.googleapis.com` | Bucket, Region, AccessKey, SecretKey, ServiceUrl | - |
| **Azure Files Share** | `DefaultEndpointsProtocol=https;...;FileEndpoint=...` | AccountName, AccountKey, FileEndpoint | ShareName |

**Detection Logic:**

The service automatically selects the appropriate driver based on connection string analysis:

```csharp
// Azure Blob Storage detection
if (connectionString.StartsWith("DefaultEndpointsProtocol="))
    return new AzureStorage(config);

// Cloudflare R2 detection
if (connectionString.Contains("AccountId="))
    return new AmazonStorage(config); // Uses S3-compatible API

// Amazon S3 / Google Cloud Storage detection
if (connectionString.Contains("Bucket="))
    return new AmazonStorage(config);

// Azure Files Share detection
if (connectionString.Contains("FileEndpoint="))
    return new AzureFileStorage(config);
```

**Configuration Examples:**

```json
{
  "ConnectionStrings": {
    "StorageConnectionString": "[provider-specific-string]"
  },
  "CosmosStorageConfig": {
    "CdnConfig": {
      "AzureCdnConfig": {
        "PublicUrl": "https://cdn.example.com"
      }
    }
  }
}
```

### StorageContext Methods

| Method | Description | Returns |
|--------|-------------|---------|
| `BlobExistsAsync(path)` | Check if a blob exists | `Task<bool>` |
| `GetFileAsync(path)` | Get file metadata | `Task<FileManagerEntry>` |
| `GetFilesAndDirectories(path)` | List files and folders | `Task<List<FileManagerEntry>>` |
| `GetStreamAsync(path)` | Get file stream | `Task<Stream>` |
| `AppendBlob(stream, metadata, mode)` | Upload file data | `Task` |
| `CopyAsync(source, destination)` | Copy file/folder | `Task` |
| `MoveFileAsync(source, destination)` | Move/rename file | `Task` |
| `MoveFolderAsync(source, destination)` | Move/rename folder | `Task` |
| `DeleteFile(path)` | Delete file | `void` |
| `DeleteFolderAsync(path)` | Delete folder | `Task` |
| `CreateFolder(path)` | Create folder | `Task<FileManagerEntry>` |
| `EnableAzureStaticWebsite()` | Enable Azure static website | `Task` |
| `DisableAzureStaticWebsite()` | Disable Azure static website | `Task` |

### FileUploadMetaData Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `UploadUid` | `string` | Yes | Unique upload identifier for tracking chunks |
| `FileName` | `string` | Yes | Original file name |
| `RelativePath` | `string` | Yes | Target storage path |
| `ContentType` | `string` | Yes | MIME content type |
| `ChunkIndex` | `long` | Yes | Current chunk number (0-based) |
| `TotalChunks` | `long` | Yes | Total number of chunks |
| `TotalFileSize` | `long` | Yes | Total file size in bytes |
| `ImageWidth` | `string` | No | Image width (for image files) |
| `ImageHeight` | `string` | No | Image height (for image files) |
| `CacheControl` | `string` | No | Cache control header |

### FileManagerEntry Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | File or folder name |
| `Path` | `string` | Full path |
| `Size` | `long` | Size in bytes |
| `IsDirectory` | `bool` | Whether item is a directory |
| `Created` | `DateTime` | Creation date (local) |
| `CreatedUtc` | `DateTime` | Creation date (UTC) |
| `Modified` | `DateTime` | Last modified date (local) |
| `ModifiedUtc` | `DateTime` | Last modified date (UTC) |
| `ContentType` | `string` | MIME content type |
| `Extension` | `string` | File extension |
| `ETag` | `string` | Entity tag |
| `HasDirectories` | `bool` | Whether folder contains subdirectories |

## Multi-Cloud Strategy

The service provides a unified approach to cloud storage with several benefits:

1. **Vendor Independence**: Avoid vendor lock-in by supporting multiple providers
2. **Cost Optimization**: Choose the most cost-effective provider for different scenarios
3. **Geographic Distribution**: Use different providers for different regions
4. **Migration Flexibility**: Easily migrate between cloud providers with minimal code changes
5. **Development Flexibility**: Use different providers for development, staging, and production

## Performance Considerations

- **Chunked Uploads**: Large files are uploaded in chunks (AWS: 5MB minimum per part, Azure: 2.5MB buffered)
- **Memory Caching**: Multi-part upload state is cached for AWS/R2 operations
- **Connection Pooling**: Efficient connection management for both Azure and AWS
- **Async Operations**: All operations are asynchronous for better scalability
- **Automatic Driver Selection**: No runtime overhead for driver selection after initial configuration

## Security Features

- **Managed Identity**: Support for Azure managed identity authentication (DefaultAzureCredential)
- **Data Protection**: Integration with ASP.NET Core data protection for key storage
- **Connection String Security**: Secure handling of connection strings and credentials
- **Access Control**: Respects cloud provider access control mechanisms (Azure RBAC, S3 IAM)
- **CORS Configuration**: Automatic CORS configuration when enabling Azure static websites

## Project Structure

## See Also

### Related Documentation
- [Storage Configuration Guide](https://cwalabs.github.io/SkyCMS/StorageConfig.html) - Detailed storage provider setup instructions
- [Azure Installation Guide](https://cwalabs.github.io/SkyCMS/AzureInstall.html) - Azure deployment and configuration
- [Editor Documentation](../Editor/README.md) - Content management interface using Blob Service
- [Publisher Documentation](../Publisher/README.md) - Public website using Blob Service
- [Common Library](../Common/README.md) - Shared utilities and models
- [Dynamic Configuration](../Cosmos.ConnectionStrings/README.md) - Multi-tenant configuration provider

### Configuration Files
- [appsettings.json](../launchSettings.json) - Application configuration examples
- [docker-compose.yml](../docker-compose.yml) - Docker container configuration

### External Resources
- [Azure Blob Storage Documentation](https://docs.microsoft.com/azure/storage/blobs/)
- [Amazon S3 Documentation](https://docs.aws.amazon.com/s3/)
- [Cloudflare R2 Documentation](https://developers.cloudflare.com/r2/)

## Contributing

This project is part of the Sky CMS ecosystem. For contribution guidelines and more information, visit:
- **Sky CMS Repository**: [https://github.com/CWALabs/SkyCMS](https://github.com/CWALabs/SkyCMS)
- **Project Website**: [https://sky-cms.com](https://sky-cms.com)

## License

Licensed under the MIT License. See the LICENSE file for details.

## Copyright

Copyright © 2025 Moonrise Software LLC. All rights reserved.

## Support

For issues, questions, or contributions, please visit the GitHub repository or the project website.
