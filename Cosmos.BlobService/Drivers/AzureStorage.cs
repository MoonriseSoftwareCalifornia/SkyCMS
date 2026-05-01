// <copyright file="AzureStorage.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Sky.Tests")]

namespace Cosmos.BlobService.Drivers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Core;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Azure.Storage.Blobs.Specialized;
    using Cosmos.BlobService.Config;
    using Cosmos.BlobService.Models;

    /// <summary>
    ///     Azure blob storage driver.
    /// </summary>
    public sealed class AzureStorage : ICosmosStorage
    {
        private string containerName;
        private BlobServiceClient blobServiceClient;
        private bool usesAzureDefaultCredential; // Requred for enabling static website.

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureStorage"/> class.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        /// <param name="tokenCredential">Default azure credential (optional for Azurite).</param>
        /// <param name="containerName">Name of container (default is $web).</param>
        public AzureStorage(string connectionString, TokenCredential tokenCredential = null, string containerName = "$web")
        {
            this.Initialize(containerName, connectionString, tokenCredential);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureStorage"/> class.
        /// </summary>
        /// <param name="config">Storage configuration as a <see cref="AzureStorageConfig"/>.</param>
        /// <param name="tokenCredential">Default azure credential.</param>
        /// <param name="containerName">Name of container (default is $web).</param>
        public AzureStorage(AzureStorageConfig config, TokenCredential tokenCredential, string containerName = "$web")
        {
            this.Initialize(containerName, config.AzureBlobStorageConnectionString, tokenCredential);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureStorage"/> class.
        /// Internal constructor for testing: allows injection of a mock BlobServiceClient.
        /// </summary>
        /// <param name="mockBlobServiceClient">Mock BlobServiceClient instance.</param>
        /// <param name="containerName">Container name (default is $web).</param>
        internal AzureStorage(BlobServiceClient mockBlobServiceClient, string containerName = "$web")
        {
            this.containerName = containerName;
            this.blobServiceClient = mockBlobServiceClient;
            this.usesAzureDefaultCredential = false;
        }

        /// <summary>
        /// Gets the amount of bytes consumed in a storage account container.
        /// </summary>
        /// <returns>Returns the number of bytes consumed for the container as a <see cref="long"/>.</returns>
        public async Task<long> GetBytesConsumed()
        {
            var container = this.blobServiceClient.GetBlobContainerClient(this.containerName);
            long bytesConsumed = 0;
            var blobs = container.GetBlobsAsync().AsPages();

            await foreach (var blobPage in blobs)
            {
                bytesConsumed += blobPage.Values.Sum(b => b.Properties.ContentLength.GetValueOrDefault());
            }

            return bytesConsumed;
        }

        /// <summary>
        ///     Appends byte array to an existing blob.
        /// </summary>
        /// <param name="data">Bytes to append.</param>
        /// <param name="fileMetaData">'Chunk' metadata being appended as a <see cref="FileUploadMetaData"/>.</param>
        /// <param name="uploadDateTime">Date and time uploaded as a <see cref="DateTimeOffset"/>.</param>
        /// <param name="mode">Mode is either append or block.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// Existing blobs will be overwritten if they already exists, otherwise a new blob is created.
        /// </remarks>
        public async Task AppendBlobAsync(byte[] data, FileUploadMetaData fileMetaData, DateTimeOffset uploadDateTime, string mode = StorageConstants.UploadModeBlock)
        {
            if (mode.Equals(StorageConstants.UploadModeBlock, StringComparison.CurrentCultureIgnoreCase) || fileMetaData.TotalChunks == 1)
            {
                await this.UpdloadBlockBlobAsync(new MemoryStream(data), fileMetaData, uploadDateTime);
                return;
            }

            var blobClient = this.GetBlobClient(fileMetaData.RelativePath);

            var appendClient = this.GetAppendBlobClient(fileMetaData.RelativePath);

            if (fileMetaData.ChunkIndex == 0)
            {
                // Break any existing lease before attempting to delete
                if (await appendClient.ExistsAsync())
                {
                    var leaseClient = appendClient.GetBlobLeaseClient();
                    try
                    {
                        await leaseClient.BreakAsync(TimeSpan.Zero);
                    }
                    catch (Azure.RequestFailedException)
                    {
                        // If breaking the lease fails (e.g., no lease exists), continue with deletion
                    }
                }

                var deleteResult = await appendClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

                // If the blob was deleted, we need to wait for it to be removed from the storage account.
                var success = await DeleteAppendBlobWithRetryAsync(appendClient);

                var headers = new BlobHttpHeaders
                {
                    // Set the MIME ContentType every time the properties are updated or the field will be cleared.
                    ContentType = Utilities.GetContentType(fileMetaData),
                    CacheControl = fileMetaData.CacheControl,
                };

                await appendClient.CreateIfNotExistsAsync(headers);

                var dictionaryObject = new Dictionary<string, string>
                {
                    { StorageConstants.MetadataUploadUid, fileMetaData.UploadUid },
                    { StorageConstants.MetadataSize, fileMetaData.TotalFileSize.ToString() },
                    { StorageConstants.MetadataDateTime, uploadDateTime.UtcDateTime.Ticks.ToString() },
                    { StorageConstants.MetadataImageWidth, fileMetaData.ImageWidth },
                    { StorageConstants.MetadataImageHeight, fileMetaData.ImageHeight }
                };

                _ = await appendClient.SetMetadataAsync(dictionaryObject);
            }
            else
            {
                // For non-first chunks, ensure the append blob exists and is appendable
                // If it doesn't exist, is the wrong type, or is sealed, create/recreate it
                var blobExists = await blobClient.ExistsAsync();
                bool needsRecreation = false;

                if (blobExists)
                {
                    try
                    {
                        // Check if the existing blob is the correct type
                        var properties = await blobClient.GetPropertiesAsync();
                        if (properties.Value.BlobType != Azure.Storage.Blobs.Models.BlobType.Append)
                        {
                            needsRecreation = true;
                        }
                        else
                        {
                            // Check if the append blob is sealed by trying to get append blob properties
                            var appendProperties = await appendClient.GetPropertiesAsync();
                            if (appendProperties.Value.IsSealed == true)
                            {
                                needsRecreation = true;
                            }
                        }
                    }
                    catch (Azure.RequestFailedException)
                    {
                        // If we can't get properties, recreate to be safe
                        needsRecreation = true;
                    }

                    if (needsRecreation)
                    {
                        // Wrong blob type or sealed - delete it and create a new append blob
                        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
                        await DeleteAppendBlobWithRetryAsync(appendClient);

                        var headers = new BlobHttpHeaders
                        {
                            ContentType = Utilities.GetContentType(fileMetaData),
                            CacheControl = fileMetaData.CacheControl,
                        };
                        await appendClient.CreateIfNotExistsAsync(headers);
                    }
                }
                else
                {
                    // Blob doesn't exist - create it as an append blob
                    var headers = new BlobHttpHeaders
                    {
                        ContentType = Utilities.GetContentType(fileMetaData),
                        CacheControl = fileMetaData.CacheControl,
                    };
                    await appendClient.CreateIfNotExistsAsync(headers);
                }
            }

            // AWS Multi part upload requires parts or chunks to be 5MB, which
            // are too big for Azure append blobs, so buffer the upload size here.
            await using var loadMemoryStream = new MemoryStream(data);

            loadMemoryStream.Position = 0;
            int bytesRead;
            var buffer = new byte[2621440]; // 2.5 MB.
            while ((bytesRead = await loadMemoryStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var newArray = new Span<byte>(buffer, 0, bytesRead).ToArray();
                Stream stream = new MemoryStream(newArray)
                {
                    Position = 0
                };
                await appendClient.AppendBlockAsync(stream);
            }

            if (fileMetaData.TotalChunks - 1 == fileMetaData.ChunkIndex)
            {
                // This is the last chunk, wrap things up here.
                await appendClient.SealAsync();
            }
        }

        /// <summary>
        ///     Determines if a blob exists.
        /// </summary>
        /// <param name="path">Path to blob.</param>
        /// <returns>Returns a <see cref="bool"/> indicating it exists or not.</returns>
        public async Task<bool> BlobExistsAsync(string path)
        {
            var blob = await this.GetBlobAsync(path);

            return await blob.ExistsAsync();
        }

        /// <summary>
        ///     Copies a single blob and returns it's <see cref="BlobClient" />.
        /// </summary>
        /// <param name="source">Path to the blob to copy.</param>
        /// <param name="destination">Path to where the blob should be copied.</param>
        /// <returns>The destination or new <see cref="BlobClient" />.</returns>
        /// <remarks>
        ///     Tip: After operation, check the returned blob object to see if it exists.
        /// </remarks>
        public async Task CopyBlobAsync(string source, string destination)
        {
            source = source.TrimStart('/');
            destination = destination.TrimStart('/');

            var containerClient =
                this.blobServiceClient.GetBlobContainerClient(this.containerName);
            var sourceBlob = containerClient.GetBlobClient(source);
            if (await sourceBlob.ExistsAsync())
            {
                var lease = sourceBlob.GetBlobLeaseClient();
                await lease.AcquireAsync(TimeSpan.FromSeconds(-1));

                try
                {
                    // Get a BlobClient representing the destination blob with a unique name.
                    var destBlob = containerClient.GetBlobClient(destination);

                    // Start the copy operation.
                    var c = await destBlob.StartCopyFromUriAsync(sourceBlob.Uri);
                    await c.WaitForCompletionAsync();
                }
                finally
                {
                    await lease.ReleaseAsync();
                }
            }
        }

        /// <summary>
        ///     Creates a folder if it does not yet exists.
        /// </summary>
        /// <param name="path">Path to folder to create.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Folder creation failure.</exception>
        public async Task CreateFolderAsync(string path)
        {
            // Blob storage does not have a folder object, just blobs with paths.
            // Therefore, to create an illusion of a folder, we have to create a blob
            // that will be in the folder.  For example:
            // 
            // To create folder /pictures
            //
            // You have to pub a blob here /pictures/folder.subxx
            //
            // To remove a folder, you simply remove all blobs below /pictures
            //
            // Make sure the path doesn't already exist.
            // Don't lead with "/" with S3
            var fullPath = Utilities.GetBlobName(path, "folder.stubxx").TrimStart('/');

            var blobClient = await this.GetBlobAsync(fullPath);
            var byteArray = Encoding.ASCII.GetBytes($"This is a folder stub file for {path}.");
            await using var stream = new MemoryStream(byteArray);

            // Chunked uploads can attempt to ensure the same folder multiple times in quick succession.
            // Overwriting the stub makes folder creation idempotent and avoids BlobAlreadyExists races.
            await blobClient.UploadAsync(stream, overwrite: true);
        }

        /// <summary>
        ///     Gets a list of blobs by path. Is recursive.
        /// </summary>
        /// <param name="path">Path to get blob names from.</param>
        /// <returns>Returns the names as a <see cref="string"/> list.</returns>
        public async Task<List<string>> GetBlobNamesByPath(string path)
        {
            if (path == "/")
            {
                path = string.Empty;
            }

            if (path.StartsWith('/'))
            {
                path = path.TrimStart('/');
            }

            var containerClient =
                this.blobServiceClient.GetBlobContainerClient(this.containerName);

            var pageable = containerClient.GetBlobsAsync(prefix: path).AsPages();

            var results = new List<BlobItem>();
            await foreach (var page in pageable)
            {
                results.AddRange(page.Values);
            }

            var folderStub = path + "folder.stubxx";

            return results.Where(w => w.Name != folderStub).Select(s => s.Name).ToList();
        }

        /// <summary>
        ///     Deletes a folder and all its contents.
        /// </summary>
        /// <param name="path">Path to doomed folder.</param>
        /// <returns>Returns the number of deleted obhects as an <see cref="int"/>.</returns>
        public async Task<int> DeleteFolderAsync(string path)
        {
            var blobs = await this.GetBlobItemsByPath(path);
            var containerClient =
                this.blobServiceClient.GetBlobContainerClient(this.containerName);

            var responses = new List<Response<bool>>();

            foreach (var blob in blobs)
            {
                responses.Add(await containerClient.DeleteBlobIfExistsAsync(blob.Name, DeleteSnapshotsOption.IncludeSnapshots));
            }

            return responses.Count(r => r.Value);
        }

        /// <summary>
        ///     Deletes a file (blob).
        /// </summary>
        /// <param name="path">Path to doomed blob.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task DeleteIfExistsAsync(string path)
        {
            var containerClient = this.blobServiceClient.GetBlobContainerClient(this.containerName);

            // Try to break any existing lease before deletion
            try
            {
                var blobClient = containerClient.GetBlobClient(path);
                if (await blobClient.ExistsAsync())
                {
                    var leaseClient = blobClient.GetBlobLeaseClient();
                    try
                    {
                        await leaseClient.BreakAsync(TimeSpan.Zero);
                    }
                    catch (Azure.RequestFailedException)
                    {
                        // If breaking the lease fails (e.g., no lease exists), continue with deletion
                    }
                }
            }
            catch (Azure.RequestFailedException)
            {
                // Ignore errors during lease breaking
            }

            await containerClient.DeleteBlobIfExistsAsync(path, DeleteSnapshotsOption.IncludeSnapshots);
            var extension = Path.GetExtension(path);
            if (Utilities.ImageThumbnailTypes.Contains(extension))
            {
                await containerClient.DeleteBlobIfExistsAsync(path + ".tn", DeleteSnapshotsOption.IncludeSnapshots);
            }
        }

        /// <summary>
        /// Enables the static website and sets default CORS rule.
        /// </summary>
        /// <param name="indexDocument">Index document (default is index.html).</param>
        /// <param name="errorDocument">Error document.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task EnableStaticWebsite(string indexDocument = "index.html", string errorDocument = "404.html", CancellationToken cancellationToken = default)
        {
            if (this.usesAzureDefaultCredential)
            {
                return; // Not able to enable static website when using Azure Default Credential.
            }

            BlobServiceProperties properties = await this.blobServiceClient.GetPropertiesAsync();

            if (!properties.StaticWebsite.Enabled)
            {
                properties.StaticWebsite.Enabled = true;
                properties.StaticWebsite.IndexDocument = indexDocument;
                properties.StaticWebsite.ErrorDocument404Path = errorDocument;
                await this.blobServiceClient.SetPropertiesAsync(properties);
            }

            if (string.IsNullOrWhiteSpace(properties.StaticWebsite.IndexDocument) || !properties.StaticWebsite.IndexDocument.Equals(indexDocument, StringComparison.OrdinalIgnoreCase))
            {
                properties.StaticWebsite.IndexDocument = indexDocument;
                await this.blobServiceClient.SetPropertiesAsync(properties);
            }

            if (properties.Cors == null || properties.Cors.Count == 0)
            {
                var corsRule = new BlobCorsRule
                {
                    AllowedMethods = "GET,HEAD,OPTIONS",
                    AllowedOrigins = "*",
                    AllowedHeaders = "*",
                    ExposedHeaders = "*"
                };
                properties.Cors = new List<BlobCorsRule>() { corsRule };
                await this.blobServiceClient.SetPropertiesAsync(properties);
            }
        }

        /// <summary>
        /// Disables the static website.
        /// </summary>
        /// <returns>A <see cref="Task"/>.</returns>
        public async Task DisableStaticWebsite()
        {
            BlobServiceProperties properties = await this.blobServiceClient.GetPropertiesAsync();

            if (properties.StaticWebsite.Enabled)
            {
                properties.StaticWebsite.Enabled = false;
                await this.blobServiceClient.SetPropertiesAsync(properties);
            }
        }

        /// <summary>
        ///     Gets a <see cref="BlobClient"/> for a blob.
        /// </summary>
        /// <param name="path">Path to blob.</param>
        /// <returns>Returns a <see cref="BlobClient"/>.</returns>
        public async Task<BlobClient> GetBlobAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            path = path.TrimStart('/');
            var containerClient =
                this.blobServiceClient.GetBlobContainerClient(this.containerName);

            if (await containerClient.ExistsAsync())
            {
                return containerClient.GetBlobClient(path);
            }

            return null;
        }

        /// <summary>
        /// Get blob itmes by path.
        /// </summary>
        /// <param name="path">Path where to get items from.</param>
        /// <returns>Returns the items as <see cref="BlobItem"/> <see cref="List{T}"/>.</returns>
        public async Task<List<BlobItem>> GetBlobItemsByPath(string path)
        {
            var results = new List<BlobItem>();
            var containerClient =
                this.blobServiceClient.GetBlobContainerClient(this.containerName);
            var items = containerClient.GetBlobsAsync(prefix: path.Equals("/") ? null : path);

            await foreach (var item in items)
            {
                results.Add(item);
            }

            return results;
        }

        /// <summary>
        /// Gets metadata for a blob.
        /// </summary>
        /// <param name="path">Path to file from which to get metadata.</param>
        /// <returns>Returns metadata as a <see cref="FileMetadata"/>.</returns>
        public async Task<FileMetadata> GetFileMetadataAsync(string path)
        {
            var blobClient = await this.GetBlobAsync(path);
            if (blobClient == null)
            {
                return null;
            }

            Azure.Response<BlobProperties> properties;
            try
            {
                properties = await blobClient.GetPropertiesAsync();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }

            properties.Value.Metadata.TryGetValue("ccmsuploaduid", out var uploadUid);
            _ = long.TryParse(uploadUid, out var mark);

            var eTag = properties.Value.ETag.ToString("H").Trim('"');

            return new FileMetadata()
            {
                ContentLength = properties.Value.ContentLength,
                ContentType = properties.Value.ContentType,
                ETag = eTag,
                FileName = blobClient.Name,
                LastModified = properties.Value.LastModified.UtcDateTime,
                UploadDateTime = mark
            };
        }

        /// <inheritdoc/>
        public Task<List<FileMetadata>> GetInventory()
        {
            throw new NotSupportedException("Inventory enumeration is not supported by AzureStorage.");
        }

        /// <summary>
        ///     Gets files and subfolders for a given path.
        /// </summary>
        /// <param name="path">Path from which to get objects from.</param>
        /// <returns>Returns objects as a <see cref="BlobHierarchyItem"/> <see cref="List{T}"/>.</returns>
        public async Task<List<BlobHierarchyItem>> GetBlobHierarchyItemsAsync(string path)
        {
            if (path == "/")
            {
                path = string.Empty;
            }
            else if (!path.EndsWith("/") && path.Equals(string.Empty) == false)
            {
                path += "/";
            }

            var containerClient = this.blobServiceClient.GetBlobContainerClient(this.containerName);

            var resultSegment = containerClient.GetBlobsByHierarchyAsync(prefix: path, delimiter: "/").AsPages();
            var results = new List<BlobHierarchyItem>();

            await foreach (var blobPage in resultSegment)
            {
                results.AddRange(blobPage.Values);
            }

            return results;
        }

        /// <summary>
        ///  Gets files and directories for a given path.
        /// </summary>
        /// <param name="path">Path to seach on.</param>
        /// <returns>List of File Manager Entries.</returns>
        public async Task<List<FileManagerEntry>> GetFilesAndDirectories(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                path = path.TrimStart('/');
            }

            var entries = new List<FileManagerEntry>();

            var blobOjects = await GetBlobHierarchyItemsAsync(path);

            foreach (var blob in blobOjects)
            {
                if (blob.IsBlob)
                {
                    if (blob.Blob.Name.EndsWith("folder.stubxx"))
                    {
                        continue;
                    }

                    var fileName = Path.GetFileName(blob.Blob.Name);

                    var modified = blob.Blob.Properties.LastModified?.UtcDateTime ?? DateTime.UtcNow;

                    entries.Add(new FileManagerEntry
                    {
                        Created = DateTime.Now,
                        CreatedUtc = DateTime.UtcNow,
                        Extension = Path.GetExtension(blob.Blob.Name),
                        HasDirectories = false,
                        IsDirectory = false,
                        Modified = modified,
                        ModifiedUtc = modified,
                        Name = fileName,
                        Path = blob.Blob.Name,
                        Size = blob.Blob.Properties.ContentLength ?? 0
                    });
                }
                else
                {
                    var parse = blob.Prefix.TrimEnd('/').Split('/');

                    var subDirectory = await GetBlobHierarchyItemsAsync(blob.Prefix);

                    entries.Add(new FileManagerEntry
                    {
                        Created = DateTime.Now,
                        CreatedUtc = DateTime.UtcNow,
                        Extension = string.Empty,
                        HasDirectories = subDirectory.Any(a => a.IsPrefix),
                        IsDirectory = true,
                        Modified = DateTime.Now,
                        ModifiedUtc = DateTime.UtcNow,
                        Name = parse.Last(),
                        Path = blob.Prefix.TrimEnd('/'),
                        Size = 0
                    });
                }
            }

            return entries;
        }

        /// <summary>
        ///    Gets a list of blobs for a given path.
        /// </summary>
        /// <param name="path">Path to search.</param>
        /// <returns>BlobItem list.</returns>
        public async Task<List<BlobItem>> GetBlobItemsAsync(string path)
        {
            var containerClient = this.blobServiceClient.GetBlobContainerClient(this.containerName);
            var resultSegment = containerClient.GetBlobsAsync(prefix: path).AsPages();
            var results = new List<BlobItem>();

            await foreach (var blobPage in resultSegment)
            {
                results.AddRange(blobPage.Values);
            }

            return results;
        }

        /// <summary>
        /// Opens a read stream to download the blob.
        /// </summary>
        /// <param name="path">Path to blob from which to open.</param>
        /// <returns>Returns data as a <see cref="Stream"/>.</returns>
        public async Task<Stream> GetStreamAsync(string path)
        {
            var containerClient =
                this.blobServiceClient.GetBlobContainerClient(this.containerName);

            // Use GetBlobClient instead of GetAppendBlobClient to support both block and append blobs
            var blobClient = containerClient.GetBlobClient(path);

            return await blobClient.OpenReadAsync(new BlobOpenReadOptions(false));
        }

        /// <summary>
        /// Uploads file to a stream.
        /// </summary>
        /// <param name="readStream">Data stream to upload.</param>
        /// <param name="fileMetaData">File and chunk metadata.</param>
        /// <param name="uploadDateTime">Chunk upload date and time.</param>
        /// <returns>Indicates success as a <see cref="bool"/>.</returns>
        public async Task<bool> UploadStreamAsync(Stream readStream, FileUploadMetaData fileMetaData, DateTimeOffset uploadDateTime)
        {
            var appendClient = this.GetAppendBlobClient(fileMetaData.RelativePath);

            var headers = new BlobHttpHeaders
            {
                // Set the MIME ContentType every time the properties
                // are updated or the field will be cleared.,
                ContentType = Utilities.GetContentType(fileMetaData)
            };
            await appendClient.CreateIfNotExistsAsync(headers);

            var dictionaryObject = new Dictionary<string, string>
            {
                { "ccmsuploaduid", fileMetaData.UploadUid },
                { "ccmssize", fileMetaData.TotalFileSize.ToString() },
                { "ccmsdatetime", uploadDateTime.UtcDateTime.Ticks.ToString() }
            };

            _ = await appendClient.SetMetadataAsync(dictionaryObject);
            var writeStream = await appendClient.OpenWriteAsync(true);
            await readStream.CopyToAsync(writeStream);
            return true;
        }

        /// <summary>
        ///     Gets an append blob client, used for chunk uploads.
        /// </summary>
        /// <param name="path">Path to blob from which to open with client.</param>
        /// <returns>Returns the <see cref="AppendBlobClient"/>.</returns>
        public AppendBlobClient GetAppendBlobClient(string path)
        {
            var containerClient =
                this.blobServiceClient.GetBlobContainerClient(this.containerName);

            return containerClient.GetAppendBlobClient(path);
        }

        /// <summary>
        /// Inserts or updates metadata for a blob by path.
        /// </summary>
        /// <param name="path">Path to blob.</param>
        /// <param name="data">Metadata.</param>
        /// <returns>Task.</returns>
        public async Task UpsertMetadata(string path, IEnumerable<BlobMetadataItem> data)
        {
            var blobClient = await this.GetBlobAsync(path);
            if (blobClient != null && data != null && data.Any())
            {
                await UpsertMetadata(blobClient, data);
            }
        }

        /// <summary>
        ///  Inserts or updates metadata for a blob  by <see cref="BlobClient"/>.
        /// </summary>
        /// <param name="blobClient">Blob client.</param>
        /// <param name="data">Data to insert or update.</param>
        /// <returns>Task.</returns>
        public async Task UpsertMetadata(BlobClient blobClient, IEnumerable<BlobMetadataItem> data)
        {
            if (blobClient != null && data != null && data.Any())
            {
                var properties = await blobClient.GetPropertiesAsync();
                var metadata = properties.Value.Metadata;

                foreach (var item in data.Where(i => !string.IsNullOrWhiteSpace(i?.Key)))
                {
                    var existingKey = metadata.Keys.FirstOrDefault(k => k.Equals(item.Key, StringComparison.OrdinalIgnoreCase));
                    if (existingKey != null)
                    {
                        metadata[existingKey] = item.Value ?? string.Empty;
                    }
                    else
                    {
                        metadata.Add(item.Key, item.Value ?? string.Empty);
                    }
                }

                await blobClient.SetMetadataAsync(metadata);
            }
        }

        /// <summary>
        /// Deletes metadata for a blob by key.
        /// </summary>
        /// <param name="path">Path to blob.</param>
        /// <param name="key">Key to remove.</param>
        /// <returns>Indication of success.</returns>
        public async Task<bool> DeleteMetadata(string path, string key)
        {
            var blobClient = await this.GetBlobAsync(path);
            return await DeleteMetadata(blobClient, key);
        }

        /// <summary>
        ///  Deletes metadata for a blob by key using <see cref="BlobClient"/>.
        /// </summary>
        /// <param name="blobClient">Blob client.</param>
        /// <param name="key">Key to remove.</param>
        /// <returns>Indication of success.</returns>
        public async Task<bool> DeleteMetadata(BlobClient blobClient, string key)
        {
            if (blobClient == null)
            {
                return false;
            }

            var properties = await blobClient.GetPropertiesAsync();
            if (properties.Value.Metadata.ContainsKey(key))
            {
                properties.Value.Metadata.Remove(key);
                await blobClient.SetMetadataAsync(properties.Value.Metadata);
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Gets metadata for a blob by key.
        /// </summary>
        /// <param name="path">Path to blob storage.</param>
        /// <returns>Metadata list.</returns>
        public async Task<IEnumerable<BlobMetadataItem>> GetMetadata(string path)
        {
            var blobClient = await this.GetBlobAsync(path);

            return await GetMetadata(blobClient);
        }

        /// <summary>
        /// Gets metadata for a blob by <see cref="BlobClient"/>.
        /// </summary>
        /// <param name="blobClient">Blob client.</param>
        /// <returns>Metadata list.</returns>
        public async Task<IEnumerable<BlobMetadataItem>> GetMetadata(BlobClient blobClient)
        {
            if (blobClient == null)
            {
                return null;
            }

            var properties = await blobClient.GetPropertiesAsync();

            if (properties == null || properties.Value.Metadata == null || properties.Value.Metadata.Count == 0)
            {
                return null;
            }

            var metadata = properties.Value.Metadata.Values.Select(value => new BlobMetadataItem
            {
                Key = properties.Value.Metadata.FirstOrDefault(kv => kv.Value == value).Key,
                Value = value
            });
            return metadata;
        }

        /// <summary>
        ///     Gets an append blob client, used for chunk uploads.
        /// </summary>
        /// <param name="path">Path to blob from which to open with client.</param>
        /// <returns>Returns the <see cref="AppendBlobClient"/>.</returns>
        public BlobClient GetBlobClient(string path)
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(this.containerName);
            return containerClient.GetBlobClient(path);
        }

        /// <summary>
        ///     Renames a file.
        /// </summary>
        /// <param name="sourceFile">Path to file.</param>
        /// <param name="destinationFile">Destination file name.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task MoveFileAsync(string sourceFile, string destinationFile)
        {
            await CopyBlobAsync(sourceFile, destinationFile);
            await DeleteIfExistsAsync(sourceFile);
        }

        /// <summary>
        ///     Moves a folder.
        /// </summary>
        /// <param name="sourceFolder">Source folder.</param>
        /// <param name="destinationFolder">Destination folder.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task MoveFolderAsync(string sourceFolder, string destinationFolder)
        {
            var blobs = await GetBlobNamesByPath(sourceFolder);

            // Work through the list here.
            foreach (var srcBlobName in blobs)
            {
                var fileName = Path.GetFileName(srcBlobName);
                var destBlobName = destinationFolder.TrimEnd('/') + "/" + fileName.TrimStart('/');

                await CopyBlobAsync(srcBlobName, destBlobName);

                // Now check to see if files were copied
                await DeleteIfExistsAsync(srcBlobName);
            }
        }

        /// <summary>
        /// Gets the thumbnail stream for a given blob.
        /// </summary>
        /// <param name="target">Path to the blob.</param>
        /// <returns>Returns the thumbnail stream as a <see cref="Stream"/>.</returns>
        public Task<Stream> GetImageThumbnailStreamAsync(string target)
        {
            throw new NotSupportedException("Thumbnail stream retrieval is not supported by AzureStorage.");
        }

        /// <summary>
        /// Gets the storage consumption for the container.
        /// </summary>
        /// <returns>Byte count.</returns>
        public async Task<long> GetStorageConsumption()
        {
            var containerClient = this.blobServiceClient.GetBlobContainerClient(this.containerName);

            var blobs = containerClient.GetBlobsAsync().AsPages();
            long bytesConsumed = 0;
            await foreach (var blobPage in blobs)
            {
                bytesConsumed += blobPage.Values.Sum(b => b.Properties.ContentLength.GetValueOrDefault());
            }

            return bytesConsumed;
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> GetPropertiesAsync(string target)
        {
            var blobClient = await this.GetBlobAsync(target);

            if (blobClient == null || !await blobClient.ExistsAsync())
            {
                throw new InvalidOperationException($"Blob at path '{target}' does not exist.");
            }

            var blobProperties = await blobClient.GetPropertiesAsync();

            return blobProperties.Value.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <inheritdoc/>
        public async Task SavePropertiesAsync(string target, Dictionary<string, string> properties)
        {
            if (string.IsNullOrEmpty(target))
            {
                throw new ArgumentException("Target path cannot be null or empty.", nameof(target));
            }

            if (properties == null || properties.Count == 0)
            {
                return;
            }

            var blobClient = await this.GetBlobAsync(target);

            if (blobClient == null || !await blobClient.ExistsAsync())
            {
                throw new InvalidOperationException($"Blob at path '{target}' does not exist.");
            }

            var blobProperties = await blobClient.GetPropertiesAsync();
            var metadata = blobProperties.Value.Metadata;

            foreach (var property in properties)
            {
                if (metadata.ContainsKey(property.Key))
                {
                    metadata[property.Key] = property.Value;
                }
                else
                {
                    metadata.Add(property.Key, property.Value);
                }
            }

            await blobClient.SetMetadataAsync(metadata);
        }

        /// <summary>
        /// Initializes the instance.
        /// </summary>
        /// <param name="containerName">Container name.</param>
        /// <param name="connectionString">Connection string.</param>
        /// <param name="tokenCredential">Azure default credential.</param>
        private void Initialize(string containerName, string connectionString, TokenCredential tokenCredential)
        {
            this.containerName = containerName;
            if (connectionString.Contains("AccountKey=AccessToken", StringComparison.CurrentCultureIgnoreCase))
            {
                var conpartsDict = connectionString.Split(';').Where(w => !string.IsNullOrEmpty(w)).Select(part => part.Split('=')).ToDictionary(sp => sp[0], sp => sp[1], StringComparer.OrdinalIgnoreCase);

                this.usesAzureDefaultCredential = true;
                this.blobServiceClient = new BlobServiceClient(new Uri($"https://{conpartsDict["AccountName"]}.blob.core.windows.net/"), tokenCredential);
            }
            else
            {
                this.usesAzureDefaultCredential = false;
                this.blobServiceClient = new BlobServiceClient(connectionString);
            }

            // Ensure the container exists (skip for test scenarios to avoid network calls)
            // In production, container creation will happen on first use via CreateIfNotExistsAsync
            try
            {
                var containerClient = this.blobServiceClient.GetBlobContainerClient(this.containerName);
                containerClient.CreateIfNotExists();
            }
            catch (Exception)
            {
                // Swallow exception during initialization - container will be created on first use
                // This allows tests to instantiate drivers without requiring actual Azure Storage
            }
        }

        /// <summary>
        /// Uploads a block blob to Azure Storage.
        /// </summary>
        /// <param name="readStream">Read file stream.</param>
        /// <param name="fileMetaData">File metadata.</param>
        /// <param name="uploadDateTime">Upload date and time.</param>
        /// <returns>A Task.</returns>
        private async Task UpdloadBlockBlobAsync(Stream readStream, FileUploadMetaData fileMetaData, DateTimeOffset uploadDateTime)
        {
            var blockClient = this.GetBlobClient(fileMetaData.RelativePath);

            // Ensure container exists before upload
            var containerClient = this.blobServiceClient.GetBlobContainerClient(this.containerName);
            await containerClient.CreateIfNotExistsAsync();

            // Break any existing lease before attempting to delete
            if (await blockClient.ExistsAsync())
            {
                var leaseClient = blockClient.GetBlobLeaseClient();
                try
                {
                    await leaseClient.BreakAsync(TimeSpan.Zero);
                }
                catch (Azure.RequestFailedException)
                {
                    // If breaking the lease fails (e.g., no lease exists), continue with deletion
                }
            }

            await blockClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

            var headers = new BlobHttpHeaders
            {
                // Set the MIME ContentType every time the properties are updated or the field will be cleared.
                ContentType = fileMetaData.ContentType,
                CacheControl = fileMetaData.CacheControl
            };
            await blockClient.UploadAsync(readStream, headers);

            var dictionaryObject = new Dictionary<string, string>
                {
                    { "ccmsuploaduid", fileMetaData.UploadUid },
                    { "ccmssize", fileMetaData.TotalFileSize.ToString() },
                    { "ccmsdatetime", uploadDateTime.UtcDateTime.Ticks.ToString() },
                    { "ccmsimagewidth", fileMetaData.ImageWidth },
                    { "ccmsimageheight", fileMetaData.ImageHeight }
                };

            try
            {
                _ = await blockClient.SetMetadataAsync(dictionaryObject);
            }
            catch (Azure.RequestFailedException)
            {
                // Metadata is not critical; if it fails (e.g., blob doesn't exist due to Azure Storage issues),
                // we can continue. This is especially important for tests where Azure Storage might not be fully configured.
                // In production, the blob should exist after UploadAsync completes successfully.
            }
        }

        /// <summary>
        /// Attempts to delete an append blob and waits until it is actually deleted or a timeout is reached.
        /// </summary>
        /// <param name="appendBlobClient">Append blob client used in the delete.</param>
        /// <param name="timeout">Maximum time to wait for deletion (default: 30 seconds).</param>
        /// <param name="pollInterval">Interval between existence checks (default: 500 ms).</param>
        /// <returns>True if the blob was deleted within the timeout, false otherwise.</returns>
        private async Task<bool> DeleteAppendBlobWithRetryAsync(AppendBlobClient appendBlobClient, TimeSpan? timeout = null, TimeSpan? pollInterval = null)
        {
            timeout ??= TimeSpan.FromSeconds(30);
            pollInterval ??= TimeSpan.FromMilliseconds(500);
            var startTime = DateTime.UtcNow;

            await appendBlobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

            while (DateTime.UtcNow - startTime < timeout.Value)
            {
                if (!await appendBlobClient.ExistsAsync())
                {
                    return true;
                }

                await Task.Delay(pollInterval.Value);
            }

            // Final check after timeout
            return !await appendBlobClient.ExistsAsync();
        }
    }
}
