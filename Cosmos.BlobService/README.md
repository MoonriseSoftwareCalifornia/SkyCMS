# Cosmos.BlobService - Multi-Cloud Blob Storage Service

## Overview

Cosmos.BlobService is a comprehensive multi-cloud blob storage abstraction layer that provides a unified interface for managing files across different cloud storage providers. It supports both Azure Blob Storage and Amazon S3, allowing applications to seamlessly switch between providers or use multiple providers simultaneously.

## Features

### Multi-Cloud Support

- **Azure Blob Storage**: Full support with managed identity authentication
- **Amazon S3**: Complete AWS S3 integration with chunked upload support
- **Unified Interface**: Single API for all storage operations regardless of provider
- **Provider Switching**: Runtime configuration of storage providers

### File Management Operations

- **Upload/Download**: Single and chunked file uploads with metadata tracking
- **Copy/Move/Rename**: File and folder operations across cloud providers
- **Delete**: File and folder deletion with recursive support
- **Metadata**: Comprehensive file metadata handling including image dimensions
- **Directory Operations**: Create, list, and manage virtual directory structures

### Advanced Features

- **Chunked Uploads**: Support for large file uploads with progress tracking
- **Static Website**: Enable/disable Azure static website hosting
- **Data Protection**: Integration with ASP.NET Core data protection
- **Multi-Tenant**: Support for single and multi-tenant configurations
- **Memory Caching**: Efficient caching for improved performance

## Architecture

### Core Components

#### StorageContext

The main service class that provides the unified interface to all storage operations. It automatically selects the appropriate driver based on configuration.

#### ICosmosStorage Interface

Defines the contract for all storage drivers, ensuring consistent behavior across different cloud providers.

#### Storage Drivers

- **AzureStorage**: Implements Azure Blob Storage operations
- **AmazonStorage**: Implements Amazon S3 operations
- **AzureFileStorage**: Provides Azure Files Share support

### Driver Pattern

The service uses a driver pattern where each cloud provider has its own implementation of the `ICosmosStorage` interface, allowing for provider-specific optimizations while maintaining a consistent API.

## Installation

This package is part of the SkyCMS solution and is not distributed separately on NuGet. You can obtain it by cloning the [SkyCMS GitHub repository](https://github.com/MoonriseSoftwareCalifornia/CosmosCMS).

## Configuration

### Azure Blob Storage Configuration

```json
{
  "ConnectionStrings": {
    "AzureBlobStorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=yourkey;EndpointSuffix=core.windows.net"
  }
}
```

For managed identity authentication:

```json
{
  "ConnectionStrings": {
    "AzureBlobStorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=AccessToken;EndpointSuffix=core.windows.net"
  }
}
```

### Amazon S3 Configuration

```json
{
  "ConnectionStrings": {
    "AzureBlobStorageConnectionString": "Provider=Amazon;Bucket=yourbucket;Region=us-west-2;KeyId=yourkeyid;Key=yoursecretkey"
  }
}
```

### Multi-Tenant Configuration

```json
{
  "MultiTenantEditor": true,
  "ConnectionStrings": {
    "DataProtectionStorage": "your-data-protection-connection-string"
  }
}
```

## Usage

### Service Registration

In your `Program.cs` or `Startup.cs`:

```csharp
using Cosmos.BlobService;

// Register storage context
builder.Services.AddCosmosStorageContext(builder.Configuration);

// Optional: Add data protection with blob storage
builder.Services.AddCosmosCmsDataProtection(builder.Configuration, new DefaultAzureCredential());
```

### Basic Usage

```csharp
using Cosmos.BlobService;
using Cosmos.BlobService.Models;

public class FileController : ControllerBase
{
    private readonly StorageContext _storageContext;

    public FileController(StorageContext storageContext)
    {
        _storageContext = storageContext;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file, string directory = "")
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
        
        await _storageContext.AppendBlobAsync(stream.ToArray(), metadata, DateTimeOffset.UtcNow, "block");
        
        return Ok(new { path = relativePath });
    }

    [HttpGet("download/{*path}")]
    public async Task<IActionResult> DownloadFile(string path)
    {
        var stream = await _storageContext.GetStreamAsync(path);
        var metadata = await _storageContext.GetFileAsync(path);
        
        return File(stream, metadata.ContentType, metadata.Name);
    }

    [HttpDelete("{*path}")]
    public async Task<IActionResult> DeleteFile(string path)
    {
        _storageContext.DeleteFile(path);
        return Ok();
    }

    [HttpPost("copy")]
    public async Task<IActionResult> CopyFile(string source, string destination)
    {
        await _storageContext.CopyAsync(source, destination);
        return Ok();
    }

    [HttpPost("move")]
    public async Task<IActionResult> MoveFile(string source, string destination)
    {
        await _storageContext.MoveFileAsync(source, destination);
        return Ok();
    }
}
```

### Example File Operations

```csharp
// Check if file exists
bool exists = await storageContext.BlobExistsAsync("path/to/file.jpg");

// Get file metadata
var fileInfo = await storageContext.GetFileAsync("path/to/file.jpg");

// List files and directories
var entries = await storageContext.GetFilesAsync("path/to/directory");

// Create directory
await storageContext.CreateFolderAsync("path/to/new/directory");

// Delete directory
await storageContext.DeleteFolderAsync("path/to/directory");
```

### Image Upload with Metadata

```csharp
[HttpPost("upload-image")]
public async Task<IActionResult> UploadImage(IFormFile file)
{
    using var image = await Image.LoadAsync(file.OpenReadStream());
    
    var metadata = new FileUploadMetaData
    {
        FileName = file.FileName,
        RelativePath = $"images/{file.FileName}",
        ContentType = file.ContentType,
        ImageWidth = image.Width.ToString(),
        ImageHeight = image.Height.ToString(),
        TotalFileSize = file.Length,
        UploadUid = Guid.NewGuid().ToString()
    };

    using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream);
    
    await storageContext.AppendBlobAsync(memoryStream.ToArray(), metadata, DateTimeOffset.UtcNow, "block");
    
    return Ok(new { 
        url = $"/{metadata.RelativePath}",
        width = image.Width,
        height = image.Height 
    });
}
```

### Azure Static Website Management

```csharp
// Enable static website hosting (Azure only)
await storageContext.EnableAzureStaticWebsite();

// Disable static website hosting (Azure only)
await storageContext.DisableAzureStaticWebsite();
```

## API Reference

### StorageContext Methods

| Method | Description | Parameters |
|--------|-------------|------------|
| `BlobExistsAsync` | Check if a blob exists | `path: string` |
| `GetFileAsync` | Get file metadata | `path: string` |
| `GetFilesAsync` | List files in directory | `path: string` |
| `GetStreamAsync` | Get file stream | `path: string` |
| `AppendBlobAsync` | Upload file data | `data: byte[]`, `metadata: FileUploadMetaData`, `uploadTime: DateTimeOffset`, `mode: string` |
| `CopyAsync` | Copy file/folder | `source: string`, `destination: string` |
| `MoveFileAsync` | Move/rename file | `source: string`, `destination: string` |
| `DeleteFile` | Delete file | `path: string` |
| `DeleteFolderAsync` | Delete folder | `path: string` |
| `CreateFolderAsync` | Create folder | `path: string` |

### FileUploadMetaData Properties

| Property | Type | Description |
|----------|------|-------------|
| `UploadUid` | `string` | Unique upload identifier |
| `FileName` | `string` | Original file name |
| `RelativePath` | `string` | Storage path |
| `ContentType` | `string` | MIME content type |
| `ChunkIndex` | `long` | Current chunk number |
| `TotalChunks` | `long` | Total number of chunks |
| `TotalFileSize` | `long` | Total file size in bytes |
| `ImageWidth` | `string` | Image width (if applicable) |
| `ImageHeight` | `string` | Image height (if applicable) |
| `CacheControl` | `string` | Cache control header |

### FileManagerEntry Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | File or folder name |
| `Path` | `string` | Full path |
| `Size` | `long` | Size in bytes |
| `IsDirectory` | `bool` | Whether item is a directory |
| `Created` | `DateTime` | Creation date |
| `Modified` | `DateTime` | Last modified date |
| `ContentType` | `string` | MIME content type |
| `Extension` | `string` | File extension |
| `ETag` | `string` | Entity tag |

## Dependencies

### Core Dependencies

- **.NET 9.0**: Modern .NET framework
- **Azure.Storage.Blobs**: Azure Blob Storage SDK
- **AWSSDK.S3**: Amazon S3 SDK
- **Azure.Identity**: Azure authentication
- **Microsoft.Extensions.Caching.Memory**: Memory caching
- **Microsoft.Extensions.DependencyInjection**: Dependency injection

### Image Processing

- **SixLabors.ImageSharp**: Image manipulation and metadata extraction

### Project Configuration

- **Microsoft.Extensions.Configuration**: Configuration management
- **Cosmos.DynamicConfig**: Dynamic configuration provider

## Multi-Cloud Strategy

The service provides a unified approach to cloud storage that offers several benefits:

1. **Vendor Independence**: Avoid vendor lock-in by supporting multiple providers
2. **Cost Optimization**: Choose the most cost-effective provider for different scenarios
3. **Geographic Distribution**: Use different providers for different regions
4. **Redundancy**: Implement cross-cloud backup strategies
5. **Migration**: Easily migrate between cloud providers

## Performance Considerations

- **Chunked Uploads**: Large files are uploaded in chunks for better reliability
- **Memory Caching**: Frequently accessed metadata is cached
- **Connection Pooling**: Efficient connection management for both Azure and AWS
- **Async Operations**: All operations are asynchronous for better scalability

## Security Features

- **Managed Identity**: Support for Azure managed identity authentication
- **Data Protection**: Integration with ASP.NET Core data protection
- **Connection String Security**: Secure handling of connection strings and credentials
- **Access Control**: Respect cloud provider access control mechanisms

## License

Licensed under the GNU Public License, Version 3.0. See the [LICENSE](LICENSE.txt) file for details.

## Contributing

This project is part of the SkyCMS ecosystem. For contribution guidelines and more information, visit the [SkyCMS GitHub repository](https://github.com/MoonriseSoftwareCalifornia/CosmosCMS).
